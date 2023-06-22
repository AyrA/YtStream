using AyrA.AutoDI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YtStream.Enums;
using YtStream.Extensions;
using YtStream.Models.Accounts;
using YtStream.Models.Favs;

namespace YtStream.Services.Accounts
{
    [AutoDIRegister(AutoDIType.Transient)]
    /// <summary>
    /// Provides account management features
    /// </summary>
    public class UserManagerService
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
        private readonly string AccountFile;
        /// <summary>
        /// Thread synchronization object
        /// </summary>
        private readonly object Locker = new();
        /// <summary>
        /// Fake account to pretend to do password authentication
        /// </summary>
        private static readonly AccountInfoModel Dummy;
        /// <summary>
        /// Accounts
        /// </summary>
        private readonly List<AccountInfoModel> Accounts;

        /// <summary>
        /// Gets the password rules of this application
        /// </summary>
        public readonly UserPasswordRuleModel Rules;

        /// <summary>
        /// Gets if at least one user exists
        /// </summary>
        /// <remarks>Disregards whether the user is enabled or not</remarks>
        public bool HasUsers { get => Accounts.Count > 0; }

        /// <summary>
        /// Gets or sets the maximum number of API keys permitted per user
        /// </summary>
        public int MaxKeysPerUser { get; }

        /// <summary>
        /// Generate dummy account
        /// </summary>
        static UserManagerService()
        {
            var D = new AccountInfoModel() { Username = "" };
            D.SetPassword("1234"); //The actual value doesn't matters as the dummy account is hardcoded to fail.
            Dummy = D;
        }

        /// <summary>
        /// Loads existing accounts or creates a new database
        /// </summary>
        public UserManagerService(BasePathService basePath, ConfigService config)
        {
            var c = config.GetConfiguration();
            MaxKeysPerUser = c.MaxKeysPerUser;

            Accounts = new List<AccountInfoModel>();
            AccountFile = Path.Combine(basePath.BasePath, FileName);
            Reload();
            Rules = new UserPasswordRuleModel();
        }

        /// <summary>
        /// Reloads accounts from file
        /// </summary>
        public void Reload()
        {
            if (File.Exists(AccountFile))
            {
                var FileAccounts = File.ReadAllText(AccountFile).FromJson<AccountInfoModel[]>(true);
                ValidateAccounts(FileAccounts);
                Accounts.Clear();
                Accounts.AddRange(FileAccounts);
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
        public bool CanDeleteOrDisable(string Username)
        {
            var Acc = GetUser(Username) ?? throw new ArgumentException("Specified user could not be found");
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
        public bool IsValidUsername(string Username, bool CheckExisting = true)
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
        public AccountInfoModel[] GetUsers()
        {
            return Accounts.ToArray();
        }

        /// <summary>
        /// Gets a user by name
        /// </summary>
        /// <param name="Username">User name</param>
        /// <returns>User</returns>
        /// <remarks>Returns null if no user found</remarks>
        public AccountInfoModel? GetUser(string? Username)
        {
            if (string.IsNullOrEmpty(Username))
            {
                return null;
            }
            return Accounts.FirstOrDefault(m => m?.Username != null && m.Username.ToLower() == Username.ToLower());
        }

        /// <summary>
        /// Gets a user by API key
        /// </summary>
        /// <param name="ApiKey">API key</param>
        /// <returns>User</returns>
        /// <remarks>Returns null if no user found</remarks>
        public AccountInfoModel? GetUser(Guid ApiKey)
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
        public AccountInfoModel AddUser(string Username, string Password, UserRoles Role = UserRoles.User)
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

                var AI = new AccountInfoModel() { Username = Username, Roles = Role };
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
        public void DeleteUser(string Username)
        {
            lock (Locker)
            {
                var User = GetUser(Username) ?? throw new ArgumentException("The supplied user name is invalid");
                Accounts.Remove(User);
                Save();
            }
        }

        /// <summary>
        /// Get the given favorite
        /// </summary>
        /// <param name="favKey">Favorite id</param>
        /// <param name="username">User name. If supplied, serach is restricted to the given user</param>
        /// <returns>Favorite. Null if none found</returns>
        public FavoriteBaseModel? GetFavorite(Guid favKey, string? username = null)
        {
            if (favKey == Guid.Empty)
            {
                return null;
            }
            if (!string.IsNullOrEmpty(username))
            {
                var acc = GetUser(username);
                if (acc != null)
                {
                    return acc.Favorites.FirstOrDefault(m => m.Id == favKey);
                }
                return null;
            }
            return Accounts.SelectMany(m => m.Favorites).FirstOrDefault(m => m.Id == favKey);
        }

        /// <summary>
        /// Saves the user list to file
        /// </summary>
        public void Save()
        {
            lock (Accounts)
            {
                foreach (var Acc in Accounts.Where(m => m.ApiKeys == null))
                {
                    Acc.ApiKeys = Array.Empty<UserApiKeyModel>();
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
        public bool Authenticate(string Username, string Password)
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                return false;
            }

            var User = GetUser(Username) ?? Dummy;
            //Always fail dummy but still perform password authentication (time based attacks)
            return User.CheckPassword(Password) && User != Dummy;
        }

        /// <summary>
        /// Validates the given accounts.
        /// Checks for basic validity, name duplicates, etc.
        /// </summary>
        /// <param name="Accounts">Accounts</param>
        /// <remarks>This will not touch the locally loaded account list</remarks>
        private void ValidateAccounts(AccountInfoModel[] Accounts)
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
            if (Accounts.Length != Accounts.Select(m => (m.Username ?? "").ToLower()).Distinct().Count())
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
