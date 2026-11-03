// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
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
    /// Checks the native chroma weights, mirrored endpoints and overlapping output at every SIMD width.
    /// </summary>
    [Fact]
    public void ChromaExpansionMatchesNativeWeights()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateChromaExpansion,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies exact quarter-weight interpolation with complete vectors, scalar tails and one-sample borders.
    /// </summary>
    private static void ValidateChromaExpansion()
    {
        const float guard = -1234567F;
        foreach (int count in new[] { 1, 2, 18, 34 })
        {
            float[] source = new float[count];
            for (int x = 0; x < count; x++)
            {
                source[x] = (x % 7) - 3;
            }

            for (int omitted = 0; omitted <= 1; omitted++)
            {
                int width = (2 * count) - omitted;
                float[] storage = new float[width + 2];
                storage.AsSpan().Fill(guard);
                Span<float> destination = storage.AsSpan(1, width);
                source.CopyTo(destination);
                JxlUpsampler.ExpandChromaHorizontal(destination, count);

                // Native chroma upsampling uses 3/4 of the center plus 1/4 of the adjacent input.
                // The even output takes its left neighbor; the odd output takes its right neighbor.
                // Mirroring repeats the endpoint. Integer inputs make these quarter sums exact in float.
                for (int x = 0; x < width; x++)
                {
                    int center = x / 2;
                    int neighbor = (x & 1) == 0 ? Math.Max(center - 1, 0) : Math.Min(center + 1, count - 1);
                    Assert.Equal(((3F * source[center]) + source[neighbor]) / 4F, destination[x]);
                }

                Assert.Equal(guard, storage[0]);
                Assert.Equal(guard, storage[^1]);
            }
        }

        foreach (int width in new[] { 1, 17, 33 })
        {
            // The upper output overwrites middle, and the lower output overwrites bottom. Both must use the original inputs.
            float[] top = new float[width];
            float[] middle = new float[width];
            float[] bottom = new float[width];
            for (int x = 0; x < width; x++)
            {
                top[x] = (x % 5) - 2;
                middle[x] = (x % 7) - 3;
                bottom[x] = (x % 11) - 5;
            }

            float[] first = new float[width + 2];
            float[] second = new float[width + 2];
            first.AsSpan().Fill(guard);
            second.AsSpan().Fill(guard);
            middle.CopyTo(first.AsSpan(1));
            bottom.CopyTo(second.AsSpan(1));
            JxlUpsampler.ExpandChromaVertical(top, first.AsSpan(1, width), second.AsSpan(1, width), first.AsSpan(1, width), second.AsSpan(1, width));

            for (int x = 0; x < width; x++)
            {
                Assert.Equal(((3F * middle[x]) + top[x]) / 4F, first[x + 1]);
                Assert.Equal(((3F * middle[x]) + bottom[x]) / 4F, second[x + 1]);
            }

            Assert.Equal(guard, first[0]);
            Assert.Equal(guard, first[^1]);
            Assert.Equal(guard, second[0]);
            Assert.Equal(guard, second[^1]);

            // An odd output height retains only the upper row of the final pair.
            middle.CopyTo(first.AsSpan(1));
            JxlUpsampler.ExpandChromaVertical(top, first.AsSpan(1, width), bottom, first.AsSpan(1, width), []);
            for (int x = 0; x < width; x++)
            {
                Assert.Equal(((3F * middle[x]) + top[x]) / 4F, first[x + 1]);
            }

            Assert.Equal(guard, first[0]);
            Assert.Equal(guard, first[^1]);
        }
    }

    /// <summary>
    /// Verifies Gaborish and all three EPF passes against both original reference decoders at every SIMD width.
    /// </summary>
    [Fact]
    public void RestorationMatchesNativeAndRust()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateRestoration,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Applies restoration to stored numeric planes using quantizers and sharpness parsed from a native-encoded owner prefix.
    /// </summary>
    private static void ValidateRestoration()
    {
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.VarDctRestorationOwner).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        MemoryAllocator allocator = Configuration.Default.MemoryAllocator;
        JxlHeader header = JxlHeader.Read(ref reader, allocator, SegmentIntegrityHandling.Strict);
        JxlFrameHeader frame = JxlFrameHeader.Read(ref reader, header, false, SegmentIntegrityHandling.Strict);
        Vector3 factors = JxlDecoderCore.ReadDcQuantization(ref reader);
        using JxlVarDctDecoder varDct = JxlVarDctDecoder.ReadGlobal(ref reader, header, frame, allocator, factors, []);
        using JxlModularDecoder modular = JxlModularDecoder.Read(ref reader, header, frame, allocator, default);
        varDct.ReadDcGroup(ref reader, modular, 0, default);
        varDct.ReadAcMetadata(ref reader, modular, 0, default);
        Assert.Equal(304, reader.Position);
        varDct.PrepareReconstruction(allocator, header, frame);
        Assert.Equal(new Size(19, 17), frame.CodingSize);
        Assert.True(frame.RestorationFilter.Gaborish);
        Assert.Equal((byte)3, frame.RestorationFilter.Iterations);
        Assert.Equal(1024, varDct.GlobalScale);

        // These stored little-endian float planes are independent numeric samples, not a generated image.
        // Original libjxl stage_gaborish/stage_epf and jxl-rs render/filter consumed the same samples and metadata.
        // The 19-column rows cross sigma blocks, include mixed skip/filter lanes, and leave a SIMD remainder.
        ReadOnlySpan<byte> input = TestFile.Create(TestImages.Jxl.VarDctRestorationSamples).Bytes;
        ReadOnlySpan<byte> native = TestFile.Create(TestImages.Jxl.VarDctRestorationNative).Bytes;
        ReadOnlySpan<byte> rust = TestFile.Create(TestImages.Jxl.VarDctRestorationRust).Bytes;
        int byteLength = 3 * 19 * 17 * sizeof(float);
        Assert.Equal(byteLength, input.Length);
        Assert.Equal(byteLength, native.Length);
        Assert.Equal(byteLength, rust.Length);
        int offset = 0;
        for (int component = 0; component < 3; component++)
        {
            Buffer2DRegion<float> plane = varDct.GetReconstructedChannel(component);
            for (int y = 0; y < plane.Height; y++)
            {
                Span<float> row = plane.DangerousGetRowSpan(y);
                for (int x = 0; x < row.Length; x++, offset += sizeof(float))
                {
                    row[x] = BinaryPrimitives.ReadSingleLittleEndian(input[offset..]);
                }
            }
        }

        varDct.ApplyRestoration(frame, allocator, default);

        // Both references include Gaborish followed by EPF0, EPF1 and EPF2, with mirrored image borders.
        // Their maximum absolute difference over all 969 outputs is 2.3283064365e-10 in normalized component units.
        // Round this measured bound upward to 3e-10 for single-precision multiply-add differences.
        offset = 0;
        for (int component = 0; component < 3; component++)
        {
            Buffer2DRegion<float> plane = varDct.GetReconstructedChannel(component);
            for (int y = 0; y < plane.Height; y++)
            {
                ReadOnlySpan<float> row = plane.DangerousGetRowSpan(y);
                for (int x = 0; x < row.Length; x++, offset += sizeof(float))
                {
                    float nativeValue = BinaryPrimitives.ReadSingleLittleEndian(native[offset..]);
                    float rustValue = BinaryPrimitives.ReadSingleLittleEndian(rust[offset..]);
                    Assert.InRange(MathF.Abs(row[x] - nativeValue), 0F, 0.0000000003F);
                    Assert.InRange(MathF.Abs(row[x] - rustValue), 0F, 0.0000000003F);
                }
            }
        }
    }
}
