// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

public partial class JxlHeaderTests
{
    /// <summary>
    /// Verifies native coding-to-device XYB scaling and device-to-linear-RGB reconstruction at every SIMD width.
    /// </summary>
    [Fact]
    public void ConvertDeclaredXybDeviceMatchesNativeLinearRgb()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateDeclaredXybDeviceConversion,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks component overloads and mixed planar traversal against independently recorded native RGB and XYB pairs.
    /// </summary>
    /// <remarks>
    /// The native pairs are recorded in RgbAndXybConversionTest.VerifyNativeLinearForward. Original libjxl revision
    /// 91837342b24034078780b687f57f670a7def8153, lib/jxl/enc_xyb.cc, ToXYB, produced the XYB values at 255 nits.
    /// ScaleXYBRow in the same source converts those values to the fixed device samples below.
    /// Device-to-RGB expectations are the original linear RGB inputs. Coding-to-device expectations are the
    /// independently recorded ScaleXYBRow results. Neither direction derives expectations from production operators.
    /// </remarks>
    private static void ValidateDeclaredXybDeviceConversion()
    {
        ReadOnlySpan<Vector3> device =
        [
            new(.353816301F, -1.46541899E-08F, .41715157F),
            new(.999999285F, .577526689F, .392322272F),
            new(.103198044F, .786984801F, .26070255F),
            new(.839155912F, .798765421F, .352154642F),
            new(.111198567F, .626465797F, .59617871F)
        ];

        ReadOnlySpan<Vector3> expected =
        [
            new(0F, 0F, 0F),
            new(1F, 0F, 0F),
            new(.12F, .75F, .33F),
            new(1.5F, .25F, .125F),
            new(-.025F, .4F, .8F)
        ];

        // These are the matching native ToXYB values from VerifyNativeLinearForward at 255 nits.
        // Keeping the literal coding and device samples checks the fixed encoding without using an inverse round trip.
        ReadOnlySpan<Vector3> coding =
        [
            new(0F, -1.23873107e-08F, -1.23873107e-08F),
            new(0.0281000584F, 0.488188207F, 0.471658945F),
            new(-0.0108984411F, 0.665244937F, 0.561094284F),
            new(0.0211055875F, 0.675203204F, 0.631933689F),
            new(-0.0105505288F, 0.52955687F, 0.648738146F)
        ];

        JxlColorConverterBase.XybEncodingOperator encoding = default;
        JxlColorConverterBase.XybDeviceOperator<JxlColorConverterBase.RgbOperator> operation = new(default);
        for (int i = 0; i < device.Length; i++)
        {
            float x = coding[i].X;
            float y = coding[i].Y;
            float blue = coding[i].Z;
            float unusedEncoding = 0F;
            encoding.Invoke(ref x, ref y, ref blue, ref unusedEncoding);
            Assert.Equal(device[i], new Vector3(x, y, blue), ApproximateFloatComparer.FromPrecision(4));

            float r = device[i].X;
            float g = device[i].Y;
            float b = device[i].Z;
            float unused = 0F;
            operation.Invoke(ref r, ref g, ref b, ref unused);
            Assert.Equal(expected[i], new Vector3(r, g, b), ApproximateFloatComparer.FromPrecision(4));

            // Each register holds one component from independent pixels. Check every lane, including
            // software execution when FeatureTestRunner disables the corresponding hardware instructions.
            Vector128<float> r128 = Vector128.Create(device[i].X);
            Vector128<float> g128 = Vector128.Create(device[i].Y);
            Vector128<float> b128 = Vector128.Create(device[i].Z);
            Vector128<float> unused128 = Vector128<float>.Zero;
            operation.Invoke(ref r128, ref g128, ref b128, ref unused128);
            for (int lane = 0; lane < Vector128<float>.Count; lane++)
            {
                Assert.Equal(
                    expected[i],
                    new Vector3(r128.GetElement(lane), g128.GetElement(lane), b128.GetElement(lane)),
                    ApproximateFloatComparer.FromPrecision(4));
            }

            Vector256<float> r256 = Vector256.Create(device[i].X);
            Vector256<float> g256 = Vector256.Create(device[i].Y);
            Vector256<float> b256 = Vector256.Create(device[i].Z);
            Vector256<float> unused256 = Vector256<float>.Zero;
            operation.Invoke(ref r256, ref g256, ref b256, ref unused256);
            for (int lane = 0; lane < Vector256<float>.Count; lane++)
            {
                Assert.Equal(
                    expected[i],
                    new Vector3(r256.GetElement(lane), g256.GetElement(lane), b256.GetElement(lane)),
                    ApproximateFloatComparer.FromPrecision(4));
            }

            Vector512<float> r512 = Vector512.Create(device[i].X);
            Vector512<float> g512 = Vector512.Create(device[i].Y);
            Vector512<float> b512 = Vector512.Create(device[i].Z);
            Vector512<float> unused512 = Vector512<float>.Zero;
            operation.Invoke(ref r512, ref g512, ref b512, ref unused512);
            for (int lane = 0; lane < Vector512<float>.Count; lane++)
            {
                Assert.Equal(
                    expected[i],
                    new Vector3(r512.GetElement(lane), g512.GetElement(lane), b512.GetElement(lane)),
                    ApproximateFloatComparer.FromPrecision(4));
            }
        }

        // Thirty-five samples provide mixed lanes and three scalar samples after complete 512-,
        // 256- or 128-bit blocks. The feature runner also sends all samples through the scalar traversal.
        const int length = 35;
        float[] first = new float[length];
        float[] second = new float[length];
        float[] third = new float[length];
        for (int i = 0; i < length; i++)
        {
            Vector3 sample = coding[i % coding.Length];
            first[i] = sample.X;
            second[i] = sample.Y;
            third[i] = sample.Z;
        }

        JxlColorConverterBase.ConvertInPlace(first, second, third, encoding);
        for (int i = 0; i < length; i++)
        {
            Assert.Equal(device[i % device.Length], new Vector3(first[i], second[i], third[i]), ApproximateFloatComparer.FromPrecision(4));
        }
    }

    /// <summary>
    /// Verifies modular XYB integer reconstruction and metadata-dependent float rounding at all SIMD widths and scalar fallback.
    /// </summary>
    [Fact]
    public void ReconstructModularXybMatchesNativeIntegerArithmetic()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateModularXybReconstruction,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks the same native-serialized numeric channels at metadata precisions eight and twenty-four.
    /// </summary>
    private static void ValidateModularXybReconstruction()
    {
        ValidateModularXybInput(TestImages.Jxl.ModularXyb8Owner, 8);
        ValidateModularXybInput(TestImages.Jxl.ModularXyb24Owner, 24);
    }

    /// <summary>
    /// Reads completed integer channels from a stored native modular stream and compares exact original reconstruction results.
    /// </summary>
    /// <param name="file">The stored native modular XYB owner prefix, with thirty-one coded columns and one row.</param>
    /// <param name="precision">The original metadata precision, which selects native X/Y conversion accuracy.</param>
    /// <remarks>
    /// Original libjxl revision 91837342b24034078780b687f57f670a7def8153, lib/jxl/dec_modular.cc, supplies MultiplySum,
    /// SingleFromSingle and SingleFromSingleAccurate. Highway x86_128-inl.h uses _mm_add_epi32 for MultiplySum's signed wrapping sum.
    /// Each stored prefix ends after its global modular stream. It declares 32-bit modular buffers even at metadata precision eight.
    /// X is 16777217 throughout; Y and B-minus-Y repeat the four integer pairs below. DC factors are 3/256, 1/8192 and 1/4096.
    /// Expected literals are IEEE 754 float bit patterns before color conversion or pixel packing.
    /// </remarks>
    private static void ValidateModularXybInput(string file, int precision)
    {
        MemoryAllocator allocator = Configuration.Default.MemoryAllocator;
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, allocator, SegmentIntegrityHandling.Strict, CancellationToken.None);
        JxlFrameHeader frame = JxlFrameHeader.Read(ref reader, header, false, SegmentIntegrityHandling.Strict);
        Assert.True(header.UsesXybTransform);
        Assert.False(header.Modular16BitBufferSufficient);
        Assert.Equal(precision, header.Metadata.BitDepth.BitsPerSample);
        Assert.True(frame.Modular);
        Assert.Equal(new Size(31, 1), frame.CodingSize);
        Assert.Equal(frame.CodingSize, frame.OutputSize);
        Vector3 factors = JxlDecoderCore.ReadDcQuantization(ref reader);
        Assert.Equal(new Vector3(3F / 256F, 1F / 8192F, 1F / 4096F), factors);
        using JxlModularDecoder modular = JxlModularDecoder.Read(ref reader, header, frame, allocator, default);
        modular.Complete(default);
        ReadOnlySpan<int> yPeriod = [16777217, int.MaxValue, int.MinValue, 1883];
        ReadOnlySpan<int> bPeriod = [-16777216, 1, -1, -442];
        ReadOnlySpan<uint> expectedY = [0x45000000U, 0x48800000U, 0xC8800000U, 0x3E6B6000U];
        ReadOnlySpan<uint> expectedB = [0x39800000U, 0xC9000000U, 0x49000000U, 0x3EB42000U];
        uint expectedX = precision >= 23 ? 0x48400001U : 0x48400000U;

        // These assertions prove the actual entropy and inverse-transform path delivered the native numeric input.
        // Thirty-one columns leave a remainder at each 16-, 8- and 4-lane width. Scalar runs process the same original integers.
        ReadOnlySpan<int> sourceY = modular.GetChannel(0).GetRow<int>(0);
        ReadOnlySpan<int> sourceX = modular.GetChannel(1).GetRow<int>(0);
        ReadOnlySpan<int> sourceB = modular.GetChannel(2).GetRow<int>(0);
        for (int x = 0; x < sourceY.Length; x++)
        {
            Assert.Equal(yPeriod[x % yPeriod.Length], sourceY[x]);
            Assert.Equal(16777217, sourceX[x]);
            Assert.Equal(bPeriod[x % bPeriod.Length], sourceB[x]);
        }

        Buffer2D<float>[] planes = modular.ReconstructXyb(frame, factors, default);
        try
        {
            Assert.Equal(3, planes.Length);
            foreach (Buffer2D<float> plane in planes)
            {
                Assert.Equal(frame.OutputSize, plane.Size);
            }

            ReadOnlySpan<float> outputX = planes[0].DangerousGetRowSpan(0);
            ReadOnlySpan<float> outputY = planes[1].DangerousGetRowSpan(0);
            ReadOnlySpan<float> outputB = planes[2].DangerousGetRowSpan(0);
            for (int x = 0; x < outputX.Length; x++)
            {
                // (16777217 + -16777216) reconstructs integer one, not float zero. Max+1 and Min-1 wrap before conversion.
                // B uses its own factor, not the factor already applied to Y. The metadata24 X result retains one extra final float level.
                Assert.Equal(expectedX, BitConverter.SingleToUInt32Bits(outputX[x]));
                Assert.Equal(expectedY[x % expectedY.Length], BitConverter.SingleToUInt32Bits(outputY[x]));
                Assert.Equal(expectedB[x % expectedB.Length], BitConverter.SingleToUInt32Bits(outputB[x]));
                Assert.Equal(yPeriod[x % yPeriod.Length], sourceY[x]);
                Assert.Equal(16777217, sourceX[x]);
                Assert.Equal(bPeriod[x % bPeriod.Length], sourceB[x]);
            }
        }
        finally
        {
            foreach (Buffer2D<float> plane in planes)
            {
                plane.Dispose();
            }
        }
    }
}
