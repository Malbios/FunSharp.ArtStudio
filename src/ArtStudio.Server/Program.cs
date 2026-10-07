using ArtStudio.Server.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var paths = AppPaths.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(paths);
builder.Services.AddDbContext<StudioDbContext>(options => options.UseSqlite($"Data Source={paths.DatabasePath}"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
    await db.Database.MigrateAsync();
    await SettingsStore.LoadAsync(db, paths);
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
