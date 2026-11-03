// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

public partial class JxlHeaderTests
{
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
        ReadOnlySpan<int> sourceY = modular.GetChannel(0).DangerousGetRowSpan(0);
        ReadOnlySpan<int> sourceX = modular.GetChannel(1).DangerousGetRowSpan(0);
        ReadOnlySpan<int> sourceB = modular.GetChannel(2).DangerousGetRowSpan(0);
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
