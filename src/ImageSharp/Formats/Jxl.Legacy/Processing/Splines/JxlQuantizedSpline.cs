// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

internal sealed class JxlQuantizedSpline : IDisposable
{
    /// <summary>
    /// Owns the reusable control-point allocation. ControlPoints exposes only its current logical range.
    /// </summary>
    private IMemoryOwner<JxlControlPoint>? memoryOwner;

    public JxlQuantizedSpline()
    {
        for (int i = 0; i < 3; i++)
        {
            this.ColorDct[i] = new int[32];
        }
    }

    private static ReadOnlySpan<int> Loops => [1, 0, 2];

    private static ReadOnlySpan<float> ChannelWeight => [0.0042f, 0.075f, 0.07f, .3333f];

    /// <summary>
    /// Gets the double-delta control points after the separately encoded starting point.
    /// </summary>
    public Memory<JxlControlPoint> ControlPoints { get; private set; }

    // NOTE: Do not use Configuration.MemoryAllocator.Allocate2D. This is
    // a 3x32 array, and renting memory introduces too much overhead for
    // 384 bytes of memory.
    // Additionally, prefer jagged arrays instead of multidimensional arrays
    // for performance.
    public int[][] ColorDct { get; set; } = new int[3][];

    public int[] SigmaDct { get; set; } = new int[32];

    /// <summary>
    /// Releases control-point storage and clears the borrowed view. Repeated disposal has no effect.
    /// </summary>
    public void Dispose()
    {
        this.memoryOwner?.Dispose();
        this.memoryOwner = null;
        this.ControlPoints = Memory<JxlControlPoint>.Empty;
    }

    /// <summary>
    /// Selects the control-point count and reuses the allocation when its capacity is sufficient.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns the control points.</param>
    /// <param name="count">The validated logical count. The caller writes every selected entry before reading it.</param>
    private void ResizeControlPoints(Configuration configuration, int count)
    {
        if (this.memoryOwner is null || this.memoryOwner.Memory.Length < count)
        {
            // Both callers replace all points, so growth needs no copy. Keep the current owner alive if allocation fails.
            IMemoryOwner<JxlControlPoint> replacement = configuration.MemoryAllocator.Allocate<JxlControlPoint>(count);
            this.memoryOwner?.Dispose();
            this.memoryOwner = replacement;
        }

        // Allocators can supply additional capacity. It is not encoded data and must not become a control point.
        this.ControlPoints = this.memoryOwner.Memory[..count];
    }

    /// <summary>
    /// Quantizes a spline and stores its control-point double deltas in one owned allocation.
    /// </summary>
    /// <param name="configuration">The configuration whose allocator owns the encoded control points.</param>
    /// <param name="original">The spline with at least one point. Its starting point is encoded separately.</param>
    /// <param name="quantizationAdjustment">The spline coefficient quantization adjustment.</param>
    /// <param name="yToX">The Y contribution removed from the X channel.</param>
    /// <param name="yToB">The Y contribution removed from the B channel.</param>
    /// <returns>The quantized spline, which the caller owns and must dispose.</returns>
    public static JxlQuantizedSpline Create(Configuration configuration, JxlSpline original, int quantizationAdjustment, float yToX, float yToB)
    {
        JxlQuantizedSpline spline = new();

        try
        {
            Span<PointF> controlPoints = original.ControlPoints.Span;

            spline.ResizeControlPoints(configuration, controlPoints.Length - 1);

            PointF startingPoint = controlPoints[0];

            // The native wire quantization rounds halfway coordinates away from zero.
            int previousX = (int)MathF.Round(startingPoint.X, MidpointRounding.AwayFromZero);
            int previousY = (int)MathF.Round(startingPoint.Y, MidpointRounding.AwayFromZero);
            int previousDx = 0; // D stands for delta
            int previousDy = 0; // D stands for delta

            // The starting point is stored separately. Encode exactly one double delta for each following point
            // into the allocation owned by the returned spline.
            Span<JxlControlPoint> controlsSpan = spline.ControlPoints.Span;

            for (int i = 0; i < controlsSpan.Length; i++)
            {
                PointF controlPoint = controlPoints[i + 1];

                int newX = (int)MathF.Round(controlPoint.X, MidpointRounding.AwayFromZero);
                int newY = (int)MathF.Round(controlPoint.Y, MidpointRounding.AwayFromZero);
                int newDx = newX - previousX;   // D stands for delta
                int newDy = newY - previousY;   // D stands for delta

                controlsSpan[i] = new(newDx - previousDx, newDy - previousDy);

                previousDx = newDx;
                previousDy = newDy;
                previousX = newX;
                previousY = newY;
            }

            float quant = AdjustedQuant(quantizationAdjustment);
            float inverseQuant = InverseAdjustedQuant(quantizationAdjustment);

            // Quantize Y first. X and B remove the Y contribution restored from those quantized coefficients.
            for (int j = 0; j < 3; j++)
            {
                int c = Loops[j];

                float factor = (c == 0) ? yToX : (c == 1) ? 0 : yToB;

                // The DC coefficient uses the square-root-of-two scale; the other DCT coefficients use a unit scale.
                for (int i = 0; i < 32; i++)
                {
                    float dctFactor = (i == 0) ? JxlDctScales.Sqrt2 : 1.0f;
                    float inverseDctFactor = (i == 0) ? JxlDctScales.Sqrt05 : 1.0f;
                    float restoredY = spline.ColorDct[1][i] * inverseDctFactor * ChannelWeight[1] * inverseQuant;
                    float decorrelated = original.ColorDct[c][i] - (factor * restoredY);
                    spline.ColorDct[c][i] = ConvertToInteger(decorrelated * dctFactor * quant / ChannelWeight[c]);
                }
            }

            for (int i = 0; i < 32; i++)
            {
                float dctFactor = (i == 0) ? JxlDctScales.Sqrt2 : 1.0f;
                spline.SigmaDct[i] = ConvertToInteger(original.SigmaDct[i] * dctFactor * quant / ChannelWeight[3]);
            }

            return spline;
        }
        catch
        {
            // Creation transfers ownership only when the completed spline is returned.
            spline.Dispose();
            throw;
        }

        // Match the native coefficient bound and halfway rounding before storing the integer code.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int ConvertToInteger(float value)
        {
            const float max = int.MaxValue - 127f;
            const float min = -max;

            return (int)MathF.Round(Math.Clamp(value, min, max), MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>
    /// Reconstructs control points and coefficients while limiting the estimated spline rendering work.
    /// </summary>
    /// <param name="configuration">The configuration that owns the reconstructed control points.</param>
    /// <param name="startingPoint">The separately decoded first point.</param>
    /// <param name="quantizationAdjustment">The encoded coefficient quantization adjustment.</param>
    /// <param name="yToX">The Y contribution restored to X.</param>
    /// <param name="yToB">The Y contribution restored to B.</param>
    /// <param name="imagePixelCount">The frame area in pixels, used only for the rendering-work limit.</param>
    /// <param name="totalEstimatedAreaReached">The accumulated work estimate for all splines in this frame.</param>
    /// <param name="result">The caller-owned spline whose existing point storage can be reused.</param>
    /// <returns>True after reconstruction. Invalid geometry or excessive work throws an image-content exception.</returns>
    public bool Dequantize(
        Configuration configuration,
        PointF startingPoint,
        int quantizationAdjustment,
        float yToX,
        float yToB,
        ulong imagePixelCount,
        ref ulong totalEstimatedAreaReached,
        JxlSpline result)
    {
        const ulong initialAreaAllowance = 1UL << 32;
        const ulong maximumAreaAllowance = 1UL << 42;
        const ulong areaPerImagePixel = 1024;

        // Native reconstruction permits 1024 * image pixels + 2^32 units, capped at 2^42.
        // Cap the pixel count before multiplication so even a large frame cannot wrap the work limit.
        ulong cappedPixelCount = Math.Min(imagePixelCount, (maximumAreaAllowance - initialAreaAllowance) / areaPerImagePixel);
        ulong areaLimit = (areaPerImagePixel * cappedPixelCount) + initialAreaAllowance;

        // Decoded starts are finite integer positions within +/-2^23. Rounding also matches the encoder's stored starting-point rule.
        long currentX = (long)MathF.Round(startingPoint.X, MidpointRounding.AwayFromZero);
        long currentY = (long)MathF.Round(startingPoint.Y, MidpointRounding.AwayFromZero);
        JxlSpline.ValidatePosition(currentX, currentY);

        // The parser limits the whole frame to 2^20 control points, including each starting point.
        result.ReserveControlPoints(configuration, this.ControlPoints.Length + 1);
        Span<PointF> controlPoints = result.ControlPoints.Span;
        ReadOnlySpan<JxlControlPoint> deltas = this.ControlPoints.Span;
        controlPoints[0] = new(currentX, currentY);

        long deltaX = 0;
        long deltaY = 0;
        ulong manhattanDistance = 0;

        for (int i = 0; i < deltas.Length; i++)
        {
            // Each accepted coordinate and reconstructed delta has magnitude below 2^23.
            // Adding one encoded Int32 delta in Int64 cannot overflow before the next geometry check.
            deltaX += deltas[i].First;
            deltaY += deltas[i].Second;
            JxlSpline.ValidatePosition(deltaX, deltaY);
            manhattanDistance += (ulong)(Math.Abs(deltaX) + Math.Abs(deltaY));

            if (manhattanDistance > areaLimit)
            {
                throw new InvalidImageContentException("JPEG XL spline length exceeds the rendering limit.");
            }

            currentX += deltaX;
            currentY += deltaY;
            JxlSpline.ValidatePosition(currentX, currentY);
            controlPoints[i + 1] = new(currentX, currentY);
        }

        float inverseQuant = InverseAdjustedQuant(quantizationAdjustment);
        Span<InlineArray32<float>> colorDct = result.ColorDct;

        for (int channel = 0; channel < 3; channel++)
        {
            for (int i = 0; i < 32; i++)
            {
                float inverseDctFactor = i == 0 ? JxlDctScales.Sqrt05 : 1F;
                colorDct[channel][i] = this.ColorDct[channel][i] * inverseDctFactor * ChannelWeight[channel] * inverseQuant;
            }
        }

        for (int i = 0; i < 32; i++)
        {
            colorDct[0][i] += yToX * colorDct[1][i];
            colorDct[2][i] += yToB * colorDct[1][i];
        }

        ulong logColor;

        try
        {
            // A coefficient contributes at most 2^31 * 2^28 = 2^59 units. That conversion fits UInt64,
            // but summing 32 contributions can reach 2^64. Check only these work-estimate sums and their correlation products.
            InlineArray3<ulong> colorStorage = default;
            Span<ulong> color = colorStorage;

            for (int channel = 0; channel < 3; channel++)
            {
                for (int i = 0; i < 32; i++)
                {
                    ulong contribution = (ulong)MathF.Ceiling(inverseQuant * MathF.Abs(this.ColorDct[channel][i]));
                    color[channel] = checked(color[channel] + contribution);
                }
            }

            // Decoded DC correlation factors have magnitude at most four. Their casts fit; scaling the channel sums can overflow.
            ulong xCorrelation = (ulong)MathF.Ceiling(MathF.Abs(yToX));
            ulong bCorrelation = (ulong)MathF.Ceiling(MathF.Abs(yToB));
            color[0] = checked(color[0] + (xCorrelation * color[1]));
            color[2] = checked(color[2] + (bCorrelation * color[1]));

            ulong maximumColor = Math.Max(color[1], Math.Max(color[0], color[2]));
            logColor = Math.Max(1UL, JxlMath.CeilLog2Nonzero(checked(1UL + maximumColor)));
        }
        catch (OverflowException exception)
        {
            throw new InvalidImageContentException("JPEG XL spline work estimate exceeds the supported range.", exception);
        }

        // Width squared, color magnitude and path length jointly estimate the rendering area.
        // Both divisors belong inside the square root, as in the native work estimate.
        float weightLimit = MathF.Ceiling(MathF.Sqrt(((float)areaLimit / logColor) / Math.Max(1UL, manhattanDistance)));
        Span<float> sigmaDct = result.SigmaDct;
        ulong widthEstimate = 0;

        for (int i = 0; i < 32; i++)
        {
            float inverseDctFactor = i == 0 ? JxlDctScales.Sqrt05 : 1F;
            sigmaDct[i] = this.SigmaDct[i] * inverseDctFactor * ChannelWeight[3] * inverseQuant;
            float weightF = MathF.Ceiling(inverseQuant * MathF.Abs(this.SigmaDct[i]));
            ulong weight = (ulong)MathF.Min(weightLimit, MathF.Max(1F, weightF));
            widthEstimate += weight * weight * logColor;
        }

        // Let A be the area limit, D the path length and L the color logarithm: A <= 2^42, D <= A, L <= 64.
        // Including float rounding, W <= 2*sqrt(A/(L*max(1,D)))+1 bounds all 32 width terms and their final area below 2^54.
        // These products fit UInt64. A zero-length path contributes no work.
        ulong estimatedArea = widthEstimate * manhattanDistance;
        if (estimatedArea > areaLimit - totalEstimatedAreaReached)
        {
            throw new InvalidImageContentException("JPEG XL splines exceed the rendering-work limit.");
        }

        totalEstimatedAreaReached += estimatedArea;
        return true;
    }

    /// <summary>
    /// Reads one spline's control-point deltas and DCT coefficients.
    /// </summary>
    /// <param name="configuration">The configuration that owns control-point storage.</param>
    /// <param name="contextMap">The stable map from spline contexts to histograms.</param>
    /// <param name="decoder">The entropy reader shared by all splines in the stream.</param>
    /// <param name="br">The encoded spline fields.</param>
    /// <param name="window">The decoder's LZ77 window, borrowed by the owning spline stream.</param>
    /// <param name="maxControlPoints">The maximum permitted total control-point count.</param>
    /// <param name="totalControlPoints">The accumulated control-point count.</param>
    /// <returns>Whether all coefficient groups were decoded.</returns>
    public bool TryDecode(
        Configuration configuration,
        ReadOnlySpan<byte> contextMap,
        JxlAnsSymbolReader decoder,
        JxlBitReader br,
        Span<int> window,
        int maxControlPoints,
        ref int totalControlPoints)
    {
        uint encodedControlPoints = (uint)decoder.ReadHybridUInt((int)JxlSplineEntropyContext.NumControlPoints, br, contextMap, window);

        // The caller already counted each spline's starting point. Check the remaining quota before adding
        // this unsigned count or allocating storage. Reaching the quota exactly is valid.
        if (encodedControlPoints > (uint)(maxControlPoints - totalControlPoints))
        {
            throw new InvalidOperationException("Too many control points");
        }

        int numControlPoints = (int)encodedControlPoints;
        totalControlPoints += numControlPoints;
        this.ResizeControlPoints(configuration, numControlPoints);

        const long deltaLimit = 1L << 30;
        Span<JxlControlPoint> controlPoints = this.ControlPoints.Span;

        int length = this.ControlPoints.Length;

        for (int i = 0; i < length; i++)
        {
            ref JxlControlPoint controlPoint = ref controlPoints[i];

            controlPoint.First = JxlPackSigned.UnpackSigned((uint)decoder.ReadHybridUInt((int)JxlSplineEntropyContext.ControlPoints, br, contextMap, window));
            controlPoint.Second = JxlPackSigned.UnpackSigned((uint)decoder.ReadHybridUInt((int)JxlSplineEntropyContext.ControlPoints, br, contextMap, window));

            if (controlPoint.First >= deltaLimit || controlPoint.First <= -deltaLimit ||
                controlPoint.Second >= deltaLimit || controlPoint.Second <= -deltaLimit)
            {
                throw new InvalidOperationException("Spline delta-delta is out of bounds");
            }
        }

        for (int i = 0; i < this.ColorDct.Length; i++)
        {
            if (!TryDecodeDct(contextMap, this.ColorDct[i], decoder, br, window))
            {
                return false;
            }
        }

        if (!TryDecodeDct(contextMap, this.SigmaDct, decoder, br, window))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads one DCT coefficient group without capturing the stream's borrowed spans.
    /// </summary>
    /// <param name="contextMap">The spline histogram mapping.</param>
    /// <param name="dct">The destination coefficients.</param>
    /// <param name="decoder">The owning entropy reader.</param>
    /// <param name="br">The encoded coefficient fields.</param>
    /// <param name="window">The window borrowed before the spline loops.</param>
    /// <returns>Whether the coefficient group is valid.</returns>
    private static bool TryDecodeDct(
        ReadOnlySpan<byte> contextMap,
        Span<int> dct,
        JxlAnsSymbolReader decoder,
        JxlBitReader br,
        Span<int> window)
    {
        const int invalidCoefficient = int.MinValue;

        for (int i = 0; i < 32; i++)
        {
            dct[i] = JxlPackSigned.UnpackSigned((uint)decoder.ReadHybridUInt((int)JxlSplineEntropyContext.Dct, br, contextMap, window));
            if (dct[i] == invalidCoefficient)
            {
                throw new InvalidOperationException("The DCT coefficient is invalid");
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float AdjustedQuant(int adjustment)
        => (adjustment >= 0)
            ? (1f + (.125f * adjustment))
            : 1f / (1f - (.125f * adjustment));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float InverseAdjustedQuant(int adjustment)
        => (adjustment >= 0)
                ? 1f / (1f + (.125f * adjustment))
                : (1f - (.125f * adjustment));
}
