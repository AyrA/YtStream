using Microsoft.AspNetCore.Authentication.Cookies;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace YtStream
{
    public static class UserManager
    {
        public const int PasswordMinLength = 8;
        public const string NamePattern = @"^[A-Za-z\d]{3,}$";
        private const string FileName = "accounts.json";
        private static readonly string AccountFile;
        private static readonly object Locker = new object();
        private static readonly AccountInfo Dummy;
        private static readonly List<AccountInfo> Accounts;

        public static readonly UserPasswordRules Rules;

        public static bool HasUsers { get => Accounts.Count > 0; }

        static UserManager()
        {
            var D = new AccountInfo() { Username = "" };
            D.SetPassword("1234"); //The actual value doesn't matters as the dummy account is hardcoded to fail.
            Dummy = D;
            Accounts = new List<AccountInfo>();
            AccountFile = Path.Combine(Startup.BasePath, FileName);
            if (File.Exists(AccountFile))
            {
                Accounts.AddRange(File.ReadAllText(AccountFile).FromJson<AccountInfo[]>(true));
            }
            ValidateAccounts();
            Rules = new UserPasswordRules();
        }

        public static AccountInfo GetUser(string Username)
        {
            return Accounts.FirstOrDefault(m => m.Username.ToLower() == Username.ToLower());
        }

        public static AccountInfo GetUser(Guid ApiKey)
        {
            return Accounts.FirstOrDefault(m => m.HasKey(ApiKey));
        }

        public static void AddUser(string Username, string Password, UserRoles Role = UserRoles.User)
        {
            lock (Locker)
            {
                if (string.IsNullOrWhiteSpace(Username))
                {
                    throw new ArgumentException($"'{nameof(Username)}' cannot be null or whitespace.", nameof(Username));
                }

                if (!Rules.IsComplexPassword(Password))
                {
                    throw new ArgumentException($"'{nameof(Password)}' fails complexity criteria.", nameof(Password));
                }

                if (GetUser(Username) != null)
                {
                    throw new ArgumentException("A user with that name already exists");
                }

                var AI = new AccountInfo() { Username = Username, Roles = Role };
                AI.SetPassword(Password);
                Accounts.Add(AI);
                Save();
            }
        }

        public static void DeleteUser(string Username)
        {
            lock (Locker)
            {
                var User = GetUser(Username);
                if (User == null)
                {
                    throw new ArgumentException("The supplied user name is invalid");
                }
                Accounts.Remove(User);
                Save();
            }
        }

        public static void Save()
        {
            lock (Accounts)
            {
                ValidateAccounts();
                File.WriteAllText(AccountFile, Accounts.ToJson(true));
            }
        }

        public static bool Authenticate(string Username, string Password)
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                return false;
            }

            var User = GetUser(Username);
            if (User == null)
            {
                User = Dummy;
            }
            //Always fail dummy but still perform password authentication (time based attacks)
            return User.CheckPassword(Password) && User != Dummy;
        }

        private static void ValidateAccounts()
        {
            if (Accounts.Any(m => !m.IsValid()))
            {
                throw new InvalidOperationException("Account list is in an invalid state.");
            }
            //Prevent duplicste user names
            if (Accounts.Count != Accounts.Select(m => m.Username.ToLower()).Distinct().Count())
            {
                throw new InvalidOperationException("Duplicate user name in list");
            }
            //Prevent duplicate keys
            var Keys = Accounts.SelectMany(m => m.ApiKeys).ToArray();
            if (Keys.Length != Keys.Distinct().Count())
            {
                throw new InvalidOperationException("Duplicate key in list");
            }
        }
    }

    public class UserPasswordRules
    {
        public int MinimumLength { get; set; }

        public bool Uppercase { get; set; }

        public bool Lowercase { get; set; }

        public bool Digits { get; set; }

        public bool Symbols { get; set; }

        public int RuleCount { get; set; }

        public UserPasswordRules()
        {
            MinimumLength = UserManager.PasswordMinLength;
            Uppercase = Lowercase = Digits = Symbols = false;
            RuleCount = 3;
        }

        public bool IsComplexPassword(string Password)
        {
            if (Password == null || Password.Length < MinimumLength)
            {
                return false;
            }
            var matches = new
            {
                Upper = Regex.IsMatch(Password, @"[A-Z]"),
                Lower = Regex.IsMatch(Password, @"[a-z]"),
                Digits = Regex.IsMatch(Password, @"\d"),
                Symbols = Regex.IsMatch(Password, @"[^a-zA-Z\d]")
            };
            int Complexity = 0;

            Complexity += matches.Lower ? 1 : 0;
            Complexity += matches.Upper ? 1 : 0;
            Complexity += matches.Digits ? 1 : 0;
            Complexity += matches.Symbols ? 1 : 0;

            if (Uppercase && !matches.Upper)
            {
                return false;
            }
            if (Lowercase && !matches.Lower)
            {
                return false;
            }
            if (Digits && !matches.Digits)
            {
                return false;
            }
            if (Symbols && !matches.Symbols)
            {
                return false;
            }
            return Complexity >= RuleCount;
        }

    }

    public class AccountInfo : IValidateable
    {
        private List<UserApiKey> _keys;

        public bool Enabled { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public UserRoles Roles { get; set; }

        public UserApiKey[] ApiKeys
        {
            get
            {
                return _keys?.ToArray();
            }
            set
            {
                if (value != null)
                {
                    _keys = new List<UserApiKey>(value);
                }
                else
                {
                    _keys = null;
                }
            }
        }

        public AccountInfo()
        {
            Enabled = true;
            Roles = UserRoles.User;
        }

        public string[] GetRoleStrings()
        {
            var RoleStrings = new List<string>();
            foreach (var Role in Enum.GetValues(typeof(UserRoles)).OfType<UserRoles>())
            {
                if (Roles.HasFlag(Role))
                {
                    RoleStrings.Add(Role.ToString());
                }
            }
            return RoleStrings.ToArray();
        }

        public void SetPassword(string NewPassword, int Difficulty = 100000)
        {
            using (var Enc = new Rfc2898DeriveBytes(NewPassword, 16, Difficulty, HashAlgorithmName.SHA256))
            {
                Password = Convert.ToBase64String(Enc.Salt) + ":" + Difficulty + ":" + Convert.ToBase64String(Enc.GetBytes(16));
            }
        }

        public bool CheckPassword(string TestPassword)
        {
            if (string.IsNullOrEmpty(TestPassword))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(Password))
            {
                var Parts = Password.Split(':');
                if (Parts.Length != 3)
                {
                    return false;
                }
                using (var Enc = new Rfc2898DeriveBytes(TestPassword, Convert.FromBase64String(Parts[0]), int.Parse(Parts[1]), HashAlgorithmName.SHA256))
                {
                    return Parts[2] == Convert.ToBase64String(Enc.GetBytes(16));
                }
            }
            return false;
        }

        public bool HasPassword()
        {
            if (string.IsNullOrEmpty(Password))
            {
                return false;
            }
            var Parts = Password.Split(':');
            try
            {
                Convert.FromBase64String(Parts[0]);
                Convert.FromBase64String(Parts[2]);
                if (int.Parse(Parts[1]) < 1)
                {
                    throw new Exception();
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        public int RemoveKey(UserApiKey Key)
        {
            if (Key == null)
            {
                throw new ArgumentNullException(nameof(Key));
            }
            if (_keys != null)
            {
                return _keys.Remove(Key) ? 1 : 0;
            }
            return 0;
        }

        public int RemoveKey(Guid G)
        {
            if (_keys != null)
            {
                return _keys.RemoveAll(m => m.Key == G);
            }
            return 0;
        }

        public void AddKey(UserApiKey Key)
        {
            if (Key == null)
            {
                throw new ArgumentNullException(nameof(Key));
            }
            if (!Key.IsValid())
            {
                throw new ArgumentException("Key is invalid");
            }
            if (_keys == null)
            {
                _keys = new List<UserApiKey>();
            }
            else if (_keys.Any(m => m.Key == Key.Key))
            {
                throw new ArgumentException($"Duplicate key: {Key.Key}");
            }
            _keys.Add(Key);
        }

        public bool HasKey(Guid Key)
        {
            return _keys != null && _keys.Any(m => m.Key == Key);
        }

        /// <summary>
        /// Create .NET Core identity for HttpContext.SignInAsync()
        /// </summary>
        public ClaimsPrincipal GetIdentity()
        {
            var Claims = new Claim[]
            {
                new Claim(ClaimTypes.Name, Username)
            };
            var identity = new ClaimsIdentity(Claims, CookieAuthenticationDefaults.AuthenticationScheme);
            foreach (var Role in GetRoleStrings())
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, Role));
            }
            return new ClaimsPrincipal(identity);
        }

        public bool IsValid()
        {
            return GetValidationMessages().Length == 0;
        }

        public string[] GetValidationMessages()
        {
            var Messages = new List<string>();
            if (string.IsNullOrWhiteSpace(Username))
            {
                Messages.Add("Username must be set and not consist of whitespace");
            }
            else if (Username.Length > 20)
            {
                Messages.Add("Username must not be longer than 20 characters");
            }
            else if(!Regex.IsMatch(Username, UserManager.NamePattern))
            {
                Messages.Add("Username may only contain alphanumeric characters");
            }
            if (string.IsNullOrWhiteSpace(Password))
            {
                Messages.Add("Password must be set and not consist of whitespace");
            }
            if (!HasPassword())
            {
                Messages.Add("Password not set or invalid");
            }
            if (_keys != null && _keys.Any(m => m == null || !m.IsValid()))
            {
                Messages.Add(_keys.Count(m => m == null || m.IsValid()) + " invalid API key(s)");
            }
            return Messages.ToArray();
        }
    }

    public class UserApiKey : IValidateable
    {
        public Guid Key { get; set; }

        public DateTime CreatedAt { get; set; }

        public string Name { get; set; }

        public UserApiKey()
        {
            Key = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }

        public string[] GetValidationMessages()
        {
            var Msg = new List<string>();
            if (string.IsNullOrWhiteSpace(Name))
            {
                Msg.Add("Name is required");
            }
            else if (Name.Length > 20)
            {
                Msg.Add("Name must not be longer than 20 characters");
            }
            if (Key == Guid.Empty)
            {
                Msg.Add("Key has not been set");
            }
            if (CreatedAt > DateTime.UtcNow || CreatedAt < new DateTime(2020, 1, 1))
            {
                Msg.Add("Invalid date range");
            }
            return Msg.ToArray();
        }

        public bool IsValid()
        {
            return GetValidationMessages().Length == 0;
        }
    }

    /// <summary>
    /// User roles
    /// </summary>
    [Flags]
    public enum UserRoles : int
    {
        User = 1,
        Administrator = 2
    }
}
