using FileShareServer.Constants;
using FileShareServer.DTOs;
using FileShareServer.Services;

namespace FileShareServer.Extensions
{
    public static class UserEndpointExtensions
    {
        public static WebApplication MapUserEndpoints(this WebApplication app, RouteGroupBuilder api)
        {
            var userApi = api.MapGroup(AppConstants.ApiRoutes.Users.Root)
                .WithName("Users")
                .RequireAuthorization();

            userApi.MapGet("/", async (UserService userService) =>
            {
                var users = await userService.GetAllUsersAsync();
                return Results.Ok(users.Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    IsOnline = u.IsOnline
                }));
            })
            .WithName("GetAllUsers");

            userApi.MapGet("/{id}", async (int id, UserService userService) =>
            {
                var user = await userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    return Results.NotFound();
                }

                return Results.Ok(new UserDetailDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    DisplayName = user.DisplayName,
                    IsOnline = user.IsOnline
                });
            })
            .WithName("GetUser");

            userApi.MapGet(AppConstants.ApiRoutes.Users.Search, async (string query, HttpContext httpContext, UserService userService) =>
            {
                var currentUserId = httpContext.GetUserId();
                var users = await userService.SearchUsersAsync(query);

                return Results.Ok(users
                    .Where(u => u.Id != currentUserId)
                    .Select(u => new UserDto
                    {
                        Id = u.Id,
                        Username = u.Username,
                        DisplayName = u.DisplayName,
                        IsOnline = u.IsOnline
                    }));
            })
            .WithName("SearchUsers")
            .RequireAuthorization();

            return app;
        }
    }
}
