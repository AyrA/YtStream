using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YtStream.Accounts
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
        public static int MaxKeysPerUser { get; set; }

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
                foreach (var Acc in Accounts.Where(m => m.ApiKeys == null))
                {
                    Acc.ApiKeys = new UserApiKey[0];
                }
            }
            ValidateAccounts();
            Rules = new UserPasswordRules();
        }

        public static bool CanDeleteOrDisable(string Username)
        {
            var Acc = GetUser(Username);
            if (Acc == null)
            {
                throw new ArgumentException("Specified user could not be found");
            }
            //Can always delete regular users
            if (!Acc.Roles.HasFlag(UserRoles.Administrator))
            {
                return true;
            }
            //Can only delete administrator if at least another active admin exists
            return Accounts.Count(m => m.Enabled && m.Roles.HasFlag(UserRoles.Administrator)) > 1;
        }

        public static AccountInfo[] GetUsers()
        {
            return Accounts.ToArray();
        }

        public static AccountInfo GetUser(string Username)
        {
            return Accounts.FirstOrDefault(m => m.Username.ToLower() == Username.ToLower());
        }

        public static AccountInfo GetUser(Guid ApiKey)
        {
            return Accounts.FirstOrDefault(m => m.HasKey(ApiKey));
        }

        public static AccountInfo AddUser(string Username, string Password, UserRoles Role = UserRoles.User)
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
                return AI;
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
                foreach (var Acc in Accounts.Where(m => m.ApiKeys == null))
                {
                    Acc.ApiKeys = new UserApiKey[0];
                }
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
            if (MaxKeysPerUser > 0 && Accounts.Any(m => m.ApiKeys.Length > MaxKeysPerUser))
            {
                throw new InvalidOperationException($"At least one account has more than {MaxKeysPerUser} keys");
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
}
