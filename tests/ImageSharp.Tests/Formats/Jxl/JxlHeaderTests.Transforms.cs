// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

public partial class JxlHeaderTests
{
    /// <summary>
    /// Verifies all inverse-transform strategies, DC insertion and strided output at each SIMD width and scalar fallback.
    /// </summary>
    [Fact]
    public void InverseTransformsMatchNativeAndRust()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateInverseTransforms,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks numeric transform inputs against independent libjxl and jxl-rs spatial outputs.
    /// </summary>
    private static void ValidateInverseTransforms()
    {
        // These physical dimensions come from the native strategy definitions. They are independent of the decoder's geometry tables.
        ReadOnlySpan<int> widths = [8, 8, 8, 8, 16, 32, 8, 16, 8, 32, 16, 32, 8, 8, 8, 8, 8, 8, 64, 32, 64, 128, 64, 128, 256, 128, 256];
        ReadOnlySpan<int> heights = [8, 8, 8, 8, 16, 32, 16, 8, 32, 8, 32, 16, 8, 8, 8, 8, 8, 8, 64, 64, 32, 128, 128, 64, 256, 256, 128];
        const float guard = -1234567F;
        for (byte strategy = 0; strategy < 27; strategy++)
        {
            int width = widths[strategy];
            int height = heights[strategy];
            int area = width * height;
            int coefficientStride = Math.Max(width, height);
            float[] coefficientStorage = new float[area + 2];
            coefficientStorage[0] = guard;
            coefficientStorage[^1] = guard;
            Span<float> coefficients = coefficientStorage.AsSpan(1, area);
            coefficients[1] = 0.125F;
            coefficients[coefficientStride] = -0.0625F;
            coefficients[^1] = 0.03125F;

            // Exact binary fractions avoid input rounding. Nonconstant DC values require a forward DC-grid transform and both LF resample factors.
            using Buffer2D<float> dc = MemoryAllocator.Default.Allocate2D<float>(width / 8, height / 8);
            for (int y = 0; y < dc.Height; y++)
            {
                Span<float> row = dc.DangerousGetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    row[x] = 0.25F + (x / 32F) - (y / 64F);
                }
            }

            int stride = width + 3;
            float[] outputStorage = new float[(stride * height) + 2];
            outputStorage.AsSpan().Fill(guard);
            float[] scratchStorage = new float[JxlInverseTransform.GetScratchLength(strategy) + 2];
            scratchStorage[0] = guard;
            scratchStorage[^1] = guard;
            Span<float> output = outputStorage.AsSpan(1, stride * height);
            JxlInverseTransform.Transform(strategy, coefficients, dc.GetRegion(), output, stride, scratchStorage.AsSpan(1, scratchStorage.Length - 2));

            Assert.Equal(guard, coefficientStorage[0]);
            Assert.Equal(guard, coefficientStorage[^1]);
            Assert.Equal(guard, outputStorage[0]);
            Assert.Equal(guard, outputStorage[^1]);
            Assert.Equal(guard, scratchStorage[0]);
            Assert.Equal(guard, scratchStorage[^1]);
            for (int y = 0; y < height; y++)
            {
                for (int x = width; x < stride; x++)
                {
                    Assert.Equal(guard, output[(y * stride) + x]);
                }
            }

            // The points cover both ends of the first rows, the center and the final row. Rectangles retain their physical orientation.
            ReadOnlySpan<int> points = [0, 1, width - 1, width, width + 1, ((height / 2) * width) + (width / 2), area - width, area - 2, area - 1];
            ReadOnlySpan<uint> native = NativeInverseTransformPoints(strategy);
            ReadOnlySpan<uint> rust = RustInverseTransformPoints(strategy);

            // Bounds are absolute errors in normalized component units. They are not integer storage levels.
            // Independent native/Rust differences reach 2.98e-8 through 32 pixels, 2.98e-7 through 128 pixels and 7.78e-6 at 256 pixels.
            // Round each bound upward to allow single-precision multiply-add variation. Identity and DCT2 use exact sums and differences.
            int longest = Math.Max(width, height);
            float bound = longest <= 32 ? 0.0000001F : longest <= 128 ? 0.000001F : 0.00001F;
            for (int i = 0; i < points.Length; i++)
            {
                int point = points[i];
                float actual = output[((point / width) * stride) + (point % width)];
                if (strategy is 1 or 2)
                {
                    Assert.Equal(native[i], BitConverter.SingleToUInt32Bits(actual));
                    Assert.Equal(rust[i], BitConverter.SingleToUInt32Bits(actual));
                }
                else
                {
                    float nativeValue = BitConverter.UInt32BitsToSingle(native[i]);
                    float rustValue = BitConverter.UInt32BitsToSingle(rust[i]);
                    Assert.True(MathF.Abs(actual - nativeValue) <= bound, $"Strategy {strategy}, point {point}: {actual:R}, native {nativeValue:R}.");
                    Assert.True(MathF.Abs(actual - rustValue) <= bound, $"Strategy {strategy}, point {point}: {actual:R}, Rust {rustValue:R}.");
                }
            }
        }

        ValidateNonconstantAfv();
    }

    /// <summary>
    /// Gets the nine spatial output samples recorded from the original libjxl SSE2 inverse transform.
    /// </summary>
    /// <param name="strategy">The raw transform strategy.</param>
    /// <returns>The sample bits in the documented spatial point order.</returns>
    /// <remarks>
    /// Generated with libjxl revision 91837342b24034078780b687f57f670a7def8153 by calling the original
    /// LowestFrequenciesFromDC and SSE2 TransformToPixels functions in lib/jxl/dec_transforms-inl.h.
    /// Inputs and sampled positions are the same as those documented on RustInverseTransformPoints.
    /// Values are IEEE 754 float bit patterns after spatial reconstruction, before color conversion or pixel packing.
    /// </remarks>
    private static ReadOnlySpan<uint> NativeInverseTransformPoints(byte strategy) => strategy switch
    {
        0 => [0x3EAD9A6CU, 0x3EAFAC96U, 0x3F01F80EU, 0x3E9B6704U, 0x3EAF814DU, 0x3E95F40CU, 0xBC4BF5D0U, 0x3E12C739U, 0x3E29AA52U],
        1 => [0x3EA00000U, 0x3EA00000U, 0x3EE00000U, 0x3EA00000U, 0x3EA00000U, 0x3E3E0000U, 0x3D800000U, 0x3E3E0000U, 0x3E5E0000U],
        2 => [0x3EA00000U, 0x3EA00000U, 0x3EE00000U, 0x3EA00000U, 0x3EA00000U, 0x3E400000U, 0x3D800000U, 0x3E200000U, 0x3E600000U],
        3 => [0x3EA00000U, 0x3EA00000U, 0x3EE00000U, 0x3EA00000U, 0x3EA00000U, 0x3E495F62U, 0x3D800000U, 0x3E295F62U, 0x3E495F62U],
        4 => [0x3E7C210CU, 0x3E7AA723U, 0x3E966BD6U, 0x3E7936A2U, 0x3E80B18AU, 0x3EA44EB4U, 0x3E61ED84U, 0x3E88DA43U, 0x3E8A8CE2U],
        5 => [0x3E7C0BF6U, 0x3E7C516CU, 0x3EB61581U, 0x3E7AFD20U, 0x3E7DB6B2U, 0x3EAC640EU, 0x3E43861AU, 0x3E996154U, 0x3E9A2177U],
        6 => [0x3E2CF065U, 0x3E35BE9EU, 0x3EAE042AU, 0x3E279C69U, 0x3E431249U, 0x3EA372ABU, 0x3E118515U, 0x3E99A47FU, 0x3EA2C11AU],
        7 => [0x3E1F7B51U, 0x3E1B97D6U, 0x3E4EFA29U, 0x3E28498AU, 0x3E370DB6U, 0x3EB14AE3U, 0x3EA749A0U, 0x3EBE943BU, 0x3EC17BA4U],
        8 => [0x3E2BF30FU, 0x3E371B4DU, 0x3EAE21F5U, 0x3E290FC9U, 0x3E3DA01DU, 0x3E9BB54FU, 0x3DE504D5U, 0x3E8AB3B7U, 0x3E92A34FU],
        9 => [0x3E1FA09CU, 0x3E1E11A2U, 0x3E876A6FU, 0x3E2AC8DAU, 0x3E32A1F6U, 0x3EC11CA2U, 0x3EA7F8BAU, 0x3EDF222EU, 0x3EE0CC89U],
        10 => [0x3E7B7190U, 0x3E7B2F11U, 0x3E9662B4U, 0x3E79C5B2U, 0x3E7E60DBU, 0x3E9C91C7U, 0x3E429D00U, 0x3E72FE34U, 0x3E752C08U],
        11 => [0x3E7C9431U, 0x3E7C3C9FU, 0x3EB63244U, 0x3E7AE131U, 0x3E7F6747U, 0x3EB420E1U, 0x3E62FDDFU, 0x3EA8F60DU, 0x3EAA04B4U],
        12 => [0x3EB8C543U, 0x3EAB418CU, 0x3C6757A0U, 0x3EB8C543U, 0x3EAB418CU, 0x3EAC02B5U, 0x3E9D9C67U, 0x3E993252U, 0x3EA26399U],
        13 => [0x3EB8C543U, 0x3EB8C543U, 0x3E9D9C67U, 0x3EAB418CU, 0x3EAB418CU, 0x3EAC02B5U, 0x3C6757A0U, 0x3E9A3B79U, 0x3EA26399U],
        14 => [0x3EA00000U, 0x3EA00000U, 0x3D800000U, 0x3EA00000U, 0x3EA00000U, 0x3EAC02B5U, 0x3E9D9C67U, 0x3E993252U, 0x3EA26399U],
        15 => [0x3D800000U, 0x3D800000U, 0x3EA00000U, 0x3D800000U, 0x3D800000U, 0x3EAC02B5U, 0x3E9D9C67U, 0x3E993252U, 0x3EA26399U],
        16 => [0x3EA26399U, 0x3E993252U, 0x3E9D9C67U, 0x3E9A3B79U, 0x3EB06CCAU, 0x3D800000U, 0x3EA00000U, 0x3D800000U, 0x3D800000U],
        17 => [0x3EA26399U, 0x3E993252U, 0x3E9D9C67U, 0x3E9A3B79U, 0x3EB06CCAU, 0x3EA00000U, 0x3D800000U, 0x3EA00000U, 0x3EA00000U],
        18 => [0x3E7C0F7AU, 0x3E7CC214U, 0x3EF5F2A9U, 0x3E7B7B00U, 0x3E7CCB4CU, 0x3EBC7989U, 0x3E0406F2U, 0x3EB98161U, 0x3EBA0221U],
        19 => [0x3E7BD728U, 0x3E7C6B4CU, 0x3EB60ED1U, 0x3E7B1B43U, 0x3E7CEA1EU, 0x3E9C64C3U, 0x3E03BAE8U, 0x3E731EB5U, 0x3E74504DU],
        20 => [0x3E7C3A6CU, 0x3E7CC59BU, 0x3EF5FE46U, 0x3E7B7A44U, 0x3E7D402AU, 0x3ECC78D1U, 0x3E43DC00U, 0x3ED9620EU, 0x3ED9F684U],
        21 => [0x3E7C114BU, 0x3E7CDEC6U, 0x3F3AF444U, 0x3E7B9BC0U, 0x3E7C90B2U, 0x3EDC7E86U, 0x3B854020U, 0x3EF98936U, 0x3EF9F9D1U],
        22 => [0x3E7C0152U, 0x3E7CC7A6U, 0x3EF5F084U, 0x3E7B81EAU, 0x3E7C9720U, 0x3E9C7A0BU, 0x3B82A340U, 0x3E731AC4U, 0x3E74088CU],
        23 => [0x3E7C1CFAU, 0x3E7CE098U, 0x3F3AF5F4U, 0x3E7B9C3AU, 0x3E7CAEBAU, 0x3EFC7DFFU, 0x3E041E52U, 0x3F1CC074U, 0x3F1CFB38U],
        24 => [0x3E7C11D1U, 0x3E7CE5FBU, 0x3F9D7979U, 0x3E7BA408U, 0x3E7C8210U, 0x3F0E3FDCU, 0xBE7BCD05U, 0x3F3CC594U, 0x3F3CFBDAU],
        25 => [0x3E7C0DAFU, 0x3E7CE019U, 0x3F3AF3FBU, 0x3E7B9D6EU, 0x3E7C8393U, 0x3E9C7ED9U, 0xBE7BD263U, 0x3E731880U, 0x3E73F4C9U],
        26 => [0x3E7C14CFU, 0x3E7CE682U, 0x3F9D79B1U, 0x3E7BA432U, 0x3E7C89A2U, 0x3F2E3FADU, 0x3B85FFA0U, 0x3F7CC486U, 0x3F7CFB6AU],
        _ => [],
    };

    /// <summary>
    /// Gets the nine spatial output samples recorded from the original jxl-rs scalar inverse transform.
    /// </summary>
    /// <param name="strategy">The raw transform strategy.</param>
    /// <returns>The sample bits in the documented spatial point order.</returns>
    /// <remarks>
    /// Generated with jxl-rs revision d7a831fae15b903f1b56a76668030c19ac07bb6f by calling the original
    /// jxl_transforms/src/transform.rs transform_to_pixels_impl with jxl_simd::ScalarDescriptor.
    /// For a W-by-H transform, the coefficient stride is max(W,H). All coefficients start at zero except
    /// coefficient[1] = 1/8, coefficient[stride] = -1/16 and coefficient[W*H-1] = 1/32.
    /// The spatial DC grid contains DC[x,y] = 1/4 + x/32 - y/64. The Rust function inserts its low frequencies
    /// and performs the inverse transform. These are its output bits, before color conversion or pixel packing.
    /// The nine row-major sample indices are 0, 1, W-1, W, W+1, (H/2)*W+W/2, W*H-W, W*H-2 and W*H-1.
    /// ValidateInverseTransforms constructs these same inputs and checks each recorded float value at those positions.
    /// </remarks>
    private static ReadOnlySpan<uint> RustInverseTransformPoints(byte strategy) => strategy switch
    {
        0 => [0x3EAD9A6CU, 0x3EAFAC96U, 0x3F01F80EU, 0x3E9B6704U, 0x3EAF814DU, 0x3E95F40CU, 0xBC4BF5D0U, 0x3E12C739U, 0x3E29AA52U],
        1 => [0x3EA00000U, 0x3EA00000U, 0x3EE00000U, 0x3EA00000U, 0x3EA00000U, 0x3E3E0000U, 0x3D800000U, 0x3E3E0000U, 0x3E5E0000U],
        2 => [0x3EA00000U, 0x3EA00000U, 0x3EE00000U, 0x3EA00000U, 0x3EA00000U, 0x3E400000U, 0x3D800000U, 0x3E200000U, 0x3E600000U],
        3 => [0x3EA00000U, 0x3EA00000U, 0x3EE00000U, 0x3EA00000U, 0x3EA00000U, 0x3E495F62U, 0x3D800000U, 0x3E295F62U, 0x3E495F62U],
        4 => [0x3E7C210CU, 0x3E7AA723U, 0x3E966BD7U, 0x3E7936A2U, 0x3E80B189U, 0x3EA44EB4U, 0x3E61ED82U, 0x3E88DA43U, 0x3E8A8CE3U],
        5 => [0x3E7C0BF6U, 0x3E7C516BU, 0x3EB61581U, 0x3E7AFD20U, 0x3E7DB6B1U, 0x3EAC640EU, 0x3E43861AU, 0x3E996154U, 0x3E9A2177U],
        6 => [0x3E2CF066U, 0x3E35BE9EU, 0x3EAE042AU, 0x3E279C69U, 0x3E431249U, 0x3EA372ABU, 0x3E118514U, 0x3E99A47EU, 0x3EA2C11AU],
        7 => [0x3E1F7B50U, 0x3E1B97D4U, 0x3E4EFA2BU, 0x3E284989U, 0x3E370DB4U, 0x3EB14AE3U, 0x3EA7499FU, 0x3EBE943BU, 0x3EC17BA4U],
        8 => [0x3E2BF30FU, 0x3E371B4DU, 0x3EAE21F5U, 0x3E290FC9U, 0x3E3DA01DU, 0x3E9BB54FU, 0x3DE504D5U, 0x3E8AB3B7U, 0x3E92A34FU],
        9 => [0x3E1FA09CU, 0x3E1E11A1U, 0x3E876A6FU, 0x3E2AC8DAU, 0x3E32A1F6U, 0x3EC11CA2U, 0x3EA7F8BAU, 0x3EDF222EU, 0x3EE0CC89U],
        10 => [0x3E7B718FU, 0x3E7B2F10U, 0x3E9662B5U, 0x3E79C5B1U, 0x3E7E60DAU, 0x3E9C91C7U, 0x3E429CFFU, 0x3E72FE34U, 0x3E752C0AU],
        11 => [0x3E7C9431U, 0x3E7C3C9FU, 0x3EB63244U, 0x3E7AE131U, 0x3E7F6746U, 0x3EB420E1U, 0x3E62FDDFU, 0x3EA8F60DU, 0x3EAA04B4U],
        12 => [0x3EB8C543U, 0x3EAB418CU, 0x3C6757A0U, 0x3EB8C543U, 0x3EAB418CU, 0x3EAC02B5U, 0x3E9D9C67U, 0x3E993252U, 0x3EA26399U],
        13 => [0x3EB8C543U, 0x3EB8C543U, 0x3E9D9C67U, 0x3EAB418CU, 0x3EAB418CU, 0x3EAC02B5U, 0x3C6757A0U, 0x3E9A3B79U, 0x3EA26399U],
        14 => [0x3EA00000U, 0x3EA00000U, 0x3D800000U, 0x3EA00000U, 0x3EA00000U, 0x3EAC02B5U, 0x3E9D9C67U, 0x3E993252U, 0x3EA26399U],
        15 => [0x3D800000U, 0x3D800000U, 0x3EA00000U, 0x3D800000U, 0x3D800000U, 0x3EAC02B5U, 0x3E9D9C67U, 0x3E993252U, 0x3EA26399U],
        16 => [0x3EA26399U, 0x3E993252U, 0x3E9D9C67U, 0x3E9A3B79U, 0x3EB06CCAU, 0x3D800000U, 0x3EA00000U, 0x3D800000U, 0x3D800000U],
        17 => [0x3EA26399U, 0x3E993252U, 0x3E9D9C67U, 0x3E9A3B79U, 0x3EB06CCAU, 0x3EA00000U, 0x3D800000U, 0x3EA00000U, 0x3EA00000U],
        18 => [0x3E7C0F7DU, 0x3E7CC218U, 0x3EF5F2A2U, 0x3E7B7B04U, 0x3E7CCB50U, 0x3EBC7989U, 0x3E0406FFU, 0x3EB98160U, 0x3EBA0220U],
        19 => [0x3E7BD724U, 0x3E7C6B46U, 0x3EB60ECFU, 0x3E7B1B3EU, 0x3E7CEA19U, 0x3E9C64C3U, 0x3E03BAECU, 0x3E731EBAU, 0x3E745052U],
        20 => [0x3E7C3A74U, 0x3E7CC5A4U, 0x3EF5FE42U, 0x3E7B7A4CU, 0x3E7D4033U, 0x3ECC78D1U, 0x3E43DC08U, 0x3ED9620AU, 0x3ED9F680U],
        21 => [0x3E7C1148U, 0x3E7CDEC3U, 0x3F3AF446U, 0x3E7B9BBDU, 0x3E7C90AFU, 0x3EDC7E86U, 0x3B853F00U, 0x3EF98938U, 0x3EF9F9D4U],
        22 => [0x3E7C015EU, 0x3E7CC7B2U, 0x3EF5F082U, 0x3E7B81F4U, 0x3E7C972CU, 0x3E9C7A15U, 0x3B82A3D0U, 0x3E731ABAU, 0x3E740880U],
        23 => [0x3E7C1CF0U, 0x3E7CE08DU, 0x3F3AF5F4U, 0x3E7B9C30U, 0x3E7CAEB0U, 0x3EFC7DFCU, 0x3E041E50U, 0x3F1CC077U, 0x3F1CFB3CU],
        24 => [0x3E7C127FU, 0x3E7CE6ABU, 0x3F9D7938U, 0x3E7BA4B5U, 0x3E7C82C0U, 0x3F0E3FDCU, 0xBE7BCAFBU, 0x3F3CC568U, 0x3F3CFBB0U],
        25 => [0x3E7C0CFBU, 0x3E7CDF65U, 0x3F3AF3D1U, 0x3E7B9CB9U, 0x3E7C82DFU, 0x3E9C7ED9U, 0xBE7BD1BBU, 0x3E731935U, 0x3E73F57DU],
        26 => [0x3E7C162EU, 0x3E7CE7E3U, 0x3F9D7987U, 0x3E7BA592U, 0x3E7C8B03U, 0x3F2E3FB2U, 0x3B862AC0U, 0x3F7CC42DU, 0x3F7CFB13U],
        _ => [],
    };

    /// <summary>
    /// Verifies signed coefficient widening, quantization bias, matrix scaling and correlation at all SIMD widths and scalar fallback.
    /// </summary>
    [Fact]
    public void DequantizeAcCoefficientsPreservesSignedValues()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateAcCoefficientDequantization,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks exact numeric dequantization cases for both coefficient storage widths.
    /// </summary>
    private static void ValidateAcCoefficientDequantization()
    {
        // For magnitude two, subtracting one-quarter divided by the coefficient gives exactly +/-1.875.
        // Unit coefficients use their component's bias instead. These binary fractions make the following scaling and correlation exact.
        ReadOnlySpan<short> shortValues = [short.MinValue, -2, -1, 0, 1, 2, short.MaxValue];
        ReadOnlySpan<float> shortX = [-24576F, -1.40625F, -0.53125F, 0F, 0.53125F, 1.40625F, 24575.25F];
        ReadOnlySpan<float> shortY = [-16384F, -0.9375F, -0.3125F, 0F, 0.3125F, 0.9375F, 16383.5F];
        ReadOnlySpan<float> shortB = [-28672F, -1.640625F, -0.796875F, 0F, 0.796875F, 1.640625F, 28671.125F];
        ValidateAcCoefficientStorage(shortValues, shortX, shortY, shortB);

        // Integer endpoints first round to +/-2^31 in float. Their tiny bias correction cannot change that float.
        // Integer absolute value must not be used: abs(int.MinValue) cannot be represented as a positive int.
        ReadOnlySpan<int> intValues = [int.MinValue, short.MinValue, -2, -1, 0, 1, 2, short.MaxValue, int.MaxValue];
        ReadOnlySpan<float> intX = [-1610612736F, -24576F, -1.40625F, -0.53125F, 0F, 0.53125F, 1.40625F, 24575.25F, 1610612736F];
        ReadOnlySpan<float> intY = [-1073741824F, -16384F, -0.9375F, -0.3125F, 0F, 0.3125F, 0.9375F, 16383.5F, 1073741824F];
        ReadOnlySpan<float> intB = [-1879048192F, -28672F, -1.640625F, -0.796875F, 0F, 0.796875F, 1.640625F, 28671.125F, 1879048192F];
        ValidateAcCoefficientStorage(intValues, intX, intY, intB);
    }

    /// <summary>
    /// Checks the complete bulk operation with 16-, 8-, 4-lane batches and a three-coefficient scalar remainder.
    /// </summary>
    /// <typeparam name="TCoefficient">The short or int coefficient storage.</typeparam>
    /// <param name="period">The repeated signed coefficient input.</param>
    /// <param name="expectedX">The independent exact X results for that period.</param>
    /// <param name="expectedY">The independent exact Y results for that period.</param>
    /// <param name="expectedB">The independent exact B results for that period.</param>
    private static void ValidateAcCoefficientStorage<TCoefficient>(
        ReadOnlySpan<TCoefficient> period,
        ReadOnlySpan<float> expectedX,
        ReadOnlySpan<float> expectedY,
        ReadOnlySpan<float> expectedB)
        where TCoefficient : unmanaged, IBinaryInteger<TCoefficient>
    {
        const int length = 31;
        const float guard = -1234567F;
        TCoefficient[] coefficients = new TCoefficient[length];
        for (int i = 0; i < length; i++)
        {
            coefficients[i] = period[i % period.Length];
        }

        float[] matrices = new float[3 * length];
        matrices.AsSpan(0, length).Fill(1F);
        matrices.AsSpan(length, length).Fill(2F);
        matrices.AsSpan(2 * length, length).Fill(0.5F);
        float[] output = new float[(3 * length) + 2];
        output[0] = guard;
        output[^1] = guard;
        JxlVarDctDecoder.DequantizeAcBlock<TCoefficient>(
            coefficients,
            coefficients,
            coefficients,
            matrices,
            output.AsSpan(1, 3 * length),
            new Vector3(0.5F, 0.25F, 2F),
            new Vector2(0.5F, -0.25F),
            new Vector4(0.75F, 0.625F, 0.875F, 0.25F));

        Assert.Equal(guard, output[0]);
        Assert.Equal(guard, output[^1]);
        for (int i = 0; i < length; i++)
        {
            int expected = i % period.Length;
            Assert.Equal(BitConverter.SingleToUInt32Bits(expectedX[expected]), BitConverter.SingleToUInt32Bits(output[i + 1]));
            Assert.Equal(BitConverter.SingleToUInt32Bits(expectedY[expected]), BitConverter.SingleToUInt32Bits(output[length + i + 1]));
            Assert.Equal(BitConverter.SingleToUInt32Bits(expectedB[expected]), BitConverter.SingleToUInt32Bits(output[(2 * length) + i + 1]));
        }
    }

    /// <summary>
    /// Checks all sixteen nonconstant AFV basis outputs and their four spatial reflections against both original references.
    /// </summary>
    private static void ValidateNonconstantAfv()
    {
        // Nonzero even/even coefficients exercise the AFV basis itself. Other coefficient positions are zero,
        // so both adjacent DCT regions stay at exactly 0.25. Native and Rust agree on every output bit for this input.
        ReadOnlySpan<int> origins = [0, 4, 32, 36];
        using Buffer2D<float> dc = MemoryAllocator.Default.Allocate2D<float>(1, 1);
        dc.DangerousGetRowSpan(0)[0] = 0.25F;
        for (byte strategy = 14; strategy <= 17; strategy++)
        {
            float[] coefficients = new float[64];
            coefficients[2] = 0.125F;
            coefficients[16] = -0.0625F;
            coefficients[54] = 0.03125F;
            float[] output = new float[64];
            float[] scratch = new float[JxlInverseTransform.GetScratchLength(strategy)];
            JxlInverseTransform.Transform(strategy, coefficients, dc.GetRegion(), output, 8, scratch);
            int origin = origins[strategy - 14];
            ReadOnlySpan<uint> expected = NonconstantAfvPoints(strategy);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    int localY = y - (origin / 8);
                    int localX = x - (origin % 8);
                    float value = localY is >= 0 and < 4 && localX is >= 0 and < 4
                        ? BitConverter.UInt32BitsToSingle(expected[(localY * 4) + localX])
                        : 0.25F;

                    Assert.True(MathF.Abs(output[(y * 8) + x] - value) <= 0.0000001F, $"AFV strategy {strategy}, pixel ({x}, {y}).");
                }
            }
        }
    }

    /// <summary>
    /// Gets the sixteen AFV quadrant samples recorded identically by original libjxl and jxl-rs.
    /// </summary>
    /// <param name="strategy">The raw AFV strategy.</param>
    /// <returns>The physical quadrant samples in row-major order.</returns>
    /// <remarks>
    /// Generated with the same libjxl and jxl-rs revisions and entry points documented on RustInverseTransformPoints.
    /// For each AFV strategy, the 8-by-8 coefficient block starts at zero except coefficient[2] = 1/8,
    /// coefficient[16] = -1/16 and coefficient[54] = 1/32. The single DC sample is 1/4.
    /// Both references return identical float bits for all 64 output samples. These 16 values describe the
    /// physical 4-by-4 AFV quadrant after that strategy's reflections; the other three quadrants remain 1/4.
    /// </remarks>
    private static ReadOnlySpan<uint> NonconstantAfvPoints(byte strategy) => strategy switch
    {
        14 =>
        [
            0x3EB81F2EU, 0x3EA4BFC7U, 0x3E76A782U, 0x3E70A5ABU,
            0x3E6EFD15U, 0x3E793ACBU, 0x3E65183AU, 0x3E7A08E3U,
            0x3E76A782U, 0x3E65183AU, 0x3E82641EU, 0x3E6ADEF2U,
            0x3E70A5ABU, 0x3E7A08E3U, 0x3E6ADEF2U, 0x3E76A782U,
        ],
        15 =>
        [
            0x3E70A5ABU, 0x3E76A782U, 0x3EA4BFC7U, 0x3EB81F2EU,
            0x3E7A08E3U, 0x3E65183AU, 0x3E793ACBU, 0x3E6EFD15U,
            0x3E6ADEF2U, 0x3E82641EU, 0x3E65183AU, 0x3E76A782U,
            0x3E76A782U, 0x3E6ADEF2U, 0x3E7A08E3U, 0x3E70A5ABU,
        ],
        16 =>
        [
            0x3E70A5ABU, 0x3E7A08E3U, 0x3E6ADEF2U, 0x3E76A782U,
            0x3E76A782U, 0x3E65183AU, 0x3E82641EU, 0x3E6ADEF2U,
            0x3E6EFD15U, 0x3E793ACBU, 0x3E65183AU, 0x3E7A08E3U,
            0x3EB81F2EU, 0x3EA4BFC7U, 0x3E76A782U, 0x3E70A5ABU,
        ],
        17 =>
        [
            0x3E76A782U, 0x3E6ADEF2U, 0x3E7A08E3U, 0x3E70A5ABU,
            0x3E6ADEF2U, 0x3E82641EU, 0x3E65183AU, 0x3E76A782U,
            0x3E7A08E3U, 0x3E65183AU, 0x3E793ACBU, 0x3E6EFD15U,
            0x3E70A5ABU, 0x3E76A782U, 0x3EA4BFC7U, 0x3EB81F2EU,
        ],
        _ => [],
    };
}
