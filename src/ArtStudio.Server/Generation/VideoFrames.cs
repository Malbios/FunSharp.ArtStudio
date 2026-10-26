using ArtStudio.Server.Api;
using ArtStudio.Server.Domain;

namespace ArtStudio.Server.Generation;

public sealed record VideoFrame(string Key, string Url, string Label, string Path);

/// <summary>
/// The images a set's videos can start or end on: its generated images, its own uploaded or pasted image, and frames
/// earlier video jobs used that are neither (an uploaded end image). Keys identify a frame within its set.
/// </summary>
public sealed class VideoFrames
{
    private readonly List<VideoFrame> _frames = [];

    public VideoFrames(PromptSet set, IEnumerable<GeneratedImage> images, IEnumerable<GenerationJob> jobs)
    {
        foreach (var (image, index) in images.OrderBy(i => i.Id).Select((image, index) => (image, index)))
            Add(new VideoFrame($"image:{image.Id}", ApiUrls.Image(image.Id), $"Image {index + 1}", image.FilePath));

        // Only the user's own images: a deviation that inspired the set is someone else's work.
        if (set is { SourceImagePath: { } sourcePath, SourceKind: SourceKind.Upload or SourceKind.Paste })
            Add(new VideoFrame("source", ApiUrls.SourceImage(set)!, "Uploaded image", sourcePath));

        foreach (var job in jobs.Where(j => j.Kind == JobKind.Video).OrderBy(j => j.Id))
        {
            if (job.VideoStartImagePath is { } start)
                Add(new VideoFrame($"job:{job.Id}:start", ApiUrls.VideoFrame(job.Id, "start"), $"Start frame of video job #{job.Id}", start));
            if (job.VideoEndImagePath is { } end)
                Add(new VideoFrame($"job:{job.Id}:end", ApiUrls.VideoFrame(job.Id, "end"), $"End frame of video job #{job.Id}", end));
        }
    }

    public IReadOnlyList<VideoFrame> All => _frames;

    // The first frame for a file wins, so a generated image is offered as itself, not as a job's frame.
    private void Add(VideoFrame frame)
    {
        if (!_frames.Any(existing => string.Equals(existing.Path, frame.Path, StringComparison.OrdinalIgnoreCase)))
            _frames.Add(frame);
    }

    public string PathOf(string key) =>
        _frames.FirstOrDefault(f => f.Key == key)?.Path ?? throw new UserFacingException("That frame is not one of this set's images.");

    public string? KeyOf(string? path) =>
        path is null ? null : _frames.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase))?.Key;
}
