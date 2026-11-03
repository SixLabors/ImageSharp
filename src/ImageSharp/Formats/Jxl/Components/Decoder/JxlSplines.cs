// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Owns parsed JPEG XL spline curves and adds them to restored native color planes.
/// </summary>
/// <remarks>
/// Spline samples are image reconstruction data. They precede color conversion and do not apply pixel bounds or alpha rules.
/// Parsing and reconstruction follow libjxl lib/jxl/splines.cc and jxl-rs jxl/src/features/spline.rs.
/// </remarks>
internal sealed partial class JxlSplines : IDisposable
{
    /// <summary>
    /// The six integer entropy contexts in libjxl splines.h SplineContexts.
    /// </summary>
    private const int ContextCount = 6;

    /// <summary>
    /// The entropy context for the signed frame-wide spline quantization adjustment.
    /// </summary>
    private const int QuantizationContext = 0;

    /// <summary>
    /// The entropy context for the delta-coded starting coordinates, in coding pixels.
    /// </summary>
    private const int StartingPositionContext = 1;

    /// <summary>
    /// The entropy context for the number of curves minus one.
    /// </summary>
    private const int SplineCountContext = 2;

    /// <summary>
    /// The entropy context for each curve's additional control-point count.
    /// </summary>
    private const int ControlPointCountContext = 3;

    /// <summary>
    /// The entropy context for signed second differences of control-point coordinates.
    /// </summary>
    private const int ControlPointContext = 4;

    /// <summary>
    /// The entropy context for the signed quantized DCT coefficients of X, Y, B and Gaussian width.
    /// </summary>
    private const int CoefficientContext = 5;

    /// <summary>
    /// The 32 continuous-DCT coefficients in each field, as defined by libjxl splines.h QuantizedSpline.
    /// </summary>
    private const int CoefficientsPerChannel = 32;

    /// <summary>
    /// The four consecutive 32-coefficient fields: X, Y, B and Gaussian width. The last field is not alpha.
    /// </summary>
    private const int CoefficientsPerSpline = 4 * CoefficientsPerChannel;

    /// <summary>
    /// The libjxl splines.cc frame control-point cap. The coded pixel count divided by two can lower this limit.
    /// </summary>
    private const int MaximumControlPoints = 1 << 20;

    /// <summary>
    /// The coding-coordinate bound, in pixels. The accepted interval is [-2^23, 2^23), matching jxl-rs.
    /// This reconstruction bound keeps one-pixel steps distinct in float arithmetic; it is not an encoded field width.
    /// </summary>
    private const int CoordinateLimit = 1 << 23;

    /// <summary>
    /// The sixteen Catmull-Rom samples per control interval in libjxl DrawCentripetalCatmullRomSpline.
    /// </summary>
    private const int InterpolationSteps = 16;

    /// <summary>
    /// The upper reconstruction work estimate from libjxl QuantizedSpline::Dequantize.
    /// The actual frame limit is min(1024 * outputPixels + 2^32, 2^42).
    /// </summary>
    private const ulong MaximumEstimatedArea = 1UL << 42;

    private readonly MemoryAllocator allocator;
    private readonly Size codingSize;
    private readonly ulong areaLimit;
    private readonly List<Curve> curves;
    private readonly int quantizationAdjustment;
    private readonly int maximumCurvePoints;

    /// <summary>
    /// Indicates whether the tracked curve buffers have been released.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlSplines"/> class with complete parsed curves.
    /// </summary>
    /// <param name="allocator">The allocator for the reusable interpolation workspace.</param>
    /// <param name="codingSize">The visible native color dimensions before common upsampling.</param>
    /// <param name="areaLimit">The bounded reconstruction work estimate in final-output pixel units.</param>
    /// <param name="curves">The complete curve buffers transferred from the parser.</param>
    /// <param name="quantizationAdjustment">The signed adjustment shared by every curve.</param>
    /// <param name="maximumCurvePoints">The largest actual curve, including its starting point.</param>
    private JxlSplines(
        MemoryAllocator allocator,
        Size codingSize,
        ulong areaLimit,
        List<Curve> curves,
        int quantizationAdjustment,
        int maximumCurvePoints)
    {
        this.allocator = allocator;
        this.codingSize = codingSize;
        this.areaLimit = areaLimit;
        this.curves = curves;
        this.quantizationAdjustment = quantizationAdjustment;
        this.maximumCurvePoints = maximumCurvePoints;
    }

    /// <summary>
    /// Reads the spline feature at the beginning of a frame's DC-global section.
    /// </summary>
    /// <param name="reader">The bounded reader after any patch dictionary and before noise and DC factors.</param>
    /// <param name="frame">The coding and final-output geometry used to bound reconstruction work.</param>
    /// <param name="allocator">The decoder's tracked allocator.</param>
    /// <param name="cancellationToken">The token checked during variable-length spline parsing.</param>
    /// <returns>The complete parsed owner. Failure releases all acquired buffers.</returns>
    public static JxlSplines Read(ref JxlBitReader reader, JxlFrameHeader frame, MemoryAllocator allocator, CancellationToken cancellationToken)
    {
        ulong codingPixels = (ulong)frame.CodingSize.Width * (uint)frame.CodingSize.Height;
        int maximumPoints = (int)Math.Min(MaximumControlPoints, codingPixels / 2);
        ulong outputPixels = (ulong)frame.OutputSize.Width * (uint)frame.OutputSize.Height;

        // Native spline work is limited to min(1024 * outputPixels + 2^32, 2^42). Cap the
        // multiplicand before scaling so an untrusted image size cannot wrap the estimate.
        ulong areaLimit = Math.Min(MaximumEstimatedArea, (1024 * Math.Min(outputPixels, MaximumEstimatedArea / 1024)) + (1UL << 32));
        using JxlEntropyDecoder codes = JxlEntropyDecoder.Read(ref reader, allocator, ContextCount, true);

        // The full stream has at most 2 + 131 * maximumPoints integers: two starting coordinates,
        // a count and 128 coefficients per curve, plus two deltas per remaining control point.
        // Byte length cannot replace this bound because singleton tables and LZ77 can expand short input.
        int maximumValues = 2 + (131 * maximumPoints);
        int historyLength = codes.UsesLz77
            ? (int)BitOperations.RoundUpToPowerOf2((uint)Math.Min(maximumValues, JxlEntropyDecoder.MaximumLz77Distance))
            : 0;

        using IMemoryOwner<uint>? historyOwner = historyLength == 0 ? null : allocator.Allocate<uint>(historyLength);
        Span<uint> history = historyOwner is null ? [] : historyOwner.Memory.Span[..historyLength];
        if (codes.UsesPrefixCodes)
        {
            return codes.UsesLz77
                ? ReadCurves<JxlEntropyDecoder.PrefixLz77Operator>(
                    ref reader, codes, history, frame.CodingSize, areaLimit, maximumPoints, allocator, cancellationToken)
                : ReadCurves<JxlEntropyDecoder.PrefixOperator>(
                    ref reader, codes, history, frame.CodingSize, areaLimit, maximumPoints, allocator, cancellationToken);
        }

        return codes.UsesLz77
            ? ReadCurves<JxlEntropyDecoder.AnsLz77Operator>(
                    ref reader, codes, history, frame.CodingSize, areaLimit, maximumPoints, allocator, cancellationToken)
            : ReadCurves<JxlEntropyDecoder.AnsOperator>(
                    ref reader, codes, history, frame.CodingSize, areaLimit, maximumPoints, allocator, cancellationToken);
    }

    /// <summary>
    /// Adds complete curves after restoration and before color upsampling and color conversion.
    /// </summary>
    /// <param name="colors">Three distinct native color planes with a completed coding-size rectangle.</param>
    /// <param name="baseCorrelation">The Y-to-X and Y-to-B base color correlations.</param>
    /// <param name="cancellationToken">The token checked between curves and painted rows.</param>
    public void Apply(ReadOnlySpan<Buffer2D<float>> colors, Vector2 baseCorrelation, CancellationToken cancellationToken)
    {
        int interpolationLength = ((this.maximumCurvePoints - 1) * InterpolationSteps) + 1;
        using IMemoryOwner<Vector2> interpolationOwner = this.allocator.Allocate<Vector2>(interpolationLength);
        Span<Vector2> interpolation = interpolationOwner.Memory.Span[..interpolationLength];
        InlineArray128<float> preparedStorage = default;
        Span<float> prepared = preparedStorage;
        float inverseQuantization = this.quantizationAdjustment >= 0
            ? 1F / (1F + (.125F * this.quantizationAdjustment))
            : 1F - (.125F * this.quantizationAdjustment);

        ulong totalEstimatedArea = 0;
        foreach (Curve curve in this.curves)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<float> values = curve.Memory.Span;
            ReadOnlySpan<Vector2> controls = MemoryMarshal.Cast<float, Vector2>(values[..(curve.PointCount * 2)]);
            ReadOnlySpan<float> coefficients = values.Slice(curve.PointCount * 2, CoefficientsPerSpline);
            PrepareCoefficients(coefficients, prepared, inverseQuantization, baseCorrelation);
            this.ValidateEstimatedArea(coefficients, inverseQuantization, baseCorrelation, curve.ManhattanDistance, ref totalEstimatedArea);

            // Each control interval produces exactly sixteen Catmull-Rom samples. Reuse the largest
            // actual curve's workspace, rather than allocating the image-wide maximum or renting per point.
            int length = ((controls.Length - 1) * InterpolationSteps) + 1;
            Span<Vector2> points = interpolation[..length];
            Interpolate(controls, points, cancellationToken);
            this.DrawCurve(colors, points, prepared, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        foreach (Curve curve in this.curves)
        {
            curve.Owner.Dispose();
        }

        this.isDisposed = true;
    }

    /// <summary>
    /// Reads complete curves with a fixed entropy mode and one history shared by all six contexts.
    /// </summary>
    /// <typeparam name="TOperator">The selected ANS or prefix mode and LZ77 behavior.</typeparam>
    /// <param name="reader">The bounded spline reader.</param>
    /// <param name="codes">The complete spline entropy tables.</param>
    /// <param name="history">The bounded power-of-two history, empty without LZ77.</param>
    /// <param name="codingSize">The coding-size raster rectangle.</param>
    /// <param name="areaLimit">The maximum native reconstruction work estimate.</param>
    /// <param name="maximumPoints">The image-derived total control-point limit.</param>
    /// <param name="allocator">The tracked allocator for curve storage.</param>
    /// <param name="cancellationToken">The token checked between parsed points and curves.</param>
    /// <returns>The complete parsed spline owner.</returns>
    private static JxlSplines ReadCurves<TOperator>(
        ref JxlBitReader reader,
        JxlEntropyDecoder codes,
        Span<uint> history,
        Size codingSize,
        ulong areaLimit,
        int maximumPoints,
        MemoryAllocator allocator,
        CancellationToken cancellationToken)
        where TOperator : struct, JxlEntropyDecoder.IEntropyOperator
    {
        JxlEntropyDecoder.Reader symbols = codes.CreateReader(ref reader);
        uint decodedCount = 0;
        uint encodedCount = ReadValue<TOperator>(ref reader, ref symbols, SplineCountContext, history, ref decodedCount);
        if (encodedCount >= maximumPoints)
        {
            throw new InvalidImageContentException("The JPEG XL spline count exceeds the control-point limit.");
        }

        int count = (int)encodedCount + 1;
        using IMemoryOwner<Vector2> startsOwner = allocator.Allocate<Vector2>(count);
        Span<Vector2> starts = startsOwner.Memory.Span[..count];
        long previousX = 0;
        long previousY = 0;
        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            uint x = ReadValue<TOperator>(ref reader, ref symbols, StartingPositionContext, history, ref decodedCount);
            uint y = ReadValue<TOperator>(ref reader, ref symbols, StartingPositionContext, history, ref decodedCount);
            long currentX = i == 0 ? x : previousX + UnpackSigned(x);
            long currentY = i == 0 ? y : previousY + UnpackSigned(y);
            ValidateCoordinates(currentX, currentY);
            starts[i] = new Vector2(currentX, currentY);
            previousX = currentX;
            previousY = currentY;
        }

        int adjustment = UnpackSigned(ReadValue<TOperator>(ref reader, ref symbols, QuantizationContext, history, ref decodedCount));
        List<Curve> curves = new(count);
        int totalPoints = count;
        int maximumCurvePoints = 1;
        try
        {
            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                uint additional = ReadValue<TOperator>(ref reader, ref symbols, ControlPointCountContext, history, ref decodedCount);
                if (additional > maximumPoints - totalPoints)
                {
                    throw new InvalidImageContentException("The JPEG XL spline control-point count exceeds its limit.");
                }

                totalPoints += (int)additional;
                int pointCount = (int)additional + 1;

                // Counts are interleaved with each curve's coordinates and DCT values. Acquire one exact
                // tracked block at that wire boundary. The fixed-capacity list cannot grow during parsing.
                IMemoryOwner<float> owner = allocator.Allocate<float>((2 * pointCount) + CoefficientsPerSpline);
                Curve curve = new(owner, pointCount, 0);
                curves.Add(curve);
                Span<float> values = curve.Memory.Span;
                Span<Vector2> points = MemoryMarshal.Cast<float, Vector2>(values[..(2 * pointCount)]);
                points[0] = starts[i];
                long currentX = (int)starts[i].X;
                long currentY = (int)starts[i].Y;
                long deltaX = 0;
                long deltaY = 0;
                ulong manhattanDistance = 0;
                for (int point = 1; point < pointCount; point++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // The signed double deltas enter long accumulators before validation. Once both
                    // consecutive first deltas fit the coordinate range, their difference also fits 25 bits;
                    // a separate native 30-bit double-delta guard would therefore reject no additional valid input.
                    deltaX += UnpackSigned(ReadValue<TOperator>(ref reader, ref symbols, ControlPointContext, history, ref decodedCount));
                    deltaY += UnpackSigned(ReadValue<TOperator>(ref reader, ref symbols, ControlPointContext, history, ref decodedCount));
                    ValidateCoordinates(deltaX, deltaY);
                    manhattanDistance += (ulong)(Math.Abs(deltaX) + Math.Abs(deltaY));
                    if (manhattanDistance > areaLimit)
                    {
                        throw new InvalidImageContentException("The JPEG XL spline path exceeds the reconstruction work limit.");
                    }

                    currentX += deltaX;
                    currentY += deltaY;
                    ValidateCoordinates(currentX, currentY);
                    points[point] = new Vector2(currentX, currentY);
                    if (points[point] == points[point - 1])
                    {
                        throw new InvalidImageContentException("The JPEG XL spline has coincident adjacent control points.");
                    }
                }

                Span<float> coefficients = values.Slice(2 * pointCount, CoefficientsPerSpline);
                for (int coefficient = 0; coefficient < coefficients.Length; coefficient++)
                {
                    // Native dequantization first converts each integer to float before multiplying.
                    // Retain that same value here. MathF.Abs also handles int.MinValue's exact float
                    // representation, so the native integer-abs overflow restriction is unnecessary.
                    coefficients[coefficient] = UnpackSigned(ReadValue<TOperator>(
                        ref reader, ref symbols, CoefficientContext, history, ref decodedCount));
                }

                curves[i] = new Curve(owner, pointCount, manhattanDistance);
                maximumCurvePoints = Math.Max(maximumCurvePoints, pointCount);
            }

            symbols.CheckFinalState();
            return new JxlSplines(allocator, codingSize, areaLimit, curves, adjustment, maximumCurvePoints);
        }
        catch
        {
            foreach (Curve curve in curves)
            {
                curve.Owner.Dispose();
            }

            throw;
        }
    }

    /// <summary>
    /// Reads one residual and advances the common history position across every spline field.
    /// </summary>
    /// <typeparam name="TOperator">The entropy mode selected before traversal.</typeparam>
    /// <param name="reader">The bounded spline bit reader.</param>
    /// <param name="symbols">The current symbol and copy state.</param>
    /// <param name="context">The spline syntax context.</param>
    /// <param name="history">The common residual history.</param>
    /// <param name="decodedCount">The number of preceding residuals in the stream.</param>
    /// <returns>The decoded unsigned residual.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadValue<TOperator>(
        ref JxlBitReader reader, ref JxlEntropyDecoder.Reader symbols, int context, Span<uint> history, ref uint decodedCount)
        where TOperator : struct, JxlEntropyDecoder.IEntropyOperator
        => symbols.ReadUInt32<TOperator>(ref reader, context, history, decodedCount++, 0);

    /// <summary>
    /// Restores the spline syntax's signed integer from its unsigned zigzag encoding.
    /// </summary>
    /// <param name="value">The decoded unsigned residual.</param>
    /// <returns>The signed value, including int.MinValue.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int UnpackSigned(uint value) => (int)(value >> 1) ^ -(int)(value & 1);

    /// <summary>
    /// Keeps coordinates and first deltas in the shared native reconstruction range.
    /// </summary>
    /// <param name="x">The widened horizontal coordinate or first delta.</param>
    /// <param name="y">The widened vertical coordinate or first delta.</param>
    /// <remarks>
    /// This is an implementation precision bound, not a serialized field width. Both references limit
    /// coordinates near 2^23: the one-pixel float resampling step must still advance at these magnitudes.
    /// Widened accumulation removes the native overflow concern; the lower endpoint follows Rust's inclusive bound.
    /// </remarks>
    private static void ValidateCoordinates(long x, long y)
    {
        if (x < -CoordinateLimit || x >= CoordinateLimit || y < -CoordinateLimit || y >= CoordinateLimit)
        {
            throw new InvalidImageContentException("The JPEG XL spline coordinate exceeds the supported reconstruction range.");
        }
    }

    /// <summary>
    /// Describes one exact tracked block of absolute control points followed by four quantized DCT rows.
    /// </summary>
    private readonly struct Curve
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Curve"/> struct.
        /// </summary>
        /// <param name="owner">The tracked float block transferred to the complete spline owner.</param>
        /// <param name="pointCount">The control-point count, including the first point.</param>
        /// <param name="manhattanDistance">The sum of the absolute first deltas.</param>
        public Curve(IMemoryOwner<float> owner, int pointCount, ulong manhattanDistance)
        {
            this.Owner = owner;
            this.Memory = owner.Memory[..((2 * pointCount) + CoefficientsPerSpline)];
            this.PointCount = pointCount;
            this.ManhattanDistance = manhattanDistance;
        }

        /// <summary>
        /// Gets the tracked block released by JxlSplines.
        /// </summary>
        public IMemoryOwner<float> Owner { get; }

        /// <summary>
        /// Gets the exact block view without another owner memory lookup.
        /// </summary>
        public Memory<float> Memory { get; }

        /// <summary>
        /// Gets the number of absolute control points at the beginning of the block.
        /// </summary>
        public int PointCount { get; }

        /// <summary>
        /// Gets the bounded first-delta distance used for the rendering work estimate.
        /// </summary>
        public ulong ManhattanDistance { get; }
    }
}
