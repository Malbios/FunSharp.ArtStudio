using ArtStudio.Server;
using ArtStudio.Server.Api;
using ArtStudio.Server.Data;
using ArtStudio.Server.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddArtStudio();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
    await db.Database.MigrateAsync();
    await SettingsStore.LoadAsync(db, scope.ServiceProvider.GetRequiredService<AppPaths>());
}

app.UseMiddleware<UserFacingErrorMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapSetEndpoints();
app.MapQueueEndpoints();
app.MapDeviantArtEndpoints();
app.MapSettingsEndpoints();
app.MapHub<StudioHub>("/hubs/studio");
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
