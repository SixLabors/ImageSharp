// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <content>
/// Holds the per-unit, per-frame and per-search state of the first pass.
/// </content>
internal sealed partial class Av1FirstPass<TSample, TOperator>
{
    /// <summary>
    /// The totals of one unit, or of a frame after accumulation. Errors are sums of squared differences with the intra and vector surcharges.
    /// Factors are sums of per-unit weights.
    /// </summary>
    private struct FrameStatistics
    {
        /// <summary>The intra prediction error.</summary>
        public long IntraError;

        /// <summary>The wavelet energy, or the invalid marker per unit when it is not measured.</summary>
        public long FrameAverageWaveletEnergy;

        /// <summary>The best of the intra error and the LAST error.</summary>
        public long CodedError;

        /// <summary>The best of the intra error and the GOLDEN error.</summary>
        public long SecondReferenceCodedError;

        /// <summary>The best of the intra error and the LAST2 error.</summary>
        public long LongTermCodedError;

        /// <summary>The number of units with a nonzero vector.</summary>
        public int MotionVectorCount;

        /// <summary>The number of units whose inter error does not exceed the intra error.</summary>
        public int InterCount;

        /// <summary>The number of units where GOLDEN beats LAST and intra.</summary>
        public int SecondReferenceCount;

        /// <summary>The weighted number of units whose intra and inter errors are close.</summary>
        public double NeutralCount;

        /// <summary>The number of units with a very small intra error.</summary>
        public int IntraSkipCount;

        /// <summary>The first unit row with image data, or <see cref="InvalidRow"/>.</summary>
        public int ImageDataStartRow;

        /// <summary>The number of nonzero vectors that differ from the preceding nonzero vector.</summary>
        public int NewMotionVectorCount;

        /// <summary>The balance of inward and outward vector components.</summary>
        public int SumInVectors;

        /// <summary>The sum of the row components in eighth samples.</summary>
        public int SumMotionVectorRow;

        /// <summary>The sum of the column components in eighth samples.</summary>
        public int SumMotionVectorColumn;

        /// <summary>The sum of the absolute row components.</summary>
        public int SumMotionVectorRowAbsolute;

        /// <summary>The sum of the absolute column components.</summary>
        public int SumMotionVectorColumnAbsolute;

        /// <summary>The sum of the squared row components.</summary>
        public long SumMotionVectorRowSquares;

        /// <summary>The sum of the squared column components.</summary>
        public long SumMotionVectorColumnSquares;

        /// <summary>The sum of the intra weights of the units.</summary>
        public double IntraFactor;

        /// <summary>The sum of the brightness weights of the units.</summary>
        public double BrightnessFactor;
    }

    /// <summary>
    /// The inclusive full-sample vector limits of a search.
    /// </summary>
    private struct FullMotionVectorLimits
    {
        /// <summary>The smallest column component.</summary>
        public int ColumnMinimum;

        /// <summary>The largest column component.</summary>
        public int ColumnMaximum;

        /// <summary>The smallest row component.</summary>
        public int RowMinimum;

        /// <summary>The largest row component.</summary>
        public int RowMaximum;
    }

    /// <summary>
    /// Borrows the planes and frame-level settings of the frame being measured.
    /// </summary>
    private ref struct FrameContext
    {
        /// <summary>Whether the frame is intra-only.</summary>
        public bool IntraOnly;

        /// <summary>The base-two logarithm of the unit size in 4x4 units.</summary>
        public int UnitLog2;

        /// <summary>The unit size.</summary>
        public Av1BlockSize FirstPassBlockSize;

        /// <summary>The number of unit rows of the unit records.</summary>
        public int UnitRows;

        /// <summary>The number of unit columns of the unit records, which is also their row stride.</summary>
        public int UnitColumns;

        /// <summary>The complete bordered source luma plane.</summary>
        public Span<TSample> Source;

        /// <summary>The source row stride.</summary>
        public int SourceStride;

        /// <summary>The index of the first coded source sample.</summary>
        public int SourceOrigin;

        /// <summary>The complete bordered luma plane of the previous source.</summary>
        public ReadOnlySpan<TSample> PreviousSource;

        /// <summary>The previous source row stride.</summary>
        public int PreviousSourceStride;

        /// <summary>The index of the first coded sample of the previous source.</summary>
        public int PreviousSourceOrigin;

        /// <summary>The complete bordered luma plane of the frame's reconstruction.</summary>
        public Span<TSample> Reconstruction;

        /// <summary>The row stride of the reconstruction and of every reference.</summary>
        public int ReconstructionStride;

        /// <summary>The index of the first coded sample of the reconstruction and of every reference.</summary>
        public int ReconstructionOrigin;

        /// <summary>The LAST reference.</summary>
        public ReadOnlySpan<TSample> Last;

        /// <summary>The GOLDEN reference.</summary>
        public ReadOnlySpan<TSample> Golden;

        /// <summary>The LAST2 reference.</summary>
        public ReadOnlySpan<TSample> Last2;

        /// <summary>The vector rate tables.</summary>
        public Av1MotionVectorCosts Costs;

        /// <summary>The first-pass search geometry.</summary>
        public Av1MotionSearchSites Sites;

        /// <summary>The rate multiplier from which the variance-domain vector cost is derived.</summary>
        public int RateMultiplier;

        /// <summary>The 16x16 variance above which a search continues with a mesh.</summary>
        public int MeshThreshold;
    }
}
