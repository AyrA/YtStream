using System.Text.RegularExpressions;

namespace YtStream.Accounts
{
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
}
