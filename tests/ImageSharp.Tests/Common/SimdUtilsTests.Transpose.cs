// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Common;

public partial class SimdUtilsTests
{
    /// <summary>
    /// Checks rectangular coordinates, raw floating-point bits, strided padding and scalar tails with each hardware configuration.
    /// </summary>
    [Fact]
    public void Transpose_PreservesCoordinatesAndPadding()
    {
        static void RunTest()
        {
            ReadOnlySpan<(int Rows, int Columns)> sizes = [(0, 5), (5, 0), (1, 1), (3, 7), (4, 4), (8, 8), (16, 16), (17, 19), (33, 65)];
            ReadOnlySpan<bool> floatingPointViews = [false, true];
            ReadOnlySpan<int> payloads = [0x7FC12345, int.MinValue, 0x7F800000, unchecked((int)0xFF800000), 0x3F800000, 0];
            const int prefix = 3;
            const int sentinel = 0x12345678;

            foreach ((int rows, int columns) in sizes)
            {
                int sourceStride = columns + 5;
                int destinationStride = rows + 7;
                int[] source = new int[prefix + (rows * sourceStride) + 5];
                int[] destination = new int[prefix + (columns * destinationStride) + 5];
                source.AsSpan().Fill(sentinel);

                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        // The first row includes NaN, signed zero and infinities. Other values identify their exact source coordinates.
                        source[prefix + (row * sourceStride) + column] = row == 0 && column < payloads.Length
                            ? payloads[column]
                            : (row * 131071) + column;
                    }
                }

                foreach (bool floatingPoint in floatingPointViews)
                {
                    destination.AsSpan().Fill(sentinel);
                    if (floatingPoint)
                    {
                        SimdUtils.Transpose(
                            MemoryMarshal.Cast<int, float>(source.AsSpan(prefix)),
                            sourceStride,
                            MemoryMarshal.Cast<int, float>(destination.AsSpan(prefix)),
                            destinationStride,
                            rows,
                            columns);
                    }
                    else
                    {
                        SimdUtils.Transpose(source.AsSpan(prefix), sourceStride, destination.AsSpan(prefix), destinationStride, rows, columns);
                    }

                    for (int i = 0; i < destination.Length; i++)
                    {
                        int relative = i - prefix;
                        int row = relative / destinationStride;
                        int column = relative % destinationStride;

                        // A transpose maps destination[row,column] to source[column,row]. All other words remain untouched.
                        int expected = relative >= 0 && row < columns && column < rows
                            ? source[prefix + (column * sourceStride) + row]
                            : sentinel;

                        Assert.Equal(expected, destination[i]);
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }
}
