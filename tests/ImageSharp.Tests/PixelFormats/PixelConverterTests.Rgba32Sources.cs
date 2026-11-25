// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.Utils;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

public abstract partial class PixelConverterTests
{
    /// <summary>
    /// Tests the SIMD kernels from the luminance and 16-bit layouts to the layouts with 8-bit channels at each vector
    /// width. Each kernel must give the same pixels as the scalar conversion of the destination pixel type.
    /// </summary>
    public class Rgba32Sources : PixelConverterTests
    {
        /// <summary>
        /// Each conversion from a luminance layout gives the values of the scalar conversion of the pixel type. The 16-bit
        /// layout uses each of the 65536 values, so each value of the 16-bit to 8-bit scale is checked.
        /// </summary>
        [Fact]
        public void ConvertLuminanceLayouts()
        {
            static void RunTest()
            {
                AssertToByteLayouts<PixelConverter.L8Rgba32Operator, L8>(
                    CreateRandom<L8>(),
                    Rgba32.FromL8,
                    Argb32.FromL8,
                    Bgra32.FromL8,
                    Abgr32.FromL8,
                    Rgb24.FromL8,
                    Bgr24.FromL8);

                AssertToByteLayouts<PixelConverter.La16Rgba32Operator, La16>(
                    CreateAllValues<La16>(),
                    Rgba32.FromLa16,
                    Argb32.FromLa16,
                    Bgra32.FromLa16,
                    Abgr32.FromLa16,
                    Rgb24.FromLa16,
                    Bgr24.FromLa16);

                AssertToByteLayouts<PixelConverter.L16Rgba32Operator, L16>(
                    CreateAllValues<L16>(),
                    Rgba32.FromL16,
                    Argb32.FromL16,
                    Bgra32.FromL16,
                    Abgr32.FromL16,
                    Rgb24.FromL16,
                    Bgr24.FromL16);

                AssertToByteLayouts<PixelConverter.La32Rgba32Operator, La32>(
                    CreateRandom<La32>(),
                    Rgba32.FromLa32,
                    Argb32.FromLa32,
                    Bgra32.FromLa32,
                    Abgr32.FromLa32,
                    Rgb24.FromLa32,
                    Bgr24.FromLa32);
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTest, AllWidths);
        }

        /// <summary>
        /// Each conversion from a 16-bit color layout gives the values of the scalar conversion of the pixel type.
        /// </summary>
        [Fact]
        public void Convert16BitColorLayouts()
        {
            static void RunTest()
            {
                AssertToByteLayouts<PixelConverter.Rgb48Rgba32Operator, Rgb48>(
                    CreateRandom<Rgb48>(),
                    Rgba32.FromRgb48,
                    Argb32.FromRgb48,
                    Bgra32.FromRgb48,
                    Abgr32.FromRgb48,
                    Rgb24.FromRgb48,
                    Bgr24.FromRgb48);

                AssertToByteLayouts<PixelConverter.Rgba64Rgba32Operator, Rgba64>(
                    CreateRandom<Rgba64>(),
                    Rgba32.FromRgba64,
                    Argb32.FromRgba64,
                    Bgra32.FromRgba64,
                    Abgr32.FromRgba64,
                    Rgb24.FromRgba64,
                    Bgr24.FromRgba64);
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTest, AllWidths);
        }

        /// <summary>
        /// Converts the source to each layout with 8-bit channels with the kernel of <typeparamref name="TOperator"/>, and
        /// compares each result with the scalar conversion of the destination type.
        /// </summary>
        /// <typeparam name="TOperator">The source operator.</typeparam>
        /// <typeparam name="TSource">The source pixel type.</typeparam>
        /// <param name="source">The source pixels.</param>
        /// <param name="rgba32">The scalar conversion to <see cref="Rgba32"/>.</param>
        /// <param name="argb32">The scalar conversion to <see cref="Argb32"/>.</param>
        /// <param name="bgra32">The scalar conversion to <see cref="Bgra32"/>.</param>
        /// <param name="abgr32">The scalar conversion to <see cref="Abgr32"/>.</param>
        /// <param name="rgb24">The scalar conversion to <see cref="Rgb24"/>.</param>
        /// <param name="bgr24">The scalar conversion to <see cref="Bgr24"/>.</param>
        private static void AssertToByteLayouts<TOperator, TSource>(
            TSource[] source,
            Func<TSource, Rgba32> rgba32,
            Func<TSource, Argb32> argb32,
            Func<TSource, Bgra32> bgra32,
            Func<TSource, Abgr32> abgr32,
            Func<TSource, Rgb24> rgb24,
            Func<TSource, Bgr24> bgr24)
            where TOperator : PixelConverter.IRgba32SourceOperator
            where TSource : unmanaged
        {
            AssertConversion(source, PixelConverter.Rgba32SourceKernels<TOperator>.To4Bytes<XYZWShuffle4>, rgba32);
            AssertConversion(source, PixelConverter.Rgba32SourceKernels<TOperator>.To4Bytes<WXYZShuffle4>, argb32);
            AssertConversion(source, PixelConverter.Rgba32SourceKernels<TOperator>.To4Bytes<ZYXWShuffle4>, bgra32);
            AssertConversion(source, PixelConverter.Rgba32SourceKernels<TOperator>.To4Bytes<WZYXShuffle4>, abgr32);
            AssertConversion(source, PixelConverter.Rgba32SourceKernels<TOperator>.To3Bytes<XYZWShuffle4>, rgb24);
            AssertConversion(source, PixelConverter.Rgba32SourceKernels<TOperator>.To3Bytes<ZYXWShuffle4>, bgr24);
        }
    }
}
