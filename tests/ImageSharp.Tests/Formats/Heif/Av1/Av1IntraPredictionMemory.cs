// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif.Av1;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

internal class Av1IntraPredictionMemory
{
    public const int Padding = 16;
    private const int TopPadding = 7;
    private const int MaxBlockSizeTimes2 = 1 << Av1Constants.MaxSuperBlockSizeLog2;
    private const int TotalPixelCount = 4096;
    private readonly byte[] destination = new byte[TotalPixelCount];
    private readonly byte[] referenceSource = new byte[TotalPixelCount];
    private readonly byte[] leftMemory = new byte[MaxBlockSizeTimes2 + Padding];
    private readonly byte[] topMemory = new byte[MaxBlockSizeTimes2 + Padding + TopPadding];
    private readonly int bitDepth;

    public Av1IntraPredictionMemory(int bitDepth) => this.bitDepth = bitDepth;

    public Span<byte> Destination => this.destination;

    public Span<byte> Left => this.leftMemory;

    public Span<byte> Top => this.topMemory;

    /// <summary>
    /// Sets referenceSource, left and top to <paramref name="value"/>.
    /// </summary>
    public void Set(byte value)
    {
        for (int r = 0; r < this.referenceSource.Length; r++)
        {
            this.referenceSource[r] = value;
        }

        // Upsampling in the directional predictors extends left/top[-1] to [-2].
        for (int i = Padding - 2; i < Padding + MaxBlockSizeTimes2; ++i)
        {
            this.leftMemory[i] = this.topMemory[i] = value;
        }
    }

    public void Scramble(Random rnd)
    {
        for (int i = 0; i < this.referenceSource.Length; i++)
        {
            this.referenceSource[i] = this.GetRandomValue(rnd);
        }

        for (int i = Padding; i < (MaxBlockSizeTimes2 >> 1) + Padding; ++i)
        {
            this.leftMemory[i] = this.GetRandomValue(rnd);
        }

        for (int i = Padding - 1; i < (MaxBlockSizeTimes2 >> 1) + Padding; ++i)
        {
            this.topMemory[i] = this.GetRandomValue(rnd);
        }

        // Some directional predictors require top-right, bottom-left.
        for (int i = MaxBlockSizeTimes2 >> 1; i < MaxBlockSizeTimes2; ++i)
        {
            this.leftMemory[Padding + i] = this.GetRandomValue(rnd);
            this.topMemory[Padding + i] = this.GetRandomValue(rnd);
        }

        // TODO(jzern): reorder this and regenerate the digests after switching
        // random number generators.
        // Upsampling in the directional predictors extends left/top[-1] to [-2].
        this.leftMemory[Padding - 1] = this.GetRandomValue(rnd);
        this.leftMemory[Padding - 2] = this.GetRandomValue(rnd);
        this.topMemory[Padding - 2] = this.GetRandomValue(rnd);
        this.leftMemory.AsSpan(0, Padding - 2).Clear();
        this.topMemory.AsSpan(0, Padding - 2).Clear();
        this.topMemory.AsSpan(MaxBlockSizeTimes2 + Padding, TopPadding).Clear();
    }

    public void CopySourceToDestination() => this.referenceSource.CopyTo(this.destination.AsSpan());

    /// <summary>
    /// Return a hash string of a block of bytes.
    /// </summary>
    /// <remarks>This test design is used extensively in 'libgav1' tests, some of which are ported here.</remarks>
    public static string GetDigest(byte[] input)
    {
        byte[] hash = MD5.HashData(input);
        StringBuilder sb = new();
        for (int i = 0; i < hash.Length; i++)
        {
            sb.Append(hash[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    public string GetDestinationDigest() => GetDigest(this.destination);

    private byte GetRandomValue(Random random) => (byte)(random.Next(1 << 16) & ((1 << this.bitDepth) - 1));
}
