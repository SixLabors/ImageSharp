// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The frame and superblock decisions that read the temporal dependency statistics: the frame importance and the
/// golden boost, the frame quantizer, the rate multiplier scaling, and the objective delta quantizer.
/// </summary>
internal static class Av1TplDecisions
{
    /// <summary>
    /// The resolution of objective delta quantizers. Reference: DEFAULT_DELTA_Q_RES_OBJECTIVE.
    /// </summary>
    public const int ObjectiveDeltaQResolution = 4;

    /// <summary>
    /// The largest golden boost factor. Reference: MAX_GFUBOOST_FACTOR.
    /// </summary>
    private const double MaximumGoldenBoostFactor = 10.0;

    /// <summary>
    /// The largest boost combination factor. Reference: MAX_BOOST_COMBINE_FACTOR.
    /// </summary>
    private const double MaximumBoostCombineFactor = 12.0;

    /// <summary>
    /// Returns the frame importance: the exponential of the source-distortion weighted mean of the logarithmic gain that
    /// later frames draw from the frame. Reference: get_frame_importance().
    /// </summary>
    /// <param name="frame">The frame statistics.</param>
    /// <returns>The frame importance.</returns>
    public static double GetFrameImportance(Av1TplFrameStatistics frame)
    {
        ReadOnlySpan<Av1TplBlockStatistics> statistics = frame.Statistics;
        double intraCostBase = 0;
        double dependencyCostBase = 0;
        double weightBase = 1;
        const int Step = 1 << Av1TplModelConstants.BlockModeInfoLog2;
        for (int row = 0; row < frame.ModeInfoRows; row += Step)
        {
            for (int column = 0; column < frame.ModeInfoColumns; column += Step)
            {
                ref readonly Av1TplBlockStatistics block = ref statistics[Av1TplFrameStatistics.GetPosition(
                    row, column, frame.Stride, Av1TplModelConstants.BlockModeInfoLog2)];

                double weight = block.SourceReferenceDistortion;
                long dependencyDelta = Av1TplModelConstants.GetCost(frame.BaseRateMultiplier, block.DependencyRate, block.DependencyDistortion);
                double scaledDistortion = block.ReconstructedReferenceDistortion << Av1TplModelConstants.RateDistortionDivisorBits;
                scaledDistortion = Math.Max(scaledDistortion, 1);
                intraCostBase += Math.Log(scaledDistortion) * weight;
                dependencyCostBase += Math.Log(scaledDistortion + dependencyDelta) * weight;
                weightBase += weight;
            }
        }

        return Math.Exp((dependencyCostBase - intraCostBase) / weightBase);
    }

    /// <summary>
    /// Measures the propagated importance of the frame being coded, which later decisions read as r0, and for an
    /// eligible frame blends the golden boost it implies into the rate control's boost. A frame whose statistics show no
    /// dependency loses its validity. The encoder calls it once per frame whose statistics are ready, before it picks
    /// the frame quantizer. Reference: process_tpl_stats_frame() with the look-ahead branch.
    /// </summary>
    /// <param name="frame">The statistics of the frame being coded.</param>
    /// <param name="tplEligible">Whether the frame is a key, golden or alternate reference frame.</param>
    /// <param name="baselineGoldenInterval">The golden interval. Reference: p_rc->baseline_gf_interval.</param>
    /// <param name="statsRequiredForBoost">Reference: p_rc->num_stats_required_for_gfu_boost.</param>
    /// <param name="statsUsedForBoost">Reference: p_rc->num_stats_used_for_gfu_boost.</param>
    /// <param name="r0">The importance of the frame, unchanged when the frame shows no dependency. Reference: cpi->rd.r0.</param>
    /// <param name="goldenBoost">The golden boost, updated for eligible frames. Reference: p_rc->gfu_boost.</param>
    public static void ProcessFrame(
        Av1TplFrameStatistics frame,
        bool tplEligible,
        int baselineGoldenInterval,
        int statsRequiredForBoost,
        int statsUsedForBoost,
        ref double r0,
        ref int goldenBoost)
    {
        if (!frame.IsValid)
        {
            return;
        }

        ReadOnlySpan<Av1TplBlockStatistics> statistics = frame.Statistics;
        double intraCostBase = 0;
        double dependencyCostBase = 0;
        double weightBase = 1;
        const int Step = 1 << Av1TplModelConstants.BlockModeInfoLog2;
        for (int row = 0; row < frame.ModeInfoRows; row += Step)
        {
            for (int column = 0; column < frame.ModeInfoColumns; column += Step)
            {
                ref readonly Av1TplBlockStatistics block = ref statistics[Av1TplFrameStatistics.GetPosition(
                    row, column, frame.Stride, Av1TplModelConstants.BlockModeInfoLog2)];

                double weight = block.SourceReferenceDistortion;
                long dependencyDelta = Av1TplModelConstants.GetCost(frame.BaseRateMultiplier, block.DependencyRate, block.DependencyDistortion);
                double scaledDistortion = block.ReconstructedReferenceDistortion << Av1TplModelConstants.RateDistortionDivisorBits;
                intraCostBase += Math.Log(scaledDistortion) * weight;
                dependencyCostBase += Math.Log(scaledDistortion + dependencyDelta) * weight;
                weightBase += weight;
            }
        }

        if (dependencyCostBase == 0)
        {
            frame.IsValid = false;
            return;
        }

        r0 = Math.Exp((intraCostBase - dependencyCostBase) / weightBase);
        if (tplEligible)
        {
            double minimumBoostFactor = Math.Sqrt(baselineGoldenInterval);
            int tplBoost = GetGoldenBoostFromR0(minimumBoostFactor, MaximumGoldenBoostFactor, r0, statsRequiredForBoost);
            goldenBoost = CombinePriorWithTplBoost(minimumBoostFactor, MaximumBoostCombineFactor, goldenBoost, tplBoost, statsUsedForBoost);
        }
    }

    /// <summary>
    /// Returns the golden boost that an importance implies: a projection factor over the frame count divided by r0.
    /// Reference: get_gfu_boost_from_r0_lap() with av1_get_gfu_boost_projection_factor().
    /// </summary>
    /// <param name="minimumFactor">The smallest factor.</param>
    /// <param name="maximumFactor">The largest factor.</param>
    /// <param name="r0">The importance.</param>
    /// <param name="frameCount">The number of frames the boost covers.</param>
    /// <returns>The golden boost.</returns>
    public static int GetGoldenBoostFromR0(double minimumFactor, double maximumFactor, double r0, int frameCount)
    {
        double factor = Math.Sqrt(frameCount);
        factor = Math.Min(factor, maximumFactor);
        factor = Math.Max(factor, minimumFactor);
        factor = 200.0 + (10.0 * factor);
        return (int)Math.Round(factor / r0, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Blends the prior golden boost with the model's by a factor that grows with the number of frames.
    /// Reference: combine_prior_with_tpl_boost().
    /// </summary>
    /// <param name="minimumFactor">The smallest factor.</param>
    /// <param name="maximumFactor">The largest factor.</param>
    /// <param name="priorBoost">The prior boost.</param>
    /// <param name="tplBoost">The model boost.</param>
    /// <param name="framesToKey">The number of frames the boost covers.</param>
    /// <returns>The combined boost.</returns>
    public static int CombinePriorWithTplBoost(double minimumFactor, double maximumFactor, int priorBoost, int tplBoost, int framesToKey)
    {
        double factor = Math.Sqrt(framesToKey);
        double range = maximumFactor - minimumFactor;
        factor = Math.Min(factor, maximumFactor);
        factor = Math.Max(factor, minimumFactor);
        factor -= minimumFactor;
        return (int)(((factor * priorBoost) + ((range - factor) * tplBoost)) / range);
    }

    /// <summary>
    /// Returns the quantizer of a frame from its importance: the leaf quantizer's DC step scaled by the inverse square
    /// root of the importance, or the leaf quantizer without ready statistics. In constant quality mode the encoder
    /// clamps it to the allowed range and uses it for every frame whose statistics are valid, and an alternate reference
    /// records it as its quantizer. Reference: av1_tpl_get_q_index() with av1_tpl_get_qstep_ratio(), and the AOM_Q
    /// branch of av1_set_size_dependent_vars().
    /// </summary>
    /// <param name="statisticsReady">
    /// Whether the statistics of the frame are ready. Reference:
    /// <see cref="Av1TplModel{TSample, TSearchOperator, TSampleOperator}.IsStatisticsReady"/>.
    /// </param>
    /// <param name="frame">The statistics of the frame.</param>
    /// <param name="leafQIndex">The leaf quantizer. Reference: rc->active_worst_quality.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <returns>The quantizer index.</returns>
    public static int GetQIndex(bool statisticsReady, Av1TplFrameStatistics frame, int leafQIndex, Av1BitDepth bitDepth)
    {
        double ratio = 1;
        if (statisticsReady)
        {
            ratio = Math.Sqrt(1 / GetFrameImportance(frame));
        }

        return GetQIndexFromQStepRatio(leafQIndex, ratio, bitDepth);
    }

    /// <summary>
    /// Returns the quantizer whose DC step first reaches a ratio of the leaf quantizer's step, searching down for a
    /// ratio below one and up otherwise. Reference: av1_get_q_index_from_qstep_ratio().
    /// </summary>
    /// <param name="leafQIndex">The leaf quantizer.</param>
    /// <param name="qStepRatio">The step ratio.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <returns>The quantizer index.</returns>
    public static int GetQIndexFromQStepRatio(int leafQIndex, double qStepRatio, Av1BitDepth bitDepth)
    {
        double leafStep = Av1QuantizationLookup.GetDcQuant(leafQIndex, 0, bitDepth);
        double targetStep = leafStep * qStepRatio;
        int qIndex;
        if (qStepRatio < 1.0)
        {
            for (qIndex = leafQIndex; qIndex > 0; qIndex--)
            {
                if (Av1QuantizationLookup.GetDcQuant(qIndex, 0, bitDepth) <= targetStep)
                {
                    break;
                }
            }
        }
        else
        {
            for (qIndex = leafQIndex; qIndex < Av1Constants.MaxQ; qIndex++)
            {
                if (Av1QuantizationLookup.GetDcQuant(qIndex, 0, bitDepth) >= targetStep)
                {
                    break;
                }
            }
        }

        return qIndex;
    }

    /// <summary>
    /// Computes the rate multiplier scaling of every 16x16 block of the frame: the ratio of its reconstruction cost to
    /// its reconstruction plus dependency cost, relative to r0, plus 1.2. Reference: av1_tpl_rdmult_setup().
    /// </summary>
    /// <param name="frame">The statistics of the frame being coded.</param>
    /// <param name="r0">The importance of the frame. Reference: cpi->rd.r0.</param>
    /// <param name="scalingFactors">
    /// Receives one factor per 16x16 block in raster order, with (mi_cols + 3) / 4 columns. Reference:
    /// tpl_rdmult_scaling_factors.
    /// </param>
    public static void SetupRateMultiplierScaling(Av1TplFrameStatistics frame, double r0, Span<double> scalingFactors)
    {
        if (!frame.IsValid)
        {
            return;
        }

        ReadOnlySpan<Av1TplBlockStatistics> statistics = frame.Statistics;
        const int BlockModeInfo = 4;
        int columns = (frame.ModeInfoColumns + BlockModeInfo - 1) / BlockModeInfo;
        int rows = (frame.ModeInfoRows + BlockModeInfo - 1) / BlockModeInfo;
        const double Offset = 1.2;
        const int Step = 1 << Av1TplModelConstants.BlockModeInfoLog2;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                double intraCost = 0.0;
                double dependencyCost = 0.0;
                for (int modeInfoRow = row * BlockModeInfo; modeInfoRow < (row + 1) * BlockModeInfo; modeInfoRow += Step)
                {
                    for (int modeInfoColumn = column * BlockModeInfo; modeInfoColumn < (column + 1) * BlockModeInfo; modeInfoColumn += Step)
                    {
                        if (modeInfoRow >= frame.ModeInfoRows || modeInfoColumn >= frame.ModeInfoColumns)
                        {
                            continue;
                        }

                        ref readonly Av1TplBlockStatistics block = ref statistics[Av1TplFrameStatistics.GetPosition(
                            modeInfoRow, modeInfoColumn, frame.Stride, Av1TplModelConstants.BlockModeInfoLog2)];

                        long dependencyDelta = Av1TplModelConstants.GetCost(frame.BaseRateMultiplier, block.DependencyRate, block.DependencyDistortion);
                        intraCost += block.ReconstructedReferenceDistortion << Av1TplModelConstants.RateDistortionDivisorBits;
                        dependencyCost += (block.ReconstructedReferenceDistortion << Av1TplModelConstants.RateDistortionDivisorBits) + dependencyDelta;
                    }
                }

                double ratio = intraCost / dependencyCost;
                scalingFactors[(row * columns) + column] = (ratio / r0) + Offset;
            }
        }
    }

    /// <summary>
    /// Computes the superblock rate multiplier scaling: the frame scaling of its 16x16 blocks, normalized so that their
    /// geometric mean equals the ratio of the rate multipliers at the superblock and frame quantizers. The reference
    /// encoder writes the result to tpl_sb_rdmult_scaling_factors, which no decision reads. Reference:
    /// av1_tpl_rdmult_setup_sb().
    /// </summary>
    /// <param name="frame">The statistics of the frame being coded.</param>
    /// <param name="scalingFactors">The frame scaling of <see cref="SetupRateMultiplierScaling"/>.</param>
    /// <param name="superblockScalingFactors">Receives the superblock scaling at the same indices.</param>
    /// <param name="superblockModeInfoSize">The superblock size in mode-information units.</param>
    /// <param name="modeInfoRow">The superblock row in mode-information units.</param>
    /// <param name="modeInfoColumn">The superblock column in mode-information units.</param>
    /// <param name="frameRateMultiplier">The rate multiplier at the frame quantizer plus the luma DC delta.</param>
    /// <param name="superblockRateMultiplier">The rate multiplier at the superblock quantizer plus the luma DC delta.</param>
    public static void SetupSuperblockRateMultiplierScaling(
        Av1TplFrameStatistics frame,
        ReadOnlySpan<double> scalingFactors,
        Span<double> superblockScalingFactors,
        int superblockModeInfoSize,
        int modeInfoRow,
        int modeInfoColumn,
        int frameRateMultiplier,
        int superblockRateMultiplier)
    {
        const int BlockModeInfo = 4;
        int columns = (frame.ModeInfoColumns + BlockModeInfo - 1) / BlockModeInfo;
        int rows = (frame.ModeInfoRows + BlockModeInfo - 1) / BlockModeInfo;
        int blockColumns = (superblockModeInfoSize + BlockModeInfo - 1) / BlockModeInfo;
        int blockRows = (superblockModeInfoSize + BlockModeInfo - 1) / BlockModeInfo;
        double baseBlockCount = 0.0;
        double logSum = 0.0;
        for (int row = modeInfoRow / BlockModeInfo; row < rows && row < (modeInfoRow / BlockModeInfo) + blockRows; row++)
        {
            for (int column = modeInfoColumn / BlockModeInfo; column < columns && column < (modeInfoColumn / BlockModeInfo) + blockColumns; column++)
            {
                logSum += Math.Log(scalingFactors[(row * columns) + column]);
                baseBlockCount += 1.0;
            }
        }

        double scalingFactor = superblockRateMultiplier / (double)frameRateMultiplier;
        double scaleAdjustment = Math.Log(scalingFactor) - (logSum / baseBlockCount);
        scaleAdjustment = ExpBounded(scaleAdjustment);
        for (int row = modeInfoRow / BlockModeInfo; row < rows && row < (modeInfoRow / BlockModeInfo) + blockRows; row++)
        {
            for (int column = modeInfoColumn / BlockModeInfo; column < columns && column < (modeInfoColumn / BlockModeInfo) + blockColumns; column++)
            {
                int index = (row * columns) + column;
                superblockScalingFactors[index] = scaleAdjustment * scalingFactors[index];
            }
        }
    }

    /// <summary>
    /// Returns the objective quantizer of a superblock: the frame quantizer moved by the offset whose DC step scales by
    /// the inverse square root of the ratio of r0 to the superblock's importance, limited to nine resolution steps. It
    /// also returns the regularized importance that the coding-block rate multiplier divides by, and optionally the
    /// estimated change of rate-distortion cost that the frame-level delta quantizer decision sums. Reference:
    /// av1_get_q_for_deltaq_objective().
    /// </summary>
    /// <param name="frame">The statistics of the frame being coded.</param>
    /// <param name="superblockModeInfoSize">The superblock size in mode-information units.</param>
    /// <param name="modeInfoRow">The superblock row in mode-information units.</param>
    /// <param name="modeInfoColumn">The superblock column in mode-information units.</param>
    /// <param name="baseQIndex">The frame quantizer. Reference: base_qindex.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="r0">The importance of the frame. Reference: cpi->rd.r0.</param>
    /// <param name="regularizedImportance">
    /// Receives the regularized importance of the superblock, or keeps its value when the statistics give none.
    /// Reference: x->rb.
    /// </param>
    /// <param name="deltaDistortion">Receives the rate-distortion change, when requested. Reference: delta_dist.</param>
    /// <param name="computeDeltaDistortion">Whether to compute <paramref name="deltaDistortion"/>.</param>
    /// <returns>The superblock quantizer.</returns>
    public static int GetQForDeltaQObjective(
        Av1TplFrameStatistics frame,
        int superblockModeInfoSize,
        int modeInfoRow,
        int modeInfoColumn,
        int baseQIndex,
        Av1BitDepth bitDepth,
        double r0,
        ref double regularizedImportance,
        out long deltaDistortion,
        bool computeDeltaDistortion)
    {
        deltaDistortion = 0;
        if (!frame.IsValid)
        {
            return baseQIndex;
        }

        ReadOnlySpan<Av1TplBlockStatistics> statistics = frame.Statistics;
        double intraCost = 0;
        double dependencyRegularized = 0;
        double dependencyCost = 0;
        double weightBase = 1;
        double sourceDistortion = 0;
        double sourceSse = 0;
        double sourceRate = 0;
        const int Step = 1 << Av1TplModelConstants.BlockModeInfoLog2;
        for (int row = modeInfoRow; row < modeInfoRow + superblockModeInfoSize; row += Step)
        {
            for (int column = modeInfoColumn; column < modeInfoColumn + superblockModeInfoSize; column += Step)
            {
                if (row >= frame.ModeInfoRows || column >= frame.ModeInfoColumns)
                {
                    continue;
                }

                ref readonly Av1TplBlockStatistics block = ref statistics[Av1TplFrameStatistics.GetPosition(
                    row, column, frame.Stride, Av1TplModelConstants.BlockModeInfoLog2)];

                double weight = block.SourceReferenceDistortion;
                long dependencyDelta = Av1TplModelConstants.GetCost(frame.BaseRateMultiplier, block.DependencyRate, block.DependencyDistortion);
                double scaledDistortion = block.ReconstructedReferenceDistortion << Av1TplModelConstants.RateDistortionDivisorBits;
                intraCost += Math.Log(scaledDistortion) * weight;
                dependencyCost += Math.Log(scaledDistortion + dependencyDelta) * weight;
                dependencyRegularized += Math.Log((3 * scaledDistortion) + dependencyDelta) * weight;
                sourceDistortion += block.SourceReferenceDistortion << Av1TplModelConstants.RateDistortionDivisorBits;
                sourceSse += block.SourceReferenceSse << Av1TplModelConstants.RateDistortionDivisorBits;
                sourceRate += (long)block.SourceReferenceRate << Av1TplModelConstants.DependencyCostScaleLog2;
                weightBase += weight;
            }
        }

        if (!(dependencyCost > 0 && intraCost > 0))
        {
            return baseQIndex;
        }

        double importance = Math.Exp((intraCost - dependencyCost) / weightBase);
        regularizedImportance = Math.Exp((intraCost - dependencyRegularized) / weightBase);
        double beta = r0 / importance;
        int offset = GetDeltaQOffset(bitDepth, baseQIndex, beta);
        offset = Math.Min(offset, (ObjectiveDeltaQResolution * 9) - 1);
        offset = Math.Max(offset, (-ObjectiveDeltaQResolution * 9) + 1);
        int qIndex = baseQIndex + offset;
        qIndex = Math.Min(qIndex, Av1Constants.MaxQ);
        qIndex = Math.Max(qIndex, 0);

        if (computeDeltaDistortion)
        {
            // The distortion scales with the square of the step and the rate inversely, bounded by the prediction
            // energy; a nonzero delta costs four bits.
            int frameStep = Av1QuantizationLookup.GetDcQuant(baseQIndex, 0, bitDepth);
            int superblockStep = Av1QuantizationLookup.GetDcQuant(baseQIndex, offset, bitDepth);
            double superblockDistortion = sourceDistortion * Math.Pow((double)superblockStep / frameStep, 2.0);
            double superblockRate = sourceRate * ((double)frameStep / superblockStep);
            superblockDistortion = Math.Min(superblockDistortion, sourceSse);
            deltaDistortion = (long)((superblockDistortion - sourceDistortion) / importance);
            deltaDistortion += Av1TplModelConstants.GetCost(frame.BaseRateMultiplier, 4 * 256, 0);
            deltaDistortion += Av1TplModelConstants.GetCost(frame.BaseRateMultiplier, (long)(superblockRate - sourceRate), 0);
        }

        return qIndex;
    }

    /// <summary>
    /// Returns whether objective delta quantizers lower the frame's estimated rate-distortion cost, summed over every
    /// superblock. A frame that fails keeps its delta quantizer syntax off. Reference: allow_deltaq_mode().
    /// </summary>
    /// <param name="frame">The statistics of the frame being coded.</param>
    /// <param name="superblockModeInfoSize">The superblock size in mode-information units.</param>
    /// <param name="baseQIndex">The frame quantizer.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="r0">The importance of the frame.</param>
    /// <param name="regularizedImportance">The running regularized importance, which the calls update. Reference: x->rb.</param>
    /// <returns><see langword="true"/> when the summed change is negative.</returns>
    public static bool AllowDeltaQ(
        Av1TplFrameStatistics frame,
        int superblockModeInfoSize,
        int baseQIndex,
        Av1BitDepth bitDepth,
        double r0,
        ref double regularizedImportance)
    {
        long deltaCost = 0;
        for (int row = 0; row < frame.ModeInfoRows; row += superblockModeInfoSize)
        {
            for (int column = 0; column < frame.ModeInfoColumns; column += superblockModeInfoSize)
            {
                GetQForDeltaQObjective(frame, superblockModeInfoSize, row, column, baseQIndex, bitDepth, r0, ref regularizedImportance, out long delta, true);
                deltaCost += delta;
            }
        }

        return deltaCost < 0;
    }

    /// <summary>
    /// Returns the change of quantizer index whose DC step equals the step divided by the square root of beta, searching
    /// from the frame quantizer. Reference: av1_get_deltaq_offset().
    /// </summary>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="qIndex">The frame quantizer.</param>
    /// <param name="beta">The importance ratio.</param>
    /// <returns>The quantizer index change.</returns>
    public static int GetDeltaQOffset(Av1BitDepth bitDepth, int qIndex, double beta)
    {
        int q = Av1QuantizationLookup.GetDcQuant(qIndex, 0, bitDepth);
        int newQ = (int)Math.Round(q / Math.Sqrt(beta), MidpointRounding.ToEven);
        int originalQIndex = qIndex;
        if (newQ == q)
        {
            return 0;
        }

        if (newQ < q)
        {
            while (qIndex > 0)
            {
                qIndex--;
                q = Av1QuantizationLookup.GetDcQuant(qIndex, 0, bitDepth);
                if (newQ >= q)
                {
                    break;
                }
            }
        }
        else
        {
            while (qIndex < Av1Constants.MaxQ)
            {
                qIndex++;
                q = Av1QuantizationLookup.GetDcQuant(qIndex, 0, bitDepth);
                if (newQ <= q)
                {
                    break;
                }
            }
        }

        return qIndex - originalQIndex;
    }

    /// <summary>
    /// Returns the rate multiplier of a coding block with objective delta quantizers: the multiplier at the superblock
    /// quantizer, scaled by the block's importance over the superblock's regularized importance. Reference:
    /// av1_get_cb_rdmult().
    /// </summary>
    /// <param name="statisticsReady">Whether the statistics of the frame are ready.</param>
    /// <param name="frame">The statistics of the frame being coded.</param>
    /// <param name="blockSize">The coding block size.</param>
    /// <param name="modeInfoRow">The block row in mode-information units.</param>
    /// <param name="modeInfoColumn">The block column in mode-information units.</param>
    /// <param name="deltaQRateMultiplier">
    /// The rate multiplier at the frame quantizer plus the superblock delta and the luma DC delta, with the layer and
    /// boost adjustments. Reference: set_rdmult(cpi, x, -1).
    /// </param>
    /// <param name="regularizedImportance">The regularized importance of the superblock. Reference: x->rb.</param>
    /// <returns>The rate multiplier, at least one.</returns>
    public static int GetCodingBlockRateMultiplier(
        bool statisticsReady,
        Av1TplFrameStatistics frame,
        Av1BlockSize blockSize,
        int modeInfoRow,
        int modeInfoColumn,
        int deltaQRateMultiplier,
        double regularizedImportance)
    {
        if (!statisticsReady || regularizedImportance == 0)
        {
            return deltaQRateMultiplier;
        }

        ReadOnlySpan<Av1TplBlockStatistics> statistics = frame.Statistics;
        int wide = blockSize.Get4x4WideCount();
        int high = blockSize.Get4x4HighCount();
        double intraCostBase = 0;
        double dependencyCostBase = 0;
        double weightBase = 0;
        const int Step = 1 << Av1TplModelConstants.BlockModeInfoLog2;
        for (int row = modeInfoRow; row < modeInfoRow + high; row += Step)
        {
            for (int column = modeInfoColumn; column < modeInfoColumn + wide; column += Step)
            {
                if (row >= frame.ModeInfoRows || column >= frame.ModeInfoColumns)
                {
                    continue;
                }

                ref readonly Av1TplBlockStatistics block = ref statistics[Av1TplFrameStatistics.GetPosition(
                    row, column, frame.Stride, Av1TplModelConstants.BlockModeInfoLog2)];

                double weight = block.SourceReferenceDistortion;
                long dependencyDelta = Av1TplModelConstants.GetCost(frame.BaseRateMultiplier, block.DependencyRate, block.DependencyDistortion);
                double scaledDistortion = block.ReconstructedReferenceDistortion << Av1TplModelConstants.RateDistortionDivisorBits;
                intraCostBase += Math.Log(scaledDistortion) * weight;
                dependencyCostBase += Math.Log((3 * scaledDistortion) + dependencyDelta) * weight;
                weightBase += weight;
            }
        }

        if (weightBase == 0)
        {
            return deltaQRateMultiplier;
        }

        double importance = Math.Exp((intraCostBase - dependencyCostBase) / weightBase);
        int rateMultiplier = (int)(deltaQRateMultiplier * (importance / regularizedImportance));
        return Math.Max(rateMultiplier, 1);
    }

    /// <summary>
    /// Chooses the references a superblock keeps from the selective reference pruning: INTRA, LAST and the three
    /// references whose prediction error falls most below LAST's, then further references until one gains less than an
    /// eighth of the previous. An overlay keeps all. The result is all zero without ready statistics of an eligible
    /// frame. Reference: init_ref_frame_space().
    /// </summary>
    /// <param name="statisticsReady">Whether the statistics of the frame are ready.</param>
    /// <param name="frame">The statistics of the frame being coded.</param>
    /// <param name="updateType">The update type of the frame.</param>
    /// <param name="tplEligible">Whether the frame is a key, golden or alternate reference frame.</param>
    /// <param name="superblockModeInfoSize">The superblock size in mode-information units.</param>
    /// <param name="modeInfoRow">The superblock row.</param>
    /// <param name="modeInfoColumn">The superblock column.</param>
    /// <param name="keepReferenceFrame">Receives one flag per reference type, INTRA to ALTREF. Reference: x->tpl_keep_ref_frame.</param>
    public static void GetKeptReferenceFrames(
        bool statisticsReady,
        Av1TplFrameStatistics frame,
        Av1FrameUpdateType updateType,
        bool tplEligible,
        int superblockModeInfoSize,
        int modeInfoRow,
        int modeInfoColumn,
        Span<bool> keepReferenceFrame)
    {
        keepReferenceFrame.Clear();
        if (!statisticsReady || !tplEligible)
        {
            return;
        }

        if (updateType == Av1FrameUpdateType.Overlay)
        {
            keepReferenceFrame.Fill(true);
            return;
        }

        const int References = Av1TplModelConstants.InterReferenceCount;
        ReadOnlySpan<Av1TplBlockStatistics> statistics = frame.Statistics;
        Span<long> interCost = stackalloc long[References];
        interCost.Clear();
        int rowEnd = Math.Min(superblockModeInfoSize + modeInfoRow, frame.ModeInfoRows);
        int columnEnd = Math.Min(modeInfoColumn + superblockModeInfoSize, frame.ModeInfoColumns);
        const int Step = 1 << Av1TplModelConstants.BlockModeInfoLog2;
        for (int row = modeInfoRow; row < rowEnd; row += Step)
        {
            for (int column = modeInfoColumn; column < columnEnd; column += Step)
            {
                ref readonly Av1TplBlockStatistics block = ref statistics[Av1TplFrameStatistics.GetPosition(
                    row, column, frame.Stride, Av1TplModelConstants.BlockModeInfoLog2)];

                // The winner is the smallest nonzero prediction error; its reduction against LAST is summed.
                long bestInterCost = block.PredictionError[0];
                int bestReference = 0;
                for (int reference = 1; reference < References; reference++)
                {
                    if (block.PredictionError[reference] < bestInterCost && block.PredictionError[reference] != 0)
                    {
                        bestInterCost = block.PredictionError[reference];
                        bestReference = reference;
                    }
                }

                if (bestReference != 0)
                {
                    interCost[bestReference] += block.PredictionError[bestReference] - block.PredictionError[0];
                }
            }
        }

        // An insertion sort by increasing summed reduction (most negative first).
        Span<int> rank = stackalloc int[References - 1];
        for (int index = 0; index < References - 1; index++)
        {
            rank[index] = index + 1;
            for (int i = index; i > 0; i--)
            {
                if (interCost[rank[i - 1]] > interCost[rank[i]])
                {
                    (rank[i - 1], rank[i]) = (rank[i], rank[i - 1]);
                }
            }
        }

        keepReferenceFrame[0] = true;
        keepReferenceFrame[1] = true;
        bool cutoff = false;
        for (int index = 0; index < References - 1; index++)
        {
            keepReferenceFrame[rank[index] + 1] = true;
            if (index > 2)
            {
                if (!cutoff &&
                    (Math.Abs(interCost[rank[index]]) < Math.Abs(interCost[rank[index - 1]]) / 8 || interCost[rank[index]] == 0))
                {
                    cutoff = true;
                }

                if (cutoff)
                {
                    keepReferenceFrame[rank[index] + 1] = false;
                }
            }
        }
    }

    /// <summary>
    /// Gathers the model costs and vectors of every 16x16 block of a superblock, in raster order with the
    /// superblock's block stride; blocks outside the frame get the maximum costs and invalid vectors. Nothing is
    /// gathered for key frames, overlays, or without ready statistics. Reference: av1_get_tpl_stats_sb().
    /// </summary>
    /// <param name="statisticsReady">Whether the statistics of the frame are ready.</param>
    /// <param name="frame">The statistics of the frame being coded.</param>
    /// <param name="keyFrame">Whether the frame is a key frame.</param>
    /// <param name="updateType">The update type of the frame.</param>
    /// <param name="superblockModeInfoSize">The superblock size in mode-information units.</param>
    /// <param name="modeInfoRow">The superblock row.</param>
    /// <param name="modeInfoColumn">The superblock column.</param>
    /// <param name="interCosts">Receives the inter costs, scaled by sixteen. Reference: tpl_inter_cost.</param>
    /// <param name="intraCosts">Receives the intra costs, scaled by sixteen. Reference: tpl_intra_cost.</param>
    /// <param name="vectors">Receives seven vectors per block, one per reference. Reference: tpl_mv.</param>
    /// <param name="stride">Receives the number of blocks per superblock row. Reference: tpl_stride.</param>
    /// <returns>The number of blocks inside the frame. Reference: tpl_data_count.</returns>
    public static int GetSuperblockStatistics(
        bool statisticsReady,
        Av1TplFrameStatistics frame,
        bool keyFrame,
        Av1FrameUpdateType updateType,
        int superblockModeInfoSize,
        int modeInfoRow,
        int modeInfoColumn,
        Span<long> interCosts,
        Span<long> intraCosts,
        Span<Av1MotionVector> vectors,
        out int stride)
    {
        stride = 0;
        if (keyFrame || updateType is Av1FrameUpdateType.IntermediateOverlay or Av1FrameUpdateType.Overlay || !statisticsReady)
        {
            return 0;
        }

        ReadOnlySpan<Av1TplBlockStatistics> statistics = frame.Statistics;
        const int Step = Av1TplModelConstants.BlockSize >> 2;
        stride = superblockModeInfoSize / Step;
        int count = 0;
        int blocksInside = 0;
        for (int row = modeInfoRow; row < modeInfoRow + superblockModeInfoSize; row += Step)
        {
            for (int column = modeInfoColumn; column < modeInfoColumn + superblockModeInfoSize; column += Step)
            {
                Span<Av1MotionVector> blockVectors = vectors.Slice(count * Av1TplModelConstants.InterReferenceCount, Av1TplModelConstants.InterReferenceCount);
                if (row >= frame.ModeInfoRows || column >= frame.ModeInfoColumns)
                {
                    interCosts[count] = long.MaxValue;
                    intraCosts[count] = long.MaxValue;
                    blockVectors.Fill(Av1TplBlockStatistics.InvalidMotionVector);
                    count++;
                    continue;
                }

                ref readonly Av1TplBlockStatistics block = ref statistics[Av1TplFrameStatistics.GetPosition(
                    row, column, frame.Stride, Av1TplModelConstants.BlockModeInfoLog2)];

                interCosts[count] = (long)block.InterCost << Av1TplModelConstants.DependencyCostScaleLog2;
                intraCosts[count] = (long)block.IntraCost << Av1TplModelConstants.DependencyCostScaleLog2;
                for (int reference = 0; reference < Av1TplModelConstants.InterReferenceCount; reference++)
                {
                    blockVectors[reference] = block.MotionVectors[reference];
                }

                blocksInside++;
                count++;
            }
        }

        return blocksInside;
    }

    /// <summary>
    /// Returns bounded exponentials, zero below -700 and the largest double above 700. Reference: exp_bounded().
    /// </summary>
    private static double ExpBounded(double value)
    {
        if (value > 700)
        {
            return double.MaxValue;
        }

        if (value < -700)
        {
            return 0;
        }

        return Math.Exp(value);
    }
}
