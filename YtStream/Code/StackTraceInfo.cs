using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace YtStream
{
    public class StackTraceInfo
    {
        private const string Pattern = @"at\s+([^\(]+)\(([^\)]*)\)\s+in\s+(.+):line\s+(\d+)";

        public string Raw { get; private set; }

        public string Function { get; private set; }

        public string Arguments { get; private set; }

        public string Filename { get; private set; }

        public int Line { get; private set; }

        public StackTraceInfo(string TraceLine)
        {
            var Result = Regex.Match(TraceLine, Pattern);
            if (Result.Success)
            {
                Function = Result.Groups[1].Value;
                Arguments = Result.Groups[2].Value;
                Filename = Result.Groups[3].Value;
                Line = int.TryParse(Result.Groups[4].Value, out int i) ? i : 0;
            }
            Raw = TraceLine;
        }
    }
}
