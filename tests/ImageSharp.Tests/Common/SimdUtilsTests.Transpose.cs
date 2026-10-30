// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Common;

public partial class SimdUtilsTests
{
    /// <summary>
    /// Checks complete-register kernels directly, including their portable paths when the current process disables hardware intrinsics.
    /// </summary>
    [Fact]
    public void Transpose_RegistersPreserveCoordinates()
    {
        static void RunTest()
        {
            ReadOnlySpan<int> counts = [4, 8, 16];
            foreach (int count in counts)
            {
                int[] source = new int[count * count];
                int[] destination = new int[source.Length + 2];
                destination.AsSpan().Fill(123);
                for (int i = 0; i < source.Length; i++)
                {
                    source[i] = unchecked((int)0x80000000) | (i * 131071);
                }

                ref int input = ref MemoryMarshal.GetArrayDataReference(source);
                ref int output = ref destination[1];
                if (count == 16)
                {
                    Vector512<int> row0 = Vector512.LoadUnsafe(ref input, 0U);
                    Vector512<int> row1 = Vector512.LoadUnsafe(ref input, 16U);
                    Vector512<int> row2 = Vector512.LoadUnsafe(ref input, 32U);
                    Vector512<int> row3 = Vector512.LoadUnsafe(ref input, 48U);
                    Vector512<int> row4 = Vector512.LoadUnsafe(ref input, 64U);
                    Vector512<int> row5 = Vector512.LoadUnsafe(ref input, 80U);
                    Vector512<int> row6 = Vector512.LoadUnsafe(ref input, 96U);
                    Vector512<int> row7 = Vector512.LoadUnsafe(ref input, 112U);
                    Vector512<int> row8 = Vector512.LoadUnsafe(ref input, 128U);
                    Vector512<int> row9 = Vector512.LoadUnsafe(ref input, 144U);
                    Vector512<int> row10 = Vector512.LoadUnsafe(ref input, 160U);
                    Vector512<int> row11 = Vector512.LoadUnsafe(ref input, 176U);
                    Vector512<int> row12 = Vector512.LoadUnsafe(ref input, 192U);
                    Vector512<int> row13 = Vector512.LoadUnsafe(ref input, 208U);
                    Vector512<int> row14 = Vector512.LoadUnsafe(ref input, 224U);
                    Vector512<int> row15 = Vector512.LoadUnsafe(ref input, 240U);

                    SimdUtils.Transpose(
                        ref row0,
                        ref row1,
                        ref row2,
                        ref row3,
                        ref row4,
                        ref row5,
                        ref row6,
                        ref row7,
                        ref row8,
                        ref row9,
                        ref row10,
                        ref row11,
                        ref row12,
                        ref row13,
                        ref row14,
                        ref row15);

                    row0.StoreUnsafe(ref output, 0U);
                    row1.StoreUnsafe(ref output, 16U);
                    row2.StoreUnsafe(ref output, 32U);
                    row3.StoreUnsafe(ref output, 48U);
                    row4.StoreUnsafe(ref output, 64U);
                    row5.StoreUnsafe(ref output, 80U);
                    row6.StoreUnsafe(ref output, 96U);
                    row7.StoreUnsafe(ref output, 112U);
                    row8.StoreUnsafe(ref output, 128U);
                    row9.StoreUnsafe(ref output, 144U);
                    row10.StoreUnsafe(ref output, 160U);
                    row11.StoreUnsafe(ref output, 176U);
                    row12.StoreUnsafe(ref output, 192U);
                    row13.StoreUnsafe(ref output, 208U);
                    row14.StoreUnsafe(ref output, 224U);
                    row15.StoreUnsafe(ref output, 240U);
                }
                else if (count == 8)
                {
                    Vector256<int> row0 = Vector256.LoadUnsafe(ref input, 0U);
                    Vector256<int> row1 = Vector256.LoadUnsafe(ref input, 8U);
                    Vector256<int> row2 = Vector256.LoadUnsafe(ref input, 16U);
                    Vector256<int> row3 = Vector256.LoadUnsafe(ref input, 24U);
                    Vector256<int> row4 = Vector256.LoadUnsafe(ref input, 32U);
                    Vector256<int> row5 = Vector256.LoadUnsafe(ref input, 40U);
                    Vector256<int> row6 = Vector256.LoadUnsafe(ref input, 48U);
                    Vector256<int> row7 = Vector256.LoadUnsafe(ref input, 56U);

                    SimdUtils.Transpose(ref row0, ref row1, ref row2, ref row3, ref row4, ref row5, ref row6, ref row7);
                    row0.StoreUnsafe(ref output, 0U);
                    row1.StoreUnsafe(ref output, 8U);
                    row2.StoreUnsafe(ref output, 16U);
                    row3.StoreUnsafe(ref output, 24U);
                    row4.StoreUnsafe(ref output, 32U);
                    row5.StoreUnsafe(ref output, 40U);
                    row6.StoreUnsafe(ref output, 48U);
                    row7.StoreUnsafe(ref output, 56U);
                }
                else
                {
                    Vector128<int> row0 = Vector128.LoadUnsafe(ref input, 0U);
                    Vector128<int> row1 = Vector128.LoadUnsafe(ref input, 4U);
                    Vector128<int> row2 = Vector128.LoadUnsafe(ref input, 8U);
                    Vector128<int> row3 = Vector128.LoadUnsafe(ref input, 12U);

                    SimdUtils.Transpose(ref row0, ref row1, ref row2, ref row3);
                    row0.StoreUnsafe(ref output, 0U);
                    row1.StoreUnsafe(ref output, 4U);
                    row2.StoreUnsafe(ref output, 8U);
                    row3.StoreUnsafe(ref output, 12U);
                }

                for (int row = 0; row < count; row++)
                {
                    for (int column = 0; column < count; column++)
                    {
                        Assert.Equal(source[(column * count) + row], destination[1 + (row * count) + column]);
                    }
                }

                Assert.Equal(123, destination[0]);
                Assert.Equal(123, destination[^1]);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks the separate four-by-four matrices in each 128-bit group of the wider floating-point registers.
    /// </summary>
    [Fact]
    public void Transpose4_PreservesLaneLocalMatrices()
    {
        static void RunTest()
        {
            ReadOnlySpan<int> counts = [4, 8, 16];
            foreach (int count in counts)
            {
                int[] source = new int[4 * count];
                int[] destination = new int[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    // These bit patterns include NaNs and signed values. No numeric conversion or arithmetic belongs in a transpose.
                    source[i] = unchecked((int)0xFF800000) | (i * 12345);
                }

                ref int input = ref MemoryMarshal.GetArrayDataReference(source);
                ref int output = ref MemoryMarshal.GetArrayDataReference(destination);
                if (count == 16)
                {
                    SimdUtils.Transpose4(
                        Vector512.LoadUnsafe(ref input, 0U).AsSingle(),
                        Vector512.LoadUnsafe(ref input, 16U).AsSingle(),
                        Vector512.LoadUnsafe(ref input, 32U).AsSingle(),
                        Vector512.LoadUnsafe(ref input, 48U).AsSingle(),
                        out Vector512<float> column0,
                        out Vector512<float> column1,
                        out Vector512<float> column2,
                        out Vector512<float> column3);

                    column0.AsInt32().StoreUnsafe(ref output, 0U);
                    column1.AsInt32().StoreUnsafe(ref output, 16U);
                    column2.AsInt32().StoreUnsafe(ref output, 32U);
                    column3.AsInt32().StoreUnsafe(ref output, 48U);
                }
                else if (count == 8)
                {
                    SimdUtils.Transpose4(
                        Vector256.LoadUnsafe(ref input, 0U).AsSingle(),
                        Vector256.LoadUnsafe(ref input, 8U).AsSingle(),
                        Vector256.LoadUnsafe(ref input, 16U).AsSingle(),
                        Vector256.LoadUnsafe(ref input, 24U).AsSingle(),
                        out Vector256<float> column0,
                        out Vector256<float> column1,
                        out Vector256<float> column2,
                        out Vector256<float> column3);

                    column0.AsInt32().StoreUnsafe(ref output, 0U);
                    column1.AsInt32().StoreUnsafe(ref output, 8U);
                    column2.AsInt32().StoreUnsafe(ref output, 16U);
                    column3.AsInt32().StoreUnsafe(ref output, 24U);
                }
                else
                {
                    SimdUtils.Transpose4(
                        Vector128.LoadUnsafe(ref input, 0U).AsSingle(),
                        Vector128.LoadUnsafe(ref input, 4U).AsSingle(),
                        Vector128.LoadUnsafe(ref input, 8U).AsSingle(),
                        Vector128.LoadUnsafe(ref input, 12U).AsSingle(),
                        out Vector128<float> column0,
                        out Vector128<float> column1,
                        out Vector128<float> column2,
                        out Vector128<float> column3);

                    column0.AsInt32().StoreUnsafe(ref output, 0U);
                    column1.AsInt32().StoreUnsafe(ref output, 4U);
                    column2.AsInt32().StoreUnsafe(ref output, 8U);
                    column3.AsInt32().StoreUnsafe(ref output, 12U);
                }

                for (int row = 0; row < 4; row++)
                {
                    for (int lane = 0; lane < count; lane++)
                    {
                        int groupStart = lane & ~3;
                        int sourceRow = lane & 3;
                        Assert.Equal(source[(sourceRow * count) + groupStart + row], destination[(row * count) + lane]);
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks rectangular coordinates, raw floating-point bits, strided padding and scalar tails with each hardware configuration.
    /// </summary>
    [Fact]
    public void Transpose_PreservesCoordinatesAndPadding()
    {
        static void RunTest()
        {
            ReadOnlySpan<(int Rows, int Columns)> sizes =
            [
                (0, 5), (5, 0), (1, 1), (3, 7), (4, 4), (8, 8), (16, 16), (16, 8), (8, 16), (16, 12), (12, 16), (17, 19), (33, 65)
            ];

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
