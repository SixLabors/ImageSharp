// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests.Drawing.Utils
{
    using System;
    using System.Linq;

    using SixLabors.ImageSharp.Utils;

    using Xunit;

    public class QuickSortTests
    {
        public static readonly TheoryData<float[]> Data = new TheoryData<float[]>()
                                                              {
                                                                  new float[]{ 3, 2, 1 },
                                                                  new float[0],
                                                                  new float[] { 42},
                                                                  new float[] { 1, 2},
                                                                  new float[] { 2, 1},
                                                                  new float[] { 5, 1, 2, 3, 0}
                                                              };

        [Theory]
        [MemberData(nameof(Data))]
        public void Sort(float[] data)
        {
            float[] expected = data.ToArray();

            Array.Sort(expected);

            QuickSort.Sort(data);

            Assert.Equal(expected, data);
        }

        [Fact]
        public void SortSlice()
        {
            float[] data = { 3, 2, 1, 0, -1 };

            Span<float> slice = data.AsSpan(1, 3);
            QuickSort.Sort(slice);
            float[] actual = slice.ToArray();
            float[] expected = { 0, 1, 2 };

            Assert.Equal(actual, expected);
        }
    }
}