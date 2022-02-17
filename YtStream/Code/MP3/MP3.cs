using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace YtStream.MP3
{
    public static class MP3
    {
        public static MP3Header GetInfo(string Filename)
        {
            using(var FS = File.OpenRead(Filename))
            {
                return GetInfo(FS);
            }
        }

        public static MP3Header GetInfo(Stream Source)
        {
            throw new NotImplementedException();
        }
    }
}
