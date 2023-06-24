using System.Collections.Generic;

namespace YtStream.Code
{
    public class HttpStatusDescriptions
    {
        private const string NoDescription = "No detailed description is available for this error type.";

        private static readonly Dictionary<int, string> descriptions = new()
        {
            { 400, "The server is refusing to complete your request because it doesn't satisfies the conditions." },
            { 401, "The server requires you to authenticate." },
            { 403, "You do not have access to this location, or are not allowed to perform the desired action." },
            { 404, "The URL you requested was not found on this server." },
            { 405, "Your browser used the wrong request type. " +
                "Most likely cause is that you copied the URL from a page that was the result of a form " +
                "and tried to open it in anorther browser." },
            { 410, "The URL you requested was not found on this server, " +
                "and the server knows that this URL is likely never to be valid again." }
        };

        public static string GetDescription(int HttpCode)
        {
            return descriptions.TryGetValue(HttpCode, out string? ret) ? ret : NoDescription;
        }
    }
}
