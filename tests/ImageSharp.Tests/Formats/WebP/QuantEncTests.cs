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

    [Fact]
    public void QuantizeBlock_Works() => RunQuantizeBlockTest();

    [Fact]
    public void QuantizeBlock_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunQuantizeBlockTest, HwIntrinsics.AllowAll);

    [Fact]
    public void QuantizeBlock_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunQuantizeBlockTest, HwIntrinsics.DisableSSE42);

    [Fact]
    public void QuantizeBlock_WithoutAVX2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunQuantizeBlockTest, HwIntrinsics.DisableAVX2);
}
