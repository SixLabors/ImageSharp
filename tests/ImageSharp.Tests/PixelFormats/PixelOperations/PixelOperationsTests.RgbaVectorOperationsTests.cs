// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations;

public partial class PixelOperationsTests
{
    /// <summary>
    /// Checks every named RgbaVector source conversion and its generic equivalent across SIMD widths and scalar tails.
    /// </summary>
    [Fact]
    public void RgbaVectorFromMethodsMatchSinglePixels() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        ValidateRgbaVectorFromMethods,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Exercises all generated source overloads through the existing pixel-operation test buffers.
    /// </summary>
    private static void ValidateRgbaVectorFromMethods()
    {
        AssertRgbaVectorFrom<Argb32>(RgbaVector.FromArgb32, (s, d) => PixelOperations<RgbaVector>.Instance.FromArgb32(Configuration.Default, s, d.GetSpan()));
        AssertRgbaVectorFrom<Abgr32>(RgbaVector.FromAbgr32, (s, d) => PixelOperations<RgbaVector>.Instance.FromAbgr32(Configuration.Default, s, d.GetSpan()));
        AssertRgbaVectorFrom<Bgr24>(RgbaVector.FromBgr24, (s, d) => PixelOperations<RgbaVector>.Instance.FromBgr24(Configuration.Default, s, d.GetSpan()));
        AssertRgbaVectorFrom<Bgra32>(RgbaVector.FromBgra32, (s, d) => PixelOperations<RgbaVector>.Instance.FromBgra32(Configuration.Default, s, d.GetSpan()));
        AssertRgbaVectorFrom<L8>(RgbaVector.FromL8, (s, d) => PixelOperations<RgbaVector>.Instance.FromL8(Configuration.Default, s, d.GetSpan()));
        AssertRgbaVectorFrom<L16>(RgbaVector.FromL16, (s, d) => PixelOperations<RgbaVector>.Instance.FromL16(Configuration.Default, s, d.GetSpan()));
        AssertRgbaVectorFrom<La16>(RgbaVector.FromLa16, (s, d) => PixelOperations<RgbaVector>.Instance.FromLa16(Configuration.Default, s, d.GetSpan()));
        AssertRgbaVectorFrom<La32>(RgbaVector.FromLa32, (s, d) => PixelOperations<RgbaVector>.Instance.FromLa32(Configuration.Default, s, d.GetSpan()));
        AssertRgbaVectorFrom<Rgb24>(RgbaVector.FromRgb24, (s, d) => PixelOperations<RgbaVector>.Instance.FromRgb24(Configuration.Default, s, d.GetSpan()));
        AssertRgbaVectorFrom<Rgba32>(RgbaVector.FromRgba32, (s, d) => PixelOperations<RgbaVector>.Instance.FromRgba32(Configuration.Default, s, d.GetSpan()));
        AssertRgbaVectorFrom<Rgb48>(RgbaVector.FromRgb48, (s, d) => PixelOperations<RgbaVector>.Instance.FromRgb48(Configuration.Default, s, d.GetSpan()));
        AssertRgbaVectorFrom<Rgba64>(RgbaVector.FromRgba64, (s, d) => PixelOperations<RgbaVector>.Instance.FromRgba64(Configuration.Default, s, d.GetSpan()));
        AssertRgbaVectorFrom<Bgra5551>(
            RgbaVector.FromBgra5551,
            (s, d) => PixelOperations<RgbaVector>.Instance.FromBgra5551(Configuration.Default, s, d.GetSpan()));

        AssertRgbaVectorFrom<Argb32P>(
            RgbaVector.FromArgb32P,
            (s, d) => PixelOperations<RgbaVector>.Instance.FromArgb32P(Configuration.Default, s, d.GetSpan()));

        AssertRgbaVectorFrom<Abgr32P>(
            RgbaVector.FromAbgr32P,
            (s, d) => PixelOperations<RgbaVector>.Instance.FromAbgr32P(Configuration.Default, s, d.GetSpan()));

        AssertRgbaVectorFrom<Bgra32P>(
            RgbaVector.FromBgra32P,
            (s, d) => PixelOperations<RgbaVector>.Instance.FromBgra32P(Configuration.Default, s, d.GetSpan()));

        AssertRgbaVectorFrom<Rgba32P>(
            RgbaVector.FromRgba32P,
            (s, d) => PixelOperations<RgbaVector>.Instance.FromRgba32P(Configuration.Default, s, d.GetSpan()));
    }

    /// <summary>
    /// Checks a named conversion and generic conversion against the single-pixel extraction contract.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="convertPixel">The corresponding single-pixel conversion.</param>
    /// <param name="convert">The named bulk conversion.</param>
    private static void AssertRgbaVectorFrom<TPixel>(Func<TPixel, RgbaVector> convertPixel, Action<TPixel[], IMemoryOwner<RgbaVector>> convert)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Include complete 128-, 256- and 512-bit groups, scalar tails, and a row beyond the generic API's former 1024-pixel slice.
        foreach (int count in new[] { 0, 1, 4, 8, 16, 17, 33, 65, 1025 })
        {
            TPixel[] source = PixelOperationsTests<TPixel>.CreatePixelTestData(count);
            RgbaVector[] expected = new RgbaVector[count];

            for (int i = 0; i < count; i++)
            {
                // The existing single-pixel conversion defines channel order, alpha handling, and floating-point rounding.
                expected[i] = convertPixel(source[i]);
            }

            PixelOperationsTests<RgbaVector>.TestOperation(source, expected, convert);
            PixelOperationsTests<RgbaVector>.TestOperation(
                source,
                expected,
                (s, d) => PixelOperations<RgbaVector>.Instance.From(Configuration.Default, s, d.GetSpan()));
        }
    }
}
