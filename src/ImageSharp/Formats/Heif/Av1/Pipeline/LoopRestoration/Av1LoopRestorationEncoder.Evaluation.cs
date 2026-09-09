// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <content>
/// Evaluates complete restoration units using the final stripe filtering path.
/// </content>
internal static partial class Av1LoopRestorationEncoder
{
    /// <summary>
    /// Refines transmitted Wiener taps in their defined axis and step order.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    /// <param name="context">The active plane and reusable filtering workspace.</param>
    /// <param name="bounds">The stripe-adjusted unit rectangle.</param>
    /// <param name="window">The fitted five- or seven-tap window.</param>
    /// <param name="refine">Whether to refine the initial fitted taps.</param>
    /// <param name="unit">The fitted taps, replaced by the refined result.</param>
    /// <returns>The final filtered unit error.</returns>
    private static long RefineWiener<TSample>(
        in UnitSearchContext<TSample> context,
        Rectangle bounds,
        int window,
        bool refine,
        ref Av1LoopRestorationUnit unit)
        where TSample : unmanaged
    {
        long bestError = context.MeasureCandidate(bounds, unit);
        if (!refine)
        {
            return bestError;
        }

        ReadOnlySpan<int> minimum = [-5, -23, -17];
        ReadOnlySpan<int> maximum = [10, 8, 46];
        for (int step = 4; step >= 1; step >>= 1)
        {
            for (int axis = 0; axis < 2; axis++)
            {
                for (int tap = (7 - window) >> 1; tap < 3; tap++)
                {
                    bool movedNegative = false;
                    while (true)
                    {
                        int value = axis == 0 ? unit.WienerHorizontal[tap] : unit.WienerVertical[tap];
                        if (value - step < minimum[tap])
                        {
                            break;
                        }

                        Av1LoopRestorationUnit candidate = unit;
                        if (axis == 0)
                        {
                            candidate.WienerHorizontal[tap] = value - step;
                        }
                        else
                        {
                            candidate.WienerVertical[tap] = value - step;
                        }

                        long error = context.MeasureCandidate(bounds, candidate);
                        if (error > bestError)
                        {
                            break;
                        }

                        unit = candidate;
                        bestError = error;
                        movedNegative = true;
                        if (step != 4)
                        {
                            break;
                        }
                    }

                    // An accepted negative step ends this axis's tap sweep. At the coarsest step,
                    // continue in that direction first; ties keep moving within the transmitted range.
                    if (movedNegative)
                    {
                        break;
                    }

                    while (true)
                    {
                        int value = axis == 0 ? unit.WienerHorizontal[tap] : unit.WienerVertical[tap];
                        if (value + step > maximum[tap])
                        {
                            break;
                        }

                        Av1LoopRestorationUnit candidate = unit;
                        if (axis == 0)
                        {
                            candidate.WienerHorizontal[tap] = value + step;
                        }
                        else
                        {
                            candidate.WienerVertical[tap] = value + step;
                        }

                        long error = context.MeasureCandidate(bounds, candidate);
                        if (error > bestError)
                        {
                            break;
                        }

                        unit = candidate;
                        bestError = error;
                        if (step != 4)
                        {
                            break;
                        }
                    }
                }
            }
        }

        return bestError;
    }

    /// <summary>
    /// Borrows one plane and its frame-scoped candidate workspace for restoration search.
    /// </summary>
    /// <typeparam name="TSample">The physical component sample type.</typeparam>
    private readonly ref struct UnitSearchContext<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// The original visible samples.
        /// </summary>
        public readonly Buffer2DRegion<TSample> Source;

        /// <summary>
        /// The unfiltered reconstruction with addressable borders.
        /// </summary>
        public readonly Buffer2DRegion<TSample> Reconstruction;

        /// <summary>
        /// The separate trial destination, reused by every candidate.
        /// </summary>
        public readonly Buffer2DRegion<TSample> Trial;

        /// <summary>
        /// The saved pre-CDEF internal rows and post-CDEF frame edges.
        /// </summary>
        public readonly Av1LoopRestorationBoundary Boundary;

        /// <summary>
        /// The plane index.
        /// </summary>
        public readonly int Plane;

        /// <summary>
        /// The significant component precision.
        /// </summary>
        public readonly int BitDepth;

        /// <summary>
        /// The horizontal chroma shift.
        /// </summary>
        public readonly int SubsamplingX;

        /// <summary>
        /// The vertical chroma shift.
        /// </summary>
        public readonly int SubsamplingY;

        /// <summary>
        /// The six temporary rows used while installing stripe context.
        /// </summary>
        public readonly Span<TSample> SavedRows;

        /// <summary>
        /// The two-pass Wiener convolution workspace.
        /// </summary>
        public readonly Span<ushort> WienerScratch;

        /// <summary>
        /// The self-guided processing-block workspace.
        /// </summary>
        public readonly Span<int> SelfGuidedScratch;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitSearchContext{TSample}"/> struct.
        /// </summary>
        public UnitSearchContext(
            Buffer2DRegion<TSample> source,
            Buffer2DRegion<TSample> reconstruction,
            Buffer2DRegion<TSample> trial,
            Av1LoopRestorationBoundary boundary,
            int plane,
            int bitDepth,
            int subsamplingX,
            int subsamplingY,
            Span<TSample> savedRows,
            Span<ushort> wienerScratch,
            Span<int> selfGuidedScratch)
        {
            this.Source = source;
            this.Reconstruction = reconstruction;
            this.Trial = trial;
            this.Boundary = boundary;
            this.Plane = plane;
            this.BitDepth = bitDepth;
            this.SubsamplingX = subsamplingX;
            this.SubsamplingY = subsamplingY;
            this.SavedRows = savedRows;
            this.WienerScratch = wienerScratch;
            this.SelfGuidedScratch = selfGuidedScratch;
        }

        /// <summary>
        /// Filters one unit with its preserved stripe context and measures the visible samples.
        /// </summary>
        /// <param name="bounds">The stripe-adjusted unit rectangle.</param>
        /// <param name="unit">The filter choice and transmitted coefficients.</param>
        /// <returns>The exact squared error against the original samples.</returns>
        public long MeasureCandidate(Rectangle bounds, Av1LoopRestorationUnit unit)
        {
            this.FilterUnit(bounds, unit);
            return this.MeasureError(bounds, this.Trial);
        }

        /// <summary>
        /// Filters a unit into the shared trial plane and restores its overwritten source context.
        /// </summary>
        /// <param name="bounds">The stripe-adjusted unit rectangle.</param>
        /// <param name="unit">The selected filter parameters.</param>
        public void FilterUnit(Rectangle bounds, Av1LoopRestorationUnit unit)
        {
            int sourceOrigin = (this.Reconstruction.Bounds.Y * this.Reconstruction.Stride) + this.Reconstruction.Bounds.X;
            int trialOrigin = (this.Trial.Bounds.Y * this.Trial.Stride) + this.Trial.Bounds.X;
            Av1LoopRestorationFilter.FilterUnit(
                this.Boundary,
                this.BitDepth,
                this.Plane,
                this.SubsamplingX,
                this.SubsamplingY,
                this.Reconstruction.Buffer.DangerousGetSingleSpan(),
                sourceOrigin,
                this.Reconstruction.Stride,
                this.Trial.Buffer.DangerousGetSingleSpan()[trialOrigin..],
                this.Trial.Stride,
                this.Reconstruction.Height,
                bounds.X,
                bounds.Width,
                bounds.Y,
                bounds.Bottom,
                unit,
                this.SavedRows,
                this.WienerScratch,
                this.SelfGuidedScratch);
        }

        /// <summary>
        /// Measures a unit in either the reconstruction or the filtered trial plane.
        /// </summary>
        /// <param name="bounds">The exact unit rectangle.</param>
        /// <param name="candidate">The plane containing the candidate samples.</param>
        /// <returns>The exact squared error at the stored sample precision.</returns>
        public long MeasureError(Rectangle bounds, Buffer2DRegion<TSample> candidate)
        {
            int sourceOffset = ((this.Source.Bounds.Y + bounds.Y) * this.Source.Stride) + this.Source.Bounds.X + bounds.X;
            int candidateOffset = ((candidate.Bounds.Y + bounds.Y) * candidate.Stride) + candidate.Bounds.X + bounds.X;
            ReadOnlySpan<TSample> source = this.Source.Buffer.DangerousGetSingleSpan()[sourceOffset..];
            ReadOnlySpan<TSample> samples = candidate.Buffer.DangerousGetSingleSpan()[candidateOffset..];

            // Sample storage is fixed by the enclosing closed generic frame path. Dispatch once per
            // unit to the existing vectorized error calculation; no sample conversion is required.
            if (typeof(TSample) == typeof(byte))
            {
                return Av1ResidualBuilder.SumSquaredError(
                    MemoryMarshal.Cast<TSample, byte>(source),
                    this.Source.Stride,
                    MemoryMarshal.Cast<TSample, byte>(samples),
                    candidate.Stride,
                    bounds.Width,
                    bounds.Height);
            }

            return Av1ResidualBuilder.SumSquaredError(
                MemoryMarshal.Cast<TSample, ushort>(source),
                this.Source.Stride,
                MemoryMarshal.Cast<TSample, ushort>(samples),
                candidate.Stride,
                bounds.Width,
                bounds.Height);
        }
    }
}
