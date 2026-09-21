// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Indexes visible square luma blocks for intra-block-copy motion search.
/// </summary>
internal readonly struct Av1IntraBlockCopySearchIndex
{
    private const int BlockSize = 8;
    private const int MaximumBucketCount = 1 << 16;
    private const int MaximumCandidatesPerBucket = 256;
    private const int MaximumFullPixelSearchOffset = (1 << 10) - 1;
    private const int MinimumFullPixelMotionVector = -(1 << 11) + 1;
    private const int MaximumFullPixelMotionVector = (1 << 11) - 1;
    private readonly int maximumHashBlockSize;
    private readonly Memory<byte> storage;
    private readonly int width;
    private readonly int height;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1IntraBlockCopySearchIndex"/> struct over picture-lifetime storage.
    /// </summary>
    /// <param name="storage">The packed per-size hashes, links, and bucket storage.</param>
    /// <param name="width">The visible luma width.</param>
    /// <param name="height">The visible luma height.</param>
    /// <param name="maximumHashBlockSize">The largest square block represented by the index.</param>
    public Av1IntraBlockCopySearchIndex(Memory<byte> storage, int width, int height, int maximumHashBlockSize)
    {
        this.maximumHashBlockSize = maximumHashBlockSize;
        this.width = width;
        this.height = height;
        this.OriginWidth = Math.Max(0, width - BlockSize + 1);
        this.OriginHeight = Math.Max(0, height - BlockSize + 1);
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
        /// Gets the sum of absolute differences between the source block and reconstructed predictor.
        /// </summary>
        /// <param name="source">The coded source plane.</param>
        /// <param name="sourceOrigin">The source block origin.</param>
        /// <param name="reconstruction">The reconstructed luma plane.</param>
        /// <param name="predictionOrigin">The predictor block origin.</param>
        /// <returns>The unnormalized absolute difference over the complete 8x8 block.</returns>
        public static abstract int GetSumOfAbsoluteDifferences(
            Buffer2DRegion<TSample> source,
            Point sourceOrigin,
            Buffer2DRegion<TSample> reconstruction,
            Point predictionOrigin);

        /// <summary>
        /// Gets four sums of absolute differences for horizontally adjacent reconstructed predictors.
        /// </summary>
        /// <param name="source">The coded source plane.</param>
        /// <param name="sourceOrigin">The source block origin.</param>
        /// <param name="reconstruction">The reconstructed luma plane.</param>
        /// <param name="firstPredictionOrigin">The first of four horizontally adjacent predictor origins.</param>
        /// <param name="sums">Storage receiving the four unnormalized absolute differences.</param>
        public static abstract void GetFourSumsOfAbsoluteDifferences(
            Buffer2DRegion<TSample> source,
            Point sourceOrigin,
            Buffer2DRegion<TSample> reconstruction,
            Point firstPredictionOrigin,
            Span<int> sums);

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
    /// <param name="maximumHashBlockSize">The largest square block represented by the index.</param>
    /// <returns>The required byte length.</returns>
    public static int GetStorageLength(int width, int height, int maximumHashBlockSize)
    {
        int length = 0;
        int maximumSize = Math.Min(maximumHashBlockSize, Math.Min(width, height));
        for (int size = 4; size <= maximumSize; size <<= 1)
        {
            int origins = checked((width - size + 1) * (height - size + 1));
            length = checked(length + (2 * origins * sizeof(uint)) +
                (MaximumBucketCount * ((2 * sizeof(int)) + sizeof(ushort))));
        }

        // The first reduction borrows the not-yet-populated index for its 2x2 seeds. Very narrow
        // pictures can need more seed storage than retained entries, so reserve the larger live extent.
        return maximumSize < 4 ? 0 : Math.Max(length, checked((width - 1) * (height - 1) * sizeof(uint)));
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
        int maximumSize = Math.Min(this.maximumHashBlockSize, Math.Min(this.width, this.height));
        if (maximumSize < 4)
        {
            return;
        }

        int sourceWidth = this.width - 1;
        Span<uint> previous = MemoryMarshal.Cast<byte, uint>(this.storage.Span)[..(sourceWidth * (this.height - 1))];
        for (int y = 0; y < this.height - 1; y++)
        {
            ReadOnlySpan<TSample> top = source.DangerousGetRowSpan(y);
            ReadOnlySpan<TSample> bottom = source.DangerousGetRowSpan(y + 1);
            for (int x = 0; x < sourceWidth; x++)
            {
                // Fold each sample's upper byte into its lower byte before packing the four
                // positions. Every source bit contributes, including ten- and twelve-bit samples.
                uint p0 = TOperation.GetHashSample(top[x]);
                uint p1 = TOperation.GetHashSample(top[x + 1]);
                uint p2 = TOperation.GetHashSample(bottom[x]);
                uint p3 = TOperation.GetHashSample(bottom[x + 1]);
                previous[(y * sourceWidth) + x] =
                    (((p0 ^ (p0 >> 8)) & 255) << 24) |
                    (((p1 ^ (p1 >> 8)) & 255) << 16) |
                    (((p2 ^ (p2 >> 8)) & 255) << 8) |
                    ((p3 ^ (p3 >> 8)) & 255);
            }
        }

        for (int size = 4; size <= maximumSize; size <<= 1)
        {
            this.GetLevel(
                size,
                out Span<uint> hashes,
                out Span<int> links,
                out Span<int> heads,
                out Span<int> tails,
                out Span<ushort> counts);

            int originWidth = this.width - size + 1;
            int originHeight = this.height - size + 1;
            int half = size >> 1;
            for (int y = 0; y < originHeight; y++)
            {
                for (int x = 0; x < originWidth; x++)
                {
                    int top = (y * sourceWidth) + x;
                    int bottom = top + (half * sourceWidth);
                    hashes[(y * originWidth) + x] = CombineHashes(
                        previous[top], previous[top + half], previous[bottom], previous[bottom + half]);
                }
            }

            // During the first reduction, compacted writes stay behind every unread seed. The
            // seed suffix can now become bucket storage; later levels read retained parent hashes.
            links.Clear();
            heads.Clear();
            tails.Clear();
            counts.Clear();
            int step = size;
            int offsetX = 0;
            int offsetY = 0;
            while (step > 1)
            {
                for (int x = offsetX; x < originWidth; x += step)
                {
                    for (int y = offsetY; y < originHeight; y += step)
                    {
                        int position = (y * originWidth) + x;
                        int bucket = (int)(hashes[position] & (MaximumBucketCount - 1));
                        if (counts[bucket] == MaximumCandidatesPerBucket)
                        {
                            continue;
                        }

                        // Preserve coarse-to-fine insertion order. Capping the bucket before later
                        // offsets keeps the retained candidates spread across the complete picture.
                        int encodedPosition = position + 1;
                        int tail = tails[bucket];
                        if (tail == 0)
                        {
                            heads[bucket] = encodedPosition;
                        }
                        else
                        {
                            links[tail - 1] = encodedPosition;
                        }

                        tails[bucket] = encodedPosition;
                        counts[bucket]++;
                    }
                }

                if (offsetX == 0 && offsetY == 0)
                {
                    offsetX = step >> 1;
                }
                else if (offsetX == step >> 1 && offsetY == 0)
                {
                    offsetX = 0;
                    offsetY = step >> 1;
                }
                else if (offsetX == 0 && offsetY == step >> 1)
                {
                    offsetX = step >> 1;
                }
                else
                {
                    step >>= 1;
                    offsetX = step >> 1;
                    offsetY = 0;
                }
            }

            previous = hashes;
            sourceWidth = originWidth;
        }
    }

    /// <summary>
    /// Selects one block-copy displacement per permitted search direction.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperation">The closed sample operation.</typeparam>
    /// <param name="source">The coded source luma plane.</param>
    /// <param name="reconstruction">The coded reconstructed luma plane.</param>
    /// <param name="blockOrigin">The current coding-block origin.</param>
    /// <param name="blockSize">The coding-block dimensions.</param>
    /// <param name="tile">The active tile boundaries.</param>
    /// <param name="sequenceHeader">The sequence geometry and sample precision.</param>
    /// <param name="costs">The retained integer displacement rates.</param>
    /// <param name="reference">The spatial displacement-vector reference.</param>
    /// <param name="qIndex">The effective segment quantizer index.</param>
    /// <param name="rateMultiplier">The active rate-distortion multiplier.</param>
    /// <param name="searchStepParameter">The initial search scale selected for the current frame.</param>
    /// <param name="settings">The frame's resolved motion-search policy.</param>
    /// <param name="sites">The retained full-pixel search geometry.</param>
    /// <param name="candidates">Storage receiving the above winner followed by the left winner.</param>
    /// <returns>The number of candidates written.</returns>
    public int FindCandidates<TSample, TOperation>(
        Buffer2DRegion<TSample> source,
        Buffer2DRegion<TSample> reconstruction,
        Point blockOrigin,
        Av1BlockSize blockSize,
        Av1TileInfo tile,
        ObuSequenceHeader sequenceHeader,
        Av1MotionVectorCosts costs,
        Av1MotionVector reference,
        int qIndex,
        int rateMultiplier,
        int searchStepParameter,
        Av1MotionSearchSettings settings,
        Av1MotionSearchSites sites,
        Span<Av1MotionVector> candidates)
        where TSample : unmanaged
        where TOperation : struct, ISearchOperation<TSample>, Av1MotionSearchBase.IMotionSearchOperator<TSample>
    {
        const int ModeInfoSampleSize = 1 << Av1Constants.ModeInfoSizeLog2;
        int width = blockSize.GetWidth();
        int height = blockSize.GetHeight();
        int tileLeft = tile.ModeInfoColumnStart * ModeInfoSampleSize;
        int tileTop = tile.ModeInfoRowStart * ModeInfoSampleSize;
        int tileRight = (tile.ModeInfoColumnEnd * ModeInfoSampleSize) - width;
        int tileBottom = (tile.ModeInfoRowEnd * ModeInfoSampleSize) - height;
        int superblockSize = sequenceHeader.SuperblockSize.GetWidth();
        int superblockLeft = (blockOrigin.X / superblockSize) * superblockSize;
        int superblockTop = (blockOrigin.Y / superblockSize) * superblockSize;

        int sadPerBit = Av1RateDistortion.GetMotionSearchSadPerBit(qIndex, sequenceHeader.ColorConfig.BitDepth);
        int candidateCount = 0;
        Rectangle sourceBounds = source.Bounds;
        int sourceOffset = ((sourceBounds.Y + blockOrigin.Y) * source.Stride) + sourceBounds.X + blockOrigin.X;
        Rectangle reconstructionBounds = reconstruction.Bounds;
        int reconstructionOffset = ((reconstructionBounds.Y + blockOrigin.Y) * reconstruction.Stride) +
            reconstructionBounds.X + blockOrigin.X;
        Point start = new(reference.Column >> 3, reference.Row >> 3);
        Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
        int directionCount = settings.UseFastIntraBlockCopySearch ? 1 : 2;
        for (int direction = 0; direction < directionCount; direction++)
        {
            // Above excludes this superblock row. Left excludes this superblock column and can
            // extend to the bottom of its row; both windows are then intersected with the DV range.
            int maximumColumn = direction == 0 ? tileRight : Math.Min(superblockLeft - width, tileRight);
            int maximumRow = direction == 0
                ? Math.Min(superblockTop - height, tileBottom)
                : Math.Min(superblockTop + superblockSize - height, tileBottom);
            int minimumColumn = Math.Max(
                tileLeft - blockOrigin.X,
                Math.Max(start.X - MaximumFullPixelSearchOffset, MinimumFullPixelMotionVector));
            int minimumRow = Math.Max(
                tileTop - blockOrigin.Y,
                Math.Max(start.Y - MaximumFullPixelSearchOffset, MinimumFullPixelMotionVector));
            maximumColumn = Math.Min(
                maximumColumn - blockOrigin.X,
                Math.Min(start.X + MaximumFullPixelSearchOffset, MaximumFullPixelMotionVector));
            maximumRow = Math.Min(
                maximumRow - blockOrigin.Y,
                Math.Min(start.Y + MaximumFullPixelSearchOffset, MaximumFullPixelMotionVector));
            if (minimumColumn > maximumColumn || minimumRow > maximumRow)
            {
                continue;
            }

            Rectangle bounds = Rectangle.FromLTRB(minimumColumn, minimumRow, maximumColumn + 1, maximumRow + 1);
            Av1MotionSearchBase.FullPixelSearch<TSample, TOperation> search = new(
                source.Buffer.DangerousGetSingleSpan()[sourceOffset..],
                source.Stride,
                reconstruction.Buffer.DangerousGetSingleSpan(),
                reconstruction.Stride,
                reconstructionOffset,
                new Size(width, height),
                bounds,
                reference,
                costs,
                sequenceHeader.ColorConfig.BitDepth,
                sadPerBit,
                rateMultiplier,
                [],
                []);

            Av1MotionVector bestVector = default;
            int bestCost = int.MaxValue;
            bool found = width == height && width <= this.width && height <= this.height &&
                (!settings.LimitIntraBlockCopyHashBlockSize || width <= 8) &&
                this.TryFindCandidate<TSample, TOperation>(
                    source,
                    blockOrigin,
                    blockSize,
                    tile,
                    sequenceHeader,
                    search,
                    bounds,
                    settings.PruneIntraBlockCopyHashCandidates,
                    out bestVector,
                    out bestCost);

            // Hash and pixel candidates share the same reconstructed-plane variance and DV rate.
            // Only the fast policy can accept a successful hash search without the pixel search.
            if (!found || !settings.UseFastIntraBlockCopySearch)
            {
                Av1MotionSearchBase.FullPixelResult result = search.Search(
                    start,
                    searchStepParameter,
                    settings.GetFullPixelMethod(blockSize),
                    sites,
                    settings,
                    keyFrame: true,
                    fineMeshInterval: false,
                    intraBlockCopy: true,
                    Span<int>.Empty,
                    out _);

                if (result.Cost < bestCost)
                {
                    bestVector = new Av1MotionVector(result.Vector.Y * 8, result.Vector.X * 8);
                    found = true;
                }
            }

            if (found && Av1IntraBlockCopy.IsValid(
                bestVector, modeInfoPosition, blockSize, isChroma: false, tile, sequenceHeader))
            {
                candidates[candidateCount++] = bestVector;
            }
        }

        return candidateCount;
    }

    /// <summary>
    /// Selects a same-frame displacement using hash lookup and a fixed local probe.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperation">The closed sample operation.</typeparam>
    /// <param name="source">The coded source luma plane.</param>
    /// <param name="reconstruction">The coded reconstructed luma plane.</param>
    /// <param name="blockOrigin">The current coding-block origin.</param>
    /// <param name="blockSize">The coding-block dimensions.</param>
    /// <param name="tile">The active tile boundaries.</param>
    /// <param name="sequenceHeader">The sequence geometry and sample precision.</param>
    /// <param name="costs">The retained integer displacement rates.</param>
    /// <param name="reference">The spatial displacement-vector reference.</param>
    /// <param name="qIndex">The effective segment quantizer index.</param>
    /// <param name="rateMultiplier">The active rate-distortion multiplier.</param>
    /// <param name="settings">The frame's resolved motion-search policy.</param>
    /// <param name="bestVector">The selected legal displacement.</param>
    /// <returns>Whether a legal candidate was found.</returns>
    public bool TryFindEstimatedCandidate<TSample, TOperation>(
        Buffer2DRegion<TSample> source,
        Buffer2DRegion<TSample> reconstruction,
        Point blockOrigin,
        Av1BlockSize blockSize,
        Av1TileInfo tile,
        ObuSequenceHeader sequenceHeader,
        Av1MotionVectorCosts costs,
        Av1MotionVector reference,
        int qIndex,
        int rateMultiplier,
        Av1MotionSearchSettings settings,
        out Av1MotionVector bestVector)
        where TSample : unmanaged
        where TOperation : struct, ISearchOperation<TSample>, Av1MotionSearchBase.IMotionSearchOperator<TSample>
    {
        int width = blockSize.GetWidth();
        int height = blockSize.GetHeight();
        Rectangle bounds = Rectangle.FromLTRB(
            (tile.ModeInfoColumnStart << Av1Constants.ModeInfoSizeLog2) - blockOrigin.X,
            (tile.ModeInfoRowStart << Av1Constants.ModeInfoSizeLog2) - blockOrigin.Y,
            (tile.ModeInfoColumnEnd << Av1Constants.ModeInfoSizeLog2) - blockOrigin.X - width + 1,
            (tile.ModeInfoRowEnd << Av1Constants.ModeInfoSizeLog2) - blockOrigin.Y - height + 1);

        Rectangle sourceBounds = source.Bounds;
        int sourceOffset = ((sourceBounds.Y + blockOrigin.Y) * source.Stride) + sourceBounds.X + blockOrigin.X;
        Rectangle reconstructionBounds = reconstruction.Bounds;
        int reconstructionOffset = ((reconstructionBounds.Y + blockOrigin.Y) * reconstruction.Stride) +
            reconstructionBounds.X + blockOrigin.X;
        ReadOnlySpan<TSample> sourceSamples = source.Buffer.DangerousGetSingleSpan()[sourceOffset..];
        ReadOnlySpan<TSample> reconstructedSamples = reconstruction.Buffer.DangerousGetSingleSpan();
        int sadPerBit = Av1RateDistortion.GetMotionSearchSadPerBit(qIndex, sequenceHeader.ColorConfig.BitDepth);
        Av1MotionSearchBase.FullPixelSearch<TSample, TOperation> search = new(
            sourceSamples,
            source.Stride,
            reconstructedSamples,
            reconstruction.Stride,
            reconstructionOffset,
            new Size(width, height),
            bounds,
            reference,
            costs,
            sequenceHeader.ColorConfig.BitDepth,
            sadPerBit,
            rateMultiplier,
            [],
            []);

        bestVector = default;
        if (width == height && width <= this.width && height <= this.height &&
            (!settings.LimitIntraBlockCopyHashBlockSize || width <= 8) &&
            this.TryFindCandidate<TSample, TOperation>(
                source,
                blockOrigin,
                blockSize,
                tile,
                sequenceHeader,
                search,
                bounds,
                settings.PruneIntraBlockCopyHashCandidates,
                out bestVector,
                out _))
        {
            return true;
        }

        ReadOnlySpan<sbyte> rowOffsets = [0, -1, 1, 0, 0, -2, 2, 0, 0, -1, -1, 1, 1];
        ReadOnlySpan<sbyte> columnOffsets = [0, 0, 0, -1, 1, 0, 0, -2, 2, -1, 1, -1, 1];
        Point start = new(reference.Column >> 3, reference.Row >> 3);
        Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
        int bestCost = int.MaxValue;

        // Every probe is relative to the original spatial predictor. Moving the center after a
        // successful probe would turn this bounded test into a different, iterative search.
        for (int index = 0; index < rowOffsets.Length; index++)
        {
            Point displacement = new(start.X + columnOffsets[index], start.Y + rowOffsets[index]);
            Av1MotionVector vector = new(displacement.Y * 8, displacement.X * 8);
            if (!bounds.Contains(displacement) ||
                !Av1IntraBlockCopy.IsValid(vector, modeInfoPosition, blockSize, isChroma: false, tile, sequenceHeader))
            {
                continue;
            }

            int offset = reconstructionOffset + (displacement.Y * reconstruction.Stride) + displacement.X;
            int sad = TOperation.SumAbsoluteDifferences(
                sourceSamples, source.Stride, reconstructedSamples[offset..], reconstruction.Stride, width, height, 1);
            int rate = ((costs.GetCost(vector, reference) * 108) + 64) >> 7;
            int cost = sad + rate;
            if (cost < bestCost)
            {
                bestCost = cost;
                bestVector = vector;
            }
        }

        return bestCost != int.MaxValue;
    }

    /// <summary>
    /// Combines four child hashes in top-left, top-right, bottom-left, bottom-right order.
    /// </summary>
    private static uint CombineHashes(uint topLeft, uint topRight, uint bottomLeft, uint bottomRight)
    {
        // Feed each 32-bit word least-significant byte first. The runtime selects the hardware
        // CRC32C instruction where available and preserves the same arithmetic in its fallback.
        uint crc = BitOperations.Crc32C(uint.MaxValue, topLeft | ((ulong)topRight << 32));
        return ~BitOperations.Crc32C(crc, bottomLeft | ((ulong)bottomRight << 32));
    }

    /// <summary>
    /// Computes a query hash when the source block extends into coded-frame padding.
    /// </summary>
    private static uint GetBlockHash<TSample, TOperation>(Buffer2DRegion<TSample> source, Point origin, int size)
        where TSample : unmanaged
        where TOperation : struct, ISearchOperation<TSample>
    {
        if (size == 2)
        {
            // The reference hashes its border-extended source, so samples past the plane repeat its last row
            // and column. Clamping the coordinates reads the same values.
            int lastRow = source.Height - 1;
            int lastColumn = source.Width - 1;
            ReadOnlySpan<TSample> top = source.DangerousGetRowSpan(Math.Min(origin.Y, lastRow));
            ReadOnlySpan<TSample> bottom = source.DangerousGetRowSpan(Math.Min(origin.Y + 1, lastRow));
            int left = Math.Min(origin.X, lastColumn);
            int right = Math.Min(origin.X + 1, lastColumn);
            uint p0 = TOperation.GetHashSample(top[left]);
            uint p1 = TOperation.GetHashSample(top[right]);
            uint p2 = TOperation.GetHashSample(bottom[left]);
            uint p3 = TOperation.GetHashSample(bottom[right]);
            return (((p0 ^ (p0 >> 8)) & 255) << 24) |
                (((p1 ^ (p1 >> 8)) & 255) << 16) |
                (((p2 ^ (p2 >> 8)) & 255) << 8) |
                ((p3 ^ (p3 >> 8)) & 255);
        }

        int half = size >> 1;
        return CombineHashes(
            GetBlockHash<TSample, TOperation>(source, origin, half),
            GetBlockHash<TSample, TOperation>(source, origin + new Size(half, 0), half),
            GetBlockHash<TSample, TOperation>(source, origin + new Size(0, half), half),
            GetBlockHash<TSample, TOperation>(source, origin + new Size(half, half), half));
    }

    /// <summary>
    /// Selects a legal displacement from the ordered, size-specific CRC bucket.
    /// </summary>
    private bool TryFindCandidate<TSample, TOperation>(
        Buffer2DRegion<TSample> source,
        Point blockOrigin,
        Av1BlockSize blockSize,
        Av1TileInfo tile,
        ObuSequenceHeader sequenceHeader,
        Av1MotionSearchBase.FullPixelSearch<TSample, TOperation> search,
        Rectangle bounds,
        bool pruneCandidates,
        out Av1MotionVector bestVector,
        out int bestCost)
        where TSample : unmanaged
        where TOperation : struct, ISearchOperation<TSample>, Av1MotionSearchBase.IMotionSearchOperator<TSample>
    {
        int size = blockSize.GetWidth();
        int originWidth = this.width - size + 1;
        int originHeight = this.height - size + 1;
        this.GetLevel(size, out Span<uint> hashes, out Span<int> links, out Span<int> heads, out _, out Span<ushort> counts);
        uint blockHash = blockOrigin.X < originWidth && blockOrigin.Y < originHeight
            ? hashes[(blockOrigin.Y * originWidth) + blockOrigin.X]
            : GetBlockHash<TSample, TOperation>(source, blockOrigin, size);
        int bucket = (int)(blockHash & (MaximumBucketCount - 1));
        int count = counts[bucket];
        bestVector = default;
        bestCost = int.MaxValue;
        if (count <= 1)
        {
            return false;
        }

        if (pruneCandidates)
        {
            count = Math.Min(count, 64);
        }

        int encodedPosition = heads[bucket];
        Point modeInfoPosition = new(blockOrigin.X >> Av1Constants.ModeInfoSizeLog2, blockOrigin.Y >> Av1Constants.ModeInfoSizeLog2);
        bool found = false;
        for (int candidate = 0; candidate < count; candidate++)
        {
            int position = encodedPosition - 1;
            encodedPosition = links[position];
            if (hashes[position] != blockHash)
            {
                continue;
            }

            int row = position / originWidth;
            int column = position - (row * originWidth);
            Point displacement = new(column - blockOrigin.X, row - blockOrigin.Y);
            Av1MotionVector vector = new(displacement.Y * 8, displacement.X * 8);
            if (!bounds.Contains(displacement) ||
                !Av1IntraBlockCopy.IsValid(vector, modeInfoPosition, blockSize, isChroma: false, tile, sequenceHeader))
            {
                continue;
            }

            int cost = search.GetVarianceResult(displacement).Cost;
            if (cost < bestCost)
            {
                bestCost = cost;
                bestVector = vector;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Borrows the retained hashes and ordered bucket lists for one square block size.
    /// </summary>
    private void GetLevel(
        int size,
        out Span<uint> hashes,
        out Span<int> links,
        out Span<int> heads,
        out Span<int> tails,
        out Span<ushort> counts)
    {
        int offset = 0;
        for (int previousSize = 4; previousSize < size; previousSize <<= 1)
        {
            int previousCount = (this.width - previousSize + 1) * (this.height - previousSize + 1);
            offset += (2 * previousCount * sizeof(uint)) +
                (MaximumBucketCount * ((2 * sizeof(int)) + sizeof(ushort)));
        }

        int originCount = (this.width - size + 1) * (this.height - size + 1);
        Span<byte> data = this.storage.Span[offset..];
        int hashLength = originCount * sizeof(uint);
        int bucketLength = MaximumBucketCount * sizeof(int);
        hashes = MemoryMarshal.Cast<byte, uint>(data[..hashLength]);
        links = MemoryMarshal.Cast<byte, int>(data.Slice(hashLength, hashLength));
        heads = MemoryMarshal.Cast<byte, int>(data.Slice(2 * hashLength, bucketLength));
        tails = MemoryMarshal.Cast<byte, int>(data.Slice((2 * hashLength) + bucketLength, bucketLength));
        counts = MemoryMarshal.Cast<byte, ushort>(data.Slice((2 * hashLength) + (2 * bucketLength), MaximumBucketCount * sizeof(ushort)));
    }
}
