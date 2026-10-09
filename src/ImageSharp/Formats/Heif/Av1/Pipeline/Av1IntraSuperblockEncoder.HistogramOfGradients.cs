// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Provides the source-gradient model used to prune unlikely directional intra modes.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    private const int GradientBinCount = 32;

    private static readonly float[] GradientModelBias =
    [
        0.450578F, 0.695518F, -0.717944F, -0.639894F,
        -0.602019F, -0.453454F, 0.055857F, -0.465480F
    ];

    private static readonly float[] GradientModelWeights =
    [
        -3.076402F, -3.757063F, -3.275266F, -3.180665F, -3.452105F, -3.216593F, -2.871212F, -3.134296F,
        -1.822324F, -2.401411F, -1.541016F, -1.195322F, -0.434156F, 0.322868F, 2.260546F, 3.368715F,
        3.989290F, 3.308487F, 2.277893F, 0.923793F, 0.026412F, -0.385174F, -0.718622F, -1.408867F,
        -1.050558F, -2.323941F, -2.225827F, -2.585453F, -3.054283F, -2.875087F, -2.985709F, -3.447155F,
        3.758139F, 3.204353F, 2.170998F, 0.826587F, -0.269665F, -0.702068F, -1.085776F, -2.175249F,
        -1.623180F, -2.975142F, -2.779629F, -3.190799F, -3.521900F, -3.375480F, -3.319355F, -3.897389F,
        -3.172334F, -3.594528F, -2.879132F, -2.547777F, -2.921023F, -2.281844F, -1.818988F, -2.041771F,
        -0.618268F, -1.396458F, -0.567153F, -0.285868F, -0.088058F, 0.753494F, 2.092413F, 3.215266F,
        -3.300277F, -2.748658F, -2.315784F, -2.423671F, -2.257283F, -2.269583F, -2.196660F, -2.301076F,
        -2.646516F, -2.271319F, -2.254366F, -2.300102F, -2.217960F, -2.473300F, -2.116866F, -2.528246F,
        -3.314712F, -1.701010F, -0.589040F, -0.088077F, 0.813112F, 1.702213F, 2.653045F, 3.351749F,
        3.243554F, 3.199409F, 2.437856F, 1.468854F, 0.533039F, -0.099065F, -0.622643F, -2.200732F,
        -4.228861F, -2.875263F, -1.273956F, -0.433280F, 0.803771F, 1.975043F, 3.179528F, 3.939064F,
        3.454379F, 3.689386F, 3.116411F, 1.970991F, 0.798406F, -0.628514F, -1.252546F, -2.825176F,
        -4.090178F, -3.777448F, -3.227314F, -3.479403F, -3.320569F, -3.159372F, -2.729202F, -2.722341F,
        -3.054913F, -2.742923F, -2.612703F, -2.662632F, -2.907314F, -3.117794F, -3.102660F, -3.970972F,
        -4.891357F, -3.935582F, -3.347758F, -2.721924F, -2.219011F, -1.702391F, -0.866529F, -0.153743F,
        0.107733F, 1.416882F, 2.572884F, 3.607755F, 3.974820F, 3.997783F, 2.970459F, 0.791687F,
        -1.478921F, -1.228154F, -1.216955F, -1.765932F, -1.951003F, -1.985301F, -1.975881F, -1.985593F,
        -2.422371F, -2.419978F, -2.531288F, -2.951853F, -3.071380F, -3.277027F, -3.373539F, -4.462010F,
        -0.967888F, 0.805524F, 2.794130F, 3.685984F, 3.745195F, 3.252444F, 2.316108F, 1.399146F,
        -0.136519F, -0.162811F, -1.004357F, -1.667911F, -1.964662F, -2.937579F, -3.019533F, -3.942766F,
        -5.102767F, -3.882073F, -3.532027F, -3.451956F, -2.944015F, -2.643064F, -2.529872F, -2.077290F,
        -2.809965F, -1.803734F, -1.783593F, -1.662585F, -1.415484F, -1.392673F, -0.788794F, -1.204819F,
        -1.998864F, -1.182102F, -0.892110F, -1.317415F, -1.359112F, -1.522867F, -1.468552F, -1.779072F,
        -2.332959F, -2.160346F, -2.329387F, -2.631259F, -2.744936F, -3.052494F, -2.787363F, -3.442548F,
        -4.245075F, -3.032172F, -2.061609F, -1.768116F, -1.286072F, -0.706587F, -0.192413F, 0.386938F,
        0.716997F, 1.481393F, 2.216702F, 2.737986F, 3.109809F, 3.226084F, 2.490098F, -0.095827F,
        -3.864816F, -3.507248F, -3.128925F, -2.908251F, -2.883836F, -2.881411F, -2.524377F, -2.624478F,
        -2.399573F, -2.367718F, -1.918255F, -1.926277F, -1.694584F, -1.723790F, -0.966491F, -1.183115F,
        -1.430687F, 0.872896F, 2.766550F, 3.610080F, 3.578041F, 3.334928F, 2.586680F, 1.895721F,
        1.122195F, 0.488519F, -0.140689F, -0.799076F, -1.222860F, -1.502437F, -1.900969F, -3.206816F
    ];

    internal partial struct ModeDecision<TSample, TOperator>
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        /// <summary>
        /// Computes the gradients of the current superblock once for every block of its partition search, when the
        /// search prunes directional modes with gradient histograms. The partition search reads each sample in many
        /// blocks, so one pass per superblock removes the repeated gradient work.
        /// </summary>
        /// <param name="modeWorkspace">The mode decision buffers, which hold the gradient cache.</param>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="superblockOrigin">The luma origin of the superblock.</param>
        public void PrepareGradientCache(
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            Point superblockOrigin)
        {
            this.lumaGradientsCached = false;
            this.chromaGradientsCached = false;
            this.gradientSuperblockOrigin = superblockOrigin;

            // Caching pays only when the full partition search visits each sample many times. Inter frames gate
            // intra early, and a given partition searches few blocks, so both compute the gradients per block.
            Av1EncoderSpeedSettings speedSettings = this.picture.Parent.SpeedSettings;
            if (!this.picture.Parent.FrameHeader.IsIntra || UsesGivenPartition(this.picture))
            {
                return;
            }

            // The blocks read the gradients inside the coded image only, which ends at the mode information grid.
            int superblockSize = 1 << this.picture.Sequence.SequenceHeader.SuperblockSizeLog2;
            int lumaRows = Math.Min(superblockSize, (this.picture.Parent.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2) - superblockOrigin.Y);
            int lumaColumns = Math.Min(superblockSize, (this.picture.Parent.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2) - superblockOrigin.X);
            if (speedSettings.IntraHogPruningLevel != 0)
            {
                CacheSuperblockGradients(
                    this.source.GetPlane(Av1Plane.Y),
                    sourceLuma,
                    superblockOrigin,
                    lumaRows,
                    lumaColumns,
                    modeWorkspace.GetGradientMagnitudes(Av1PlaneType.Y),
                    modeWorkspace.GetGradientBins(Av1PlaneType.Y));

                this.lumaGradientsCached = true;
            }

            ObuColorConfig colorConfig = this.picture.Sequence.SequenceHeader.ColorConfig;
            if (speedSettings.ChromaHogPruningLevel != 0 && !colorConfig.IsMonochrome)
            {
                int subsamplingX = colorConfig.SubSamplingX ? 1 : 0;
                int subsamplingY = colorConfig.SubSamplingY ? 1 : 0;
                CacheSuperblockGradients(
                    this.source.GetPlane(Av1Plane.U),
                    sourceBlue,
                    new Point(superblockOrigin.X >> subsamplingX, superblockOrigin.Y >> subsamplingY),
                    lumaRows >> subsamplingY,
                    lumaColumns >> subsamplingX,
                    modeWorkspace.GetGradientMagnitudes(Av1PlaneType.Uv),
                    modeWorkspace.GetGradientBins(Av1PlaneType.Uv));

                this.chromaGradientsCached = true;
            }
        }

        /// <summary>
        /// Computes the directional-mode skip mask of a block, from the gradient cache of its superblock when the
        /// superblock cached that plane type, else from its source samples. Both paths give the same mask.
        /// </summary>
        /// <param name="modeWorkspace">The mode decision buffers, which hold the gradient cache.</param>
        /// <param name="planeType">The luma or chroma plane type.</param>
        /// <param name="source">The source component plane.</param>
        /// <param name="sourceSamples">The samples of the complete source plane, read once by the caller.</param>
        /// <param name="origin">The visible block origin in the plane.</param>
        /// <param name="rows">The visible row count.</param>
        /// <param name="columns">The visible column count.</param>
        /// <param name="histogramScale">The chroma subsampling area factor, or one for luma.</param>
        /// <param name="threshold">The speed-dependent neural score threshold.</param>
        /// <returns>A bit mask whose eight bits correspond to the contiguous directional prediction modes.</returns>
        private readonly byte GetBlockDirectionalModeSkipMask(
            in Av1EncoderModeDecisionWorkspace<TSample> modeWorkspace,
            Av1PlaneType planeType,
            Av1PlaneRegion<TSample> source,
            ReadOnlySpan<TSample> sourceSamples,
            Point origin,
            int rows,
            int columns,
            int histogramScale,
            float threshold)
        {
            bool cached = planeType == Av1PlaneType.Y ? this.lumaGradientsCached : this.chromaGradientsCached;
            if (!cached)
            {
                return GetDirectionalModeSkipMask(source, sourceSamples, origin, rows, columns, histogramScale, threshold);
            }

            // The block origin relative to the superblock origin in the same plane locates the block in the cache.
            int subsamplingX = planeType == Av1PlaneType.Y || !this.picture.Sequence.SequenceHeader.ColorConfig.SubSamplingX ? 0 : 1;
            int subsamplingY = planeType == Av1PlaneType.Y || !this.picture.Sequence.SequenceHeader.ColorConfig.SubSamplingY ? 0 : 1;
            int offsetX = origin.X - (this.gradientSuperblockOrigin.X >> subsamplingX);
            int offsetY = origin.Y - (this.gradientSuperblockOrigin.Y >> subsamplingY);
            return GetCachedDirectionalModeSkipMask(
                modeWorkspace.GetGradientMagnitudes(planeType),
                modeWorkspace.GetGradientBins(planeType),
                (offsetY * Av1EncoderModeDecisionWorkspace<TSample>.MaximumBlockDimension) + offsetX,
                rows,
                columns,
                histogramScale,
                threshold);
        }

        /// <summary>
        /// Computes the directional-mode skip mask from a source block's Sobel histogram.
        /// </summary>
        /// <param name="source">The source component plane.</param>
        /// <param name="sourceSamples">The samples of the complete source plane, read once by the caller.</param>
        /// <param name="origin">The visible block origin.</param>
        /// <param name="rows">The visible row count.</param>
        /// <param name="columns">The visible column count.</param>
        /// <param name="histogramScale">The chroma subsampling area factor, or one for luma.</param>
        /// <param name="threshold">The speed-dependent neural score threshold.</param>
        /// <returns>A bit mask whose eight bits correspond to the contiguous directional prediction modes.</returns>
        internal static byte GetDirectionalModeSkipMask(
            Av1PlaneRegion<TSample> source,
            ReadOnlySpan<TSample> sourceSamples,
            Point origin,
            int rows,
            int columns,
            int histogramScale,
            float threshold)
        {
            Span<float> histogram = stackalloc float[GradientBinCount];
            histogram.Clear();
            float total = 0.1F;
            if (rows > 2 && columns > 2)
            {
                // The gradients, magnitudes and bins of a row are computed with vectors. The histogram and total
                // then add the magnitudes one sample at a time in row order, because they are floats and the
                // reference accumulates them in that order.
                // A block is at most 128 samples wide, so each row's scratch holds 128 values and the Sobel window
                // holds the three rows around the current one.
                Span<short> window = stackalloc short[3 * 128];
                Span<short> magnitudes = stackalloc short[128];
                Span<int> bins = stackalloc int[128];
                Span<short> horizontal = stackalloc short[128];
                Span<short> vertical = stackalloc short[128];
                int interior = columns - 2;

                // The window is a ring of three rows: each step widens only the new row below, into the slot of the row
                // that left the window, so each source row is widened once.
                ReadOnlySpan<TSample> blockSamples = sourceSamples[source.GetOffset(origin.X, origin.Y)..];
                TOperator.CopyPaletteSamples(blockSamples, source.Stride, 2, columns, window);
                for (int row = 1; row < rows - 1; row++)
                {
                    Span<short> belowRow = window.Slice(((row + 1) % 3) * columns, columns);
                    TOperator.CopyPaletteSamples(blockSamples[((row + 1) * source.Stride)..], source.Stride, 1, columns, belowRow);
                    Av1GradientHistogram.ComputeRow(
                        window.Slice(((row - 1) % 3) * columns, columns),
                        window.Slice((row % 3) * columns, columns),
                        belowRow,
                        columns,
                        magnitudes,
                        bins,
                        horizontal,
                        vertical);

                    for (int column = 0; column < interior; column++)
                    {
                        int magnitude = magnitudes[column];
                        if (magnitude == 0)
                        {
                            continue;
                        }

                        total += magnitude;
                        int bin = bins[column];
                        if (bin == Av1GradientHistogram.VerticalBin)
                        {
                            histogram[0] += magnitude >> 1;
                            histogram[^1] += magnitude >> 1;
                            continue;
                        }

                        histogram[bin] += magnitude;
                    }
                }
            }

            return ScoreDirectionalModes(histogram, total, histogramScale, threshold);
        }

        /// <summary>
        /// Computes the directional-mode skip mask of a block from the gradients cached for its superblock.
        /// This avoids a second gradient pass over samples that the superblock pass already processed.
        /// </summary>
        /// <param name="magnitudes">The cached gradient magnitudes of the plane type, see <see cref="CacheSuperblockGradients"/>.</param>
        /// <param name="bins">The cached histogram bins of the plane type.</param>
        /// <param name="blockOffset">The cache index of the block origin.</param>
        /// <param name="rows">The visible row count.</param>
        /// <param name="columns">The visible column count.</param>
        /// <param name="histogramScale">The chroma subsampling area factor, or one for luma.</param>
        /// <param name="threshold">The speed-dependent neural score threshold.</param>
        /// <returns>A bit mask whose eight bits correspond to the contiguous directional prediction modes.</returns>
        internal static byte GetCachedDirectionalModeSkipMask(
            ReadOnlySpan<short> magnitudes,
            ReadOnlySpan<sbyte> bins,
            int blockOffset,
            int rows,
            int columns,
            int histogramScale,
            float threshold)
        {
            Span<float> histogram = stackalloc float[GradientBinCount];
            histogram.Clear();
            float total = 0.1F;

            // The cache holds the same gradient as the block's own Sobel window at every interior sample, because
            // each gradient reads only its 3x3 neighbors, and those lie inside the block. The histogram adds them
            // in the same row order, so the float sums are the same as those of the uncached path.
            const int Stride = Av1EncoderModeDecisionWorkspace<TSample>.MaximumBlockDimension;
            for (int row = 1; row < rows - 1; row++)
            {
                int rowOffset = blockOffset + (row * Stride);
                for (int column = 1; column < columns - 1; column++)
                {
                    int magnitude = magnitudes[rowOffset + column];
                    if (magnitude == 0)
                    {
                        continue;
                    }

                    total += magnitude;
                    int bin = bins[rowOffset + column];
                    if (bin == Av1GradientHistogram.VerticalBin)
                    {
                        histogram[0] += magnitude >> 1;
                        histogram[^1] += magnitude >> 1;
                        continue;
                    }

                    histogram[bin] += magnitude;
                }
            }

            return ScoreDirectionalModes(histogram, total, histogramScale, threshold);
        }

        /// <summary>
        /// Computes the gradient magnitude and histogram bin of every interior sample of a superblock region, so that
        /// the blocks of the superblock read them instead of computing them again.
        /// Each sample depth is first copied to 16-bit rows, so one row kernel serves both depths.
        /// </summary>
        /// <param name="source">The source component plane.</param>
        /// <param name="sourceSamples">The samples of the complete source plane, read once by the caller.</param>
        /// <param name="origin">The superblock origin in the plane.</param>
        /// <param name="rows">The number of superblock rows inside the coded image.</param>
        /// <param name="columns">The number of superblock columns inside the coded image.</param>
        /// <param name="magnitudes">Receives |dx| + |dy| of each interior sample, one cache row per superblock row.</param>
        /// <param name="bins">Receives the histogram bin of each interior sample.</param>
        internal static void CacheSuperblockGradients(
            Av1PlaneRegion<TSample> source,
            ReadOnlySpan<TSample> sourceSamples,
            Point origin,
            int rows,
            int columns,
            Span<short> magnitudes,
            Span<sbyte> bins)
        {
            if (rows <= 2 || columns <= 2)
            {
                return;
            }

            // The same vector row kernel as the uncached path fills one cache row at a time from a three-row window.
            const int Stride = Av1EncoderModeDecisionWorkspace<TSample>.MaximumBlockDimension;
            Span<short> window = stackalloc short[3 * Stride];
            Span<short> rowMagnitudes = stackalloc short[Stride];
            Span<int> rowBins = stackalloc int[Stride];
            Span<short> horizontal = stackalloc short[Stride];
            Span<short> vertical = stackalloc short[Stride];
            int interior = columns - 2;

            // The window is a ring of three rows: each step widens only the new row below, into the slot of the row that
            // left the window, so each source row is widened once.
            ReadOnlySpan<TSample> blockSamples = sourceSamples[source.GetOffset(origin.X, origin.Y)..];
            TOperator.CopyPaletteSamples(blockSamples, source.Stride, 2, columns, window);
            for (int row = 1; row < rows - 1; row++)
            {
                Span<short> belowRow = window.Slice(((row + 1) % 3) * columns, columns);
                TOperator.CopyPaletteSamples(blockSamples[((row + 1) * source.Stride)..], source.Stride, 1, columns, belowRow);
                Av1GradientHistogram.ComputeRow(
                    window.Slice(((row - 1) % 3) * columns, columns),
                    window.Slice((row % 3) * columns, columns),
                    belowRow,
                    columns,
                    rowMagnitudes,
                    rowBins,
                    horizontal,
                    vertical);

                int rowOffset = (row * Stride) + 1;
                rowMagnitudes[..interior].CopyTo(magnitudes.Slice(rowOffset, interior));
                Span<sbyte> binRow = bins.Slice(rowOffset, interior);
                for (int column = 0; column < interior; column++)
                {
                    binRow[column] = (sbyte)rowBins[column];
                }
            }
        }

        /// <summary>
        /// Scores the eight directional modes with the gradient model and marks the modes whose score is at most the
        /// threshold. The intra mode search skips each marked mode.
        /// </summary>
        /// <param name="histogram">The gradient histogram of the block.</param>
        /// <param name="total">The sum of the gradient magnitudes plus the 0.1 bias.</param>
        /// <param name="histogramScale">The chroma subsampling area factor, or one for luma.</param>
        /// <param name="threshold">The speed-dependent neural score threshold.</param>
        /// <returns>A bit mask whose eight bits correspond to the contiguous directional prediction modes.</returns>
        private static byte ScoreDirectionalModes(ReadOnlySpan<float> histogram, float total, int histogramScale, float threshold)
        {
            // The histogram is normalized and scaled once, then every mode weighs the same values.
            InlineArray32<float> featureStorage = default;
            Span<float> features = featureStorage;
            for (int bin = 0; bin < GradientBinCount; bin++)
            {
                features[bin] = (histogram[bin] / total) * histogramScale;
            }

            byte mask = 0;
            ReadOnlySpan<float> weights = GradientModelWeights;
            ReadOnlySpan<float> bias = GradientModelBias;
            for (int mode = 0; mode < bias.Length; mode++)
            {
                float score = bias[mode];
                int weightOffset = mode * GradientBinCount;
                for (int bin = 0; bin < GradientBinCount; bin++)
                {
                    score += weights[weightOffset + bin] * features[bin];
                }

                // Reduce neural outputs to Q9 before comparing the speed-dependent threshold.
                score = (int)((score * 512F) + 0.5F) / 512F;
                if (score <= threshold)
                {
                    mask |= (byte)(1 << mode);
                }
            }

            return mask;
        }
    }
}
