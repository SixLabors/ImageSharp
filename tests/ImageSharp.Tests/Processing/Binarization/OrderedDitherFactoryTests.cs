// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Dithering;
using SixLabors.ImageSharp.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Binarization
{
    public class OrderedDitherFactoryTests
    {
        private static readonly Fast2DArray<uint> Expected2x2Matrix = new Fast2DArray<uint>(
        new uint[2, 2]
        {
            { 0, 2 },
            { 3, 1 }
        });

        private static readonly Fast2DArray<uint> Expected3x3Matrix = new Fast2DArray<uint>(
        new uint[3, 3]
        {
            { 0, 5, 2 },
            { 7, 4, 8 },
            { 3, 6, 1 }
        });

        private static readonly Fast2DArray<uint> Expected4x4Matrix = new Fast2DArray<uint>(
        new uint[4, 4]
        {
            {  0, 8, 2, 10 },
            { 12, 4, 14, 6 },
            {  3, 11, 1, 9 },
            { 15, 7, 13, 5 }
        });

        private static readonly Fast2DArray<uint> Expected8x8Matrix = new Fast2DArray<uint>(
        new uint[8, 8]
        {
            {  0, 32,  8, 40,  2, 34, 10, 42 },
            { 48, 16, 56, 24, 50, 18, 58, 26 },
            { 12, 44,  4, 36, 14, 46,  6, 38 },
            { 60, 28, 52, 20, 62, 30, 54, 22 },
            {  3, 35, 11, 43,  1, 33,  9, 41 },
            { 51, 19, 59, 27, 49, 17, 57, 25 },
            { 15, 47,  7, 39, 13, 45,  5, 37 },
            { 63, 31, 55, 23, 61, 29, 53, 21 }
        });


        [Fact]
        public void OrderedDitherFactoryCreatesCorrect2x2Matrix()
        {
            Fast2DArray<uint> actual = OrderedDitherFactory.CreateDitherMatrix(2);
            for (int y = 0; y < actual.Height; y++)
            {
                for (int x = 0; x < actual.Width; x++)
                {
                    Assert.Equal(Expected2x2Matrix[y, x], actual[y, x]);
                }
            }
        }

        [Fact]
        public void OrderedDitherFactoryCreatesCorrect3x3Matrix()
        {
            Fast2DArray<uint> actual = OrderedDitherFactory.CreateDitherMatrix(3);
            for (int y = 0; y < actual.Height; y++)
            {
                for (int x = 0; x < actual.Width; x++)
                {
                    Assert.Equal(Expected3x3Matrix[y, x], actual[y, x]);
                }
            }
        }

        [Fact]
        public void OrderedDitherFactoryCreatesCorrect4x4Matrix()
        {
            Fast2DArray<uint> actual = OrderedDitherFactory.CreateDitherMatrix(4);
            for (int y = 0; y < actual.Height; y++)
            {
                for (int x = 0; x < actual.Width; x++)
                {
                    Assert.Equal(Expected4x4Matrix[y, x], actual[y, x]);
                }
            }
        }

        [Fact]
        public void OrderedDitherFactoryCreatesCorrect8x8Matrix()
        {
            Fast2DArray<uint> actual = OrderedDitherFactory.CreateDitherMatrix(8);
            for (int y = 0; y < actual.Height; y++)
            {
                for (int x = 0; x < actual.Width; x++)
                {
                    Assert.Equal(Expected8x8Matrix[y, x], actual[y, x]);
                }
            }
        }
    }
}