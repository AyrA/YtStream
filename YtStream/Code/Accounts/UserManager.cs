using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace YtStream.Accounts
{
    /// <summary>
    /// Provides account management features
    /// </summary>
    public static class UserManager
    {
        /// <summary>
        /// Minimum required password length
        /// </summary>
        public const int PasswordMinLength = 8;
        /// <summary>
        /// User name mask
        /// </summary>
        public const string NamePattern = @"^[A-Za-z\d]{3,}$";
        /// <summary>
        /// File for account data
        /// </summary>
        public const string FileName = "accounts.json";
        /// <summary>
        /// Account file full path
        /// </summary>
        private static readonly string AccountFile;
        /// <summary>
        /// Thread synchronization object
        /// </summary>
        private static readonly object Locker = new object();
        /// <summary>
        /// Fake account to pretend to do password authentication
        /// </summary>
        private static readonly AccountInfo Dummy;
        /// <summary>
        /// Accounts
        /// </summary>
        private static readonly List<AccountInfo> Accounts;

        /// <summary>
        /// Gets the password rules of this application
        /// </summary>
        public static readonly UserPasswordRules Rules;

        /// <summary>
        /// Gets if at least one user exists
        /// </summary>
        /// <remarks>Disregards whether the user is enabled or not</remarks>
        public static bool HasUsers { get => Accounts.Count > 0; }

        /// <summary>
        /// Gets or sets the maximum number of API keys permitted per user
        /// </summary>
        public static int MaxKeysPerUser { get; set; }

        /// <summary>
        /// Loads existing accounts or creates a new database
        /// </summary>
        static UserManager()
        {
            var D = new AccountInfo() { Username = "" };
            D.SetPassword("1234"); //The actual value doesn't matters as the dummy account is hardcoded to fail.
            Dummy = D;
            Accounts = new List<AccountInfo>();
            AccountFile = Path.Combine(Startup.BasePath, FileName);
            Reload();
            Rules = new UserPasswordRules();
        }

        /// <summary>
        /// Reloads accounts from file
        /// </summary>
        public static void Reload()
        {
            if (File.Exists(AccountFile))
            {
                var FileAccounts = File.ReadAllText(AccountFile).FromJson<AccountInfo[]>(true);
                ValidateAccounts(FileAccounts);
                Accounts.Clear();
                Accounts.AddRange(FileAccounts);
                foreach (var Acc in Accounts.Where(m => m.ApiKeys == null))
                {
                    Acc.ApiKeys = new UserApiKey[0];
                }
            }
        }

        /// <summary>
        /// Gets if the given user can be deleted or disabled
        /// </summary>
        /// <param name="Username">User name</param>
        /// <returns>
        /// true if can be deleted or disabled
        /// </returns>
        /// <remarks>
        /// false means that the supplied user name is the only enabled administrator.
        /// This doesn't actually enforces whether a user can be deleted or not.
        /// </remarks>
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

        /// <summary>
        /// Gets if the user name is valid
        /// </summary>
        /// <param name="Username">User name</param>
        /// <param name="CheckExisting">Check for collision with existing name</param>
        /// <returns>true, if username valid (and optionally not occupied already)</returns>
        public static bool IsValidUsername(string Username, bool CheckExisting = true)
        {
            var Valid = Username != null && Regex.IsMatch(Username, NamePattern);
            if (Valid && CheckExisting)
            {
                Valid &= GetUser(Username) == null;
            }
            return Valid;
        }

        /// <summary>
        /// Gets all users
        /// </summary>
        /// <returns>All users</returns>
        public static AccountInfo[] GetUsers()
        {
            return Accounts.ToArray();
        }

        /// <summary>
        /// Gets a user by name
        /// </summary>
        /// <param name="Username">User name</param>
        /// <returns>User</returns>
        /// <remarks>Returns null if no user found</remarks>
        public static AccountInfo GetUser(string Username)
        {
            if (string.IsNullOrEmpty(Username))
            {
                return null;
            }
            return Accounts.FirstOrDefault(m => m.Username.ToLower() == Username.ToLower());
        }

        /// <summary>
        /// Gets a user by API key
        /// </summary>
        /// <param name="ApiKey">API key</param>
        /// <returns>User</returns>
        /// <remarks>Returns null if no user found</remarks>
        public static AccountInfo GetUser(Guid ApiKey)
        {
            return Accounts.FirstOrDefault(m => m.HasKey(ApiKey));
        }

        /// <summary>
        /// Adds a user to the system
        /// </summary>
        /// <param name="Username">User name</param>
        /// <param name="Password">Paassword</param>
        /// <param name="Role">User roles</param>
        /// <returns>Added user account</returns>
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

        /// <summary>
        /// Deletes the given user
        /// </summary>
        /// <param name="Username">User name</param>
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

        /// <summary>
        /// Saves the user list to file
        /// </summary>
        public static void Save()
        {
            lock (Accounts)
            {
                foreach (var Acc in Accounts.Where(m => m.ApiKeys == null))
                {
                    Acc.ApiKeys = new UserApiKey[0];
                }
                ValidateAccounts(Accounts.ToArray());
                File.WriteAllText(AccountFile, Accounts.ToJson(true));
            }
        }

        /// <summary>
        /// Authenticates a user
        /// </summary>
        /// <param name="Username">User name</param>
        /// <param name="Password">Password</param>
        /// <returns>true, if authentication sucessfull</returns>
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

        /// <summary>
        /// Validates the given accounts.
        /// Checks for basic validity, name duplicates, etc.
        /// </summary>
        /// <param name="Accounts">Accounts</param>
        /// <remarks>This will not touch the locally loaded account list</remarks>
        private static void ValidateAccounts(AccountInfo[] Accounts)
        {
            //Check if all accounts are valid
            if (Accounts.Any(m => !m.IsValid()))
            {
                throw new InvalidOperationException("Account list is in an invalid state.");
            }
            //Check if at least one active admin exists
            if (!Accounts.Any(m => m.Enabled && m.Roles.HasFlag(UserRoles.Administrator)))
            {
                throw new InvalidOperationException("Account list lacks an active administrator");
            }
            //Check if a user has too many keys
            if (MaxKeysPerUser > 0 && Accounts.Any(m => m.ApiKeys.Length > MaxKeysPerUser))
            {
                throw new InvalidOperationException($"At least one account has more than {MaxKeysPerUser} keys");
            }
            //Prevent duplicate user names
            if (Accounts.Length != Accounts.Select(m => m.Username.ToLower()).Distinct().Count())
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
