using ArtStudio.Server.DeviantArt;

namespace ArtStudio.Server.Tests.Fakes;

public sealed class FakeFolderOpener : IFolderOpener
{
    public List<string> Opened { get; } = [];

    public void Open(string folder)
    {
        lock (Opened)
            Opened.Add(folder);
    }
}
