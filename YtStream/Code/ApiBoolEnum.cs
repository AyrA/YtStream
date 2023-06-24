using System.Text.Json.Serialization;

namespace YtStream.Code
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ApiBoolEnum
    {
        Y = 1,
        N = 0
    }
}
