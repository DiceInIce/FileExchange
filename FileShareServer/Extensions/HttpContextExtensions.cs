using System.Security.Claims;

namespace FileShareServer.Extensions
{
    public static class HttpContextExtensions
    {
        public static int GetUserId(this HttpContext context)
        {
            var value = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(value, out var id) ? id : 0;
        }
    }
}
