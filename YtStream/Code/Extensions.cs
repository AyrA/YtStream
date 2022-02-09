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
    }
}
