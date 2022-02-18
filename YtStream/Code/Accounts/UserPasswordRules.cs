using System.Text.RegularExpressions;

namespace YtStream.Accounts
{
    /// <summary>
    /// Represents password rules and tests passwords against them
    /// </summary>
    public class UserPasswordRules
    {
        /// <summary>
        /// Minimum password length in characters
        /// </summary>
        public int MinimumLength { get; set; }

        /// <summary>
        /// Require uppercase
        /// </summary>
        public bool Uppercase { get; set; }

        /// <summary>
        /// Require lowercase
        /// </summary>
        public bool Lowercase { get; set; }

        /// <summary>
        /// Require digits
        /// </summary>
        public bool Digits { get; set; }

        /// <summary>
        /// Require symbols
        /// </summary>
        public bool Symbols { get; set; }

        /// <summary>
        /// Require a given number of categories
        /// </summary>
        /// <remarks>Categories are: Uppercase, lowercase, digit, symbol</remarks>
        public int RuleCount { get; set; }

        /// <summary>
        /// Create a default instance (8 chars, 3 out of 4 categories)
        /// </summary>
        /// <remarks>This rule set is pretty much the default rule set of active directory</remarks>
        public UserPasswordRules()
        {
            MinimumLength = UserManager.PasswordMinLength;
            Uppercase = Lowercase = Digits = Symbols = false;
            RuleCount = 3;
        }

        /// <summary>
        /// Test a password against current complexity rules
        /// </summary>
        /// <param name="Password">Password</param>
        /// <returns>true, if password passes the rules</returns>
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
}
