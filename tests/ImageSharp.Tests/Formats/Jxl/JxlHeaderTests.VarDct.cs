// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

public partial class JxlHeaderTests
{
    /// <summary>
    /// Checks transform-coded pixels through the registered public decoder with every SIMD width and scalar fallback.
    /// </summary>
    [Fact]
    public void DecodeVarDctMatchesNativeSrgb()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateVarDctPixels,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks final float pixels against original libjxl output, including extended RGB and an opaque alpha component.
    /// </summary>
    private static void ValidateVarDctPixels()
    {
        // Original libjxl revision 91837342b24034078780b687f57f670a7def8153 decoded these unchanged files
        // with djxl --num_threads=0 --color_space=RGB_D65_SRG_Rel_SRG --bits_per_sample=32.
        // Each uint is the IEEE float bit pattern in row-major R,G,B order, after restoration and sRGB conversion.
        // Square contains one repeated RGB triple; the lossy input contains all nine triples below.
        ReadOnlySpan<uint> square = [0x3224F19BU, 0x3224F1A6U, 0x3224F1A4U];
        ReadOnlySpan<uint> lossy =
        [
            0x3F801323U, 0xBD1F0788U, 0x3CBE2698U, 0xBDD46C8EU, 0x3F806F4DU, 0x3D03312AU, 0xBD6F35B3U, 0xBDF56924U, 0x3F81E195U,
            0x3F00DF5BU, 0x3E72ED32U, 0x3E796675U, 0x3E6607E7U, 0x3F0214B5U, 0x3E7491A7U, 0x3E6BCFD8U, 0x3E59FA40U, 0x3F01F1A9U,
            0x3F81CA1BU, 0x3F822989U, 0x3F8388E8U, 0x3F022B9DU, 0x3F03C421U, 0x3F02F482U, 0xBC301371U, 0xBC92C65DU, 0xBC3F2EA7U
        ];

        foreach (string file in new[] { TestImages.Jxl.Square, TestImages.Jxl.LossyRgb3x3 })
        {
            bool isSquare = file == TestImages.Jxl.Square;
            using Image<RgbaVector> image = Image.Load<RgbaVector>(TestFile.Create(file).Bytes);
            Assert.Equal(isSquare ? new Size(8, 8) : new Size(3, 3), image.Size);
            ReadOnlySpan<uint> expected = isSquare ? square : lossy;
            int sample = 0;
            for (int y = 0; y < image.Height; y++)
            {
                ReadOnlySpan<RgbaVector> row = image.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
                foreach (RgbaVector pixel in row)
                {
                    Vector4 actual = pixel.ToVector4();
                    for (int component = 0; component < 3; component++, sample++)
                    {
                        float reference = BitConverter.UInt32BitsToSingle(expected[isSquare ? component : sample]);

                        // CompandingTests establishes this shared power bound from 524,424 native input/exponent pairs.
                        // Scale it by magnitude for extended RGB. Earlier component checks use their tighter arithmetic bounds.
                        float bound = 0.0005F * MathF.Max(1F, MathF.Abs(reference));
                        Assert.True(MathF.Abs(actual[component] - reference) <= bound,
                            $"{file}, ({sample / 3 % image.Width},{y}), component {component}: {actual[component]:R}, native {reference:R}.");
                    }

                    Assert.Equal(1F, actual.W);
                }
            }
        }
    }
}
