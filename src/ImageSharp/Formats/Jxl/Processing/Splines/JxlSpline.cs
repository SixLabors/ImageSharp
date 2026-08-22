// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

internal sealed class JxlSpline : IDisposable
{
    private IMemoryOwner<PointF>? controlPoints;

    public Memory<PointF> ControlPoints { get; private set; }

    public JxlDct32[] ColorDct { get; set; } = [];

    public JxlDct32 SigmaDct { get; set; }

    public void ClearControlPoints()
    {
        this.controlPoints?.Dispose();
        this.controlPoints = null;

        this.ControlPoints = Memory<PointF>.Empty;
    }

    public void ReserveControlPoints(Configuration configuration, int n)
    {
        this.ClearControlPoints();

        this.controlPoints = configuration.MemoryAllocator.Allocate<PointF>(n);
        this.ControlPoints = this.controlPoints.Memory;
    }

    public void Dispose()
    {
        this.ClearControlPoints();
        GC.SuppressFinalize(this);
    }
}
