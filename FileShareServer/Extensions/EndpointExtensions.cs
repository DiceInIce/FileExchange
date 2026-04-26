using FileShareServer.Hubs;
using FileShareServer.Constants;

namespace FileShareServer.Extensions
{
    public static class EndpointExtensions
    {
        public static WebApplication MapApplicationEndpoints(this WebApplication app)
        {
            var api = app.MapGroup(AppConstants.ApiRoutes.ApiPrefix);

            app.MapAuthEndpoints(api)
               .MapUserEndpoints(api)
               .MapFriendshipEndpoints(api)
               .MapChatEndpoints(api)
               .MapFileEndpoints(api, app);

            app.MapHub<ChatHub>("/chathub");

            return app;
        }
    }
}
