using System;

public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);
    float NextFloat();
}

/// <summary>
/// Small deterministic PRNG used for run simulation. Its output is stable for a given seed
/// within the game version and does not depend on UnityEngine.Random or runtime string hashing.
/// </summary>
public sealed class DeterministicRandom : IRandomSource
{
    uint _state;

    public DeterministicRandom(int seed)
    {
        _state = unchecked((uint)seed);
        if (_state == 0) _state = 0x6D2B79F5u;
    }

    public uint State => _state;
    public static DeterministicRandom Restore(uint state)
    {
        if (state == 0) throw new ArgumentException("Invalid zero PRNG state.", nameof(state));
        return new DeterministicRandom(unchecked((int)state));
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));

        uint range = (uint)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextUInt() % range);
    }

    public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

    uint NextUInt()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x;
    }
}

public sealed class RunRandomContext
{
    public string SeedText { get; }
    public int RootSeed { get; }

    public RunRandomContext(string seedText)
    {
        SeedText = NormalizeSeed(seedText);
        RootSeed = StableHash(SeedText);
    }

    public IRandomSource CreateStream(string streamName, int context = 0)
    {
        if (string.IsNullOrWhiteSpace(streamName))
            throw new ArgumentException("A stream name is required.", nameof(streamName));

        return new DeterministicRandom(DeriveSeed(RootSeed, streamName, context));
    }

    public static string NormalizeSeed(string value)
    {
        string normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? "DEALBREAK" : normalized;
    }

    public static int StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619u;
            }
            return (int)hash;
        }
    }

    public static int DeriveSeed(int rootSeed, string streamName, int context)
    {
        unchecked
        {
            uint hash = (uint)rootSeed;
            hash ^= (uint)StableHash(streamName);
            hash *= 16777619u;
            hash ^= (uint)context;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            return (int)hash;
        }
    }
}
