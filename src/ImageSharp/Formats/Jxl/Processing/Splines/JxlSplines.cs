// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

internal sealed class JxlSplines
{
    private List<JxlQuantizedSpline> splinesStorage = [];
    private readonly List<PointF> startingPointsStorage = [];
    private JxlSplineDataView data = new();
    private readonly List<JxlSplineSegment> segments = [];
    private IMemoryOwner<byte> segmentIndices = EmptyMemoryOwner.Instance;
    private IMemoryOwner<byte> segmentYStart = EmptyMemoryOwner.Instance;

    public bool HasAny => this.data.HasAny;

    public Span<JxlQuantizedSpline> QuantizedSplines => CollectionsMarshal.AsSpan(this.data.Splines);

    public Span<PointF> StartingPoints => CollectionsMarshal.AsSpan(this.data.StartingPoints);

    public int QuantizationAdjustment { get; private set; }

    public void SetData(JxlSplineDataView data)
    {
        this.Clear();
        this.data = data;
    }

    public void Clear()
    {
        this.QuantizationAdjustment = 0;
        this.splinesStorage.Clear();
        this.startingPointsStorage.Clear();
        this.data = new();
        this.segments.Clear();
        this.segmentIndices = EmptyMemoryOwner.Instance;
        this.segmentYStart = EmptyMemoryOwner.Instance;
    }

    public void Decode(Configuration configuration, JxlBitReader reader, int numPixels)
    {
        List<byte> contextMap = [];

        JxlAnsReader.DecodeHistograms(reader, NumSplineContexts, out JxlAnsCode code, contextMap);
        JxlAnsSymbolReader decoder = new(code, reader);

        int numSplines = decoder.ReadHybridUint(NumSplinesContext, reader, contextMap);
        int maxControlPoints = Math.Min(MaxNumControlPoints, numPixels / MaxNumControlPointsPerPixelRatio);

        if (numSplines > maxControlPoints || numSplines + 1 > maxControlPoints)
        {
            throw new InvalidOperationException("Too many splines: " + numSplines);
        }

        numSplines++;

        DecodeAllStartingPoints(this.startingPointsStorage, reader, decoder, contextMap, numSplines);

        this.QuantizationAdjustment = JxlPackSigned.UnpackSigned(decoder.ReadHybridUint(QuantizationAdjustmentContext, reader, contextMap));
        this.splinesStorage = new List<JxlQuantizedSpline>(numSplines);

        int numControlPoints = numSplines;

        for (int i = 0; i < numSplines; ++i)
        {
            JxlQuantizedSpline spline = new();
            if (!spline.TryDecode(
                configuration,
                CollectionsMarshal.AsSpan(contextMap),
                decoder,
                reader,
                maxControlPoints,
                ref numControlPoints))
            {
                throw new InvalidOperationException("Could not decode quantized spline. Index of the quantized spline: " + i);
            }

            this.splinesStorage.Add(spline);
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

    public void InitializeDrawCache(Configuration configuration, int imageXSize, int imageYSize, JxlColorCorrelation colorCorrelation)
    {
        this.segments.Clear();
        this.segmentIndices = EmptyMemoryOwner.Instance;
        this.segmentYStart = EmptyMemoryOwner.Instance;

        List<JxlSplineSegmentSpan> segmentsSpans = [];
        List<PointF> intermediatePoints = [];
        List<JxlSpline> splines = [];
        long totalEstimatedAreaReached = 0;

        for (int i = 0; i < this.data.Splines.Count; i++)
        {
            JxlSpline spline = new();

            if (!this.data.Splines[i].Dequantize(
                configuration,
                this.data.StartingPoints[i],
                this.QuantizationAdjustment,
                colorCorrelation.YToXRatio(0),
                colorCorrelation.YToBRatio(0),
                imageXSize * imageYSize,
                ref totalEstimatedAreaReached,
                spline))
            {
                throw new InvalidOperationException("Could not dequantize a quantized spline");
            }

            if (AdjacentFind(spline.ControlPoints.Span) != spline.ControlPoints.Length - 1)
            {
                throw new InvalidOperationException("Identical successive control points in spline " + i);
            }

            splines.Add(spline);
        }

#if JPEG_XL_THROW_ON_LARGE_SPLINE_AREA
        if (totalEstimatedAreaReached > Math.Min((8 * imageXSize * imageYSize) + (1 << 25), 1 << 30))
        {
            throw new InvalidOperationException("Total spline area is too large");
        }
#endif

        foreach (JxlSpline spline in splines)
        {
            List<(PointF Point, float Multiplier)> pointsToDraw = [];

            void AddPoint(PointF point, float multiplier) => pointsToDraw.Add((point, multiplier));

            intermediatePoints.Clear();

            JxlSplineUtils.DrawCentripetalCatmullRomSpline(spline.ControlPoints.Span, intermediatePoints);
            JxlSplineUtils.ForEachEquallySpacedPoint(CollectionsMarshal.AsSpan(intermediatePoints), AddPoint);

            float arcLength = ((pointsToDraw.Count - 2) * JxlSplineUtils.DesiredRenderingDistance) + pointsToDraw[^1].Multiplier;
            if (arcLength <= 0f)
            {
                // This spline wouldn't have any effect.
                continue;
            }

            JxlSplineUtils.SegmentsFromPoints(imageYSize, spline, pointsToDraw, arcLength, this.segments, segmentsSpans);
        }

        int segmentYStartNumBytes = (imageYSize + 2) * 4;
        this.segmentYStart = configuration.MemoryAllocator.Allocate<byte>(segmentYStartNumBytes);

        Span<byte> segmentYStart = this.segmentYStart.Memory.Span;
        segmentYStart.Clear();

        Span<byte> population = segmentYStart[1..];

        foreach (JxlSplineSegmentSpan segmentSpan in segmentsSpans)
        {
            population[segmentSpan.StartInclusive]++;
            population[segmentSpan.EndInclusive]--;
        }

        int total = 0;
        int coverage = 0;

        for (int y = 0; y < imageYSize; y++)
        {
            if (population[y] < 0)
            {
                if (coverage < -population[y])
                {
                    throw new InvalidOperationException("Coverage is invalid");
                }
            }

            coverage += population[y];
            population[y] = (byte)total;
            total += coverage;
        }

        this.segmentIndices = configuration.MemoryAllocator.Allocate<byte>(total * 4);
        Span<int> segmentIndices = MemoryMarshal.Cast<byte, int>(this.segmentIndices.Memory.Span);

        for (int i = 0; i < this.segments.Count; i++)
        {
            JxlSplineSegmentSpan segmentSpan = segmentsSpans[i];

            for (int y = segmentSpan.StartInclusive; y < segmentSpan.EndInclusive; y++)
            {
                segmentIndices[population[y]++] = i;
            }
        }
    }

    private static int AdjacentFind<T>(Span<T> span)
        where T : IEquatable<T>
    {
        for (int i = 0; i < span.Length - 1; i++)
        {
            if (span[i].Equals(span[i + 1]))
            {
                return i;
            }
        }

        return span.Length;
    }

    public void AddTo(JxlImage3F opsin, Rectangle opsinRect) => this.Apply(add: true, opsin, opsinRect);

    public void AddToRow(Memory<float> rowX, Memory<float> rowY, Memory<float> rowB, int y, int x0, int x1)
        => this.ApplyToRow(add: true, rowX, rowY, rowB, y, x0, x1);

    public void SubtractFrom(JxlImage3F opsin) => this.Apply(add: false, opsin, opsin.GetRectangle());

    private void ApplyToRow(bool add, Memory<float> rowX, Memory<float> rowY, Memory<float> rowB, int y, int x0, int x1)
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
            MemoryMarshal.Cast<byte, int>(this.segmentIndices.Memory.Span),
            MemoryMarshal.Cast<byte, int>(this.segmentYStart.Memory.Span));
    }

    private void Apply(bool add, JxlImage3F opsin, Rectangle opsinRect)
    {
        if (this.segments.Count == 0)
        {
            return;
        }

        int y0 = RectangleUtils.Y0(in opsinRect);
        int x0 = RectangleUtils.X0(in opsinRect);
        int x1 = RectangleUtils.X1(in opsinRect);

        for (int y = 0; y < opsinRect.Height; y++)
        {
            this.ApplyToRow(
                add,
                opsin.PlaneRowMemory(0, y0 + y)[x0..],
                opsin.PlaneRowMemory(1, y0 + y)[x0..],
                opsin.PlaneRowMemory(2, y0 + y)[x0..],
                y0 + y,
                x0,
                x1);
        }
    }

    private sealed class EmptyMemoryOwner : IMemoryOwner<byte>
    {
        public static readonly EmptyMemoryOwner Instance = new();

        public Memory<byte> Memory => Memory<byte>.Empty;

        public void Dispose()
        {
        }
    }
}
