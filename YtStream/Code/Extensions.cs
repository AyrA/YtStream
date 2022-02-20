using System.Text;
using System.Text.Json;

namespace YtStream
{
    /// <summary>
    /// Provides extension methods
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Parse a JSON into an object
        /// </summary>
        /// <typeparam name="T">target type</typeparam>
        /// <param name="s">String to parse</param>
        /// <param name="Throw">
        /// true to throw instead of returning the types default value
        /// </param>
        /// <returns>Deserialized data</returns>
        public static T FromJson<T>(this string s, bool Throw = false)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return JsonSerializer.Deserialize<T>(s);
                }
            }
            catch
            {
                if (Throw)
                {
                    throw;
                }
            }
            return default;
        }

        /// <summary>
        /// Serialize an object as JSON
        /// </summary>
        /// <param name="o">Object</param>
        /// <param name="Pretty">print nicely instead of as compact as possible</param>
        /// <returns>Serialized data</returns>
        public static string ToJson(this object o, bool Pretty = false)
        {
            var Opt = new JsonSerializerOptions()
            {
                WriteIndented = Pretty
            };
            return JsonSerializer.Serialize(o, Opt);
        }

        /// <summary>
        /// Clones an object via JSON serialization and deserialization
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <param name="o">Object</param>
        /// <param name="Throw">true to throw on deserialization error</param>
        /// <returns>Object clone</returns>
        public static T JsonClone<T>(this T o, bool Throw = false)
        {
            if (o == null)
            {
                return o;
            }
            return o.ToJson().FromJson<T>(Throw);
        }

        /// <summary>
        /// Convert byte array into UTF8 string
        /// </summary>
        /// <param name="data">Byte array</param>
        /// <returns>UTF8 string</returns>
        /// <remarks>
        /// <paramref name="data"/> should be valid UTF-8
        /// </remarks>
        public static string Utf8(this byte[] data)
        {
            return Encoding.UTF8.GetString(data);
        }

        /// <summary>
        /// Convert string into UTF8 byte array
        /// </summary>
        /// <param name="data">String</param>
        /// <returns>UTF8 byte array</returns>
        public static byte[] Utf8(this string data)
        {
            return Encoding.UTF8.GetBytes(data);
        }
    }
}
