// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Comparison;

/// <summary>
/// Provides the shared comparison and difference image implementations for all comparers.
/// </summary>
internal static class ImageComparisonEngine
{
    // Pixels of identical images are rendered as mid-gray. Every differing component of the actual
    // image is rendered as white when the component of the actual image is above the component of
    // the expected image, and black when it is below. Rendering is per RGB channel, so the channels
    // which changed and their direction are immediately visible. The difference image is fully
    // opaque, so that it can be saved without alteration; alpha differences are reflected in the
    // total difference of the report only.
    private static readonly Rgba32 EqualPixelColor = new(128, 128, 128, 255);

    /// <summary>
    /// Compares two image frames and returns the report describing their difference.
    /// </summary>
    /// <typeparam name="TPixelA">The pixel type of the expected frame.</typeparam>
    /// <typeparam name="TPixelB">The pixel type of the actual frame.</typeparam>
    /// <param name="index">The index of the compared frames, used to identify the report.</param>
    /// <param name="expected">The expected image frame.</param>
    /// <param name="actual">The actual image frame.</param>
    /// <param name="imageThreshold">The maximal tolerated difference of the comparer.</param>
    /// <param name="perPixelManhattanThreshold">
    /// The threshold of the individual pixels before they accumulate towards the overall difference.
    /// </param>
    /// <returns>An <see cref="ImageSimilarityReport"/> describing the difference.</returns>
    public static ImageSimilarityReport CompareFrames<TPixelA, TPixelB>(
        int index,
        ImageFrame<TPixelA> expected,
        ImageFrame<TPixelB> actual,
        float imageThreshold,
        int perPixelManhattanThreshold)
        where TPixelA : unmanaged, IPixel<TPixelA>
        where TPixelB : unmanaged, IPixel<TPixelB>
    {
        int width = actual.Width;

        // Reuse floating-point rows so comparison does not quantize or clip the decoded components.
        RgbaVector[] aBuffer = new RgbaVector[width];
        RgbaVector[] bBuffer = new RgbaVector[width];
        Span<Vector4> aVectors = MemoryMarshal.Cast<RgbaVector, Vector4>(aBuffer.AsSpan());
        Span<Vector4> bVectors = MemoryMarshal.Cast<RgbaVector, Vector4>(bBuffer.AsSpan());

        float totalDifference = 0F;
        bool associated = GetAssociated(expected, actual);
        Configuration configuration = expected.Configuration;
        Buffer2D<TPixelA> expectedBuffer = expected.PixelBuffer;
        Buffer2D<TPixelB> actualBuffer = actual.PixelBuffer;

        for (int y = 0; y < actual.Height; y++)
        {
            ConvertRow(configuration, expectedBuffer.DangerousGetRowSpan(y), aVectors, associated);
            ConvertRow(configuration, actualBuffer.DangerousGetRowSpan(y), bVectors, associated);

            for (int x = 0; x < width; x++)
            {
                // Equal infinities and matching NaNs have no difference; subtracting them would instead produce NaN.
                if (aBuffer[x] == bBuffer[x])
                {
                    continue;
                }

                float d = GetManhattanDistanceInRgbaSpace(ref aBuffer[x], ref bBuffer[x]);

                if (!(d <= perPixelManhattanThreshold))
                {
                    totalDifference += d;
                }
            }
        }

        float normalizedDifference = totalDifference / (actual.Width * (float)actual.Height);
        normalizedDifference /= 4F * 65535F;

        // A non-finite difference must also fail; comparing only with '>' would silently accept NaN.
        bool isEmpty = normalizedDifference <= imageThreshold;
        return new ImageSimilarityReport(index, expected, actual, normalizedDifference, isEmpty);
    }

    /// <summary>
    /// Creates a difference image for two image frames. Pixels with equal components are rendered
    /// as mid-gray. Every component of the actual image which differs is rendered as white when it
    /// is above the component of the expected image, and black when it is below. Rendering is per
    /// RGB channel; the difference image is fully opaque.
    /// </summary>
    /// <typeparam name="TPixelA">The pixel type of the expected frame.</typeparam>
    /// <typeparam name="TPixelB">The pixel type of the actual frame.</typeparam>
    /// <param name="expected">The expected image frame.</param>
    /// <param name="actual">The actual image frame.</param>
    /// <returns>The difference image.</returns>
    public static Image CreateDifferenceImage<TPixelA, TPixelB>(ImageFrame<TPixelA> expected, ImageFrame<TPixelB> actual)
        where TPixelA : unmanaged, IPixel<TPixelA>
        where TPixelB : unmanaged, IPixel<TPixelB>
    {
        int width = actual.Width;
        Image<Rgba32> differenceImage = new(expected.Configuration, width, actual.Height);
        Buffer2D<Rgba32> differenceBuffer = differenceImage.Frames.RootFrame.PixelBuffer;

        // Reuse floating-point rows so rendering does not quantize or clip the decoded components.
        RgbaVector[] aBuffer = new RgbaVector[width];
        RgbaVector[] bBuffer = new RgbaVector[width];
        Span<Vector4> aVectors = MemoryMarshal.Cast<RgbaVector, Vector4>(aBuffer.AsSpan());
        Span<Vector4> bVectors = MemoryMarshal.Cast<RgbaVector, Vector4>(bBuffer.AsSpan());

        bool associated = GetAssociated(expected, actual);
        Configuration configuration = expected.Configuration;
        Buffer2D<TPixelA> expectedBuffer = expected.PixelBuffer;
        Buffer2D<TPixelB> actualBuffer = actual.PixelBuffer;

        for (int y = 0; y < actual.Height; y++)
        {
            ConvertRow(configuration, expectedBuffer.DangerousGetRowSpan(y), aVectors, associated);
            ConvertRow(configuration, actualBuffer.DangerousGetRowSpan(y), bVectors, associated);

            Span<Rgba32> differenceRow = differenceBuffer.DangerousGetRowSpan(y);
            differenceRow.Fill(EqualPixelColor);

            for (int x = 0; x < width; x++)
            {
                // Equal infinities and matching NaNs have no difference; a differing component is
                // rendered by its direction. Mismatched NaNs compare as equal on the channel level
                // because they fail both directional comparisons.
                RgbaVector a = aBuffer[x];
                RgbaVector b = bBuffer[x];

                if (a == b)
                {
                    continue;
                }

                differenceRow[x] = new Rgba32(
                    ToDifferenceLevel(a.R, b.R),
                    ToDifferenceLevel(a.G, b.G),
                    ToDifferenceLevel(a.B, b.B),
                    byte.MaxValue);
            }
        }

        return differenceImage;
    }

    /// <summary>
    /// Converts a single pixel component of the actual image into the gray level visualizing its
    /// difference against the expected image.
    /// </summary>
    /// <param name="expected">The component of the expected image.</param>
    /// <param name="actual">The component of the actual image.</param>
    /// <returns>The gray level of the component.</returns>
    private static byte ToDifferenceLevel(float expected, float actual)
    {
        if (actual > expected)
        {
            return byte.MaxValue;
        }

        if (actual < expected)
        {
            return 0;
        }

        return EqualPixelColor.R;
    }

    /// <summary>
    /// Converts a row of pixels into the floating-point buffer. Two associated formats are converted
    /// using their stored associated components; other pairs are converted using unassociated
    /// components, matching the conversion used by the comparison.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type of the row.</typeparam>
    /// <param name="configuration">The configuration used by the conversion.</param>
    /// <param name="row">The row of pixels.</param>
    /// <param name="vectors">The vector view of the buffer receiving the components.</param>
    /// <param name="associated">
    /// Whether the row is converted using associated components.
    /// </param>
    private static void ConvertRow<TPixel>(
        Configuration configuration,
        Span<TPixel> row,
        Span<Vector4> vectors,
        bool associated)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (associated)
        {
            // Compare stored RGB when both formats are associated. Unassociation would amplify differences at low alpha.
            PixelOperations<TPixel>.Instance.ToAssociatedScaledVector4(configuration, row, vectors);
        }
        else
        {
            // Mixed alpha representations need straight RGB on both sides before their components can be compared.
            PixelOperations<TPixel>.Instance.ToUnassociatedScaledVector4(configuration, row, vectors);
        }
    }

    /// <summary>
    /// Determines whether both frames are compared using their stored associated components.
    /// </summary>
    /// <typeparam name="TPixelA">The pixel type of the expected frame.</typeparam>
    /// <typeparam name="TPixelB">The pixel type of the actual frame.</typeparam>
    /// <param name="expected">The expected image frame.</param>
    /// <param name="actual">The actual image frame.</param>
    /// <returns>Whether both frames use associated alpha.</returns>
    private static bool GetAssociated<TPixelA, TPixelB>(ImageFrame<TPixelA> expected, ImageFrame<TPixelB> actual)
        where TPixelA : unmanaged, IPixel<TPixelA>
        where TPixelB : unmanaged, IPixel<TPixelB>
        => TPixelA.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated
            && TPixelB.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated;

    /// <summary>
    /// Calculates the sum of absolute RGBA differences in sixteen-bit component units.
    /// </summary>
    /// <param name="a">The first floating-point pixel.</param>
    /// <param name="b">The second floating-point pixel.</param>
    /// <returns>The component distance, including fractional sixteen-bit codes.</returns>
    private static float GetManhattanDistanceInRgbaSpace(ref RgbaVector a, ref RgbaVector b)
    {
        // Keep the existing threshold units: one eight-bit code equals 257 sixteen-bit units.
        Vector4 delta = Vector4.Abs(a.ToScaledVector4() - b.ToScaledVector4()) * ushort.MaxValue;
        return delta.X + delta.Y + delta.Z + delta.W;
    }
}
