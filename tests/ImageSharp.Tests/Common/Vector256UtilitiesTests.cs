// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Tests.Common;

public class Vector256UtilitiesTests
{
    [Theory]
    [InlineData(new int[] { 4, 5, 6, 7, 8, 9, 10, 11 }, new int[] { 1, 2, 3, 4, 0, -1, -2, -3 }, new int[] { 4, 1, 5, 2, 6, 3, 7, 4 })]
    public void TestVector256InterleaveLower(int[] a, int[] b, int[] expected)
    {
        Vector256<int> v256a = Vector256.Create(a);
        Vector256<int> v256b = Vector256.Create(b);

        Vector256<int> v256 = Vector256_.InterleaveLower(v256a, v256b);

        int[] result = new int[Vector256<int>.Count];
        v256.CopyTo(result);

        bool isEqual = expected.SequenceEqual(result);
        if (!isEqual)
        {
            Assert.Fail($"Lower shuffle failed.\n\nExpected: [{string.Join(", ", expected)}]\nActual: [{string.Join(", ", result)}]");
        }
    }

    [Theory]
    [InlineData(new int[] { 4, 5, 6, 7, 8, 9, 10, 11 }, new int[] { 1, 2, 3, 4, 0, -1, -2, -3 }, new int[] { 8, 0, 9, -1, 10, -2, 11, -3 })]
    public void TestVector256InterleaveUpper(int[] a, int[] b, int[] expected)
    {
        Vector256<int> v256a = Vector256.Create(a);
        Vector256<int> v256b = Vector256.Create(b);

        Vector256<int> v256 = Vector256_.InterleaveUpper(v256a, v256b);

        int[] result = new int[Vector256<int>.Count];
        v256.CopyTo(result);

        bool isEqual = expected.SequenceEqual(result);
        if (!isEqual)
        {
            Assert.Fail($"Lower shuffle failed.\n\nExpected: [{string.Join(", ", expected)}]\nActual: [{string.Join(", ", result)}]");
        }
    }
}
