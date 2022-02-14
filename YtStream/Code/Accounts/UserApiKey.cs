using System;
using System.Collections.Generic;

namespace YtStream.Accounts
{
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
}
