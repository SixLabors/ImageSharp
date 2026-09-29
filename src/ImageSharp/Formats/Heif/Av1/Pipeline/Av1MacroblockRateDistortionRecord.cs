// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Remembers the luma transform search results of the recent inter blocks of a superblock, keyed by a hash of the
/// block residual, so that a later search of the same residual reuses the result. Reference: MB_RD_RECORD with
/// get_block_residue_hash(), find_mb_rd_info(), fetch_mb_rd_info() and save_mb_rd_info().
/// </summary>
internal sealed class Av1MacroblockRateDistortionRecord
{
    /// <summary>
    /// The number of retained results. Reference: RD_RECORD_BUFFER_LEN.
    /// </summary>
    private const int Length = 8;

    private readonly Entry[] entries = new Entry[Length];
    private int count;
    private int start;

    /// <summary>
    /// Forgets every result. Reference: reset_mb_rd_record().
    /// </summary>
    public void Reset()
    {
        this.count = 0;
        this.start = 0;
    }

    /// <summary>
    /// Returns the hash of a block residual. Reference: get_block_residue_hash(), with the CRC-32C of
    /// av1_get_crc32c_value() over the 16-bit residual.
    /// </summary>
    /// <param name="residual">The residual of the block, one row after another.</param>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The hash.</returns>
    public static uint GetHash(ReadOnlySpan<short> residual, Av1BlockSize blockSize)
    {
        ReadOnlySpan<ulong> words = MemoryMarshal.Cast<short, ulong>(residual);
        uint crc = uint.MaxValue;
        foreach (ulong word in words)
        {
            crc = BitOperations.Crc32C(crc, word);
        }

        return (~crc << 5) + (uint)blockSize;
    }

    /// <summary>
    /// Finds a retained result for a residual. A search without a cost bound never reuses one.
    /// Reference: find_mb_rd_info().
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
    /// Returns a retained result. Reference: fetch_mb_rd_info().
    /// </summary>
    /// <param name="index">The index from <see cref="Find"/>.</param>
    /// <returns>The result.</returns>
    public ref readonly Entry Get(int index) => ref this.entries[index];

    /// <summary>
    /// Retains a search result, replacing the oldest when the record is full. Reference: save_mb_rd_info().
    /// </summary>
    /// <param name="hash">The residual hash.</param>
    /// <param name="statistics">The search result.</param>
    /// <param name="states">The chosen luma transform states.</param>
    /// <param name="modeInfo">The block with the chosen transform sizes.</param>
    public void Save(uint hash, Av1RateDistortionStatistics statistics, ReadOnlySpan<Av1EncoderTransformBlockState> states, in Av1EncoderBlockModeInfo modeInfo)
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
    /// One retained result. Reference: MB_RD_INFO.
    /// </summary>
    public struct Entry
    {
        /// <summary>The residual hash. Reference: hash_value.</summary>
        public uint Hash;

        /// <summary>The search result. Reference: rd_stats.</summary>
        public Av1RateDistortionStatistics Statistics;

        /// <summary>The number of chosen luma transform states.</summary>
        public int StateCount;

        /// <summary>The chosen luma transform states. Reference: tx_type_map.</summary>
        public InlineArray64<Av1EncoderTransformBlockState> States;

        /// <summary>The block transform size. Reference: tx_size.</summary>
        public Av1TransformSize TransformSize;

        /// <summary>The inter transform sizes. Reference: inter_tx_size.</summary>
        public InlineArray16<Av1TransformSize> InterTransformSizes;
    }
}
