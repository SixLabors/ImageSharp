// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantification;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1InverseQuantizationTests
{
    [Fact]
    public void QuantizationMatricesCoverAllLevelsPlanesAndTransformSizes()
    {
        for (int level = 0; level < Av1Constants.QuantificationMatrixLevelCount; level++)
        {
            for (Av1Plane plane = Av1Plane.Y; (int)plane < Av1Constants.MaxPlanes; plane++)
            {
                for (int transformSizeIndex = 0; transformSizeIndex < (int)Av1TransformSize.AllSizes; transformSizeIndex++)
                {
                    Av1TransformSize transformSize = (Av1TransformSize)transformSizeIndex;
                    Av1TransformSize adjustedSize = transformSize.GetAdjusted();
                    int expectedLength = adjustedSize.GetWidth() * adjustedSize.GetHeight();

                    ReadOnlySpan<int> matrix = Av1InverseQuantizationLookup.GetQuantizationMatrix(level, plane, transformSize);

                    Assert.Equal(expectedLength, matrix.Length);
                }
            }
        }
    }
}
