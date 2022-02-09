using System.Text.Json;

namespace YtStream
{
    public static class Extensions
    {
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
