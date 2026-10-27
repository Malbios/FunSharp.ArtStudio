using ArtStudio.Server.DeviantArt;

namespace ArtStudio.Server.Tests.Fakes;

public sealed class FakeFolderOpener : IFolderOpener
{
    public List<string> Opened { get; } = [];
    public List<string> Closed { get; } = [];

    public void Open(string folder)
    {
        lock (Opened)
            Opened.Add(folder);
    }

    public void Close(string folder)
    {
        lock (Closed)
            Closed.Add(folder);
    }
}
