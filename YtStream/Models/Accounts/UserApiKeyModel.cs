using System;
using System.Collections.Generic;
using YtStream.Interfaces;

namespace YtStream.Models.Accounts
{
    /// <summary>
    /// Represents an API key
    /// </summary>
    /// <remarks>
    /// API keys are currently for streaming only.
    /// It's the developers task to ensure that no two users share the same key.
    /// </remarks>
    public class UserApiKeyModel : IValidateable
    {
        /// <summary>
        /// Gets or sets the key
        /// </summary>
        public Guid Key { get; set; }

        /// <summary>
        /// Gets or sets the date this instance was created at
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the name
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Creates a new key without a name
        /// </summary>
        public UserApiKeyModel()
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
