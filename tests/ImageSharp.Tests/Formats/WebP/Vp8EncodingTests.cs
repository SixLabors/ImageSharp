// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
public class Vp8EncodingTests
{
    private static void RunFTransform2Test()
    {
        // arrange
        byte[] src = [154, 154, 151, 151, 149, 148, 151, 157, 163, 163, 154, 132, 102, 98, 104, 108, 107, 104, 104, 103, 101, 106, 123, 119, 170, 171, 172, 171, 168, 175, 171, 173, 151, 151, 149, 150, 147, 147, 146, 159, 164, 165, 154, 129, 92, 90, 101, 105, 104, 103, 104, 101, 100, 105, 123, 117, 172, 172, 172, 168, 170, 177, 170, 175, 151, 149, 150, 150, 147, 147, 156, 161, 161, 161, 151, 126, 93, 90, 102, 107, 104, 103, 104, 101, 104, 104, 122, 117, 172, 172, 170, 168, 170, 177, 172, 175, 150, 149, 152, 151, 148, 151, 160, 159, 157, 157, 148, 133, 96, 90, 103, 107, 104, 104, 101, 100, 102, 102, 121, 117, 170, 170, 169, 171, 171, 179, 173, 175
        ];
        byte[] reference = [128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129
        ];
        short[] actualOutput1 = new short[16];
        short[] actualOutput2 = new short[16];
        short[] expectedOutput1 = [182, 4, 1, 1, 6, 7, -1, -4, 5, 0, -2, 1, 2, 1, 1, 1];
        short[] expectedOutput2 = [192, -34, 10, 1, -11, 8, 10, -7, 6, 3, -8, 4, 5, -3, -2, 6];

        // act
        Vp8Encoding.FTransform2(src, reference, actualOutput1, actualOutput2, new int[16]);

        // assert
        Assert.True(expectedOutput1.SequenceEqual(actualOutput1));
        Assert.True(expectedOutput2.SequenceEqual(actualOutput2));
    }

    private static void RunFTransformTest()
    {
        // arrange
        byte[] src =
        [
            154, 154, 151, 151, 149, 148, 151, 157, 163, 163, 154, 132, 102, 98, 104, 108, 107, 104, 104, 103,
            101, 106, 123, 119, 170, 171, 172, 171, 168, 175, 171, 173, 151, 151, 149, 150, 147, 147, 146, 159,
            164, 165, 154, 129, 92, 90, 101, 105, 104, 103, 104, 101, 100, 105, 123, 117, 172, 172, 172, 168,
            170, 177, 170, 175, 151, 149, 150, 150, 147, 147, 156, 161, 161, 161, 151, 126, 93, 90, 102, 107,
            104, 103, 104, 101, 104, 104, 122, 117, 172, 172, 170, 168, 170, 177, 172, 175, 150, 149, 152, 151,
            148, 151, 160, 159, 157, 157, 148, 133, 96, 90, 103, 107, 104, 104, 101, 100, 102, 102, 121, 117,
            170, 170, 169, 171, 171, 179, 173, 175
        ];
        byte[] reference =
        [
            128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129,
            129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 128, 128, 128, 128, 128, 128, 128, 128,
            128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129,
            129, 129, 129, 129, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128,
            129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 128, 128, 128, 128,
            128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129, 129, 129, 129, 129,
            129, 129, 129, 129, 129, 129, 129, 129
        ];
        short[] actualOutput = new short[16];
        short[] expectedOutput = [182, 4, 1, 1, 6, 7, -1, -4, 5, 0, -2, 1, 2, 1, 1, 1];

        // act
        Vp8Encoding.FTransform(src, reference, actualOutput, new int[16]);

        // assert
        Assert.True(expectedOutput.SequenceEqual(actualOutput));
    }

    private static void RunOneInverseTransformTest()
    {
        // arrange
        byte[] reference =
        [
            128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129,
            129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 128, 128, 128, 128, 128, 128, 128, 128,
            128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129,
            129, 129, 129, 129, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128,
            129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 128, 128, 128, 128,
            128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129, 129, 129, 129, 129,
            129, 129, 129, 129, 129, 129, 129, 129
        ];
        short[] input = [1, 216, -48, 0, 96, -24, -48, 24, 0, -24, 24, 0, 0, 0, 0, 0, 38, -240, -72, -24, 0, -24, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        ];
        byte[] dst = new byte[128];
        byte[] expected =
        [
            161, 160, 149, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 160, 160, 133, 85, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 156, 147, 109, 76, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 152, 128, 87, 83, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0
        ];
        int[] scratch = new int[16];

        // act
        Vp8Encoding.ITransformOne(reference, input, dst, scratch);

        // assert
        Assert.True(dst.SequenceEqual(expected));
    }

    private static void RunTwoInverseTransformTest()
    {
        // arrange
        byte[] reference =
        [
            128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129,
            129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 128, 128, 128, 128, 128, 128, 128, 128,
            128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129,
            129, 129, 129, 129, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128,
            129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 129, 128, 128, 128, 128,
            128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 129, 129, 129, 129, 129, 129, 129,
            129, 129, 129, 129, 129, 129, 129, 129
        ];
        short[] input = [1, 216, -48, 0, 96, -24, -48, 24, 0, -24, 24, 0, 0, 0, 0, 0, 38, -240, -72, -24, 0, -24, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        ];
        byte[] dst = new byte[128];
        byte[] expected =
        [
            161, 160, 149, 105, 78, 127, 156, 170, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 160, 160, 133, 85, 81, 129, 155, 167, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 156, 147, 109, 76, 85, 130, 153, 163, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 152, 128, 87, 83, 88, 132, 152, 159, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        ];
        int[] scratch = new int[16];

        // act
        Vp8Encoding.ITransformTwo(reference, input, dst, scratch);

        // assert
        Assert.True(dst.SequenceEqual(expected));
    }

    [Fact]
    public void FTransform2_Works() => RunFTransform2Test();

    [Fact]
    public void FTransform_Works() => RunFTransformTest();

    [Fact]
    public void OneInverseTransform_Works() => RunOneInverseTransformTest();

    [Fact]
    public void TwoInverseTransform_Works() => RunTwoInverseTransformTest();

    [Fact]
    public void FTransform2_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunFTransform2Test, HwIntrinsics.AllowAll);

    [Fact]
    public void FTransform2_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunFTransform2Test, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void FTransform_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunFTransformTest, HwIntrinsics.AllowAll);

    [Fact]
    public void FTransform_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunFTransformTest, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void OneInverseTransform_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunOneInverseTransformTest, HwIntrinsics.AllowAll);

    [Fact]
    public void OneInverseTransform_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunOneInverseTransformTest, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void TwoInverseTransform_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTwoInverseTransformTest, HwIntrinsics.AllowAll);

    [Fact]
    public void TwoInverseTransform_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTwoInverseTransformTest, HwIntrinsics.DisableHWIntrinsic);
}
