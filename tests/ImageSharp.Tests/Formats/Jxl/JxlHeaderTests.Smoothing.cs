// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
    /// Verifies native DC smoothing, original-row reuse, borders and subsampling at each SIMD width and scalar fallback.
    /// </summary>
    [Fact]
    public void SmoothDcMatchesNativeAndRust()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateDcSmoothing,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks coupled 4:4:4 and independently sampled DC planes from stored native bitstreams.
    /// </summary>
    private static void ValidateDcSmoothing()
    {
        ValidateDcSmoothingInput(TestImages.Jxl.VarDctSmoothing444, false);
        ValidateDcSmoothingInput(TestImages.Jxl.VarDctSmoothingSubsampled, true);
    }

    /// <summary>
    /// Reads a stored DC stream, smooths its completed planes and checks the original reference outputs.
    /// </summary>
    /// <param name="file">The native header, DC-global and modular DC input. This partial input has no TOC or AC payload.</param>
    /// <param name="subsampled">Whether X and B have half the full-grid width.</param>
    private static void ValidateDcSmoothingInput(string file, bool subsampled)
    {
        MemoryAllocator allocator = Configuration.Default.MemoryAllocator;
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);
        using BufferedReadStream buffered = new(Configuration.Default, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, allocator, SegmentIntegrityHandling.Strict);
        JxlFrameHeader frame = JxlFrameHeader.Read(ref reader, header, false, SegmentIntegrityHandling.Strict);
        Assert.Equal(subsampled ? 87 : 78, reader.Position);
        Vector3 factors = JxlDecoderCore.ReadDcQuantization(ref reader);
        using JxlVarDctDecoder varDct = JxlVarDctDecoder.ReadGlobal(ref reader, header, frame, allocator, factors);
        using JxlModularDecoder modular = JxlModularDecoder.Read(ref reader, header, frame, allocator, default);
        Assert.Equal(subsampled ? 169 : 110, reader.Position);
        varDct.ReadDcGroup(ref reader, modular, 0, default);
        Assert.Equal(subsampled ? 865 : 891, reader.Position);
        Assert.Equal(1, varDct.DcContextCount);

        // These snapshots assert only the mutation boundary: the four borders must remain unchanged.
        // Interior expectations below come from original libjxl and jxl-rs, not from these snapshots.
        float[][] original = new float[3][];
        for (int component = 0; component < 3; component++)
        {
            Buffer2DRegion<float> plane = varDct.GetDcChannel(component);
            int expectedWidth = subsampled ? component == 1 ? 36 : 18 : 35;
            Assert.Equal(expectedWidth, plane.Width);
            Assert.Equal(5, plane.Height);
            original[component] = new float[plane.Width * plane.Height];
            for (int y = 0; y < plane.Height; y++)
            {
                plane.DangerousGetRowSpan(y).CopyTo(original[component].AsSpan(y * plane.Width, plane.Width));
            }
        }

        varDct.SmoothDc(allocator, default);
        for (int component = 0; component < 3; component++)
        {
            Buffer2DRegion<float> plane = varDct.GetDcChannel(component);
            for (int y = 0; y < plane.Height; y++)
            {
                ReadOnlySpan<float> row = plane.DangerousGetRowSpan(y);
                for (int x = 0; x < plane.Width; x++)
                {
                    if (y == 0 || y == plane.Height - 1 || x == 0 || x == plane.Width - 1)
                    {
                        Assert.Equal(original[component][(y * plane.Width) + x], row[x]);
                    }
                }
            }

            // Three interior rows force both ring rotations. Thirty-three interior columns cover two 512-bit batches and a scalar tail.
            // The B-plane discontinuity at (17,2) also limits smoothing in X and Y for 4:4:4, through their shared strength.
            ReadOnlySpan<int> columns = plane.Width > 18 ? [1, 8, 16, 17, 32, plane.Width - 2] : [1, 7, 8, 9, 15, 16];
            ReadOnlySpan<uint> native = NativeDcSmoothingPoints(subsampled, component);
            ReadOnlySpan<uint> rust = RustDcSmoothingPoints(subsampled, component);

            // These are absolute normalized-component errors. The complete independent reference outputs disagree by at most
            // 9.32e-10 for 4:4:4 and 2.33e-10 for subsampled planes. Round upward without changing an existing tolerance.
            float bound = subsampled ? 0.0000000003F : 0.000000001F;
            int point = 0;
            for (int y = 1; y < plane.Height - 1; y++)
            {
                ReadOnlySpan<float> row = plane.DangerousGetRowSpan(y);
                foreach (int x in columns)
                {
                    float value = row[x];
                    float nativeValue = BitConverter.UInt32BitsToSingle(native[point]);
                    float rustValue = BitConverter.UInt32BitsToSingle(rust[point]);
                    Assert.True(MathF.Abs(value - nativeValue) <= bound, $"Subsampled={subsampled}, component {component}, ({x},{y}), native.");
                    Assert.True(MathF.Abs(value - rustValue) <= bound, $"Subsampled={subsampled}, component {component}, ({x},{y}), Rust.");
                    point++;
                }
            }
        }

        // Smoothing changes only float DC samples. A single DC context has the constant index zero on the full padded grid.
        for (int y = 0; y < varDct.DcContexts.Height; y++)
        {
            foreach (byte value in varDct.DcContexts.DangerousGetRowSpan(y))
            {
                Assert.Equal((byte)0, value);
            }
        }
    }

    /// <summary>
    /// Gets the interior smoothing samples recorded from the original libjxl AdaptiveDCSmoothing implementation.
    /// </summary>
    /// <param name="subsampled">Whether X and B have half the luma resolution.</param>
    /// <param name="component">The X, Y or B component index.</param>
    /// <returns>Six columns from each of three consecutive interior rows.</returns>
    /// <remarks>
    /// Generated with libjxl revision 91837342b24034078780b687f57f670a7def8153 by parsing the stored smoothing owner inputs,
    /// then calling original DequantDC and hardware-dispatched AdaptiveDCSmoothing in lib/jxl/compressed_dc.cc.
    /// The native writer serialized a 280-by-40 frame with default DC factors, global scale 32768, DC quantizer 100 and extra precision one.
    /// Quantized samples are 2*x + 3*y + 5*c, plus one when (x + 2*y + c) modulo five is zero; c indexes X, Y, B.
    /// B also adds eight at (17,2). The subsampled input halves X/B horizontally and pads the full block width from 35 to 36.
    /// Samples are ordered by rows one, two and three, then columns [1,8,16,17,32,width-2] for full-width planes,
    /// or [1,7,8,9,15,16] for 18-column planes. Values are IEEE 754 float bit patterns before color conversion and pixel packing.
    /// </remarks>
    private static ReadOnlySpan<uint> NativeDcSmoothingPoints(bool subsampled, int component) => (subsampled, component) switch
    {
        (false, 0) =>
        [
            0x3756813DU, 0x3843185DU, 0x38B469C0U, 0x38BDB48BU, 0x392C2065U, 0x3930C617U,
            0x37A4E90BU, 0x3863B4CAU, 0x38C7AE14U, 0x38CCCCCCU, 0x3933CE7AU, 0x3938ED33U,
            0x37E621E6U, 0x3881368EU, 0x38D32212U, 0x38DD5F84U, 0x393B7C8FU, 0x39409B47U,
        ],
        (false, 1) =>
        [
            0x3951A703U, 0x39F82FAAU, 0x3A4E035AU, 0x3A5B1957U, 0x3AB8742BU, 0x3ABE0BEBU,
            0x39878BD4U, 0x3A0B73FEU, 0x3A5C28F5U, 0x3A666666U, 0x3AC09B47U, 0x3AC5B9FFU,
            0x39A64425U, 0x3A19DE1AU, 0x3A6CBBACU, 0x3A76F91DU, 0x3AC8495BU, 0x3ACCEF0CU,
        ],
        (false, 2) =>
        [
            0x3A4E8C5AU, 0x3AD3BD5AU, 0x3B2785D6U, 0x3B302371U, 0x3B90FA0AU, 0x3B94EF55U,
            0x3A7E84F2U, 0x3AEAC796U, 0x3B31EB85U, 0x3B4E147AU, 0x3B96DADCU, 0x3B9AB1E6U,
            0x3A964CB6U, 0x3B00AC67U, 0x3B3F091AU, 0x3B49B947U, 0x3B9C60E8U, 0x3BA05634U,
        ],
        (true, 0) =>
        [
            0x3756813DU, 0x38308196U, 0x3843185DU, 0x38597759U, 0x38AA2C50U, 0x38B469C0U,
            0x37A4E90BU, 0x384F39E7U, 0x3863B4CAU, 0x38782FACU, 0x38B98879U, 0x38C2D3DDU,
            0x37E621E6U, 0x386DF23AU, 0x3881368EU, 0x388A81F0U, 0x38C8E4A2U, 0x38D32212U,
        ],
        (true, 1) =>
        [
            0x3951A703U, 0x39F82FAAU, 0x3A4E035AU, 0x3A574EBEU, 0x3AB8742BU, 0x3AC32AA3U,
            0x39878BD4U, 0x3A0B73FEU, 0x3A5D5F84U, 0x3A679CF4U, 0x3AC09B47U, 0x3ACAD8B7U,
            0x39A64425U, 0x3A19DE1AU, 0x3A6CBBACU, 0x3A76F91DU, 0x3AC8495BU, 0x3AD286CCU,
        ],
        (true, 2) =>
        [
            0x3A1A229AU, 0x3A8B73FEU, 0x3A95B16EU, 0x3A9FEEDFU, 0x3ADD5F84U, 0x3AE809FFU,
            0x3A3ABF08U, 0x3A9AD027U, 0x3AA50D97U, 0x3AAE58FBU, 0x3AECBBACU, 0x3AF5C28FU,
            0x3A59775AU, 0x3AA93A43U, 0x3AB469C1U, 0x3ABEA731U, 0x3AFC17D6U, 0x3B03DA2FU,
        ],
        _ => [],
    };

    /// <summary>
    /// Gets the interior smoothing samples recorded from the original jxl-rs adaptive_lf_smoothing implementation.
    /// </summary>
    /// <param name="subsampled">Whether X and B have half the luma resolution.</param>
    /// <param name="component">The X, Y or B component index.</param>
    /// <returns>Six columns from each of three consecutive interior rows.</returns>
    /// <remarks>
    /// Generated with jxl-rs revision d7a831fae15b903f1b56a76668030c19ac07bb6f by parsing the same stored inputs,
    /// then calling original adaptive_lf_smoothing in jxl/src/frame/adaptive_lf_smoothing.rs.
    /// The serialized input recipe and sampled positions are documented on NativeDcSmoothingPoints.
    /// Values are IEEE 754 float bit patterns before color conversion and pixel packing.
    /// </remarks>
    private static ReadOnlySpan<uint> RustDcSmoothingPoints(bool subsampled, int component) => (subsampled, component) switch
    {
        (false, 0) =>
        [
            0x3756813DU, 0x3843185CU, 0x38B469C0U, 0x38BDB48AU, 0x392C2065U, 0x3930C616U,
            0x37A4E90DU, 0x3863B4CAU, 0x38C7AE14U, 0x38CCCCCCU, 0x3933CE7AU, 0x3938ED33U,
            0x37E621E5U, 0x3881368EU, 0x38D32212U, 0x38DD5F84U, 0x393B7C8FU, 0x39409B46U,
        ],
        (false, 1) =>
        [
            0x3951A704U, 0x39F82FABU, 0x3A4E035AU, 0x3A5B195BU, 0x3AB8742DU, 0x3ABE0BEBU,
            0x39878BD4U, 0x3A0B73FDU, 0x3A5C28F5U, 0x3A666666U, 0x3AC09B46U, 0x3AC5B9FFU,
            0x39A64425U, 0x3A19DE1AU, 0x3A6CBBACU, 0x3A76F91DU, 0x3AC8495BU, 0x3ACCEF0DU,
        ],
        (false, 2) =>
        [
            0x3A4E8C5BU, 0x3AD3BD5AU, 0x3B2785D6U, 0x3B30236FU, 0x3B90FA0AU, 0x3B94EF55U,
            0x3A7E84F0U, 0x3AEAC797U, 0x3B31EB85U, 0x3B4E147AU, 0x3B96DADCU, 0x3B9AB1E6U,
            0x3A964CB5U, 0x3B00AC66U, 0x3B3F091AU, 0x3B49B947U, 0x3B9C60E8U, 0x3BA05632U,
        ],
        (true, 0) =>
        [
            0x3756813DU, 0x38308196U, 0x3843185CU, 0x38597759U, 0x38AA2C4FU, 0x38B469C0U,
            0x37A4E90DU, 0x384F39E8U, 0x3863B4CAU, 0x38782FACU, 0x38B98879U, 0x38C2D3DCU,
            0x37E621E5U, 0x386DF23AU, 0x3881368EU, 0x388A81F0U, 0x38C8E4A2U, 0x38D32212U,
        ],
        (true, 1) =>
        [
            0x3951A704U, 0x39F82FABU, 0x3A4E035AU, 0x3A574EBFU, 0x3AB8742DU, 0x3AC32AA2U,
            0x39878BD4U, 0x3A0B73FDU, 0x3A5D5F84U, 0x3A679CF4U, 0x3AC09B46U, 0x3ACAD8B7U,
            0x39A64425U, 0x3A19DE1AU, 0x3A6CBBACU, 0x3A76F91DU, 0x3AC8495BU, 0x3AD286CCU,
        ],
        (true, 2) =>
        [
            0x3A1A229BU, 0x3A8B73FDU, 0x3A95B16EU, 0x3A9FEEDFU, 0x3ADD5F84U, 0x3AE809FDU,
            0x3A3ABF08U, 0x3A9AD028U, 0x3AA50D99U, 0x3AAE58FAU, 0x3AECBBACU, 0x3AF5C28FU,
            0x3A597759U, 0x3AA93A42U, 0x3AB469C0U, 0x3ABEA731U, 0x3AFC17D6U, 0x3B03DA2FU,
        ],
        _ => [],
    };
}
