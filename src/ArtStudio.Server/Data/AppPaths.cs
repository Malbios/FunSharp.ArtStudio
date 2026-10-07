namespace ArtStudio.Server.Data;

public class AppPaths
{
    public AppPaths(string dataDirectory)
    {
        DataDirectory = dataDirectory;
        Directory.CreateDirectory(DataDirectory);
    }

    public string DataDirectory { get; }
    public string DatabasePath => Path.Combine(DataDirectory, "artstudio.db");
    public string DefaultOutputDirectory => Path.Combine(DataDirectory, "images");

    public static AppPaths FromConfiguration(IConfiguration configuration)
    {
        var configured = configuration["ArtStudio:DataDirectory"];
        var dataDirectory = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArtStudio")
            : configured;
        return new AppPaths(dataDirectory);
    }
}
