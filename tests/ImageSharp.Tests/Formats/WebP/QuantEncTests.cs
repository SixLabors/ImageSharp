// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
public class QuantEncTests
{
    private static unsafe void RunQuantizeBlockTest()
    {
        // arrange
        short[] input = [378, 777, -851, 888, 259, 148, 0, -111, -185, -185, -74, -37, 148, 74, 111, 74];
        short[] output = new short[16];
        ushort[] q = [42, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37, 37];
        ushort[] iq = [3120, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542, 3542];
        uint[] bias = [49152, 55296, 55296, 55296, 55296, 55296, 55296, 55296, 55296, 55296, 55296, 55296, 55296, 55296, 55296, 55296
        ];
        uint[] zthresh = [26, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21];
        short[] expectedOutput = [9, 21, 7, -5, 4, -23, 24, 0, -5, 4, 2, -2, -3, -1, 3, 2];
        int expectedResult = 1;
        Vp8Matrix vp8Matrix = default;
        for (int i = 0; i < 16; i++)
        {
            vp8Matrix.Q[i] = q[i];
            vp8Matrix.IQ[i] = iq[i];
            vp8Matrix.Bias[i] = bias[i];
            vp8Matrix.ZThresh[i] = zthresh[i];
        }

        // act
        int actualResult = QuantEnc.QuantizeBlock(input, output, ref vp8Matrix);

        // assert
        Assert.True(output.SequenceEqual(expectedOutput));
        Assert.Equal(expectedResult, actualResult);
    }

    private static void RunQuantizeBlockWithSharpeningTest()
    {
        // arrange
        // A luma AC matrix (type 0) gets non-zero sharpening for every AC coefficient. With Q = 200 the
        // sharpen values are 2, 5 or 8, and the inputs sit just below a level boundary, so several levels
        // are only reached when the sharpening is added before quantizing.
        short[] input = [300, -110, 112, -310, 113, -500, 290, -115, 700, -108, 295, -490, 111, -298, 505, -299];
        short[] output = new short[16];
        short[] expectedOutput = [7, 0, 1, 3, -2, 1, -2, 1, -1, 1, -1, 1, -1, -2, 2, -1];
        short[] expectedInput = [280, 0, 200, -400, 200, -400, 200, -200, 600, -200, 200, -400, 200, -200, 400, -200];
        Vp8Matrix vp8Matrix = default;
        vp8Matrix.Q[0] = 40;
        vp8Matrix.Q[1] = 200;
        vp8Matrix.Expand(0);

        // act
        int actualResult = QuantEnc.QuantizeBlock(input, output, ref vp8Matrix);

        // assert
        Assert.Equal(expectedOutput, output);
        Assert.Equal(expectedInput, input);
        Assert.Equal(1, actualResult);
    }

    [Fact]
    public void QuantizeBlock_Works() => RunQuantizeBlockTest();

    [Fact]
    public void QuantizeBlock_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunQuantizeBlockTest, HwIntrinsics.AllowAll);

    [Fact]
    public void QuantizeBlock_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunQuantizeBlockTest, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void QuantizeBlock_WithoutAVX2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunQuantizeBlockTest, HwIntrinsics.DisableAVX2);

    [Fact]
    public void QuantizeBlock_WithSharpening_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunQuantizeBlockWithSharpeningTest, HwIntrinsics.AllowAll);

    [Fact]
    public void QuantizeBlock_WithSharpening_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunQuantizeBlockWithSharpeningTest, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void QuantizeBlock_WithSharpening_WithoutAVX2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunQuantizeBlockWithSharpeningTest, HwIntrinsics.DisableAVX2);
}
