using FileShareServer.Constants;
using FileShareServer.DTOs;
using FileShareServer.Services;

namespace FileShareServer.Extensions
{
    public static class AuthEndpointExtensions
    {
        public static WebApplication MapAuthEndpoints(this WebApplication app, RouteGroupBuilder api)
        {
            var authApi = api.MapGroup(AppConstants.ApiRoutes.Auth.Root).WithName("Auth");

            authApi.MapPost(AppConstants.ApiRoutes.Auth.Register, async (AuthService authService, RegisterRequest request) =>
            {
                var (success, token, user) = await authService.RegisterAsync(request.Username, request.Email, request.Password);
                if (!success)
                {
                    return Results.BadRequest(AppConstants.ErrorMessages.UsernameExists);
                }

                return Results.Ok(new AuthResponseDto
                {
                    Token = token,
                    User = new UserDetailDto
                    {
                        Id = user?.Id ?? 0,
                        Username = user?.Username ?? "",
                        Email = user?.Email ?? "",
                        DisplayName = user?.DisplayName,
                        IsOnline = user?.IsOnline ?? false
                    }
                });
            })
            .WithName("Register");

            authApi.MapPost(AppConstants.ApiRoutes.Auth.Login, async (AuthService authService, LoginRequest request) =>
            {
                var (success, token, user) = await authService.LoginAsync(request.Username, request.Password);
                if (!success)
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new AuthResponseDto
                {
                    Token = token,
                    User = new UserDetailDto
                    {
                        Id = user?.Id ?? 0,
                        Username = user?.Username ?? "",
                        Email = user?.Email ?? "",
                        DisplayName = user?.DisplayName,
                        IsOnline = user?.IsOnline ?? false
                    }
                });
            })
            .WithName("Login");

            return app;
        }
    }
}
