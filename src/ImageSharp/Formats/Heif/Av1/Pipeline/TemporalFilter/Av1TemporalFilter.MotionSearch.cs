// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Motion.Av1MotionSearchBase;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Motion.Av1MotionSearchSettings;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

/// <content>
/// Searches the motion of the temporal filter blocks.
/// </content>
internal static partial class Av1TemporalFilter
{
    /// <summary>
    /// The number of sixteen-by-sixteen sub-blocks in a filter block, NUM_16X16.
    /// </summary>
    internal const int SubblockCount = 16;

    /// <summary>
    /// The largest component magnitude of a full-pixel motion vector, MAX_FULL_PEL_VAL.
    /// </summary>
    private const int MaximumFullPixelComponent = 1023;

    /// <summary>
    /// Fills the motion-vector rate storage with the L1 norm used by MV_COST_L1_LOWRES, MV_COST_L1_MIDRES and
    /// MV_COST_L1_HDRES. Reference: the L1 branches of mvsad_err_cost() and mv_err_cost().
    /// </summary>
    /// <param name="storage">The <see cref="Av1MotionVectorCosts.IntegerStorageLength"/> values of an integer-precision
    /// <see cref="Av1MotionVectorCosts"/>: four joint rates, then the row and the column rates of every signed
    /// eighth-sample difference.</param>
    /// <remarks>
    /// The shared full-pixel and fractional searches measure motion rate as joint + row + column table rates
    /// scaled by a quantizer factor. The temporal filter measures it as lambda * (|row| + |column|) / 8 instead.
    /// A table holding |difference| / 8 with zero joint rates gives the L1 norm in whole samples, s, for every
    /// full-pixel vector. The search SAD cost (s * sadPerBit + 256) &gt;&gt; 9 is then exactly lambda * s when
    /// sadPerBit is 512 * lambda, and the variance cost (s * errorPerBit + 8192) &gt;&gt; 14 is exactly
    /// lambda * s when errorPerBit is 16384 * lambda. A zero lambda uses the smallest error-per-bit, one, for which
    /// the cost stays zero because s is at most 2 * 1023 + 2 * 2047 samples. The fractional search uses that zero
    /// cost as MV_COST_NONE.
    /// </remarks>
    internal static void FillL1MotionCosts(Span<int> storage)
    {
        const int ComponentCount = (2 * Av1MotionVectorCosts.MaximumComponent) + 1;
        Span<int> costs = storage[..Av1MotionVectorCosts.IntegerStorageLength];
        costs[..4].Clear();
        Span<int> rows = costs.Slice(4, ComponentCount);
        Span<int> columns = costs.Slice(4 + ComponentCount, ComponentCount);
        for (int i = 0; i < ComponentCount; i++)
        {
            int cost = Math.Abs(i - Av1MotionVectorCosts.MaximumComponent) >> 3;
            rows[i] = cost;
            columns[i] = cost;
        }
    }

    /// <summary>
    /// Returns the smallest and the largest log variance of the four-by-four luma blocks of a filter block.
    /// Reference: get_log_var_4x4sub_blk() with av1_calc_normalized_variance().
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TSearch">The motion search sample arithmetic.</typeparam>
    /// <param name="plane">The luma plane of the frame to filter.</param>
    /// <param name="stride">The luma row stride.</param>
    /// <param name="blockOrigin">The index of the top-left sample of the filter block.</param>
    /// <param name="zeros">At least four zero samples.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="minimum">The logarithm of one plus the smallest variance divided by sixteen.</param>
    /// <param name="maximum">The logarithm of one plus the largest variance divided by sixteen.</param>
    internal static void GetLogVarianceRange<TSample, TSearch>(
        ReadOnlySpan<TSample> plane,
        int stride,
        int blockOrigin,
        ReadOnlySpan<TSample> zeros,
        int bitDepth,
        out double minimum,
        out double maximum)
        where TSample : unmanaged
        where TSearch : struct, IMotionSearchOperator<TSample>
    {
        int smallest = int.MaxValue;
        int largest = 0;
        for (int i = 0; i < BlockSize; i += 4)
        {
            for (int j = 0; j < BlockSize; j += 4)
            {
                // The variance against a zero block with a zero stride is sixteen times the source variance.
                int variance = (int)GetVariance<TSample, TSearch>(plane[(blockOrigin + (i * stride) + j)..], stride, zeros, 0, 4, 4, bitDepth, out _);
                smallest = Math.Min(smallest, variance);
                largest = Math.Max(largest, variance);
            }
        }

        minimum = Math.Log(1 + (smallest / 16.0));
        maximum = Math.Log(1 + (largest / 16.0));
    }

    /// <summary>
    /// Returns the variance of a block difference in the eight-bit error domain, with its squared difference.
    /// Reference: aom_variance*() for eight bits, aom_highbd_10_variance*() and aom_highbd_12_variance*().
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TSearch">The motion search sample arithmetic.</typeparam>
    /// <param name="first">The first block; the signed sum is taken as first minus second.</param>
    /// <param name="firstStride">The first block row stride.</param>
    /// <param name="second">The second block.</param>
    /// <param name="secondStride">The second block row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="squaredError">The rounded squared difference, the sse output of the variance function.</param>
    /// <returns>The variance.</returns>
    internal static uint GetVariance<TSample, TSearch>(
        ReadOnlySpan<TSample> first,
        int firstStride,
        ReadOnlySpan<TSample> second,
        int secondStride,
        int width,
        int height,
        int bitDepth,
        out uint squaredError)
        where TSample : unmanaged
        where TSearch : struct, IMotionSearchOperator<TSample>
    {
        TSearch.GetMoments(first, firstStride, second, secondStride, width, height, out int sum, out long squares);
        int shift = bitDepth - 8;
        if (shift == 0)
        {
            // variance(): the unsigned subtraction cannot wrap because the squared sum bounds the mean square.
            squaredError = (uint)squares;
            return (uint)squares - (uint)(((long)sum * sum) / (width * height));
        }

        // highbd_10_variance() and highbd_12_variance() round both moments to eight bits, and the rounding can
        // make the difference negative, which they clamp to zero.
        squaredError = (uint)((squares + (1L << ((2 * shift) - 1))) >> (2 * shift));
        long roundedSum = (sum + (1L << (shift - 1))) >> shift;
        long variance = squaredError - ((roundedSum * roundedSum) / (width * height));
        return variance >= 0 ? (uint)variance : 0;
    }

    /// <summary>
    /// Decides whether each 32x32 block and the whole filter block keep their sub-block motion or share one vector.
    /// Reference: tf_determine_block_partition().
    /// </summary>
    /// <param name="blockVector">The vector of the whole block.</param>
    /// <param name="blockError">The mean squared error of the whole block.</param>
    /// <param name="midblockVectors">The four 32x32 block vectors.</param>
    /// <param name="midblockErrors">The four 32x32 block errors.</param>
    /// <param name="subblockVectors">The sixteen 16x16 vectors, replaced by the chosen partition.</param>
    /// <param name="subblockErrors">The sixteen 16x16 errors, replaced by the chosen partition.</param>
    internal static void DetermineBlockPartition(
        Av1MotionVector blockVector,
        int blockError,
        ReadOnlySpan<Av1MotionVector> midblockVectors,
        ReadOnlySpan<int> midblockErrors,
        Span<Av1MotionVector> subblockVectors,
        Span<int> subblockErrors)
    {
        // libaom multiplies int errors by int factors; an INT_MAX placeholder error wraps on the x64 build, and the
        // unchecked products reproduce that before the comparison widens them to sixty-four bits.
        for (int index = 0; index < 4; index++)
        {
            int minimum = int.MaxValue;
            int maximum = int.MinValue;
            long sum = 0;
            for (int i = index * 4; i < (index * 4) + 4; i++)
            {
                sum += subblockErrors[i];
                minimum = Math.Min(minimum, subblockErrors[i]);
                maximum = Math.Max(maximum, subblockErrors[i]);
            }

            int spread = unchecked(maximum - minimum);
            if ((unchecked(midblockErrors[index] * 15) <= sum * 4 && spread < 48) ||
                (unchecked(midblockErrors[index] * 14) <= sum * 4 && spread < 24))
            {
                for (int i = index * 4; i < (index * 4) + 4; i++)
                {
                    subblockVectors[i] = midblockVectors[index];
                    subblockErrors[i] = midblockErrors[index];
                }
            }
        }

        int blockMinimum = int.MaxValue;
        int blockMaximum = int.MinValue;
        long blockSum = 0;
        for (int i = 0; i < SubblockCount; i++)
        {
            blockSum += subblockErrors[i];
            blockMinimum = Math.Min(blockMinimum, subblockErrors[i]);
            blockMaximum = Math.Max(blockMaximum, subblockErrors[i]);
        }

        int blockSpread = unchecked(blockMaximum - blockMinimum);
        if ((unchecked(blockError * 15) <= blockSum && unchecked(blockSpread * 16) < blockSum * 3) ||
            (unchecked(blockError * 14) <= blockSum && unchecked(blockSpread * 8) < blockSum))
        {
            for (int i = 0; i < SubblockCount; i++)
            {
                subblockVectors[i] = blockVector;
                subblockErrors[i] = blockError;
            }
        }
    }

    /// <summary>
    /// Rounds an eighth-sample vector to the nearest full-sample vector, halves away from zero.
    /// Reference: get_fullmv_from_mv() with GET_MV_RAWPEL().
    /// </summary>
    /// <param name="vector">The eighth-sample vector.</param>
    /// <returns>The full-sample vector.</returns>
    private static Point ToFullPixel(Av1MotionVector vector)
        => new((vector.Column + 3 + (vector.Column >= 0 ? 1 : 0)) >> 3, (vector.Row + 3 + (vector.Row >= 0 ? 1 : 0)) >> 3);

    /// <summary>
    /// Returns the rounded mean squared error of a search error. Reference: DIVIDE_AND_ROUND() on the unsigned error.
    /// </summary>
    /// <param name="error">The search error.</param>
    /// <param name="pixels">The number of block samples.</param>
    /// <returns>The mean squared error.</returns>
    private static int DivideAndRound(uint error, int pixels)
        => (int)((error + (uint)(pixels >> 1)) / (uint)pixels);

    /// <summary>
    /// Searches the luma motion of the filter blocks of one reference frame, with the parameters that
    /// tf_motion_search() and subblock_motion_search() give av1_full_pixel_search() and find_fractional_mv_step().
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    /// <typeparam name="TSearch">The motion search sample arithmetic.</typeparam>
    internal readonly ref struct BlockMotionSearch<TSample, TSearch>
        where TSample : unmanaged
        where TSearch : struct, IMotionSearchOperator<TSample>
    {
        private readonly ReadOnlySpan<TSample> source;
        private readonly ReadOnlySpan<TSample> reference;
        private readonly int stride;
        private readonly int sourceOrigin;
        private readonly int referenceOrigin;
        private readonly Span<TSample> fractionalBuffer;
        private readonly ReadOnlySpan<TSample> zeros;
        private readonly Span<int> searchSiteStorage;
        private readonly Av1MotionVectorCosts costs;
        private readonly Av1MotionSearchSettings settings;
        private readonly TemporalFilterContext context;
        private readonly int sadPerBit;
        private readonly int rateMultiplier;
        private readonly int stepParameter;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockMotionSearch{TSample, TSearch}"/> struct. Reference:
        /// av1_make_default_fullpel_ms_params().
        /// </summary>
        /// <param name="source">The complete bordered luma plane of the frame to filter.</param>
        /// <param name="sourceOrigin">The index of the top-left visible sample of <paramref name="source"/>.</param>
        /// <param name="reference">The complete bordered luma plane of the reference frame.</param>
        /// <param name="referenceOrigin">The index of the top-left visible sample of <paramref name="reference"/>.</param>
        /// <param name="stride">The row stride of both planes.</param>
        /// <param name="fractionalBuffer">Scratch for fractional predictions.</param>
        /// <param name="zeros">At least 64 zero samples.</param>
        /// <param name="searchSiteStorage">Storage for the NSTEP search sites, configured for <paramref name="stride"/>.</param>
        /// <param name="costStorage">The L1 motion-rate table from <see cref="FillL1MotionCosts"/>.</param>
        /// <param name="settings">The motion search speed features of the encoder.</param>
        /// <param name="context">The filter parameters of the frame.</param>
        public BlockMotionSearch(
            ReadOnlySpan<TSample> source,
            int sourceOrigin,
            ReadOnlySpan<TSample> reference,
            int referenceOrigin,
            int stride,
            Span<TSample> fractionalBuffer,
            ReadOnlySpan<TSample> zeros,
            Span<int> searchSiteStorage,
            Span<int> costStorage,
            Av1MotionSearchSettings settings,
            in TemporalFilterContext context)
        {
            this.source = source;
            this.sourceOrigin = sourceOrigin;
            this.reference = reference;
            this.referenceOrigin = referenceOrigin;
            this.stride = stride;
            this.fractionalBuffer = fractionalBuffer;
            this.zeros = zeros;
            this.searchSiteStorage = searchSiteStorage;
            this.costs = new Av1MotionVectorCosts(costStorage, Av1MotionVectorPrecision.Integer);
            this.settings = settings;
            this.context = context;

            // tf_motion_search() selects the L1 regularization by the shorter visible dimension. The SAD lambdas
            // are SAD_LAMBDA_HDRES 8, SAD_LAMBDA_MIDRES 15 and SAD_LAMBDA_LOWRES 32; the variance lambdas are
            // SSE_LAMBDA_HDRES 1, SSE_LAMBDA_MIDRES 0 and SSE_LAMBDA_LOWRES 2. See FillL1MotionCosts().
            int minimumSize = Math.Min(context.FrameWidth, context.FrameHeight);
            int sadLambda = minimumSize >= 720 ? 8 : minimumSize >= 480 ? 15 : 32;
            int varianceLambda = minimumSize >= 720 ? 1 : minimumSize >= 480 ? 0 : 2;
            this.sadPerBit = sadLambda << 9;
            this.rateMultiplier = Math.Max(varianceLambda << 14, 1) << 6;
            this.stepParameter = GetInitialStepParameter(Math.Max(context.FrameWidth, context.FrameHeight));
        }

        /// <summary>
        /// Searches the whole filter block and, when allowed, its 32x32 and 16x16 sub-blocks, and decides the
        /// partition. Reference: tf_motion_search().
        /// </summary>
        /// <param name="blockRow">The filter block row.</param>
        /// <param name="blockColumn">The filter block column.</param>
        /// <param name="referenceVector">The vector passed along the frames: the start on entry, the next start on exit.</param>
        /// <param name="allowSubblockSearch">Whether the 32x32 and 16x16 sub-blocks are searched.</param>
        /// <param name="subblockVectors">The sixteen sub-block vectors to write.</param>
        /// <param name="subblockErrors">The sixteen sub-block errors to write.</param>
        /// <param name="isDcDifferenceLarge">Whether the block error is mostly a mean difference.</param>
        /// <param name="isLowContrast">Whether the block variance is at most twice the search distortion.</param>
        public void Search(
            int blockRow,
            int blockColumn,
            ref Av1MotionVector referenceVector,
            bool allowSubblockSearch,
            Span<Av1MotionVector> subblockVectors,
            Span<int> subblockErrors,
            out bool isDcDifferenceLarge,
            out bool isLowContrast)
        {
            const int BlockPixels = BlockSize * BlockSize;
            TemporalFilterContext parameters = this.context;
            Rectangle block = new(blockColumn * BlockSize, blockRow * BlockSize, BlockSize, BlockSize);
            int blockOffset = (block.Y * this.stride) + block.X;
            ReadOnlySpan<TSample> blockSource = this.source[(this.sourceOrigin + blockOffset)..];
            isDcDifferenceLarge = false;
            isLowContrast = false;

            // av1_tf_do_filtering_row() initializes the outputs before every search: zero vectors and INT_MAX errors,
            // which the partition decision reads when a forced integer search leaves the sub-blocks unsearched.
            subblockVectors[..SubblockCount].Clear();
            subblockErrors[..SubblockCount].Fill(int.MaxValue);

            // Only a nonzero sharpness measures the source variance; otherwise it stays INT32_MAX.
            long sourceVariance = int.MaxValue;
            if (parameters.Sharpness != 0)
            {
                sourceVariance = GetVariance<TSample, TSearch>(blockSource, this.stride, this.zeros, 0, BlockSize, BlockSize, parameters.BitDepth, out _);
            }

            Av1MotionVector blockVector = default;
            int blockError;
            Span<Av1MotionVector> midblockVectors = stackalloc Av1MotionVector[4];
            Span<int> midblockErrors = stackalloc int[4];
            midblockVectors.Clear();
            midblockErrors.Fill(int.MaxValue);

            FullPixelResult fullResult = this.SearchFullPixel(block, block, ToFullPixel(referenceVector));
            if (parameters.ForceIntegerMotion)
            {
                // Only the full-pixel search runs. The variance takes the reference block first.
                Point best = fullResult.Vector;
                uint error = GetVariance<TSample, TSearch>(
                    this.reference[(this.referenceOrigin + blockOffset + (best.Y * this.stride) + best.X)..],
                    this.stride,
                    blockSource,
                    this.stride,
                    BlockSize,
                    BlockSize,
                    parameters.BitDepth,
                    out _);

                blockError = DivideAndRound(error, BlockPixels);
                blockVector = new Av1MotionVector(best.Y * 8, best.X * 8);
                isLowContrast = sourceVariance <= 2 * (long)error;
            }
            else
            {
                FractionalResult fractional = this.SearchFractional(block, block, fullResult);
                uint error = (uint)fractional.Cost;
                blockError = DivideAndRound(error, BlockPixels);
                blockVector = fractional.Vector;
                referenceVector = fractional.Vector;

                // Both terms are unsigned int; the product wraps as it does in libaom.
                isDcDifferenceLarge = unchecked(50u * error) < (uint)fractional.SquaredError;
                isLowContrast = sourceVariance <= 2 * (long)fractional.Variance;

                // High bit depth sharpness 3 keeps the zero vector for a region with no motion.
                // That occurs when the zero-vector error is within one sixteenth of the searched error, or its mean is less than 16.
                // The decisions above keep the searched error. Reference: the CONFIG_AV1_HIGHBITDEPTH zero motion override of tf_motion_search().
                if (parameters.BitDepth > 8 && parameters.Sharpness == 3)
                {
                    ReadOnlySpan<TSample> referenceBlock = this.reference[(this.referenceOrigin + blockOffset)..];
                    uint zeroError = GetVariance<TSample, TSearch>(
                        referenceBlock, this.stride, blockSource, this.stride, BlockSize, BlockSize, parameters.BitDepth, out _);

                    if (zeroError <= unchecked(error + (error >> 4)) || zeroError / BlockPixels < 16)
                    {
                        blockVector = default;
                        referenceVector = default;
                        blockError = DivideAndRound(zeroError, BlockPixels);
                    }
                }

                if (allowSubblockSearch)
                {
                    int midblockIndex = 0;
                    for (int i = 0; i < BlockSize; i += BlockSize / 2)
                    {
                        for (int j = 0; j < BlockSize; j += BlockSize / 2)
                        {
                            Rectangle midblock = new(block.X + j, block.Y + i, BlockSize / 2, BlockSize / 2);
                            (midblockVectors[midblockIndex], midblockErrors[midblockIndex]) =
                                this.SearchSubblock(block, midblock, ToFullPixel(referenceVector));

                            Point subblockStart = ToFullPixel(midblockVectors[midblockIndex]);
                            int subblockIndex = midblockIndex * 4;
                            for (int bi = 0; bi < BlockSize / 2; bi += BlockSize / 4)
                            {
                                for (int bj = 0; bj < BlockSize / 2; bj += BlockSize / 4)
                                {
                                    Rectangle subblock = new(midblock.X + bj, midblock.Y + bi, BlockSize / 4, BlockSize / 4);
                                    (subblockVectors[subblockIndex], subblockErrors[subblockIndex]) =
                                        this.SearchSubblock(block, subblock, subblockStart);

                                    subblockIndex++;
                                }
                            }

                            midblockIndex++;
                        }
                    }
                }
            }

            if (allowSubblockSearch)
            {
                DetermineBlockPartition(blockVector, blockError, midblockVectors, midblockErrors, subblockVectors, subblockErrors);
            }
            else
            {
                subblockVectors[..SubblockCount].Fill(blockVector);
                subblockErrors[..SubblockCount].Fill(blockError);
            }

            // A large error stops the vector from seeding the search of the next frame.
            int threshold = Math.Min(parameters.FrameWidth, parameters.FrameHeight) >= 720 ? 12 : 3;
            if (blockError > (threshold << (parameters.BitDepth - 8)))
            {
                referenceVector = default;
            }
        }

        /// <summary>
        /// Searches one 32x32 or 16x16 sub-block. Reference: subblock_motion_search().
        /// </summary>
        /// <param name="block">The luma rectangle of the whole filter block.</param>
        /// <param name="subblock">The luma rectangle of the sub-block.</param>
        /// <param name="start">The full-sample start of the search.</param>
        /// <returns>The sub-block vector and its rounded mean squared error.</returns>
        private (Av1MotionVector Vector, int Error) SearchSubblock(Rectangle block, Rectangle subblock, Point start)
        {
            FullPixelResult fullResult = this.SearchFullPixel(block, subblock, start);
            FractionalResult fractional = this.SearchFractional(block, subblock, fullResult);
            return (fractional.Vector, DivideAndRound((uint)fractional.Cost, subblock.Width * subblock.Height));
        }

        /// <summary>
        /// Runs the full-pixel search of one block with the temporal filter parameters. Reference:
        /// av1_make_default_fullpel_ms_params() with the overrides of tf_motion_search(), then av1_full_pixel_search().
        /// </summary>
        /// <param name="block">The luma rectangle of the whole filter block, whose position sets the sharpness margins.</param>
        /// <param name="searched">The luma rectangle of the searched block.</param>
        /// <param name="start">The full-sample start of the search.</param>
        /// <returns>The integer winner with its variance and squared error.</returns>
        /// <remarks>
        /// tf_motion_search() sets run_mesh_search, so libaom always follows the NSTEP search with the mesh search,
        /// unless prune_mesh_search stops it: at PRUNE_MESH_SEARCH_LVL_1 the filter prunes when q exceeds 20 and the
        /// winner is within two samples of the start, and at PRUNE_MESH_SEARCH_LVL_2 within four samples.
        /// </remarks>
        private FullPixelResult SearchFullPixel(Rectangle block, Rectangle searched, Point start)
        {
            TemporalFilterContext parameters = this.context;
            int offset = (searched.Y * this.stride) + searched.X;
            Rectangle bounds = this.GetFullPixelBounds(block, searched);
            FullPixelSearch<TSample, TSearch> search = new(
                this.source[(this.sourceOrigin + offset)..],
                this.stride,
                this.reference,
                this.stride,
                this.referenceOrigin + offset,
                searched.Size,
                bounds,
                default,
                this.costs,
                parameters.BitDepthKind,
                this.sadPerBit,
                this.rateMultiplier,
                [],
                []);

            // cond_cost_list() publishes the integer neighborhood only for the pruned fractional trees with
            // use_fullpel_costlist, which only real-time usage enables; the filter runs with a lookahead, in good
            // quality usage, so it never has one.
            Span<int> neighborhood = Span<int>.Empty;
            Av1MotionSearchSites sites = new(this.searchSiteStorage);
            int pruneDistance = this.settings.MeshPruningLevel == 2 ? 4 :
                this.settings.MeshPruningLevel == 1 && parameters.QFactor > 20 ? 2 : -1;

            return search.Search(
                start,
                this.stepParameter,
                FullPixelSearchMethod.NStep,
                sites,
                this.settings,
                parameters.CurrentFrameIsKeyFrameUpdate,
                false,
                false,
                neighborhood,
                out _,
                forceMesh: true,
                meshPruneDistance: pruneDistance);
        }

        /// <summary>
        /// Refines a full-pixel winner to eighth-sample precision without motion-vector cost. Reference:
        /// av1_make_default_subpel_ms_params() with the overrides of tf_motion_search(), then find_fractional_mv_step().
        /// </summary>
        /// <param name="block">The luma rectangle of the whole filter block, whose position sets the sharpness margins.</param>
        /// <param name="searched">The luma rectangle of the searched block.</param>
        /// <param name="fullResult">The full-pixel winner.</param>
        /// <returns>The fractional winner; its cost is the variance alone.</returns>
        private FractionalResult SearchFractional(Rectangle block, Rectangle searched, FullPixelResult fullResult)
        {
            TemporalFilterContext parameters = this.context;
            int offset = (searched.Y * this.stride) + searched.X;
            Rectangle frameBounds = this.GetFrameBounds(block, searched);
            Rectangle bounds = default(Av1MotionVector).GetSubpixelSearchBounds(frameBounds);
            if (parameters.Sharpness == 3)
            {
                // The margins sit at the whole filter block's origin, with the searched block's size.
                bounds = Av1MotionVector.ClampToSharpnessMargins(
                    bounds, block.Location, searched.Size, new Size(parameters.FrameWidth, parameters.FrameHeight), Av1MotionVector.SubpixelScale);
            }

            FractionalSearch<TSample, TSearch> search = new(
                this.source[(this.sourceOrigin + offset)..],
                this.stride,
                this.reference,
                this.stride,
                this.referenceOrigin + offset,
                this.fractionalBuffer,
                searched.Size,
                bounds,
                default,
                this.costs,
                parameters.BitDepthKind,
                MotionCostNoneRateMultiplier,
                [],
                []);

            // The filter sets best_mv_stats->err_cost to zero: the refinement starts from the integer variance alone,
            // and the subpixel search type is USE_8_TAPS whatever use_accurate_subpel_search selects.
            // Without use_fullpel_costlist the pruned trees have no integer neighborhood either. Reference:
            // cond_cost_list_const() in av1_make_default_subpel_ms_params().
            FullPixelResult start = new(fullResult.Vector, fullResult.Variance, fullResult.SquaredError, 0);
            ReadOnlySpan<int> neighborhood = [];
            search.Search(
                new Av1MotionVector(fullResult.Vector.Y * 8, fullResult.Vector.X * 8),
                start,
                this.settings.FractionalMethod,
                SearchPrecision.EighthSample,
                parameters.AllowHighPrecisionMotion,
                this.settings.FractionalIterationsPerStep,
                8,
                neighborhood,
                [],
                out FractionalResult result);

            return result;
        }

        /// <summary>
        /// Returns the full-pixel search range of a block. Reference: av1_set_mv_row_limits(), av1_set_mv_col_limits(),
        /// av1_set_mv_search_range() and the sharpness margins of av1_make_default_fullpel_ms_params().
        /// </summary>
        /// <param name="block">The luma rectangle of the whole filter block.</param>
        /// <param name="searched">The luma rectangle of the searched block.</param>
        /// <returns>The permitted full-pixel vectors, with exclusive right and bottom edges.</returns>
        private Rectangle GetFullPixelBounds(Rectangle block, Rectangle searched)
        {
            TemporalFilterContext parameters = this.context;
            Rectangle bounds = default(Av1MotionVector).GetFullPixelSearchBounds(this.GetFrameBounds(block, searched));
            if (parameters.Sharpness == 3)
            {
                bounds = Av1MotionVector.ClampToSharpnessMargins(
                    bounds, block.Location, searched.Size, new Size(parameters.FrameWidth, parameters.FrameHeight), 1);
            }

            return bounds;
        }

        /// <summary>
        /// Returns the displacement range that keeps a block's prediction inside the frame border.
        /// Reference: av1_set_mv_row_limits() and av1_set_mv_col_limits() with the encoder border.
        /// </summary>
        /// <param name="block">The luma rectangle of the whole filter block.</param>
        /// <param name="searched">The luma rectangle of the searched block.</param>
        /// <returns>The permitted full-pixel displacements, with exclusive right and bottom edges.</returns>
        private Rectangle GetFrameBounds(Rectangle block, Rectangle searched)
            => Av1MotionVector.GetFrameSearchBounds(
                searched,
                new Size(this.context.CodedWidth, this.context.CodedHeight),
                this.context.BorderInPixels);
    }
}
