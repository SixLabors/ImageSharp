// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

public class ExactImageComparer : ImageComparer
{
    public static ExactImageComparer Instance { get; } = new();

    /// <inheritdoc />
    public override ImageSimilarityReport<TPixelA, TPixelB> CompareImagesOrFrames<TPixelA, TPixelB>(
        int index,
        ImageFrame<TPixelA> expected,
        ImageFrame<TPixelB> actual)
    {
        if (expected.Size != actual.Size)
        {
            throw new InvalidOperationException("Calling ImageComparer is invalid when dimensions mismatch!");
        }

        int width = actual.Width;

        // Exact comparison must retain differences smaller than one integer component code.
        RgbaVector[] aBuffer = new RgbaVector[width];
        RgbaVector[] bBuffer = new RgbaVector[width];
        Span<Vector4> aVectors = MemoryMarshal.Cast<RgbaVector, Vector4>(aBuffer.AsSpan());
        Span<Vector4> bVectors = MemoryMarshal.Cast<RgbaVector, Vector4>(bBuffer.AsSpan());

        List<PixelDifference> differences = [];
        Configuration configuration = expected.Configuration;
        Buffer2D<TPixelA> expectedBuffer = expected.PixelBuffer;
        Buffer2D<TPixelB> actualBuffer = actual.PixelBuffer;
        bool associated = TPixelA.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated
            && TPixelB.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated;

        for (int y = 0; y < actual.Height; y++)
        {
            Span<TPixelA> aSpan = expectedBuffer.DangerousGetRowSpan(y);
            Span<TPixelB> bSpan = actualBuffer.DangerousGetRowSpan(y);

            if (associated)
            {
                // Compare stored RGB when both formats are associated. Unassociation would amplify differences at low alpha.
                PixelOperations<TPixelA>.Instance.ToAssociatedScaledVector4(configuration, aSpan, aVectors);
                PixelOperations<TPixelB>.Instance.ToAssociatedScaledVector4(configuration, bSpan, bVectors);
            }
            else
            {
                // Mixed alpha representations need straight RGB on both sides before their components can be compared.
                PixelOperations<TPixelA>.Instance.ToUnassociatedScaledVector4(configuration, aSpan, aVectors);
                PixelOperations<TPixelB>.Instance.ToUnassociatedScaledVector4(configuration, bSpan, bVectors);
            }

            for (int x = 0; x < width; x++)
            {
                RgbaVector aPixel = aBuffer[x];
                RgbaVector bPixel = bBuffer[x];

                if (aPixel != bPixel)
                {
                    PixelDifference diff = new(new Point(x, y), aPixel, bPixel);
                    differences.Add(diff);
                }
            }
        }

        return new ImageSimilarityReport<TPixelA, TPixelB>(index, expected, actual, differences);
    }
}
