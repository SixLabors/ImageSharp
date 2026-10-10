// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Keeps the luma transform search results of the recent inter blocks of a superblock. A hash of the block residual is the key.
/// A later search of the same residual reuses the result.
/// </summary>
internal sealed class Av1MacroblockRateDistortionRecord
{
    /// <summary>
    /// The number of retained results. The record is a ring buffer of this length.
    /// </summary>
    private const int Length = 8;

    private readonly Entry[] entries = new Entry[Length];
    private int count;
    private int start;

    /// <summary>
    /// Forgets every result.
    /// </summary>
    public void Reset()
    {
        this.count = 0;
        this.start = 0;
    }

    /// <summary>
    /// Returns the hash of a block residual. The hash is the CRC-32C of the 16-bit residual, shifted left by 5 bits, plus the block size.
    /// </summary>
    /// <param name="residual">The residual of the block, one row after another.</param>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The hash.</returns>
    public static uint GetHash(ReadOnlySpan<short> residual, Av1BlockSize blockSize)
    {
        // Read the residual as 64-bit words. Every block width is a multiple of four samples, so no tail remains.
        ReadOnlySpan<ulong> words = MemoryMarshal.Cast<short, ulong>(residual);
        uint crc = uint.MaxValue;
        foreach (ulong word in words)
        {
            crc = BitOperations.Crc32C(crc, word);
        }

        // Invert the CRC to finish it. The low 5 bits then hold the block size, so equal residuals of different block sizes get different hashes.
        return (~crc << 5) + (uint)blockSize;
    }

    /// <summary>
    /// Finds a retained result for a residual. A search without a cost bound never reuses one.
    /// </summary>
    /// <param name="costLimit">The cost bound of the search.</param>
    /// <param name="hash">The residual hash.</param>
    /// <returns>The index of the result, or -1.</returns>
    public int Find(long costLimit, uint hash)
    {
        if (costLimit == long.MaxValue)
        {
            return -1;
        }

        for (int i = 0; i < this.count; i++)
        {
            int index = (this.start + i) % Length;
            if (this.entries[index].Hash == hash)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Returns a retained result.
    /// </summary>
    /// <param name="index">The index from <see cref="Find"/>.</param>
    /// <returns>The result.</returns>
    public ref readonly Entry Get(int index) => ref this.entries[index];

    /// <summary>
    /// Retains a search result. When the record is full, the new result replaces the oldest result.
    /// </summary>
    /// <param name="hash">The residual hash.</param>
    /// <param name="statistics">The search result.</param>
    /// <param name="states">The chosen luma transform states.</param>
    /// <param name="modeInfo">The block with the chosen transform sizes.</param>
    public void Save(uint hash, Av1RateDistortionStatistics statistics, ReadOnlySpan<Av1EncoderTransformBlockState> states, Av1EncoderBlockModeInfo modeInfo)
    {
        int index;
        if (this.count < Length)
        {
            index = (this.start + this.count) % Length;
            this.count++;
        }
        else
        {
            index = this.start;
            this.start = (this.start + 1) % Length;
        }

        ref Entry entry = ref this.entries[index];
        entry.Hash = hash;
        entry.Statistics = statistics;
        entry.StateCount = states.Length;
        states.CopyTo(entry.States);
        entry.TransformSize = modeInfo.TransformSize;
        modeInfo.InterTransformSizes.CopyTo(entry.InterTransformSizes);
    }

    /// <summary>
    /// One retained result.
    /// </summary>
    public struct Entry
    {
        /// <summary>The residual hash.</summary>
        public uint Hash;

        /// <summary>The search result.</summary>
        public Av1RateDistortionStatistics Statistics;

        /// <summary>The number of chosen luma transform states.</summary>
        public int StateCount;

        /// <summary>The chosen luma transform states.</summary>
        public InlineArray64<Av1EncoderTransformBlockState> States;

        /// <summary>The block transform size.</summary>
        public Av1TransformSize TransformSize;

        /// <summary>The inter transform sizes.</summary>
        public InlineArray16<Av1TransformSize> InterTransformSizes;
    }
}
