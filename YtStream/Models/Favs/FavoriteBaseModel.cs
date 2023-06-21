using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using YtStream.Interfaces;

namespace YtStream.Models.Favs
{
    [JsonDerivedType(typeof(PlayerFavoriteModel), (int)FavoriteType.Player)]
    [JsonDerivedType(typeof(StreamFavoriteModel), (int)FavoriteType.Stream)]
    public class FavoriteBaseModel : IValidateable
    {
        public FavoriteType Type { get; set; }

        public Guid Id { get; set; }

        public string? Name { get; set; }

        public virtual string[] GetValidationMessages()
        {
            var msgs = new List<string>();
            if (!Enum.IsDefined(Type) || Type == FavoriteType.None)
            {
                msgs.Add("Invalid favorite type");
            }
            if (string.IsNullOrEmpty(Name))
            {
                msgs.Add("Name is empty");
            }
            if (Id == Guid.Empty)
            {
                msgs.Add("Id is invalid");
            }
            return msgs.ToArray();
        }

        public virtual bool IsValid()
        {
            return Enum.IsDefined(Type) &&
                Type != FavoriteType.None &&
                !string.IsNullOrEmpty(Name) &&
                Id != Guid.Empty;
        }
    }
}
