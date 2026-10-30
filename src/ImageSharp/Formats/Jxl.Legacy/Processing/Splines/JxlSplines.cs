// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

internal sealed class JxlSplines
{
    /// <summary>
    /// Owns decoded splines. A view supplied through SetData remains owned by its caller.
    /// </summary>
    private readonly List<JxlQuantizedSpline> splinesStorage = [];
    private readonly List<PointF> startingPointsStorage = [];
    private JxlSplineDataView data = new();
    private readonly List<JxlSplineSegment> segments = [];
    private IMemoryOwner<int>? segmentIndicesOwner;
    private IMemoryOwner<int>? segmentYStartOwner;
    private Memory<int> segmentIndices;
    private Memory<int> segmentYStart;

    public bool HasAny => this.data.HasAny;

    public Span<JxlQuantizedSpline> QuantizedSplines => CollectionsMarshal.AsSpan(this.data.Splines);

    public Span<PointF> StartingPoints => CollectionsMarshal.AsSpan(this.data.StartingPoints);

    public int QuantizationAdjustment { get; private set; }

    /// <summary>
    /// Releases decoded storage and borrows a caller-owned spline view.
    /// </summary>
    /// <param name="data">The spline data, which the caller keeps alive while this object uses it.</param>
    public void SetData(JxlSplineDataView data)
    {
        this.Clear();
        this.data = data;
    }

    /// <summary>
    /// Releases decoded splines and cached segment storage, then removes the active data view.
    /// </summary>
    public void Clear()
    {
        this.QuantizationAdjustment = 0;

        // Only this list owns its entries. SetData can borrow a different list without transferring ownership.
        foreach (JxlQuantizedSpline spline in this.splinesStorage)
        {
            spline.Dispose();
        }

        this.splinesStorage.Clear();
        this.startingPointsStorage.Clear();
        this.data = new();
        this.ClearDrawCache();
    }

    /// <summary>
    /// Reads spline positions, quantization, and control-point coefficients from the entropy stream.
    /// </summary>
    /// <param name="configuration">The configuration that owns entropy and control-point storage.</param>
    /// <param name="reader">The encoded spline fields.</param>
    /// <param name="numPixels">The image pixel count used to limit spline data.</param>
    public void Decode(Configuration configuration, JxlBitReader reader, int numPixels)
    {
        this.Clear();

        try
        {
            // Native spline limits bound both control-point storage and the amount of data per image pixel.
            const int maximumControlPoints = 1 << 20;
            const int maxControlPointsPerPixelRatio = 2;

            List<byte> contextMap = [];

            JxlAnsCode code = new();
            JxlAnsReader.DecodeHistograms(configuration, reader, (int)JxlSplineEntropyContext.NumSplineContexts, code, ref contextMap, false);

            // Spline fields use direct LZ77 distances; multiplier zero selects that native stream rule.
            using JxlAnsSymbolReader decoder = JxlAnsSymbolReader.Create(configuration, code, reader, 0);

            // One stream covers starting positions and every spline. Keep its borrowed window and map stable across those loops.
            Span<int> window = decoder.Lz77Window;
            ReadOnlySpan<byte> contexts = CollectionsMarshal.AsSpan(contextMap);
            uint encodedSplines = (uint)decoder.ReadHybridUInt((int)JxlSplineEntropyContext.NumSplines, reader, contexts, window);
            int maxControlPoints = Math.Min(maximumControlPoints, numPixels / maxControlPointsPerPixelRatio);

            // The wire count excludes the first spline. Reject before adding one or narrowing the UInt32 value.
            if (encodedSplines >= (uint)maxControlPoints)
            {
                throw new InvalidOperationException("Too many splines: " + encodedSplines);
            }

            int numSplines = (int)encodedSplines + 1;

            _ = this.startingPointsStorage.EnsureCapacity(numSplines);
            long lastX = 0;
            long lastY = 0;

            // The first point uses unsigned absolute coordinates. Later points store signed deltas from the previous start.
            // Keep integer positions until validation; every accepted coordinate is exactly representable as a float.
            for (int i = 0; i < numSplines; i++)
            {
                uint encodedX = (uint)decoder.ReadHybridUInt((int)JxlSplineEntropyContext.StartingPosition, reader, contexts, window);
                uint encodedY = (uint)decoder.ReadHybridUInt((int)JxlSplineEntropyContext.StartingPosition, reader, contexts, window);
                long x = i == 0 ? encodedX : lastX + JxlPackSigned.UnpackSigned(encodedX);
                long y = i == 0 ? encodedY : lastY + JxlPackSigned.UnpackSigned(encodedY);
                JxlSpline.ValidatePosition(x, y);
                this.startingPointsStorage.Add(new PointF(x, y));
                lastX = x;
                lastY = y;
            }

            int quantizationAdjustment = decoder.ReadHybridUInt((int)JxlSplineEntropyContext.QuantizationAdjustment, reader, contexts, window);
            this.QuantizationAdjustment = JxlPackSigned.UnpackSigned((uint)quantizationAdjustment);
            _ = this.splinesStorage.EnsureCapacity(numSplines);

            int numControlPoints = numSplines;

            // Attach each spline before decoding. Clear also releases the current allocation if that decode fails.
            for (int i = 0; i < numSplines; ++i)
            {
                JxlQuantizedSpline spline = new();
                this.splinesStorage.Add(spline);

                if (!spline.TryDecode(
                    configuration,
                    contexts,
                    decoder,
                    reader,
                    window,
                    maxControlPoints,
                    ref numControlPoints))
                {
                    throw new InvalidOperationException("Could not decode quantized spline. Index of the quantized spline: " + i);
                }
            }

            if (!decoder.CheckAnsFinalState())
            {
                throw new InvalidOperationException("Not ANS final state");
            }

            this.data = new JxlSplineDataView()
            {
                Splines = this.splinesStorage,
                StartingPoints = this.startingPointsStorage
            };

            if (!this.HasAny)
            {
                throw new InvalidOperationException("Decoded splines but got none");
            }
        }
        catch
        {
            // No decoded owner escapes a failed stream, including a later spline or the final ANS check.
            this.Clear();
            throw;
        }
    }

    /// <summary>
    /// Reconstructs the spline segments and builds a contiguous segment-index range for each image row.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns the row index and segment-index buffers.</param>
    /// <param name="imageXSize">The frame width in pixels.</param>
    /// <param name="imageYSize">The frame height in pixels.</param>
    /// <param name="colorCorrelation">The color correlation restored before spline rendering.</param>
    public void InitializeDrawCache(Configuration configuration, int imageXSize, int imageYSize, JxlColorCorrelation colorCorrelation)
    {
        this.ClearDrawCache();

        try
        {
            List<Range> segmentSpans = [];
            List<PointF> intermediatePoints = [];
            List<(PointF Point, float Multiplier)> pointsToDraw = [];
            using JxlSpline spline = new();
            ulong totalEstimatedAreaReached = 0;
            ulong imagePixelCount = (ulong)imageXSize * (ulong)imageYSize;

            // One reconstructed spline and its lists serve the whole frame. Segment construction retains values,
            // not references to this workspace, so the next spline can reuse its control-point allocation.
            for (int i = 0; i < this.data.Splines.Count; i++)
            {
                this.data.Splines[i].Dequantize(
                    configuration,
                    this.data.StartingPoints[i],
                    this.QuantizationAdjustment,
                    colorCorrelation.YToXRatio(0),
                    colorCorrelation.YToBRatio(0),
                    imagePixelCount,
                    ref totalEstimatedAreaReached,
                    spline);

                ReadOnlySpan<PointF> controlPoints = spline.ControlPoints.Span;

                // Equal neighboring points make the centripetal interpolation denominator zero.
                for (int point = 1; point < controlPoints.Length; point++)
                {
                    if (controlPoints[point - 1] == controlPoints[point])
                    {
                        throw new InvalidImageContentException("JPEG XL spline has identical neighboring control points.");
                    }
                }

                intermediatePoints.Clear();
                pointsToDraw.Clear();
                JxlSplineUtils.DrawCentripetalCatmullRomSpline(controlPoints, intermediatePoints);
                JxlSplineUtils.GetEquallySpacedPoints(CollectionsMarshal.AsSpan(intermediatePoints), pointsToDraw);

                // The first point carries unit weight. The final point carries the remaining distance, including zero at exact endpoints.
                float arcLength = ((pointsToDraw.Count - 2) * JxlSplineUtils.DesiredRenderingDistance) + pointsToDraw[^1].Multiplier;
                if (arcLength <= 0F)
                {
                    continue;
                }

                JxlSplineUtils.SegmentsFromPoints(imageYSize, spline, pointsToDraw, arcLength, this.segments, segmentSpans);
            }

            // Splines with no length or width contribute no segments, so drawing needs no row index.
            if (this.segments.Count == 0)
            {
                return;
            }

            // Signed differences record where segments enter and leave the image rows. All ranges use forward indices.
            // The frame header limits height to 2^30 + 18687, leaving room for two sentinel rows within Int32.
            this.segmentYStartOwner = configuration.MemoryAllocator.Allocate<int>(imageYSize + 2, AllocationOptions.Clean);
            this.segmentYStart = this.segmentYStartOwner.Memory[..(imageYSize + 2)];
            Span<int> rowStarts = this.segmentYStart.Span;
            Span<int> population = rowStarts[1..];
            long indexCount = 0;

            foreach (Range span in segmentSpans)
            {
                population[span.Start.Value]++;
                population[span.End.Value]--;
                indexCount += span.End.Value - span.Start.Value;
            }

            // Each interval length and the number of segments fit Int32, so their total fits Int64.
            // Validate the aggregate once before narrowing. Every subsequent row offset is at most this count.
            if (indexCount > int.MaxValue)
            {
                throw new InvalidImageContentException("JPEG XL spline row index exceeds the supported length.");
            }

            int total = 0;
            int coverage = 0;

            // Coverage is an active-segment count, bounded by segmentSpans.Count. The validated total bounds every insertion offset.
            for (int y = 0; y < imageYSize; y++)
            {
                coverage += population[y];
                population[y] = total;
                total += coverage;
            }

            this.segmentIndicesOwner = configuration.MemoryAllocator.Allocate<int>(total);
            this.segmentIndices = this.segmentIndicesOwner.Memory[..total];
            Span<int> indices = this.segmentIndices.Span;

            // Filling each row advances its insertion offset to the next row's start. Because population begins
            // one element after rowStarts, the finished rowStarts[y..y+2] is exactly that row's segment range.
            for (int i = 0; i < segmentSpans.Count; i++)
            {
                Range span = segmentSpans[i];

                for (int y = span.Start.Value; y < span.End.Value; y++)
                {
                    indices[population[y]++] = i;
                }
            }
        }
        catch
        {
            // No partial index is published after reconstruction, geometry, or allocation failure.
            this.ClearDrawCache();
            throw;
        }
    }

    /// <summary>
    /// Releases the owned row index and segment-index buffers, retaining reusable segment-list capacity.
    /// </summary>
    private void ClearDrawCache()
    {
        this.segments.Clear();
        this.segmentIndicesOwner?.Dispose();
        this.segmentYStartOwner?.Dispose();
        this.segmentIndicesOwner = null;
        this.segmentYStartOwner = null;
        this.segmentIndices = default;
        this.segmentYStart = default;
    }

    /// <summary>
    /// Adds the cached spline samples to an XYB frame region.
    /// </summary>
    /// <param name="opsin">The frame's XYB planes.</param>
    /// <param name="opsinRect">The image-coordinate region to modify.</param>
    public void AddTo(JxlImage3<float> opsin, Rectangle opsinRect) => this.Apply(true, opsin, opsinRect);

    /// <summary>
    /// Adds cached spline samples to one borrowed XYB row segment.
    /// </summary>
    /// <param name="rowX">The X samples beginning at x0.</param>
    /// <param name="rowY">The Y samples beginning at x0.</param>
    /// <param name="rowB">The B samples beginning at x0.</param>
    /// <param name="y">The image row index.</param>
    /// <param name="x0">The first image column, inclusive.</param>
    /// <param name="x1">The final image column, exclusive.</param>
    public void AddToRow(Memory<float> rowX, Memory<float> rowY, Memory<float> rowB, int y, int x0, int x1)
        => this.ApplyToRow(true, rowX, rowY, rowB, y, x0, x1, this.segmentIndices.Span, this.segmentYStart.Span);

    /// <summary>
    /// Removes the cached spline samples from a complete XYB frame.
    /// </summary>
    /// <param name="opsin">The XYB frame to modify.</param>
    public void SubtractFrom(JxlImage3<float> opsin) => this.Apply(false, opsin, opsin.GetRectangle());

    /// <summary>
    /// Applies the cached segment range to one borrowed color row.
    /// </summary>
    /// <param name="add">Whether to add or subtract the spline samples.</param>
    /// <param name="rowX">The X color row beginning at x0.</param>
    /// <param name="rowY">The Y color row beginning at x0.</param>
    /// <param name="rowB">The B color row beginning at x0.</param>
    /// <param name="y">The image row index.</param>
    /// <param name="x0">The first image column, inclusive.</param>
    /// <param name="x1">The final image column, exclusive.</param>
    /// <param name="indices">The borrowed segment indices for all rows.</param>
    /// <param name="rowStarts">The borrowed offsets delimiting each row's index range.</param>
    private void ApplyToRow(
        bool add,
        Memory<float> rowX,
        Memory<float> rowY,
        Memory<float> rowB,
        int y,
        int x0,
        int x1,
        ReadOnlySpan<int> indices,
        ReadOnlySpan<int> rowStarts)
    {
        if (this.segments.Count == 0)
        {
            return;
        }

        JxlSplineUtils.DrawSegments(
            rowX,
            rowY,
            rowB,
            y,
            x0,
            x1,
            add,
            CollectionsMarshal.AsSpan(this.segments),
            indices,
            rowStarts);
    }

    /// <summary>
    /// Applies the cached splines row by row while borrowing the index buffers once.
    /// </summary>
    /// <param name="add">Whether to add or subtract the spline samples.</param>
    /// <param name="opsin">The XYB planes to modify.</param>
    /// <param name="opsinRect">The image-coordinate region to process.</param>
    private void Apply(bool add, JxlImage3<float> opsin, Rectangle opsinRect)
    {
        if (this.segments.Count == 0)
        {
            return;
        }

        int y0 = RectangleUtils.Y0(in opsinRect);
        int x0 = RectangleUtils.X0(in opsinRect);
        int x1 = RectangleUtils.X1(in opsinRect);
        ReadOnlySpan<int> indices = this.segmentIndices.Span;
        ReadOnlySpan<int> rowStarts = this.segmentYStart.Span;

        for (int y = 0; y < opsinRect.Height; y++)
        {
            this.ApplyToRow(
                add,
                opsin.PlaneRowMemory(0, y0 + y)[x0..],
                opsin.PlaneRowMemory(1, y0 + y)[x0..],
                opsin.PlaneRowMemory(2, y0 + y)[x0..],
                y0 + y,
                x0,
                x1,
                indices,
                rowStarts);
        }
    }

}
