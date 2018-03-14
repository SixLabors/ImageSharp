// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Drawing;

namespace SixLabors.ImageSharp.Tests
{
    using System;
    using Xunit;
    using ImageSharp;

    public abstract class PhotometricInterpretationTestBase
    {
        public static Rgba32 DefaultColor = new Rgba32(42, 96, 18, 128);

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

        internal static void AssertDecode(Rgba32[][] expectedResult, Action<PixelAccessor<Rgba32>> decodeAction)
        {
            int resultWidth = expectedResult[0].Length;
            int resultHeight = expectedResult.Length;
            Image<Rgba32> image = new Image<Rgba32>(resultWidth, resultHeight);
            image.Mutate(x => x.Fill(DefaultColor));

            using (PixelAccessor<Rgba32> pixels = image.Lock())
            {
                decodeAction(pixels);
            }

            using (PixelAccessor<Rgba32> pixels = image.Lock())
            {
                for (int y = 0; y < resultHeight; y++)
                {
                    for (int x = 0; x < resultWidth; x++)
                    {
                        Assert.True(expectedResult[y][x] == pixels[x, y],
                            $"Pixel ({x}, {y}) should be {expectedResult[y][x]} but was {pixels[x, y]}");
                    }
                }
            }
        }
    }
}