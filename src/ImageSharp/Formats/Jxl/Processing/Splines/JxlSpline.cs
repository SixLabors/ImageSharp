// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

internal sealed class JxlSpline : IDisposable
{
    /// <summary>
    /// The exclusive coordinate limit used by native spline reconstruction to keep geometry arithmetic bounded.
    /// </summary>
    public const int MaximumCoordinate = 1 << 23;

    private IMemoryOwner<PointF>? controlPoints;
    private InlineArray3<InlineArray32<float>> colorDct;
    private InlineArray32<float> sigmaDct;

    /// <summary>
    /// Gets the logical control points, without unused allocator capacity.
    /// </summary>
    public Memory<PointF> ControlPoints { get; private set; }

    /// <summary>
    /// Gets the three fixed groups of 32 color coefficients, stored with this spline.
    /// </summary>
    public Span<InlineArray32<float>> ColorDct => this.colorDct;

    /// <summary>
    /// Gets the 32 width coefficients, stored with this spline.
    /// </summary>
    public Span<float> SigmaDct => this.sigmaDct;

    /// <summary>
    /// Releases the control-point allocation and clears its logical view.
    /// </summary>
    public void ClearControlPoints()
    {
        this.controlPoints?.Dispose();
        this.controlPoints = null;

        this.ControlPoints = Memory<PointF>.Empty;
    }

    /// <summary>
    /// Selects the control-point count, reusing the current allocation when possible.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns the points.</param>
    /// <param name="count">The logical count. The caller writes all selected points before reading them.</param>
    public void ReserveControlPoints(Configuration configuration, int count)
    {
        if (this.controlPoints is null || this.controlPoints.Memory.Length < count)
        {
            // Reconstruction replaces every point. Growth requires no copy, and allocation failure retains the previous owner.
            IMemoryOwner<PointF> replacement = configuration.MemoryAllocator.Allocate<PointF>(count);
            this.controlPoints?.Dispose();
            this.controlPoints = replacement;
        }

        this.ControlPoints = this.controlPoints.Memory[..count];
    }

    /// <summary>
    /// Rejects spline coordinates or reconstructed deltas outside the native geometry limit.
    /// </summary>
    /// <param name="x">The horizontal coordinate or delta, before narrowing.</param>
    /// <param name="y">The vertical coordinate or delta, before narrowing.</param>
    public static void ValidatePosition(long x, long y)
    {
        // This is a reconstruction safety bound, not an image-edge clamp. Splines can extend outside the frame.
        if (x <= -MaximumCoordinate || x >= MaximumCoordinate || y <= -MaximumCoordinate || y >= MaximumCoordinate)
        {
            throw new InvalidImageContentException("JPEG XL spline coordinates exceed the supported range.");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.ClearControlPoints();
    }
}
