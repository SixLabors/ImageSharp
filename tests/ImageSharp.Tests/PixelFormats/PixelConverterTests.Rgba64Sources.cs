// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.Utils;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

public abstract partial class PixelConverterTests
{
    /// <summary>
    /// Tests the SIMD kernels to the layouts with 16-bit channels at each vector width. Each kernel must give the same
    /// pixels as the scalar conversion of the destination pixel type.
    /// </summary>
    public class Rgba64Sources : PixelConverterTests
    {
        /// <summary>
        /// Each conversion from a 16-bit layout gives the values of the scalar conversion of the pixel type.
        /// </summary>
        [Fact]
        public void Convert16BitLayouts()
        {
            static void RunTest()
            {
                L16[] l16 = CreateAllValues<L16>();
                AssertConversion<L16, Rgba64>(l16, PixelConverter.Rgba64SourceKernels<PixelConverter.L16Rgba64Operator>.ToRgba64, Rgba64.FromL16);
                AssertConversion<L16, Rgb48>(l16, PixelConverter.Rgba64SourceKernels<PixelConverter.L16Rgba64Operator>.ToRgb48, Rgb48.FromL16);

                La32[] la32 = CreateRandom<La32>();
                AssertConversion<La32, Rgba64>(la32, PixelConverter.Rgba64SourceKernels<PixelConverter.La32Rgba64Operator>.ToRgba64, Rgba64.FromLa32);
                AssertConversion<La32, Rgb48>(la32, PixelConverter.Rgba64SourceKernels<PixelConverter.La32Rgba64Operator>.ToRgb48, Rgb48.FromLa32);

                Rgb48[] rgb48 = CreateRandom<Rgb48>();
                AssertConversion<Rgb48, Rgba64>(rgb48, PixelConverter.Rgba64SourceKernels<PixelConverter.Rgb48Rgba64Operator>.ToRgba64, Rgba64.FromRgb48);

                Rgba64[] rgba64 = CreateRandom<Rgba64>();
                AssertConversion<Rgba64, Rgb48>(rgba64, PixelConverter.Rgba64SourceKernels<PixelConverter.Rgba64Rgba64Operator>.ToRgb48, Rgb48.FromRgba64);
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTest, AllWidths);
        }

        /// <summary>
        /// The 8-bit to 16-bit scale gives the value of <see cref="ColorNumerics.From8BitTo16Bit(byte)"/> for each byte,
        /// also when the source is the last half of the destination storage, as the bulk conversions use it.
        /// </summary>
        [Fact]
        public void ScaleUnorm8To16()
        {
            static void RunTest()
            {
                byte[] source = new byte[RandomPixelCount];
                new Random(RandomPixelCount).NextBytes(source);
                ushort[] expected = [.. source.Select(ColorNumerics.From8BitTo16Bit)];

                ushort[] separate = new ushort[source.Length];
                PixelConverter.ScaleUnorm8To16(source, separate);
                Assert.Equal(expected, separate);

                // The source bytes occupy the last half of the destination storage, and the scale writes all of it.
                ushort[] shared = new ushort[source.Length];
                Span<byte> storage = MemoryMarshal.AsBytes(shared.AsSpan());
                Span<byte> staged = storage[source.Length..];
                source.CopyTo(staged);
                PixelConverter.ScaleUnorm8To16(staged, shared);
                Assert.Equal(expected, shared);
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTest, AllWidths);
        }
    }
}
