// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SharpAdler32 = ICSharpCode.SharpZipLib.Checksum.Adler32;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

[Trait("Format", "Png")]
public class Adler32Tests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CalculateAdler_ReturnsCorrectWhenEmpty(uint input) => Assert.Equal(input, Adler32.Calculate(input, default));

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(215)]
    [InlineData(1024)]
    [InlineData(1024 + 15)]
    [InlineData(2034)]
    [InlineData(4096)]
    public void CalculateAdler_MatchesReference(int length) => CalculateAdlerAndCompareToReference(length);

    private static void CalculateAdlerAndCompareToReference(int length)
    {
        // arrange
        byte[] data = GetBuffer(length);
        SharpAdler32 adler = new SharpAdler32();
        adler.Update(data);
        long expected = adler.Value;

        // act
        long actual = Adler32.Calculate(data);

        // assert
        Assert.Equal(expected, actual);
    }

    private static byte[] GetBuffer(int length)
    {
        byte[] data = new byte[length];
        new Random(1).NextBytes(data);

        return data;
    }

    [Fact]
    public void RunCalculateAdlerTest_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCalculateAdlerTest, HwIntrinsics.AllowAll);

    [Fact]
    public void RunCalculateAdlerTest_WithAvxDisabled_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCalculateAdlerTest, HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);

    [Fact]
    public void RunCalculateAdlerTest_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCalculateAdlerTest, HwIntrinsics.DisableHWIntrinsic);

    private static void RunCalculateAdlerTest()
    {
        int[] testData = { 0, 8, 215, 1024, 1024 + 15, 2034, 4096 };
        for (int i = 0; i < testData.Length; i++)
        {
            CalculateAdlerAndCompareToReference(testData[i]);
        }
    }
}
