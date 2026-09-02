// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Indexes visible 8x8 luma blocks for intra-block-copy motion search.
/// </summary>
internal readonly struct Av1IntraBlockCopySearchIndex
{
    private const int BlockSize = 8;
    private const int MaximumBucketCount = 1 << 16;
    private const int MaximumCandidatesPerBucket = 256;
    private const uint HorizontalHashMultiplier = 257;
    private const uint VerticalHashMultiplier = 65599;
    private static readonly uint HorizontalLeadingWeight = GetLeadingWeight(HorizontalHashMultiplier);
    private static readonly uint VerticalLeadingWeight = GetLeadingWeight(VerticalHashMultiplier);
    private readonly Memory<byte> storage;
    private readonly int hashLinkLength;
    private readonly int bucketCount;
    private readonly int headOffset;
    private readonly int tailOffset;
    private readonly int countOffset;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1IntraBlockCopySearchIndex"/> struct over picture-lifetime storage.
    /// </summary>
    /// <param name="storage">The packed hash-link and bucket storage.</param>
    /// <param name="width">The visible luma width.</param>
    /// <param name="height">The visible luma height.</param>
    public Av1IntraBlockCopySearchIndex(Memory<byte> storage, int width, int height)
    {
        this.OriginWidth = Math.Max(0, width - BlockSize + 1);
        this.OriginHeight = Math.Max(0, height - BlockSize + 1);
        this.hashLinkLength = this.OriginWidth == 0 || this.OriginHeight == 0
            ? 0
            : checked(this.OriginWidth * height);

        this.bucketCount = GetBucketCount(this.OriginWidth, this.OriginHeight);
        this.headOffset = checked(this.hashLinkLength * sizeof(int));
        this.tailOffset = checked(this.headOffset + (this.bucketCount * sizeof(int)));
        this.countOffset = checked(this.tailOffset + (this.bucketCount * sizeof(int)));
        this.storage = storage;
    }

    /// <summary>
    /// Defines sample-width-specific search arithmetic for the closed generic encoder path.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    internal interface ISearchOperation<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Converts one native sample into the unsigned hash domain.
        /// </summary>
        /// <param name="sample">The sample to convert.</param>
        /// <returns>The unsigned sample value.</returns>
        public static abstract uint GetHashSample(TSample sample);

        /// <summary>
        /// Compares two complete 8x8 blocks.
        /// </summary>
        /// <param name="plane">The plane containing both blocks.</param>
        /// <param name="first">The first block origin.</param>
        /// <param name="second">The second block origin.</param>
        /// <returns><see langword="true"/> when every sample is equal.</returns>
        public static abstract bool BlocksEqual(Buffer2DRegion<TSample> plane, Point first, Point second);

        /// <summary>
        /// Gets the normalized 8x8 variance between a source block and reconstructed predictor.
        /// </summary>
        /// <param name="source">The coded source plane.</param>
        /// <param name="sourceOrigin">The source block origin.</param>
        /// <param name="reconstruction">The reconstructed luma plane.</param>
        /// <param name="predictionOrigin">The predictor block origin.</param>
        /// <param name="bitDepth">The coded sample precision.</param>
        /// <returns>The variance in the eight-bit distortion domain.</returns>
        public static abstract int GetVariance(
            Buffer2DRegion<TSample> source,
            Point sourceOrigin,
            Buffer2DRegion<TSample> reconstruction,
            Point predictionOrigin,
            Av1BitDepth bitDepth);
    }

    /// <summary>
    /// Gets the visible horizontal origin count represented by the index.
    /// </summary>
    public int OriginWidth { get; }

    /// <summary>
    /// Gets the visible vertical origin count represented by the index.
    /// </summary>
    public int OriginHeight { get; }

    /// <summary>
    /// Gets the packed storage length required for a visible frame.
    /// </summary>
    /// <param name="width">The visible luma width.</param>
    /// <param name="height">The visible luma height.</param>
    /// <returns>The required byte length.</returns>
    public static int GetStorageLength(int width, int height)
    {
        int originWidth = Math.Max(0, width - BlockSize + 1);
        int originHeight = Math.Max(0, height - BlockSize + 1);
        if (originWidth == 0 || originHeight == 0)
        {
            return 0;
        }

        int hashLinkLength = checked(originWidth * height);
        int bucketCount = GetBucketCount(originWidth, originHeight);
        return checked(
            (hashLinkLength * sizeof(int)) +
            (bucketCount * sizeof(int) * 2) +
            (bucketCount * sizeof(ushort)));
    }

    /// <summary>
    /// Builds the complete visible-frame hash index into its picture-lifetime storage.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperation">The closed sample operation.</typeparam>
    /// <param name="source">The coded source luma plane.</param>
    public void Initialize<TSample, TOperation>(Buffer2DRegion<TSample> source)
        where TSample : unmanaged
        where TOperation : struct, ISearchOperation<TSample>
    {
        if (this.hashLinkLength == 0)
        {
            return;
        }

        Span<int> hashesAndLinks = this.GetHashesAndLinks();
        Span<int> heads = this.GetHeads();
        Span<int> tails = this.GetTails();
        Span<ushort> counts = this.GetCounts();
        heads.Clear();
        tails.Clear();
        counts.Clear();

        for (int row = 0; row < source.Height; row++)
        {
            ReadOnlySpan<TSample> sourceRow = source.DangerousGetRowSpan(row);
            int hashRowOffset = row * this.OriginWidth;
            uint hash = 0;
            for (int column = 0; column < BlockSize; column++)
            {
                hash = unchecked((hash * HorizontalHashMultiplier) + TOperation.GetHashSample(sourceRow[column]));
            }

            hashesAndLinks[hashRowOffset] = (int)hash;
            for (int column = 1; column < this.OriginWidth; column++)
            {
                uint previous = TOperation.GetHashSample(sourceRow[column - 1]);
                uint next = TOperation.GetHashSample(sourceRow[column + BlockSize - 1]);
                hash = unchecked(((hash - (previous * HorizontalLeadingWeight)) * HorizontalHashMultiplier) + next);
                hashesAndLinks[hashRowOffset + column] = (int)hash;
            }
        }

        for (int column = 0; column < this.OriginWidth; column++)
        {
            uint hash = 0;
            for (int row = 0; row < BlockSize; row++)
            {
                hash = unchecked((hash * VerticalHashMultiplier) + (uint)hashesAndLinks[(row * this.OriginWidth) + column]);
            }

            for (int row = 0; row < this.OriginHeight; row++)
            {
                int position = (row * this.OriginWidth) + column;
                uint previous = (uint)hashesAndLinks[position];
                hashesAndLinks[position] = (int)hash;
                if (row + 1 < this.OriginHeight)
                {
                    uint next = (uint)hashesAndLinks[((row + BlockSize) * this.OriginWidth) + column];
                    hash = unchecked(((hash - (previous * VerticalLeadingWeight)) * VerticalHashMultiplier) + next);
                }
            }
        }

        // Coarse-to-fine insertion disperses the first 256 identical blocks across the image instead of
        // retaining one dense cluster. Links occupy the hash workspace after every hash has been derived.
        int step = BlockSize;
        int columnOffset = 0;
        int rowOffset = 0;
        while (step > 1)
        {
            for (int column = columnOffset; column < this.OriginWidth; column += step)
            {
                for (int row = rowOffset; row < this.OriginHeight; row += step)
                {
                    int position = (row * this.OriginWidth) + column;
                    int bucket = hashesAndLinks[position] & (this.bucketCount - 1);
                    if (counts[bucket] < MaximumCandidatesPerBucket)
                    {
                        int encodedPosition = position + 1;
                        hashesAndLinks[position] = 0;
                        if (heads[bucket] == 0)
                        {
                            heads[bucket] = encodedPosition;
                        }
                        else
                        {
                            hashesAndLinks[tails[bucket] - 1] = encodedPosition;
                        }

                        tails[bucket] = encodedPosition;
                        counts[bucket]++;
                    }
                }
            }

            if (columnOffset == 0 && rowOffset == 0)
            {
                columnOffset = step / 2;
            }
            else if (columnOffset == step / 2 && rowOffset == 0)
            {
                columnOffset = 0;
                rowOffset = step / 2;
            }
            else if (columnOffset == 0 && rowOffset == step / 2)
            {
                columnOffset = step / 2;
            }
            else
            {
                step /= 2;
                columnOffset = step / 2;
                rowOffset = 0;
            }
        }
    }

    /// <summary>
    /// Finds the best exact-source hash candidate in the reference above and left search regions.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperation">The closed sample operation.</typeparam>
    /// <param name="source">The coded source luma plane.</param>
    /// <param name="reconstruction">The coded reconstructed luma plane.</param>
    /// <param name="blockOrigin">The current 8x8 block origin.</param>
    /// <param name="tile">The active tile boundaries.</param>
    /// <param name="sequenceHeader">The sequence geometry and sample precision.</param>
    /// <param name="writer">The live tile entropy model used for displacement rate.</param>
    /// <param name="reference">The spatial displacement-vector reference.</param>
    /// <param name="rateMultiplier">The active rate-distortion multiplier.</param>
    /// <param name="candidates">Storage receiving the above candidate followed by the left candidate.</param>
    /// <returns>The number of candidates written.</returns>
    public int FindCandidates<TSample, TOperation>(
        Buffer2DRegion<TSample> source,
        Buffer2DRegion<TSample> reconstruction,
        Point blockOrigin,
        Av1TileInfo tile,
        ObuSequenceHeader sequenceHeader,
        Av1SymbolEncoder writer,
        Av1MotionVector reference,
        int rateMultiplier,
        Span<Av1MotionVector> candidates)
        where TSample : unmanaged
        where TOperation : struct, ISearchOperation<TSample>
    {
        if (this.hashLinkLength == 0)
        {
            return 0;
        }

        const int ModeInfoSampleSize = 1 << Av1Constants.ModeInfoSizeLog2;
        int tileLeft = tile.ModeInfoColumnStart * ModeInfoSampleSize;
        int tileTop = tile.ModeInfoRowStart * ModeInfoSampleSize;
        int tileRight = tile.ModeInfoColumnEnd * ModeInfoSampleSize;
        int tileBottom = tile.ModeInfoRowEnd * ModeInfoSampleSize;
        int superblockSize = sequenceHeader.SuperblockSize.GetWidth();
        int superblockLeft = (blockOrigin.X / superblockSize) * superblockSize;
        int superblockTop = (blockOrigin.Y / superblockSize) * superblockSize;
        int candidateCount = 0;

        if (this.TryFindCandidate<TSample, TOperation>(
            source,
            reconstruction,
            blockOrigin,
            tile,
            sequenceHeader,
            writer,
            reference,
            rateMultiplier,
            tileLeft,
            tileTop,
            tileRight - BlockSize,
            superblockTop - BlockSize,
            out Av1MotionVector above))
        {
            candidates[candidateCount++] = above;
        }

        if (this.TryFindCandidate<TSample, TOperation>(
            source,
            reconstruction,
            blockOrigin,
            tile,
            sequenceHeader,
            writer,
            reference,
            rateMultiplier,
            tileLeft,
            tileTop,
            superblockLeft - BlockSize,
            Math.Min(superblockTop + superblockSize, tileBottom) - BlockSize,
            out Av1MotionVector left))
        {
            candidates[candidateCount++] = left;
        }

        return candidateCount;
    }

    private static uint GetLeadingWeight(uint multiplier)
    {
        uint result = 1;
        for (int i = 1; i < BlockSize; i++)
        {
            result = unchecked(result * multiplier);
        }

        return result;
    }

    private static int GetBucketCount(int originWidth, int originHeight)
    {
        int originCount = checked(originWidth * originHeight);
        if (originCount == 0)
        {
            return 0;
        }

        // One power-of-two bucket per possible origin avoids libaom's fixed multi-megabyte pointer table
        // on small images while retaining its 16-bit upper bound and constant-time mask lookup.
        return originCount >= MaximumBucketCount
            ? MaximumBucketCount
            : 1 << (int)Av1Math.CeilLog2((uint)originCount);
    }

    private static uint GetBlockHash<TSample, TOperation>(Buffer2DRegion<TSample> source, Point origin)
        where TSample : unmanaged
        where TOperation : struct, ISearchOperation<TSample>
    {
        uint blockHash = 0;
        for (int row = 0; row < BlockSize; row++)
        {
            ReadOnlySpan<TSample> sourceRow = source.DangerousGetRowSpan(origin.Y + row);
            uint rowHash = 0;
            for (int column = 0; column < BlockSize; column++)
            {
                rowHash = unchecked(
                    (rowHash * HorizontalHashMultiplier) +
                    TOperation.GetHashSample(sourceRow[origin.X + column]));
            }

            blockHash = unchecked((blockHash * VerticalHashMultiplier) + rowHash);
        }

        return blockHash;
    }

    private bool TryFindCandidate<TSample, TOperation>(
        Buffer2DRegion<TSample> source,
        Buffer2DRegion<TSample> reconstruction,
        Point blockOrigin,
        Av1TileInfo tile,
        ObuSequenceHeader sequenceHeader,
        Av1SymbolEncoder writer,
        Av1MotionVector reference,
        int rateMultiplier,
        int minimumColumn,
        int minimumRow,
        int maximumColumn,
        int maximumRow,
        out Av1MotionVector bestVector)
        where TSample : unmanaged
        where TOperation : struct, ISearchOperation<TSample>
    {
        bestVector = default;
        if (maximumColumn < minimumColumn || maximumRow < minimumRow)
        {
            return false;
        }

        uint blockHash = GetBlockHash<TSample, TOperation>(source, blockOrigin);
        int bucket = (int)(blockHash & (this.bucketCount - 1));
        Span<int> hashesAndLinks = this.GetHashesAndLinks();
        int encodedPosition = this.GetHeads()[bucket];
        int bestCost = int.MaxValue;
        bool found = false;
        Point modeInfoPosition = new(
            blockOrigin.X >> Av1Constants.ModeInfoSizeLog2,
            blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);

        while (encodedPosition != 0)
        {
            int position = encodedPosition - 1;
            int row = position / this.OriginWidth;
            int column = position - (row * this.OriginWidth);
            Point candidateOrigin = new(column, row);
            encodedPosition = hashesAndLinks[position];
            if (column < minimumColumn || column > maximumColumn || row < minimumRow || row > maximumRow ||
                !TOperation.BlocksEqual(source, blockOrigin, candidateOrigin))
            {
                continue;
            }

            Av1MotionVector vector = new(
                (row - blockOrigin.Y) * 8,
                (column - blockOrigin.X) * 8);

            if (!Av1IntraBlockCopy.IsValid(
                vector,
                modeInfoPosition,
                Av1BlockSize.Block8x8,
                isChroma: false,
                tile,
                sequenceHeader))
            {
                continue;
            }

            int variance = TOperation.GetVariance(
                source,
                blockOrigin,
                reconstruction,
                candidateOrigin,
                sequenceHeader.ColorConfig.BitDepth);

            int rate = writer.GetDisplacementVectorSearchCost(vector, reference);
            int cost = Av1RateDistortion.GetMotionSearchCost(rateMultiplier, rate, variance);
            if (cost < bestCost)
            {
                bestCost = cost;
                bestVector = vector;
                found = true;
            }
        }

        return found;
    }

    private Span<int> GetHashesAndLinks()
        => MemoryMarshal.Cast<byte, int>(this.storage.Span[..this.headOffset]);

    private Span<int> GetHeads()
        => MemoryMarshal.Cast<byte, int>(this.storage.Span.Slice(this.headOffset, this.bucketCount * sizeof(int)));

    private Span<int> GetTails()
        => MemoryMarshal.Cast<byte, int>(this.storage.Span.Slice(this.tailOffset, this.bucketCount * sizeof(int)));

    private Span<ushort> GetCounts()
        => MemoryMarshal.Cast<byte, ushort>(this.storage.Span.Slice(this.countOffset, this.bucketCount * sizeof(ushort)));
}
