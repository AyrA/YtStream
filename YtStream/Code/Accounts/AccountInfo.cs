using Microsoft.AspNetCore.Authentication.Cookies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace YtStream.Accounts
{
    /// <summary>
    /// Represents a user account
    /// </summary>
    public class AccountInfo : IValidateable
    {
        /// <summary>
        /// Streaming keys
        /// </summary>
        private List<UserApiKey> _keys;

        /// <summary>
        /// Gets or sets if the account is enabled
        /// </summary>
        /// <remarks>
        /// A disabled account cannot log in or use stream keys
        /// </remarks>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets if ads are disabled
        /// </summary>
        /// <remarks>
        /// Ads can be disabled for administrators globally in the settings.
        /// </remarks>
        public bool DisableAds { get; set; }

        /// <summary>
        /// Gets or sets the user name used to log into the system.
        /// </summary>
        /// <remarks>This is case insensitive</remarks>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the hashed password.
        /// Use <see cref="SetPassword(string, int)"/> instead
        /// </summary>
        /// <remarks>
        /// If the value is invalid the user will be locked out until an administrator resets the password
        /// </remarks>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the roles this user account has
        /// </summary>
        public UserRoles Roles { get; set; }

        /// <summary>
        /// Gets or sets the streaming keys of this user
        /// </summary>
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

        /// <summary>
        /// Creates an instance with defaults
        /// </summary>
        public AccountInfo()
        {
            Enabled = true;
            Roles = UserRoles.User;
        }

        /// <summary>
        /// Gets all roles as string for use with the .NET Identity handler
        /// </summary>
        /// <returns>Role strings</returns>
        /// <remarks>
        /// This can be an empty string if no roles are enabled
        /// </remarks>
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

        /// <summary>
        /// Sets the hashed password
        /// </summary>
        /// <param name="NewPassword">User supplied password</param>
        /// <param name="Difficulty">
        /// Difficulty value. Doubling this number doubles time difficulty.
        /// Default: 100'000
        /// </param>
        public void SetPassword(string NewPassword, int Difficulty = 100000)
        {
            using (var Enc = new Rfc2898DeriveBytes(NewPassword, 16, Difficulty, HashAlgorithmName.SHA256))
            {
                Password = Convert.ToBase64String(Enc.Salt) + ":" + Difficulty + ":" + Convert.ToBase64String(Enc.GetBytes(16));
            }
        }

        /// <summary>
        /// Checks the supplied password against the stored hashed password.
        /// </summary>
        /// <param name="TestPassword">Password to test</param>
        /// <returns>
        /// true if password matches, false if argument not supplied or user password not set or invalid format.
        /// </returns>
        /// <remarks>
        /// This disregards whether the account is enabled or not
        /// </remarks>
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

        /// <summary>
        /// Checks if the user has a hashed password
        /// </summary>
        /// <returns>true, if hashed password present</returns>
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

        /// <summary>
        /// Removes a streaming key
        /// </summary>
        /// <param name="Key">Streaming key</param>
        /// <returns>Number of keys removed</returns>
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

        /// <summary>
        /// Removes a streaming key
        /// </summary>
        /// <param name="G">Streaming key</param>
        /// <returns>Number of keys removed</returns>
        public int RemoveKey(Guid G)
        {
            if (_keys != null)
            {
                return _keys.RemoveAll(m => m.Key == G);
            }
            return 0;
        }

        /// <summary>
        /// Adds a new streaming key
        /// </summary>
        /// <param name="Key">Streaming key</param>
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

        /// <summary>
        /// Checks if the given key is assigned to this user
        /// </summary>
        /// <param name="Key">Key</param>
        /// <returns>true, if this user has this key</returns>
        /// <remarks>Disregards whether the account is enabled or not</remarks>
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
            else if (!Regex.IsMatch(Username, UserManager.NamePattern))
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
}
