using AyrA.AutoDI;
using System;
using System.IO;
using YtStream.Models.Mp3;

namespace YtStream.Services.Mp3
{
    [AutoDIRegister(AutoDIType.Singleton)]
    /// <summary>
    /// MP3 utility functions
    /// </summary>
    public class Mp3InfoService
    {
        /// <summary>
        /// Gets the first header of an MP3 file
        /// </summary>
        /// <param name="Filename">MP3 file</param>
        /// <returns>Header</returns>
        public Mp3HeaderModel GetFirstHeader(string Filename)
        {
            using (var FS = File.OpenRead(Filename))
            {
                return GetFirstHeader(FS);
            }
        }

        /// <summary>
        /// Gets the first header of an MP3 stream
        /// </summary>
        /// <param name="Source">Stream</param>
        /// <returns>Header</returns>
        /// <remarks>
        /// Stream is not rewound or disposed.
        /// Stream position is directly after the header on success, or at the end if no header was found
        /// </remarks>
        public Mp3HeaderModel GetFirstHeader(Stream Source)
        {
            var FEx = new FormatException("Supplied file is not a valid MP3 file");
            byte[] Header = new byte[4];
            int read = Source.Read(Header, 0, Header.Length);
            while (read > 0)
            {
                if (Mp3HeaderModel.IsHeader(Header))
                {
                    return new Mp3HeaderModel(Header);
                }
                //Do primitive header checks to weed out "obviously wrong" headers without overhead of a function call.
                do
                {
                    int Next = Source.ReadByte();
                    if (Next < 0) //EOF
                    {
                        throw FEx;
                    }
                    Header[0] = Header[1];
                    Header[1] = Header[2];
                    Header[2] = Header[3];
                    Header[3] = (byte)Next;
                } while (Header[0] != 0xFF);
            }
            throw FEx;
        }

        /// <summary>
        /// Checks if the given MP3 file is CBR
        /// </summary>
        /// <param name="Filename">MP3 file name</param>
        /// <returns>true if CBR</returns>
        public bool IsCBR(string Filename)
        {
            using (var FS = File.OpenRead(Filename))
            {
                return IsCBR(FS);
            }
        }

        /// <summary>
        /// Checks if the given stream is a CBR MP3 file
        /// </summary>
        /// <param name="Source">MP3 data stream</param>
        /// <returns>true if CBR</returns>
        public bool IsCBR(Stream Source)
        {
            Mp3HeaderModel Current = GetFirstHeader(Source);
            Mp3HeaderModel Compare;
            do
            {
                //Discard audio and check if end of stream
                if (Source.Read(new byte[Current.NumberOfBytes], 0, Current.NumberOfBytes) != Current.NumberOfBytes)
                {
                    //No more audio data. If still in the loop, it's CBR
                    return true;
                }
                //Try to read next header
                try
                {
                    Compare = GetFirstHeader(Source);
                }
                catch
                {
                    //No more headers. If still in the loop, it's CBR
                    return true;
                }
                if (Compare.AudioFrequency != Current.AudioFrequency || Compare.AudioRate != Current.AudioRate)
                {
                    //VBR if headers mismatch in rate or frequency
                    return false;
                }
                Current = Compare;
            } while (true);
        }
    }
}
