using Microsoft.AspNetCore.Authentication.Cookies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace YtStream.Accounts
{
    public class AccountInfo : IValidateable
    {
        private List<UserApiKey> _keys;

        public bool Enabled { get; set; }

        public bool DisableAds { get; set; }

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
