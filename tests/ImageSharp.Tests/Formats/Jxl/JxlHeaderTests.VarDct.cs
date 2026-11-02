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
    /// Verifies real progressive, permuted-section and partial-alpha groups through the public decoder at every SIMD width.
    /// </summary>
    /// <param name="file">The unchanged input from the reference decoder's stored test files.</param>
    [Theory]
    [InlineData(TestImages.Jxl.PermutedSections)]
    [InlineData(TestImages.Jxl.ProgressiveAc)]
    [InlineData(TestImages.Jxl.NarrowEdgeGroup)]
    [InlineData(TestImages.Jxl.OddSizeUpsampling)]
    [InlineData(TestImages.Jxl.UpsamplingWithAlpha)]
    public void DecodeVarDctGroupsMatchNative(string file)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateVarDctGroupPixels,
            file,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks native float output at AC/DC group boundaries, partial frame edges and alpha discontinuities.
    /// </summary>
    /// <param name="file">The original stored image.</param>
    private static void ValidateVarDctGroupPixels(string file)
    {
        using Image<RgbaVector> image = Image.Load<RgbaVector>(TestFile.Create(file).Bytes);
        Size expectedSize = file switch
        {
            TestImages.Jxl.PermutedSections => new Size(2143, 1050),
            TestImages.Jxl.ProgressiveAc => new Size(2268, 1512),
            TestImages.Jxl.OddSizeUpsampling => new Size(257, 257),
            TestImages.Jxl.UpsamplingWithAlpha => new Size(800, 600),
            _ => new Size(513, 321)
        };

        Assert.Equal(expectedSize, image.Size);
        foreach ((int x, int y, uint r, uint g, uint b, uint a) in NativeVarDctGroupPoints(file))
        {
            Vector4 actual = image[x, y].ToVector4();
            Vector4 expected = new(
                BitConverter.UInt32BitsToSingle(r), BitConverter.UInt32BitsToSingle(g),
                BitConverter.UInt32BitsToSingle(b), BitConverter.UInt32BitsToSingle(a));

            // The shared power approximation has the same established bound as CompandingTests.
            // Alpha bypasses that approximation, so its bound permits only float normalization rounding.
            for (int component = 0; component < 3; component++)
            {
                float bound = 0.0005F * MathF.Max(1F, MathF.Abs(expected[component]));
                Assert.True(MathF.Abs(actual[component] - expected[component]) <= bound,
                    $"{file}, ({x},{y}), component {component}: {actual[component]:R}, native {expected[component]:R}.");
            }

            // The pixel contract bounds final alpha to zero through one, after interpolation.
            // The alpha-upsample references disagree by at most 4.76837e-7; round only that case upward to 5e-7.
            float expectedAlpha = Math.Clamp(expected.W, 0F, 1F);
            float alphaBound = file == TestImages.Jxl.UpsamplingWithAlpha ? 0.0000005F : 0.0000001F;
            Assert.InRange(MathF.Abs(actual.W - expectedAlpha), 0F, alphaBound);
        }
    }

    /// <summary>
    /// Gets independently decoded sRGB float samples at the stored inputs' group and image boundaries.
    /// </summary>
    /// <param name="file">The stored reference input.</param>
    /// <returns>The coordinates and exact IEEE float bits in R, G, B, alpha order.</returns>
    /// <remarks>
    /// Original libjxl revision 91837342b24034078780b687f57f670a7def8153 generated these values with
    /// djxl --num_threads=0 --bits_per_sample=32 --color_space=RGB_D65_SRG_Per_SRG.
    /// Original jxl-rs revision d7a831fae15b903f1b56a76668030c19ac07bb6f independently decoded these original files
    /// with --num-threads 1 --high-precision --data-type f32 and its sRGB output selection.
    /// Maximum full-image RGB disagreements are 1.35e-5, 1.20e-5 and 4.25e-5, respectively. Alpha agrees exactly.
    /// The odd-size upsampled file disagrees by at most 4.26e-6. The alpha-upsampled file differs by at most
    /// 4.42e-5 in RGB and 4.77e-7 in alpha. Its recorded raw alpha remains unclipped, as supplied by both reference decoders.
    /// Coordinates cover AC seams at 255/256, DC seams at 2047/2048, upsampling phases and final partial groups.
    /// </remarks>
    private static (int X, int Y, uint R, uint G, uint B, uint A)[] NativeVarDctGroupPoints(string file) => file switch
    {
        TestImages.Jxl.PermutedSections =>
        [
            (0, 0, 0x3C174175U, 0x38ACB656U, 0x3F7EB0DCU, 0x3F800000U),
            (255, 255, 0x3E7860E0U, 0xBB0813ACU, 0x3F4203C7U, 0x3F800000U),
            (256, 255, 0x3E7860E0U, 0xBB0813ACU, 0x3F4203C7U, 0x3F800000U),
            (255, 256, 0x3E7AFBCAU, 0xBB0A03E6U, 0x3F416948U, 0x3F800000U),
            (256, 256, 0x3E7AFBCAU, 0xBB0A03E6U, 0x3F416948U, 0x3F800000U),
            (2047, 511, 0x3EF94DCFU, 0x3AA7764AU, 0x3F033E98U, 0x3F800000U),
            (2048, 511, 0x3EF9616FU, 0x3AE4879AU, 0x3F033E0EU, 0x3F800000U),
            (2047, 512, 0x3EFA1FC6U, 0x3A88674BU, 0x3F02E371U, 0x3F800000U),
            (2048, 512, 0x3EFA330EU, 0x3B3DEF60U, 0x3F02DF3EU, 0x3F800000U),
            (2142, 0, 0x3A71077CU, 0xBBFE44E1U, 0x3F7FC462U, 0x3F800000U),
            (2142, 1049, 0x3F7FCAB4U, 0x3A147D60U, 0x3C6B391CU, 0x3F800000U),
        ],
        TestImages.Jxl.ProgressiveAc =>
        [
            (0, 0, 0x3ED8FCA5U, 0x3ED1A8DBU, 0x3ECB6683U, 0x3F800000U),
            (255, 255, 0x3EDC5E27U, 0x3EB5115EU, 0x3E9F7F45U, 0x3F800000U),
            (256, 255, 0x3ED8EE37U, 0x3EB23B40U, 0x3E9F5AA0U, 0x3F800000U),
            (255, 256, 0x3EE15542U, 0x3EB956C4U, 0x3EA3BCDBU, 0x3F800000U),
            (256, 256, 0x3EDDF390U, 0x3EB644FCU, 0x3EA2296AU, 0x3F800000U),
            (2047, 511, 0x3F06341FU, 0x3F3483F8U, 0x3F04DA11U, 0x3F800000U),
            (2048, 511, 0x3F07B838U, 0x3F34AAC6U, 0x3F041908U, 0x3F800000U),
            (2047, 512, 0x3F0177ECU, 0x3F31CBADU, 0x3F01BC65U, 0x3F800000U),
            (2048, 512, 0x3F03AF3AU, 0x3F33D62DU, 0x3F00BC3EU, 0x3F800000U),
            (2267, 0, 0x3F2BF95DU, 0x3F2B0F92U, 0x3F268B8FU, 0x3F800000U),
            (2267, 1511, 0x3EFB7863U, 0x3EBB50FBU, 0x3E880CE7U, 0x3F800000U),
        ],
        TestImages.Jxl.NarrowEdgeGroup =>
        [
            (0, 0, 0xB9C4D466U, 0xBBF13321U, 0x3F00E67EU, 0x3F800000U),
            (255, 255, 0x3EFD5826U, 0x3F4AB782U, 0x3F003993U, 0x3F018182U),
            (256, 255, 0x3EFF40E1U, 0x3F4AA790U, 0x3F006879U, 0x3F008081U),
            (255, 256, 0x3EFCA1C1U, 0x3F4C1663U, 0x3F008A28U, 0x3F008081U),
            (256, 256, 0x3EFF05A2U, 0x3F4C0718U, 0x3F00A534U, 0x3F800000U),
            (512, 0, 0x3F7ED70DU, 0x3C1CF211U, 0x3F015B92U, 0x3F800000U),
            (512, 320, 0x3F7ECEBBU, 0x3F7EA772U, 0x3F000932U, 0x3F3FBFC0U),
        ],
        TestImages.Jxl.OddSizeUpsampling =>
        [
            (0, 0, 0x3F24F9B4U, 0x3F1A23D0U, 0x3EB7EA90U, 0x3F800000U),
            (1, 0, 0x3F24F9B4U, 0x3F1A23D0U, 0x3EB7EA90U, 0x3F800000U),
            (0, 1, 0x3F24F9B4U, 0x3F1A23D0U, 0x3EB7EA90U, 0x3F800000U),
            (1, 1, 0x3F24F9B4U, 0x3F1A23D0U, 0x3EB7EA90U, 0x3F800000U),
            (127, 127, 0x3F063235U, 0x3F17FF7BU, 0x3F3C5BC2U, 0x3F800000U),
            (128, 127, 0x3F0DBC42U, 0x3F22B9E5U, 0x3F4C648FU, 0x3F800000U),
            (127, 128, 0x3F02B693U, 0x3F13D15EU, 0x3F39EF47U, 0x3F800000U),
            (128, 128, 0x3F1077D7U, 0x3F208CF1U, 0x3F45C0E4U, 0x3F800000U),
            (255, 255, 0x3F3CAF54U, 0x3F488C6FU, 0x3F5480A0U, 0x3F800000U),
            (256, 255, 0x3F3A4B20U, 0x3F45A392U, 0x3F528815U, 0x3F800000U),
            (255, 256, 0x3F394491U, 0x3F418AD6U, 0x3F49EE4BU, 0x3F800000U),
            (256, 256, 0x3F3CB4E3U, 0x3F47CB03U, 0x3F500638U, 0x3F800000U),
        ],
        TestImages.Jxl.UpsamplingWithAlpha =>
        [
            (284, 28, 0x3EF75CDDU, 0x3EF75CE7U, 0x3F6C5B57U, 0x3E536006U),
            (285, 28, 0x3EFC939CU, 0x3EFC93A1U, 0x3F63F02DU, 0x3E3B6913U),
            (286, 28, 0x3F017806U, 0x3F017806U, 0x3F58BD5BU, 0x3E273D44U),
            (287, 28, 0x3F03DA3AU, 0x3F03DA3DU, 0x3F483E15U, 0x3E0DAC73U),
            (284, 29, 0x3F02FB30U, 0x3F02FB33U, 0x3F739F59U, 0x3E9E9AC6U),
            (285, 29, 0x3F097755U, 0x3F09775AU, 0x3F712D0DU, 0x3E91C277U),
            (286, 29, 0x3F1001A7U, 0x3F1001ADU, 0x3F6E84C8U, 0x3E85F64EU),
            (287, 29, 0x3F16254FU, 0x3F162552U, 0x3F68C0BFU, 0x3E6BA91DU),
            (284, 30, 0x3EFFA9E6U, 0x3EFFA9E8U, 0x3F74BEF2U, 0x3ED33F26U),
            (285, 30, 0x3F0A010EU, 0x3F0A0113U, 0x3F70F6F6U, 0x3EC65D3CU),
            (286, 30, 0x3F141267U, 0x3F141268U, 0x3F6CC415U, 0x3EB9AEC7U),
            (287, 30, 0x3F1DD428U, 0x3F1DD428U, 0x3F68393AU, 0x3EA752F9U),
            (284, 31, 0x3EEE6B1FU, 0x3EEE6B25U, 0x3F779D96U, 0x3F05FA53U),
            (285, 31, 0x3F028344U, 0x3F028346U, 0x3F73CACAU, 0x3EFFFDBCU),
            (286, 31, 0x3F0D7744U, 0x3F0D774AU, 0x3F6F9300U, 0x3EF36C16U),
            (287, 31, 0x3F188BBBU, 0x3F188BC1U, 0x3F6ABE56U, 0x3EE0303CU),
            (0, 0, 0xB8216AAFU, 0xB8216ABEU, 0xB8BFAC7FU, 0x00000000U),
            (799, 0, 0x3224F19BU, 0x3224F1A6U, 0x3224F1A4U, 0x00000000U),
            (0, 599, 0xBA77893AU, 0xBA778943U, 0xBB456EA5U, 0x00000000U),
            (799, 599, 0x3224F19BU, 0x3224F1A6U, 0x3224F1A4U, 0x00000000U),
            (399, 299, 0x3F7BCAEDU, 0x3F672251U, 0x3F6938F5U, 0x3F7F0734U),
            (400, 299, 0x3F7A1BF4U, 0x3F5E86C2U, 0x3F6076E2U, 0x3F7E6776U),
            (399, 300, 0x3F74A052U, 0x3F4FB308U, 0x3F5131A6U, 0x3F7D8C67U),
            (400, 300, 0x3F718860U, 0x3F43D98EU, 0x3F451D67U, 0x3F7BF1DDU),
            (309, 177, 0x3EEFF346U, 0x3EEFF82EU, 0x3F584947U, 0x3F80A256U),
        ],
        _ => [],
    };

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
