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

    public bool Dequantize(
        Configuration configuration,
        PointF startingPoint,
        int quantizationAdjustment,
        float yToX,
        float yToB,
        long imageSize,
        ref long totalEstimatedAreaReached,
        JxlSpline result)
    {
        long areaLimit = Math.Min(1024 * imageSize * (1L << 32), 1L << 42);
        result.ClearControlPoints();
        result.ReserveControlPoints(configuration, this.ControlPoints.Length + 1);

        float px = MathF.Round(startingPoint.X);
        float py = MathF.Round(startingPoint.Y);

        if (!this.ValidateSplinePointPos(px, py))
        {
            throw new InvalidOperationException("Spline points out of range");
        }

        int currentX = (int)px;
        int currentY = (int)py;

        Span<PointF> controlPoints = result.ControlPoints.Span;
        Span<JxlControlPoint> thisControlPoints = this.ControlPoints.Span;

        controlPoints[0] = new(currentX, currentY);

        int currentDx = 0;  // D stands for delta
        int currentDy = 0;  // D stands for delta

        long manhattanDistance = 0;

        int length = this.ControlPoints.Length;

        for (int i = 0; i < length; i++)
        {
            JxlControlPoint point = thisControlPoints[i];
            currentDx += point.First;
            currentDy += point.Second;
            manhattanDistance = Math.Abs(currentDx) + Math.Abs(currentDy);

            if (manhattanDistance > areaLimit)
            {
                throw new InvalidOperationException("Manhattan distance is too large");
            }

            if (!ValidateSplinePointPos(currentDx, currentDy))
            {
                throw new InvalidOperationException("Delta points out of range");
            }

            currentX += currentDx;
            currentY += currentDy;

            if (!ValidateSplinePointPos(currentX, currentY))
            {
                throw new InvalidOperationException("Current points out of range");
            }

            controlPoints[i + 1] = new(currentX, currentY);
        }

        float inverseQuant = InverseAdjustedQuant(quantizationAdjustment);

        for (int c = 0; c < 3; c++)
        {
            for (int i = 0; i < 32; i++)
            {
                float inverseDctFactor = (i == 0) ? JxlDctScales.Sqrt05 : 1.0f;
                result.ColorDct[c][i] = this.ColorDct[c][i] * inverseDctFactor * ChannelWeight[c] * inverseQuant;
            }
        }

        for (int i = 0; i < 32; i++)
        {
            result.ColorDct[0][i] += yToX * result.ColorDct[1][i];
            result.ColorDct[2][i] += yToB * result.ColorDct[1][i];
        }

        long widthEstimate = 0;
        Span<long> color = stackalloc long[3];

        for (int c = 0; c < 3; c++)
        {
            for (int i = 0; i < 32; i++)
            {
                color[c] += (long)MathF.Ceiling(inverseQuant * MathF.Abs(this.ColorDct[c][i]));
            }
        }

        color[0] += (long)MathF.Ceiling(MathF.Abs(yToX)) * color[1];
        color[2] += (long)MathF.Ceiling(MathF.Abs(yToB)) * color[1];

        long maxColor = Math.Max(color[1], Math.Max(color[0], color[2]));
        long logColor = Math.Max(1L, JxlMath.CeilLog2Nonzero(1L + maxColor));
        float weightLimit = MathF.Ceiling(MathF.Sqrt((float)areaLimit / logColor) / MathF.Max(1, manhattanDistance));

        for (int i = 0; i < 32; i++)
        {
            float inverseDctFactor = (i == 0) ? JxlDctScales.Sqrt05 : 1.0f;
            result.SigmaDct[i] = this.SigmaDct[i] * inverseDctFactor * ChannelWeight[3] * inverseQuant;
            float weightF = MathF.Ceiling(inverseQuant * MathF.Abs(this.SigmaDct[i]));
            long weight = (long)Math.Min(weightLimit, Math.Max(1.0f, weightF));
            widthEstimate += weight * weight * logColor;
        }

        totalEstimatedAreaReached = widthEstimate * manhattanDistance;
        if (totalEstimatedAreaReached > areaLimit)
        {
            throw new InvalidOperationException("Total estimated area is too large");
        }

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
