// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
public class LossyUtilsTests
{
    private static void RunTransformTwoTest()
    {
        // arrange
        short[] src = [19, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 19, 23, 0, 0, -23, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        ];
        byte[] dst =
        [
            103, 103, 103, 103, 103, 103, 103, 103, 0, 0, 0, 0, 169, 169, 169, 169, 171, 171, 171, 171,
            171, 171, 171, 171, 0, 0, 0, 0, 103, 103, 103, 103, 103, 103, 103, 103, 103, 103, 103, 103,
            0, 0, 0, 0, 169, 169, 169, 169, 171, 171, 171, 171, 171, 171, 171, 171, 0, 0, 0, 0,
            103, 103, 103, 103, 103, 103, 103, 103, 103, 103, 103, 103, 0, 0, 0, 0, 169, 169, 169, 169,
            171, 171, 171, 171, 171, 171, 171, 171, 0, 0, 0, 0, 103, 103, 103, 103, 103, 103, 103, 103,
            103, 103, 103, 103, 0, 0, 0, 0, 169, 169, 169, 169, 171, 171, 171, 171, 171, 171, 171, 171,
            0, 0, 0, 0, 0, 0, 0, 0
        ];
        byte[] expected =
        [
            105, 105, 105, 105, 105, 103, 100, 98, 0, 0, 0, 0, 169, 169, 169, 169, 171, 171, 171, 171, 171, 171,
            171, 171, 0, 0, 0, 0, 103, 103, 103, 103, 105, 105, 105, 105, 108, 105, 102, 100, 0, 0, 0, 0, 169,
            169, 169, 169, 171, 171, 171, 171, 171, 171, 171, 171, 0, 0, 0, 0, 103, 103, 103, 103, 105, 105,
            105, 105, 111, 109, 106, 103, 0, 0, 0, 0, 169, 169, 169, 169, 171, 171, 171, 171, 171, 171, 171,
            171, 0, 0, 0, 0, 103, 103, 103, 103, 105, 105, 105, 105, 113, 111, 108, 106, 0, 0, 0, 0, 169, 169,
            169, 169, 171, 171, 171, 171, 171, 171, 171, 171, 0, 0, 0, 0, 0, 0, 0, 0
        ];
        int[] scratch = new int[16];

        // act
        LossyUtils.TransformTwo(src, dst, scratch);

        // assert
        Assert.True(expected.SequenceEqual(dst));
    }

    private static void RunTransformOneTest()
    {
        // arrange
        short[] src = [-176, 0, 0, 0, 29, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        byte[] dst =
        [
            128, 128, 128, 128, 128, 128, 128, 128, 0, 0, 0, 0, 0, 0, 0, 129, 128, 128, 128, 128,
            128, 128, 128, 128, 0, 0, 0, 0, 0, 0, 0, 129, 128, 128, 128, 128, 128, 128, 128, 128,
            0, 0, 0, 0, 0, 0, 0, 129, 128, 128, 128, 128, 128, 128, 128, 128, 0, 0, 0, 0,
            0, 0, 0, 129, 128, 128, 128, 128, 128, 128, 128, 128, 0, 0, 0, 0, 0, 0, 0, 129,
            128, 128, 128, 128, 128, 128, 128, 128, 0, 0, 0, 0, 0, 0, 0, 129, 128, 128, 128, 128,
            128, 128, 128, 128, 0, 0, 0, 0, 0, 0, 0, 129, 128, 128, 128, 128, 128, 128, 128, 128,
            0, 0, 0, 0, 0, 0, 0, 129
        ];
        byte[] expected =
        [
            111, 111, 111, 111, 128, 128, 128, 128, 0, 0, 0, 0, 0, 0, 0, 129, 128, 128, 128, 128,
            128, 128, 128, 128, 0, 0, 0, 0, 0, 0, 0, 129, 108, 108, 108, 108, 128, 128, 128, 128,
            0, 0, 0, 0, 0, 0, 0, 129, 128, 128, 128, 128, 128, 128, 128, 128, 0, 0, 0, 0,
            0, 0, 0, 129, 104, 104, 104, 104, 128, 128, 128, 128, 0, 0, 0, 0, 0, 0, 0, 129,
            128, 128, 128, 128, 128, 128, 128, 128, 0, 0, 0, 0, 0, 0, 0, 129, 101, 101, 101, 101,
            128, 128, 128, 128, 0, 0, 0, 0, 0, 0, 0, 129, 128, 128, 128, 128, 128, 128, 128, 128,
            0, 0, 0, 0, 0, 0, 0, 129
        ];
        int[] scratch = new int[16];

        // act
        LossyUtils.TransformOne(src, dst, scratch);

        // assert
        Assert.True(expected.SequenceEqual(dst));
    }

    private static void RunVp8Sse16X16Test()
    {
        // arrange
        Random rand = new(1234);
        byte[] a = new byte[512 * 10];
        byte[] b = new byte[512 * 10];
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = (byte)rand.Next(byte.MaxValue);
            b[i] = (byte)rand.Next(byte.MaxValue);
        }
        int[] expected = [2533110, 2818581, 2984663, 2891188, 2855134, 2634604, 2466504, 3061747, 2626010, 2640965];

        // act + assert
        int offset = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            int actual = LossyUtils.Vp8_Sse16x16(a.AsSpan(offset), b.AsSpan(offset));
            Assert.Equal(expected[i], actual);

            offset += 512;
        }
    }

    private static void RunVp8Sse16X8Test()
    {
        // arrange
        Random rand = new(1234);
        byte[] a = new byte[256 * 10];
        byte[] b = new byte[256 * 10];
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = (byte)rand.Next(byte.MaxValue);
            b[i] = (byte)rand.Next(byte.MaxValue);
        }
        int[] expected = [1298274, 1234836, 1325264, 1493317, 1551995, 1432668, 1407891, 1483297, 1537930, 1317204];

        // act + assert
        int offset = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            int actual = LossyUtils.Vp8_Sse16x8(a.AsSpan(offset), b.AsSpan(offset));
            Assert.Equal(expected[i], actual);

            offset += 256;
        }
    }

    private static void RunVp8Sse4X4Test()
    {
        // arrange
        Random rand = new(1234);
        byte[] a = new byte[128 * 10];
        byte[] b = new byte[128 * 10];
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = (byte)rand.Next(byte.MaxValue);
            b[i] = (byte)rand.Next(byte.MaxValue);
        }
        int[] expected = [194133, 125861, 165966, 195688, 106491, 173015, 266960, 200272, 311224, 122545];

        // act + assert
        int offset = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            int actual = LossyUtils.Vp8_Sse4x4(a.AsSpan(offset), b.AsSpan(offset));
            Assert.Equal(expected[i], actual);

            offset += 128;
        }
    }

    private static void RunMean16x4Test()
    {
        // arrange
        byte[] input =
        [
            154, 145, 102, 115, 127, 129, 126, 125, 126, 120, 133, 152, 157, 153, 119, 94, 104, 116, 111, 113,
            113, 109, 105, 124, 173, 175, 177, 170, 175, 172, 166, 164, 151, 141, 99, 114, 125, 126, 135, 150,
            133, 115, 127, 149, 141, 168, 100, 54, 110, 117, 115, 116, 119, 115, 117, 130, 174, 174, 174, 157,
            146, 171, 166, 158, 117, 140, 96, 111, 119, 119, 136, 171, 188, 134, 121, 126, 136, 119, 59, 77,
            109, 115, 113, 120, 120, 117, 128, 115, 174, 173, 173, 161, 152, 148, 153, 162, 105, 140, 96, 114,
            115, 122, 141, 173, 190, 190, 142, 106, 151, 78, 66, 141, 110, 117, 123, 136, 118, 124, 127, 114,
            173, 175, 166, 155, 155, 159, 159, 158
        ];
        uint[] dc = new uint[4];
        uint[] expectedDc = [1940, 2139, 2252, 1813];

        // act
        LossyUtils.Mean16x4(input, dc);

        // assert
        Assert.True(dc.SequenceEqual(expectedDc));
    }

    private static void RunHadamardTransformTest()
    {
        // arrange
        byte[] a =
        [
            27, 27, 28, 29, 29, 28, 27, 27, 27, 28, 28, 29, 29, 28, 28, 27, 129, 129, 129, 129, 129, 129, 129,
            129, 128, 128, 128, 128, 128, 128, 128, 128, 27, 27, 27, 27, 27, 27, 27, 27, 27, 28, 28, 29, 29, 28,
            28, 27, 129, 129, 129, 129, 129, 129, 129, 129, 128, 128, 128, 128, 128, 128, 128, 128, 27, 27, 26,
            26, 26, 26, 27, 27, 27, 28, 28, 29, 29, 28, 28, 27, 129, 129, 129, 129, 129, 129, 129, 129, 128,
            128, 128, 128, 128, 128, 128, 128, 28, 27, 27, 26, 26, 27, 27, 28, 27, 28, 28, 29, 29, 28, 28, 27
        ];

        byte[] b =
        [
            28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 204, 204, 204, 204, 204, 204, 204,
            204, 204, 204, 204, 204, 204, 204, 204, 204, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
            28, 28, 204, 204, 204, 204, 204, 204, 204, 204, 204, 204, 204, 204, 204, 204, 204, 204, 28, 28, 28,
            28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 204, 204, 204, 204, 204, 204, 204, 204, 204,
            204, 204, 204, 204, 204, 204, 204, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28
        ];

        ushort[] w = [38, 32, 20, 9, 32, 28, 17, 7, 20, 17, 10, 4, 9, 7, 4, 2];
        int expected = 2;

        // act
        int actual = LossyUtils.Vp8Disto4X4(a, b, w, new int[16]);

        // assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RunTransformTwo_Works() => RunTransformTwoTest();

    [Fact]
    public void RunTransformOne_Works() => RunTransformOneTest();

    [Fact]
    public void Vp8Sse16X16_Works() => RunVp8Sse16X16Test();

    [Fact]
    public void Vp8Sse16X8_Works() => RunVp8Sse16X8Test();

    [Fact]
    public void Vp8Sse4X4_Works() => RunVp8Sse4X4Test();

    [Fact]
    public void Mean16x4_Works() => RunMean16x4Test();

    [Fact]
    public void HadamardTransform_Works() => RunHadamardTransformTest();

    [Fact]
    public void TransformTwo_WithHardwareIntrinsics_Works() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformTwoTest, HwIntrinsics.AllowAll);

    [Fact]
    public void TransformTwo_WithoutHardwareIntrinsics_Works() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformTwoTest, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void TransformOne_WithHardwareIntrinsics_Works() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformOneTest, HwIntrinsics.AllowAll);

    [Fact]
    public void TransformOne_WithoutHardwareIntrinsics_Works() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformOneTest, HwIntrinsics.DisableHWIntrinsic);

    // This will test the AVX2 or ARM version.
    [Fact]
    public void Vp8Sse16X16_WithHardwareIntrinsics_Works() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunVp8Sse16X16Test, HwIntrinsics.AllowAll);

    // This will test the SSE2 version.
    [Fact]
    public void Vp8Sse16X16_WithoutAVX2_Works()
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            return;
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunVp8Sse16X16Test, HwIntrinsics.DisableAVX2);
    }

    // This will test the fallback scalar version.
    [Fact]
    public void Vp8Sse16X16_WithoutHwIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunVp8Sse16X16Test, HwIntrinsics.DisableHWIntrinsic);

    // This will test the AVX2 or ARM version.
    [Fact]
    public void Vp8Sse16X8_WithHardwareIntrinsics_Works() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunVp8Sse16X8Test, HwIntrinsics.AllowAll);

    // This will test the SSE2 version.
    [Fact]
    public void Vp8Sse16X8_WithoutAVX2_Works()
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            return;
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunVp8Sse16X8Test, HwIntrinsics.DisableAVX2);
    }

    // This will test the fallback scalar version.
    [Fact]
    public void Vp8Sse16X8_WithoutHardwareIntrinsics_Works() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunVp8Sse16X8Test, HwIntrinsics.DisableHWIntrinsic);

    // This will test the AVX2 version or ARM version.
    [Fact]
    public void Vp8Sse4X4_WithHardwareIntrinsics_Works() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunVp8Sse4X4Test, HwIntrinsics.AllowAll);

    // This will test the SSE2 version.
    [Fact]
    public void Vp8Sse4X4_WithoutAVX2_Works()
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            return;
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunVp8Sse4X4Test, HwIntrinsics.DisableAVX2);
    }

    // This will test the fallback scalar version.
    [Fact]
    public void Vp8Sse4X4_WithoutHardwareIntrinsics_Works() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunVp8Sse4X4Test, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void Mean16x4_WithHardwareIntrinsics_Works() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunMean16x4Test, HwIntrinsics.AllowAll);

    [Fact]
    public void Mean16x4_WithoutHardwareIntrinsics_Works() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunMean16x4Test, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void HadamardTransform_WithHardwareIntrinsics_Works() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunHadamardTransformTest, HwIntrinsics.AllowAll);

    [Fact]
    public void HadamardTransform_WithoutHardwareIntrinsics_Works() =>
        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunHadamardTransformTest, HwIntrinsics.DisableHWIntrinsic);
}
