using System.Text.RegularExpressions;
using ArtStudio.Server.Generation;

namespace ArtStudio.Server.DeviantArt;

public sealed partial record DeviationUrl(string Url, string DeviationId, string? Username)
{
    [GeneratedRegex(@"^/(?<user>[A-Za-z0-9_-]+)/art/(?:[^/]*-)?(?<id>\d+)/?$")]
    private static partial Regex UserArtPath();

    [GeneratedRegex(@"^/art/(?:[^/]*-)?(?<id>\d+)/?$")]
    private static partial Regex SubdomainArtPath();

    [GeneratedRegex(@"^/deviation/(?<id>\d+)/?$")]
    private static partial Regex DeviationPath();

    public static DeviationUrl Parse(string input)
    {
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !IsDeviantArtHost(uri))
            throw new UserFacingException("That is not a DeviantArt URL.");

        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        var subdomain = uri.Host.Split('.')[0].ToLowerInvariant();

        if (UserArtPath().Match(path) is { Success: true } userArt)
            return Create(userArt.Groups["id"].Value, userArt.Groups["user"].Value);
        if (SubdomainArtPath().Match(path) is { Success: true } subdomainArt && subdomain is not ("www" or "deviantart"))
            return Create(subdomainArt.Groups["id"].Value, subdomain);
        if (DeviationPath().Match(path) is { Success: true } deviation)
            return Create(deviation.Groups["id"].Value, null);

        throw new UserFacingException("That DeviantArt URL does not point to a deviation.");

        DeviationUrl Create(string id, string? username) =>
            new($"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}", id, username);
    }

    public static string? UsernameFromProfileOrArtUrl(string input)
    {
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) || !IsDeviantArtHost(uri))
            return null;

        var subdomain = uri.Host.Split('.')[0];
        if (subdomain is not ("www" or "deviantart"))
            return subdomain;
        return uri.Segments.Skip(1).FirstOrDefault()?.TrimEnd('/') is { Length: > 0 } user ? user : null;
    }

    private static bool IsDeviantArtHost(Uri uri) =>
        uri.Host.Equals("deviantart.com", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.EndsWith(".deviantart.com", StringComparison.OrdinalIgnoreCase);
}
