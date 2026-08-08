// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

internal sealed class JxlQuantizedSpline : IDisposable
{
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

    public Memory<JxlControlPoint> ControlPoints { get; set; }

    // NOTE: Do not use Configuration.MemoryAllocator.Allocate2D. This is
    // a 3x32 array, and renting memory introduces too much overhead for
    // 384 bytes of memory.
    // Additionally, prefer jagged arrays instead of multidimensional arrays
    // for performance.
    public int[][] ColorDct { get; set; } = new int[3][];

    public int[] SigmaDct { get; set; } = new int[32];

    public void Dispose()
    {
        this.memoryOwner?.Dispose();
        this.ControlPoints = Memory<JxlControlPoint>.Empty;
        GC.SuppressFinalize(this);
    }

    public void ReserveControlPoints(Configuration configuration, int n)
    {
        this.memoryOwner = configuration.MemoryAllocator.Allocate<JxlControlPoint>(n);

        this.ControlPoints = this.memoryOwner.Memory;
    }

    public static JxlQuantizedSpline Create(Configuration configuration, JxlSpline original, int quantizationAdjustment, float yToX, float yToB)
    {
        JxlQuantizedSpline spline = new();

        spline.ReserveControlPoints(configuration, original.ControlPoints.Count - 1);

        PointF startingPoint = original.ControlPoints.First();
        int previousX = (int)MathF.Round(startingPoint.X);
        int previousY = (int)MathF.Round(startingPoint.Y);
        int previousDx = 0; // D stands for delta
        int previousDy = 0; // D stands for delta

        int length = original.ControlPoints.Length;
        IMemoryOwner<JxlControlPoint> newControls = configuration.MemoryAllocator.Allocate<JxlControlPoint>(length);
        Span<JxlControlPoint> controlsSpan = newControls.Memory.Span;

        for (int i = 0; i < length; i++)
        {
            PointF controlPoint = original.ControlPoints[i];

            int newX = (int)MathF.Round(controlPoint.X);
            int newY = (int)MathF.Round(controlPoint.Y);
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

        for (int j = 0; j < 3; j++)
        {
            int c = Loops[j];

            float factor = (c == 0) ? yToX : (c == 1) ? 0 : yToB;

            // TODO: lower amount of branches by duplicating code
            // for i=0 and adding a separate loop for i=1..31
            for (int i = 0; i < 32; i++)
            {
                float dctFactor = (i == 0) ? Sqrt2 : 1.0f;
                float inverseDctFactor = (i == 0) ? Sqrt05 : 1.0f;
                float restoredY = spline.ColorDct[1][i] * inverseDctFactor * ChannelWeight[1] * inverseQuant;
                float decorrelated = spline.ColorDct[c][i] - (factor * restoredY);
                spline.ColorDct[c][i] = ConvertToInteger(decorrelated * dctFactor * quant / ChannelWeight[c]);
            }
        }

        for (int i = 0; i < 32; i++)
        {
            float dctFactor = (i == 0) ? Sqrt2 : 1.0f;
            spline.SigmaDct[i] = ConvertToInteger(original.SigmaDct[i] * dctFactor * quant / ChannelWeight[1]);
        }

        return spline;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int ConvertToInteger(float value)
        {
            const float max = int.MaxValue - 127f;
            const float min = -max;

            return (int)MathF.Round(Math.Clamp(value, min, max));
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
                float inverseDctFactor = (i == 0) ? Sqrt05 : 1.0f;
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
            float inverseDctFactor = (i == 0) ? Sqrt05 : 1.0f;
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

    public bool Decode(
        Configuration configuration,
        Span<byte> contextMap,
        JxlAnsSymbolReader decoder,
        JxlBitReader br,
        int maxControlPoints,
        ref int totalControlPoints)
    {
        int numControlPoints = decoder.ReadHybridUnsignedInteger(NumControlPointsContext, br, contextMap);
        if (numControlPoints > maxControlPoints)
        {
            throw new InvalidOperationException("Too many control points");
        }

        totalControlPoints += numControlPoints;

        if (totalControlPoints >= maxControlPoints)
        {
            throw new InvalidOperationException("Too many control points");
        }

        this.ResizeControlPoints(configuration, numControlPoints);

        const long deltaLimit = 1L << 30;
        Span<JxlControlPoint> controlPoints = this.ControlPoints.Span;

        int length = this.ControlPoints.Length;

        for (int i = 0; i < length; i++)
        {
            ref JxlControlPoint controlPoint = ref controlPoints[i];

            controlPoint.First = JxlPackSigned.UnpackSigned(decoder.ReadHybridUnsignedInteger(ControlPointsContext, br, contextMap));
            controlPoint.Second = JxlPackSigned.UnpackSigned(decoder.ReadHybridUnsignedInteger(ControlPointsContext, br, contextMap));

            if (controlPoint.First >= deltaLimit || controlPoint.First <= -deltaLimit ||
                controlPoint.Second >= deltaLimit || controlPoint.Second <= -deltaLimit)
            {
                throw new InvalidOperationException("Spline delta-delta is out of bounds");
            }
        }

        for (int i = 0; i < this.ColorDct.Length; i++)
        {
            if (!TryDecodeDct(contextMap, this.ColorDct[i]))
            {
                return false;
            }
        }

        if (!TryDecodeDct(contextMap, this.SigmaDct))
        {
            return false;
        }

        return true;

        bool TryDecodeDct(ReadOnlySpan<byte> contextMap, Span<int> dct)
        {
            const int invalidCoefficient = int.MinValue;

            for (int i = 0; i < 32; i++)
            {
                dct[i] = JxlPackSigned.UnpackSigned(decoder.ReadHybridUnsignedInteger(DctContext, br, contextMap));
                if (dct[i] == invalidCoefficient)
                {
                    throw new InvalidOperationException("The DCT coefficient is invalid");
                }
            }

            return true;
        }
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
