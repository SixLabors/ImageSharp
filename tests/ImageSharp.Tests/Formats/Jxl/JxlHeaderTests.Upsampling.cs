// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

public partial class JxlHeaderTests
{
    /// <summary>
    /// Verifies default and custom frame upsampling, local bounds and overlapping storage at all SIMD widths and scalar fallback.
    /// </summary>
    [Fact]
    public void UpsamplingMatchesNativeAndRust()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateUpsampling,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks all output samples against two independently recorded references in a fixed case order.
    /// </summary>
    /// <remarks>
    /// The combined files contain little-endian IEEE 754 float samples, one row-major component per case, without headers or padding.
    /// Case order is 2x default/custom 33x5, 4x default/custom 33x5, 8x default/custom 33x5, 8x default 1x1, then 4x default 3x3.
    /// Each output dimension is factor times the corresponding input dimension, minus one, to exercise clipped final phases.
    /// Original libjxl revision 91837342b24034078780b687f57f670a7def8153 supplies UpsamplingStage::ProcessRow,
    /// lib/jxl/render_pipeline/stage_upsampling.cc, with the defaults from CustomTransformData::VisitFields in lib/jxl/image_metadata.cc.
    /// Original jxl-rs revision d7a831fae15b903f1b56a76668030c19ac07bb6f supplies Upsample::new and the native-size SIMD dispatches
    /// in jxl/src/render/stages/upsample.rs. Neither reference uses ImageSharp conversion or pixel packing.
    /// Custom compact weight i is ((i modulo 17) - 8) / 64. Input samples follow UpsamplingInputSample.
    /// </remarks>
    private static void ValidateUpsampling()
    {
        byte[] native = TestFile.Create(TestImages.Jxl.VarDctUpsamplingNative).Bytes;
        byte[] rust = TestFile.Create(TestImages.Jxl.VarDctUpsamplingRust).Bytes;
        Assert.Equal("387BE4456B4B9D0B24F5374234232FC60C94EBC5BE470ABFE9BBD4F9FE23F0FA", Convert.ToHexString(SHA256.HashData(native)));
        Assert.Equal("1C6FF1406BB04A635BFC460BB06CD382FC16C6AC007C6EA94AE80872B0C5BA20", Convert.ToHexString(SHA256.HashData(rust)));
        ReadOnlySpan<string> files =
        [
            TestImages.Jxl.VarDctUpsampling2Default,
            TestImages.Jxl.VarDctUpsampling2Custom,
            TestImages.Jxl.VarDctUpsampling4Default,
            TestImages.Jxl.VarDctUpsampling4Custom,
            TestImages.Jxl.VarDctUpsampling8Default,
            TestImages.Jxl.VarDctUpsampling8Custom,
            TestImages.Jxl.VarDctUpsampling8Tiny,
            TestImages.Jxl.VarDctUpsampling4Tiny,
        ];

        ReadOnlySpan<int> factors = [2, 2, 4, 4, 8, 8, 8, 4];
        ReadOnlySpan<int> framePositions = [78, 318, 78, 958, 78, 3438, 70, 70];
        ReadOnlySpan<int> globalPositions = [110, 350, 110, 990, 110, 3470, 102, 102];
        ReadOnlySpan<int> dcPositions = [153, 393, 153, 1033, 153, 3513, 145, 145];
        ReadOnlySpan<int> metadataPositions = [242, 482, 242, 1122, 242, 3602, 219, 219];
        int referenceOffset = 0;
        for (int index = 0; index < files.Length; index++)
        {
            MemoryAllocator allocator = Configuration.Default.MemoryAllocator;
            using MemoryStream stream = new(TestFile.Create(files[index]).Bytes, false);
            using BufferedReadStream buffered = new(Configuration.Default, stream);
            JxlBitReader reader = new(new JxlCodestreamReader(buffered));
            JxlHeader header = JxlHeader.Read(ref reader, allocator, SegmentIntegrityHandling.Strict);
            JxlFrameHeader frame = JxlFrameHeader.Read(ref reader, header, false, SegmentIntegrityHandling.Strict);
            Assert.Equal(framePositions[index], reader.Position);
            Assert.Equal(factors[index], frame.Upsampling);
            Vector3 dcFactors = JxlDecoderCore.ReadDcQuantization(ref reader);
            using JxlVarDctDecoder varDct = JxlVarDctDecoder.ReadGlobal(ref reader, header, frame, allocator, dcFactors);
            using JxlModularDecoder modular = JxlModularDecoder.Read(ref reader, header, frame, allocator, default);
            Assert.Equal(globalPositions[index], reader.Position);
            varDct.ReadDcGroup(ref reader, modular, 0, default);
            Assert.Equal(dcPositions[index], reader.Position);
            varDct.ReadAcMetadata(ref reader, modular, 0, default);
            Assert.Equal(metadataPositions[index], reader.Position);
            Assert.Equal(1U, varDct.UsedStrategies);
            varDct.PrepareReconstruction(allocator, header, frame);

            Size inputSize = index switch { 6 => new Size(1, 1), 7 => new Size(3, 3), _ => new Size(33, 5) };
            int factor = factors[index];
            bool custom = index is 1 or 3 or 5;
            Size expectedSize = new((inputSize.Width * factor) - 1, (inputSize.Height * factor) - 1);
            Assert.Equal(inputSize, frame.CodingSize);
            Assert.Equal(expectedSize, frame.Size);
            ReadOnlySpan<float> customWeights = factor switch
            {
                2 => header.Upsampling2Weights.Span,
                4 => header.Upsampling4Weights.Span,
                _ => header.Upsampling8Weights.Span,
            };

            int expectedWeightCount = custom ? factor switch { 2 => 15, 4 => 55, _ => 210 } : 0;
            Assert.Equal(expectedWeightCount, customWeights.Length);
            for (int i = 0; i < customWeights.Length; i++)
            {
                Assert.Equal(((i % 17) - 8) / 64F, customWeights[i]);
            }

            // These are numeric plane inputs, not generated images. Distinct exact powers of two catch component-selection mistakes.
            // The stored native/Rust one-plane results scale by those same exact powers without changing these finite binary operations.
            for (int component = 0; component < 3; component++)
            {
                Buffer2DRegion<float> plane = varDct.GetReconstructedChannel(component);
                Assert.Equal(inputSize.Width, plane.Width);
                Assert.Equal(inputSize.Height, plane.Height);
                int multiplier = 1 << component;
                for (int y = 0; y < plane.Height; y++)
                {
                    Span<float> row = plane.DangerousGetRowSpan(y);
                    for (int x = 0; x < plane.Width; x++)
                    {
                        row[x] = UpsamplingInputSample(x, y, inputSize) * multiplier;
                    }
                }
            }

            float[] minimum = new float[inputSize.Width * inputSize.Height];
            float[] maximum = new float[minimum.Length];
            PrepareExpectedUpsamplingBounds(inputSize, minimum, maximum);
            varDct.ApplyUpsampling(header, frame, allocator, default);
            for (int component = 0; component < 3; component++)
            {
                Buffer2DRegion<float> plane = varDct.GetReconstructedChannel(component);
                Assert.Equal(expectedSize.Width, plane.Width);
                Assert.Equal(expectedSize.Height, plane.Height);
                int multiplier = 1 << component;

                // Original native/Rust default-kernel outputs differ by at most 3.58e-7 normalized units. Round upward to 4e-7.
                // The custom dyadic input/weights are exact in both references and every result must remain exact here.
                float bound = custom ? 0F : 0.0000004F * multiplier;
                for (int y = 0; y < plane.Height; y++)
                {
                    ReadOnlySpan<float> row = plane.DangerousGetRowSpan(y);
                    for (int x = 0; x < plane.Width; x++)
                    {
                        int byteOffset = referenceOffset + (((y * plane.Width) + x) * sizeof(float));
                        float nativeValue = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(native.AsSpan(byteOffset))) * multiplier;
                        float rustValue = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(rust.AsSpan(byteOffset))) * multiplier;
                        float value = row[x];
                        if (custom)
                        {
                            Assert.Equal(BitConverter.SingleToUInt32Bits(nativeValue), BitConverter.SingleToUInt32Bits(value));
                            Assert.Equal(BitConverter.SingleToUInt32Bits(rustValue), BitConverter.SingleToUInt32Bits(value));
                        }
                        else
                        {
                            bool matchesNative = MathF.Abs(value - nativeValue) <= bound;
                            bool matchesRust = MathF.Abs(value - rustValue) <= bound;
                            Assert.True(
                                matchesNative,
                                matchesNative ? null : $"Case {index}, component {component}, ({x},{y}): {value:R}, native {nativeValue:R}.");

                            Assert.True(matchesRust, matchesRust ? null : $"Case {index}, component {component}, ({x},{y}): {value:R}, Rust {rustValue:R}.");
                        }

                        // All phases from one input position share the same original 25-sample range, including mirrored edges.
                        // This is a local reconstruction bound. Negative samples and samples above one remain valid.
                        int source = ((y / factor) * inputSize.Width) + (x / factor);
                        Assert.InRange(value, minimum[source] * multiplier, maximum[source] * multiplier);
                    }
                }
            }

            referenceOffset += expectedSize.Width * expectedSize.Height * sizeof(float);
        }

        Assert.Equal(native.Length, referenceOffset);
        Assert.Equal(rust.Length, referenceOffset);
    }

    /// <summary>
    /// Computes independent neighborhood bounds from the original numeric input equation and native mirror definition.
    /// </summary>
    /// <param name="size">The original input dimensions.</param>
    /// <param name="minimum">The expected lower bound for each source position.</param>
    /// <param name="maximum">The expected upper bound for each source position.</param>
    private static void PrepareExpectedUpsamplingBounds(Size size, Span<float> minimum, Span<float> maximum)
    {
        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                float lower = UpsamplingInputSample(x, y, size);
                float upper = lower;
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        float sample = UpsamplingInputSample(MirrorUpsamplingInput(x + dx, size.Width), MirrorUpsamplingInput(y + dy, size.Height), size);
                        lower = MathF.Min(lower, sample);
                        upper = MathF.Max(upper, sample);
                    }
                }

                minimum[(y * size.Width) + x] = lower;
                maximum[(y * size.Width) + x] = upper;
            }
        }
    }

    /// <summary>
    /// Gets the exact numeric input used by both original reference programs.
    /// </summary>
    /// <param name="x">The original column.</param>
    /// <param name="y">The original row.</param>
    /// <param name="size">The original dimensions. The one-by-one case has the constant sample 1.25.</param>
    /// <returns>The input component value, with no color conversion or pixel packing.</returns>
    private static float UpsamplingInputSample(int x, int y, Size size)
        => size.Width == 1 && size.Height == 1 ? 1.25F : ((((3 * x) + (5 * y)) % 23) - 11) / 8F;

    /// <summary>
    /// Applies the original native Mirror rule, including repeated border samples in one- and two-sample axes.
    /// </summary>
    /// <param name="position">A source position within two samples of the image.</param>
    /// <param name="length">The positive source axis length.</param>
    /// <returns>The mirrored physical sample index.</returns>
    private static int MirrorUpsamplingInput(int position, int length)
    {
        // This direct reflection definition is independent of production's modulo-period implementation.
        while (position < 0 || position >= length)
        {
            position = position < 0 ? -position - 1 : (2 * length) - position - 1;
        }

        return position;
    }
}
