// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.Common;

public class SpanExtensionsTests
{
    /// <summary>
    /// Checks partial selection against full sorting for fixed numeric inputs.
    /// </summary>
    /// <param name="input">The fixed input values, including repeated and extreme finite values.</param>
    [Theory]
    [InlineData(new float[] { 7 })]
    [InlineData(new float[] { 1, 2, 3, 4, 5 })]
    [InlineData(new float[] { 5, 4, 3, 2, 1 })]
    [InlineData(new float[] { 2, 2, 2, 2, 2 })]
    [InlineData(new float[] { 2, 1, 3, 2, 3, 1 })]
    [InlineData(new float[] { -9, 6, 0, float.MinValue, float.MaxValue })]
    public void NthElement_SelectsOrderStatisticAndPreservesValues(float[] input)
    {
        float[] expected = input.ToArray();
        expected.AsSpan().Sort();

        for (int n = 0; n < input.Length; n++)
        {
            // Each selection starts with the same fixed input. Full sorting supplies an independent order statistic.
            float[] actual = input.ToArray();
            float selected = actual.AsSpan().NthElement(n);

            Assert.Equal(expected[n], selected);
            Assert.Equal(selected, actual[n]);

            // Selection partitions around n but does not sort either side.
            for (int i = 0; i < n; i++)
            {
                Assert.True(actual[i] <= selected);
            }

            for (int i = n + 1; i < actual.Length; i++)
            {
                Assert.True(actual[i] >= selected);
            }

            // Sorting the partitioned values checks that selection did not lose, duplicate, or change a value.
            actual.AsSpan().Sort();
            Assert.Equal(expected, actual);
        }
    }
}
