// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.IntraBlockCopy;

/// <summary>
/// Reconstructs AV1 intra-block-copy predictions from an earlier region of the current frame.
/// </summary>
/// <remarks>
/// Whole-sample luma displacements can map to half-sample chroma positions. The predictor therefore selects direct
/// copy, horizontal two-tap, vertical two-tap, or separable two-dimensional bilinear reconstruction per plane.
/// Filtered paths use the widest preferred SIMD width and retain an explicit scalar fallback for feature-disabled
/// execution. Narrow rows read from the frame buffer's prediction padding but use exact-width stores, so vectorization
/// never depends on writable destination padding.
/// </remarks>
internal static partial class Av1IntraBlockCopyPredictor
{
    /// <summary>
    /// Reconstructs an 8-bit intra-block-copy prediction.
    /// </summary>
    /// <param name="source">The source region beginning at the integer sample preceding any half-sample phase.</param>
    /// <param name="sourceStride">The distance, in samples, between source rows.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="halfX">Indicates whether the horizontal source phase is one half-sample.</param>
    /// <param name="halfY">Indicates whether the vertical source phase is one half-sample.</param>
    public static void Predict(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        bool halfX,
        bool halfY)
    {
        if (!halfX && !halfY)
        {
            Copy(source, sourceStride, destination, destinationStride, width, height);
        }
        else if (halfX && halfY)
        {
            Predict<BilinearOperator>(source, sourceStride, destination, destinationStride, width, height);
        }
        else if (halfX)
        {
            Predict<HorizontalOperator>(source, sourceStride, destination, destinationStride, width, height);
        }
        else
        {
            Predict<VerticalOperator>(source, sourceStride, destination, destinationStride, width, height);
        }
    }

    /// <summary>
    /// Reconstructs a high-bit-depth intra-block-copy prediction.
    /// </summary>
    /// <param name="source">The source region beginning at the integer sample preceding any half-sample phase.</param>
    /// <param name="sourceStride">The distance, in samples, between source rows.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="halfX">Indicates whether the horizontal source phase is one half-sample.</param>
    /// <param name="halfY">Indicates whether the vertical source phase is one half-sample.</param>
    public static void Predict(
        ReadOnlySpan<short> source,
        int sourceStride,
        Span<short> destination,
        int destinationStride,
        int width,
        int height,
        bool halfX,
        bool halfY)
    {
        if (!halfX && !halfY)
        {
            Copy(source, sourceStride, destination, destinationStride, width, height);
        }
        else if (halfX && halfY)
        {
            Predict<BilinearOperator>(source, sourceStride, destination, destinationStride, width, height);
        }
        else if (halfX)
        {
            Predict<HorizontalOperator>(source, sourceStride, destination, destinationStride, width, height);
        }
        else
        {
            Predict<VerticalOperator>(source, sourceStride, destination, destinationStride, width, height);
        }
    }

    /// <summary>
    /// Reconstructs an 8-bit intra-block-copy prediction without explicit hardware intrinsics.
    /// </summary>
    /// <param name="source">The source region beginning at the integer sample preceding any half-sample phase.</param>
    /// <param name="sourceStride">The distance, in samples, between source rows.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="halfX">Indicates whether the horizontal source phase is one half-sample.</param>
    /// <param name="halfY">Indicates whether the vertical source phase is one half-sample.</param>
    public static void PredictScalar(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        bool halfX,
        bool halfY)
    {
        if (!halfX && !halfY)
        {
            CopyScalar(source, sourceStride, destination, destinationStride, width, height);
        }
        else if (halfX && halfY)
        {
            PredictScalar<BilinearOperator>(source, sourceStride, destination, destinationStride, width, height);
        }
        else if (halfX)
        {
            PredictScalar<HorizontalOperator>(source, sourceStride, destination, destinationStride, width, height);
        }
        else
        {
            PredictScalar<VerticalOperator>(source, sourceStride, destination, destinationStride, width, height);
        }
    }

    /// <summary>
    /// Reconstructs a high-bit-depth intra-block-copy prediction without explicit hardware intrinsics.
    /// </summary>
    /// <param name="source">The source region beginning at the integer sample preceding any half-sample phase.</param>
    /// <param name="sourceStride">The distance, in samples, between source rows.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The distance, in samples, between destination rows.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="halfX">Indicates whether the horizontal source phase is one half-sample.</param>
    /// <param name="halfY">Indicates whether the vertical source phase is one half-sample.</param>
    public static void PredictScalar(
        ReadOnlySpan<short> source,
        int sourceStride,
        Span<short> destination,
        int destinationStride,
        int width,
        int height,
        bool halfX,
        bool halfY)
    {
        if (!halfX && !halfY)
        {
            CopyScalar(source, sourceStride, destination, destinationStride, width, height);
        }
        else if (halfX && halfY)
        {
            PredictScalar<BilinearOperator>(source, sourceStride, destination, destinationStride, width, height);
        }
        else if (halfX)
        {
            PredictScalar<HorizontalOperator>(source, sourceStride, destination, destinationStride, width, height);
        }
        else
        {
            PredictScalar<VerticalOperator>(source, sourceStride, destination, destinationStride, width, height);
        }
    }

    /// <summary>
    /// Copies an 8-bit whole-sample source block to its destination.
    /// </summary>
    private static void Copy(ReadOnlySpan<byte> source, int sourceStride, Span<byte> destination, int destinationStride, int width, int height)
    {
        // Span copying delegates each complete row to the runtime's overlap-safe native-width implementation. The
        // displacement validity rules keep source and destination blocks separate, so no intermediate buffer is needed.
        for (int row = 0; row < height; row++)
        {
            source.Slice(row * sourceStride, width).CopyTo(destination.Slice(row * destinationStride, width));
        }
    }

    /// <summary>
    /// Copies a high-bit-depth whole-sample source block to its destination.
    /// </summary>
    private static void Copy(ReadOnlySpan<short> source, int sourceStride, Span<short> destination, int destinationStride, int width, int height)
    {
        for (int row = 0; row < height; row++)
        {
            source.Slice(row * sourceStride, width).CopyTo(destination.Slice(row * destinationStride, width));
        }
    }

    /// <summary>
    /// Copies an 8-bit whole-sample source block with scalar sample assignments.
    /// </summary>
    private static void CopyScalar(ReadOnlySpan<byte> source, int sourceStride, Span<byte> destination, int destinationStride, int width, int height)
    {
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                destination[(row * destinationStride) + column] = source[(row * sourceStride) + column];
            }
        }
    }

    /// <summary>
    /// Copies a high-bit-depth whole-sample source block with scalar sample assignments.
    /// </summary>
    private static void CopyScalar(
        ReadOnlySpan<short> source,
        int sourceStride,
        Span<short> destination,
        int destinationStride,
        int width,
        int height)
    {
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                destination[(row * destinationStride) + column] = source[(row * sourceStride) + column];
            }
        }
    }
}
