using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace YtStream
{
    public static class UserManager
    {
        public const int PasswordMinLength = 8;
        private const string FileName = "accounts.json";
        private static readonly string AccountFile;
        private static readonly object Locker = new object();
        private static readonly AccountInfo Dummy;
        private static readonly List<AccountInfo> Accounts;

        public static bool HasUsers { get => Accounts.Count > 0; }

        static UserManager()
        {
            var D = new AccountInfo() { Username = "" };
            D.SetPassword("1234");
            Dummy = D;
            Accounts = new List<AccountInfo>();
            AccountFile = Path.Combine(Startup.BasePath, FileName);
            if (File.Exists(AccountFile))
            {
                try
                {
                    Accounts.AddRange(File.ReadAllText(AccountFile).FromJson<AccountInfo[]>(true));
                }
                catch
                {

                }
            }
        }

        public static AccountInfo GetUser(string Username)
        {
            return Accounts.FirstOrDefault(m => m.Username.ToLower() == Username.ToLower());
        }

        public static void AddUser(string Username, string Password, UserRoles Role = UserRoles.User)
        {
            lock (Locker)
            {
                if (string.IsNullOrWhiteSpace(Username))
                {
                    throw new ArgumentException($"'{nameof(Username)}' cannot be null or whitespace.", nameof(Username));
                }

                if (!IsComplexPassword(Password))
                {
                    throw new ArgumentException($"'{nameof(Password)}' cannot be null or empty.", nameof(Password));
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
                if (Accounts.Any(m => !m.IsValid()))
                {
                    throw new InvalidOperationException("Account list is in an invalid state.");
                }
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
            return User.CheckPassword(Password) && User != Dummy;
        }

        public static bool IsComplexPassword(string Password)
        {
            if (Password == null || Password.Length < PasswordMinLength)
            {
                return false;
            }
            int Complexity = 0;
            Complexity += Regex.IsMatch(Password, @"[a-z]") ? 1 : 0;
            Complexity += Regex.IsMatch(Password, @"[A-Z]") ? 1 : 0;
            Complexity += Regex.IsMatch(Password, @"\d") ? 1 : 0;
            Complexity += Regex.IsMatch(Password, @"[^a-zA-Z\d]") ? 1 : 0;
            return Complexity >= 3;
        }
    }

    public class AccountInfo : IValidateable
    {
        public bool Enabled { get; set; } = true;

        public string Username { get; set; }

        public string Password { get; set; }

        public UserRoles Roles { get; set; } = UserRoles.User;

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
            if (string.IsNullOrWhiteSpace(Password))
            {
                Messages.Add("Password must be set and not consist of whitespace");
            }
            if (!HasPassword())
            {
                Messages.Add("Password not set or invalid");
            }
            return Messages.ToArray();
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
