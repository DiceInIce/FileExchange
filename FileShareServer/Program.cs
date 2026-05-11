using System.Data;
using FileShareServer.Data;
using FileShareServer.Extensions;
using FileShareServer.Constants;
using FileShareServer.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "fileshare.db");

// Ограничения серверной загрузки файлов
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB
});

builder.Services
    .AddApplicationDatabase(builder.Configuration, dbPath)
    .AddApplicationServices()
    .AddSingleton<IUserConnectionManager, UserConnectionManager>()
    .AddApplicationCors()
    .AddApplicationAuthentication(builder.Configuration)
    .AddSignalR();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();

    if (!HasUsersTable(db))
    {
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors(AppConstants.CorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.MapApplicationEndpoints();

app.Run();

static bool HasUsersTable(ApplicationDbContext db)
{
    using var connection = db.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open)
    {
        connection.Open();
    }

    using var command = connection.CreateCommand();
    command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='Users';";
    var result = command.ExecuteScalar();

    return result is long count && count > 0;
}
