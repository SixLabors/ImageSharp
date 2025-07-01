// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.PhotometricInterpretation;

[Trait("Format", "Tiff")]
public abstract class PhotometricInterpretationTestBase
{
    public static Rgba32 DefaultColor = new(42, 96, 18, 128);

    public static Rgba32[][] Offset(Rgba32[][] input, int xOffset, int yOffset, int width, int height)
    {
        int inputHeight = input.Length;
        int inputWidth = input[0].Length;

        Rgba32[][] output = new Rgba32[height][];

        for (int y = 0; y < output.Length; y++)
        {
            output[y] = new Rgba32[width];

            for (int x = 0; x < width; x++)
            {
                output[y][x] = DefaultColor;
            }
        }

        for (int y = 0; y < inputHeight; y++)
        {
            for (int x = 0; x < inputWidth; x++)
            {
                output[y + yOffset][x + xOffset] = input[y][x];
            }
        }

        return output;
    }

    internal static void AssertDecode(Rgba32[][] expectedResult, Action<Buffer2D<Rgba32>> decodeAction)
    {
        int resultWidth = expectedResult[0].Length;
        int resultHeight = expectedResult.Length;

        using (Image<Rgba32> image = new(resultWidth, resultHeight))
        {
            image.Mutate(x => x.BackgroundColor(Color.FromPixel(DefaultColor)));
            Buffer2D<Rgba32> pixels = image.GetRootFramePixelBuffer();

            decodeAction(pixels);

            for (int y = 0; y < resultHeight; y++)
            {
                for (int x = 0; x < resultWidth; x++)
                {
                    Assert.True(
                        expectedResult[y][x] == pixels[x, y],
                        $"Pixel ({x}, {y}) should be {expectedResult[y][x]} but was {pixels[x, y]}");
                }
            }
        }
    }
}
