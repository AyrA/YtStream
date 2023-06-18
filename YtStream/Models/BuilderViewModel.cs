using System;
using System.Collections.Generic;

namespace YtStream.Models
{
    public class BuilderViewModel
    {
        public Dictionary<Guid, string> StreamKeys { get; } = new();
    }
}
