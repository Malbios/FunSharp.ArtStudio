using System.Security.Cryptography;

namespace ArtStudio.Server.Comfy;

public sealed class SeedGenerator
{
    private readonly HashSet<long> _usedSeeds = [];
    private readonly Lock _lock = new();

    public long Next()
    {
        lock (_lock)
        {
            long seed;
            do
            {
                seed = BitConverter.ToInt64(RandomNumberGenerator.GetBytes(sizeof(long))) & long.MaxValue;
            } while (!_usedSeeds.Add(seed));
            return seed;
        }
    }
}
