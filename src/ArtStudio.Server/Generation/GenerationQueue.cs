using ArtStudio.Server.Domain;

namespace ArtStudio.Server.Generation;

/// <summary>The ComfyUI queue; ids are generation job ids.</summary>
public sealed class GenerationQueue : WorkQueue
{
    /// <summary>The kind of job ComfyUI ran last, so its model is still loaded; unknown after a restart.</summary>
    public JobKind? LoadedKind { get; set; }

    /// <summary>
    /// Waiting jobs in the order they run. Switching ComfyUI between the image and the video model takes long, so all
    /// waiting jobs of the loaded kind run first; within a kind, the queue position decides.
    /// </summary>
    public IReadOnlyList<T> InRunOrder<T>(IEnumerable<T> waiting, Func<T, JobKind> kindOf, Func<T, long> positionOf)
    {
        var ordered = waiting.OrderBy(positionOf).ToList();
        var firstKind = ordered.Any(job => kindOf(job) == LoadedKind)
            ? LoadedKind
            : ordered.Select(job => (JobKind?)kindOf(job)).FirstOrDefault();
        return [.. ordered.Where(job => kindOf(job) == firstKind), .. ordered.Where(job => kindOf(job) != firstKind)];
    }
}
