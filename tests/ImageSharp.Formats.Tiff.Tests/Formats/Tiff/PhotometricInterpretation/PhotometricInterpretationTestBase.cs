// <copyright file="PhotometricInterpretationTestBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using Xunit;

    public abstract class PhotometricInterpretationTestBase
    {
        public static Color[][] Offset(Color[][] input, int xOffset, int yOffset, int width, int height)
        {
            int inputHeight = input.Length;
            int inputWidth = input[0].Length;

            Color[][] output = new Color[height][];

            for (int y = 0; y < output.Length; y++)
            {
                output[y] = new Color[width];
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

        public static void AssertDecode(Color[][] expectedResult, Action<PixelAccessor<Color>> decodeAction)
        {
            int resultWidth = expectedResult[0].Length;
            int resultHeight = expectedResult.Length;
            Image image = new Image(resultWidth, resultHeight);

            using (PixelAccessor<Color> pixels = image.Lock())
            {
                decodeAction(pixels);
            }

            using (PixelAccessor<Color> pixels = image.Lock())
            {
                for (int y = 0; y < resultHeight; y++)
                {
                    for (int x = 0; x < resultWidth; x++)
                    {
                        Assert.Equal(expectedResult[y][x], pixels[x, y]);
                    }
                }
            }
        }
    }
}