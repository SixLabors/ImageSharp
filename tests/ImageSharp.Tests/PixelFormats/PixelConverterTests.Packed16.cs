// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.Utils;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

public abstract partial class PixelConverterTests
{
    /// <summary>
    /// Tests the SIMD kernels of the 16-bit layouts at each vector width. Each kernel must give the same pixels as the
    /// scalar conversion of the pixel type.
    /// </summary>
    public class Packed16 : PixelConverterTests
    {
        /// <summary>
        /// The pixel count of the random sources. At each vector width, 1027 pixels fill the vector loop more than once
        /// and leave a scalar remainder.
        /// </summary>
        private const int RandomPixelCount = 1027;

        /// <summary>
        /// The runs at 512, 256 and 128 bits and without SIMD.
        /// </summary>
        private const HwIntrinsics AllWidths =
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableHWIntrinsic;

        /// <summary>
        /// Converts packed pixel bytes.
        /// </summary>
        /// <param name="source">The source bytes.</param>
        /// <param name="destination">The destination bytes.</param>
        private delegate void ConvertBytes(ReadOnlySpan<byte> source, Span<byte> destination);

        /// <summary>
        /// Each SIMD pack from a byte layout to a 16-bit layout gives the values of the scalar conversion of the pixel type.
        /// </summary>
        [Fact]
        public void PackFromByteLayouts()
        {
            static void RunTest()
            {
                AssertConversion<Rgba32, Bgr565>(CreateRandom<Rgba32>(), PixelConverter.FromRgba32.ToBgr565, Bgr565.FromRgba32);
                AssertConversion<Argb32, Bgr565>(CreateRandom<Argb32>(), PixelConverter.FromArgb32.ToBgr565, Bgr565.FromArgb32);
                AssertConversion<Bgra32, Bgr565>(CreateRandom<Bgra32>(), PixelConverter.FromBgra32.ToBgr565, Bgr565.FromBgra32);
                AssertConversion<Abgr32, Bgr565>(CreateRandom<Abgr32>(), PixelConverter.FromAbgr32.ToBgr565, Bgr565.FromAbgr32);
                AssertConversion<Rgb24, Bgr565>(CreateRandom<Rgb24>(), PixelConverter.FromRgb24.ToBgr565, Bgr565.FromRgb24);
                AssertConversion<Bgr24, Bgr565>(CreateRandom<Bgr24>(), PixelConverter.FromBgr24.ToBgr565, Bgr565.FromBgr24);

                AssertConversion<Rgba32, Bgra5551>(CreateRandom<Rgba32>(), PixelConverter.FromRgba32.ToBgra5551, Bgra5551.FromRgba32);
                AssertConversion<Argb32, Bgra5551>(CreateRandom<Argb32>(), PixelConverter.FromArgb32.ToBgra5551, Bgra5551.FromArgb32);
                AssertConversion<Bgra32, Bgra5551>(CreateRandom<Bgra32>(), PixelConverter.FromBgra32.ToBgra5551, Bgra5551.FromBgra32);
                AssertConversion<Abgr32, Bgra5551>(CreateRandom<Abgr32>(), PixelConverter.FromAbgr32.ToBgra5551, Bgra5551.FromAbgr32);
                AssertConversion<Rgb24, Bgra5551>(CreateRandom<Rgb24>(), PixelConverter.FromRgb24.ToBgra5551, Bgra5551.FromRgb24);
                AssertConversion<Bgr24, Bgra5551>(CreateRandom<Bgr24>(), PixelConverter.FromBgr24.ToBgra5551, Bgra5551.FromBgr24);
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTest, AllWidths);
        }

        /// <summary>
        /// Each SIMD unpack from a 16-bit layout to a byte layout gives the values of the scalar conversion of the pixel type.
        /// </summary>
        [Fact]
        public void UnpackToByteLayouts()
        {
            static void RunTest()
            {
                Bgr565[] bgr565 = CreateAllValues<Bgr565>();
                AssertConversion<Bgr565, Rgba32>(bgr565, PixelConverter.FromBgr565.ToRgba32, Rgba32.FromBgr565);
                AssertConversion<Bgr565, Argb32>(bgr565, PixelConverter.FromBgr565.ToArgb32, Argb32.FromBgr565);
                AssertConversion<Bgr565, Bgra32>(bgr565, PixelConverter.FromBgr565.ToBgra32, Bgra32.FromBgr565);
                AssertConversion<Bgr565, Abgr32>(bgr565, PixelConverter.FromBgr565.ToAbgr32, Abgr32.FromBgr565);
                AssertConversion<Bgr565, Rgb24>(bgr565, PixelConverter.FromBgr565.ToRgb24, Rgb24.FromBgr565);
                AssertConversion<Bgr565, Bgr24>(bgr565, PixelConverter.FromBgr565.ToBgr24, Bgr24.FromBgr565);

                Bgra5551[] bgra5551 = CreateAllValues<Bgra5551>();
                AssertConversion<Bgra5551, Rgba32>(bgra5551, PixelConverter.FromBgra5551.ToRgba32, Rgba32.FromBgra5551);
                AssertConversion<Bgra5551, Argb32>(bgra5551, PixelConverter.FromBgra5551.ToArgb32, Argb32.FromBgra5551);
                AssertConversion<Bgra5551, Bgra32>(bgra5551, PixelConverter.FromBgra5551.ToBgra32, Bgra32.FromBgra5551);
                AssertConversion<Bgra5551, Abgr32>(bgra5551, PixelConverter.FromBgra5551.ToAbgr32, Abgr32.FromBgra5551);
                AssertConversion<Bgra5551, Rgb24>(bgra5551, PixelConverter.FromBgra5551.ToRgb24, Rgb24.FromBgra5551);
                AssertConversion<Bgra5551, Bgr24>(bgra5551, PixelConverter.FromBgra5551.ToBgr24, Bgr24.FromBgra5551);
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTest, AllWidths);
        }

        /// <summary>
        /// Each SIMD conversion between the two 16-bit layouts gives the values of the scalar conversion of the pixel type.
        /// </summary>
        [Fact]
        public void ConvertBetween16BitLayouts()
        {
            static void RunTest()
            {
                AssertConversion<Bgra5551, Bgr565>(CreateAllValues<Bgra5551>(), PixelConverter.FromBgra5551.ToBgr565, Bgr565.FromBgra5551);
                AssertConversion<Bgr565, Bgra5551>(CreateAllValues<Bgr565>(), PixelConverter.FromBgr565.ToBgra5551, Bgra5551.FromBgr565);
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTest, AllWidths);
        }

        /// <summary>
        /// Converts the source with the kernel and with the scalar conversion, and compares the results.
        /// </summary>
        /// <typeparam name="TSource">The source pixel type.</typeparam>
        /// <typeparam name="TDestination">The destination pixel type.</typeparam>
        /// <param name="source">The source pixels.</param>
        /// <param name="convert">The kernel.</param>
        /// <param name="expected">The scalar conversion of one pixel.</param>
        private static void AssertConversion<TSource, TDestination>(
            TSource[] source,
            ConvertBytes convert,
            Func<TSource, TDestination> expected)
            where TSource : unmanaged
            where TDestination : unmanaged
        {
            TDestination[] actual = new TDestination[source.Length];
            convert(MemoryMarshal.AsBytes(source.AsSpan()), MemoryMarshal.AsBytes(actual.AsSpan()));

            Assert.Equal(source.Select(expected).ToArray(), actual);
        }

        /// <summary>
        /// Creates pixels from random bytes. The seed is constant, so each run uses the same pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <returns>The pixels.</returns>
        private static TPixel[] CreateRandom<TPixel>()
            where TPixel : unmanaged
        {
            byte[] bytes = new byte[RandomPixelCount * Unsafe.SizeOf<TPixel>()];
            new Random(RandomPixelCount).NextBytes(bytes);
            return MemoryMarshal.Cast<byte, TPixel>(bytes).ToArray();
        }

        /// <summary>
        /// Creates one pixel for each of the 65536 values of a 16-bit pixel type.
        /// </summary>
        /// <typeparam name="TPixel">The 16-bit pixel type.</typeparam>
        /// <returns>The pixels.</returns>
        private static TPixel[] CreateAllValues<TPixel>()
            where TPixel : unmanaged
        {
            ushort[] values = new ushort[ushort.MaxValue + 1];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (ushort)i;
            }

            return MemoryMarshal.Cast<ushort, TPixel>(values).ToArray();
        }
    }
}
