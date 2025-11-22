// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
public class LosslessUtilsTests
{
    private static void RunCombinedShannonEntropyTest()
    {
        int[] x = [3, 5, 2, 5, 3, 1, 2, 2, 3, 3, 1, 2, 1, 2, 1, 1, 0, 0, 0, 1, 1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 2, 0, 1, 1, 0, 0, 2, 1, 1, 0, 3, 1, 2, 3, 2, 3
        ];
        int[] y = [11, 12, 8, 3, 4, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 2, 2, 1, 1, 2, 4, 6, 4
        ];
        const float expected = 884.7585f;

        float actual = LosslessUtils.CombinedShannonEntropy(x, y);

        Assert.Equal(expected, actual, precision: 5);
    }

    private static void RunSubtractGreenTest()
    {
        uint[] pixelData =
        [
            4293035898, 4293101432, 4292903793, 4292838511, 4292837995, 4292771950, 4292903791, 4293299316,
            4293563769, 4293629303, 4293363312, 4291913575, 4289282905, 4288692313, 4289349210, 4289809240,
            4289743703, 4289874775, 4289940567, 4289743701, 4290137943, 4290860378, 4291058267, 4291386715,
            4291583836, 4291715937, 4291585379, 4291650657, 4291650143, 4291584863, 4291716451, 4291847521,
            4291913571, 4292044130, 4291978850, 4291847521, 4291847524, 4291847779, 4291913571, 4291848293,
            4291651689, 4291585895, 4291519584, 4291715936, 4291520355, 4291650658, 4291847263, 4291913313,
            4291847777, 4291781731, 4291783015
        ];

        uint[] expectedOutput =
        [
            4284188659, 4284254193, 4284318702, 4284187883, 4284318441, 4284383470, 4284318700, 4284124392,
            4283799012, 4283864546, 4284581610, 4285163264, 4284891926, 4284497945, 4284761620, 4284893965,
            4284828428, 4284959500, 4284959755, 4284828426, 4284960520, 4285289733, 4285159937, 4285292030,
            4285358077, 4285228030, 4284966398, 4285097213, 4285227773, 4285096956, 4285097470, 4285228540,
            4285163516, 4285425149, 4285294332, 4285228540, 4285228543, 4285163261, 4285163516, 4285032701,
            4284835841, 4284835584, 4284966140, 4285228029, 4284770300, 4285097214, 4285293819, 4285228795,
            4285163259, 4285228287, 4284901886
        ];

        LosslessUtils.SubtractGreenFromBlueAndRed(pixelData);

        Assert.Equal(expectedOutput, pixelData);
    }

    private static void RunAddGreenToBlueAndRedTest()
    {
        uint[] pixelData =
        [
            4284188659, 4284254193, 4284318702, 4284187883, 4284318441, 4284383470, 4284318700, 4284124392,
            4283799012, 4283864546, 4284581610, 4285163264, 4284891926, 4284497945, 4284761620, 4284893965,
            4284828428, 4284959500, 4284959755, 4284828426, 4284960520, 4285289733, 4285159937, 4285292030,
            4285358077, 4285228030, 4284966398, 4285097213, 4285227773, 4285096956, 4285097470, 4285228540,
            4285163516, 4285425149, 4285294332, 4285228540, 4285228543, 4285163261, 4285163516, 4285032701,
            4284835841, 4284835584, 4284966140, 4285228029, 4284770300, 4285097214, 4285293819, 4285228795,
            4285163259, 4285228287, 4284901886
        ];

        uint[] expectedOutput =
        [
            4293035898, 4293101432, 4292903793, 4292838511, 4292837995, 4292771950, 4292903791, 4293299316,
            4293563769, 4293629303, 4293363312, 4291913575, 4289282905, 4288692313, 4289349210, 4289809240,
            4289743703, 4289874775, 4289940567, 4289743701, 4290137943, 4290860378, 4291058267, 4291386715,
            4291583836, 4291715937, 4291585379, 4291650657, 4291650143, 4291584863, 4291716451, 4291847521,
            4291913571, 4292044130, 4291978850, 4291847521, 4291847524, 4291847779, 4291913571, 4291848293,
            4291651689, 4291585895, 4291519584, 4291715936, 4291520355, 4291650658, 4291847263, 4291913313,
            4291847777, 4291781731, 4291783015
        ];

        LosslessUtils.AddGreenToBlueAndRed(pixelData);

        Assert.Equal(expectedOutput, pixelData);
    }

    private static void RunTransformColorTest()
    {
        uint[] pixelData =
        [
            5998579, 65790, 130301, 16646653, 196350, 130565, 16712702, 16583164, 16452092, 65790, 782600,
            647446, 16571414, 16448771, 263931, 132601, 16711935, 131072, 511, 16711679, 132350, 329469,
            16647676, 132093, 66303, 16647169, 16515584, 196607, 196096, 16646655, 514, 131326, 16712192,
            327169, 16646655, 16776960, 3, 16712190, 511, 16646401, 16580612, 65535, 196092, 327425, 16319743,
            392450, 196861, 16712192, 16711680, 130564, 16451071
        ];

        Vp8LMultipliers m = new()
        {
            GreenToBlue = 240,
            GreenToRed = 232,
            RedToBlue = 0
        };

        uint[] expectedOutput =
        [
            100279, 65790, 16710907, 16712190, 130813, 65028, 131840, 264449, 133377, 65790, 61697, 15917319,
            14801924, 16317698, 591614, 394748, 16711935, 131072, 65792, 16711679, 328704, 656896, 132607,
            328703, 197120, 66563, 16646657, 196607, 130815, 16711936, 131587, 131326, 66049, 261632, 16711936,
            16776960, 3, 511, 65792, 16711938, 16580612, 65535, 65019, 327425, 16516097, 261377, 196861, 66049,
            16711680, 65027, 16712962
        ];

        LosslessUtils.TransformColor(m, pixelData, pixelData.Length);

        Assert.Equal(expectedOutput, pixelData);
    }

    private static void RunTransformColorInverseTest()
    {
        uint[] pixelData =
        [
            100279, 65790, 16710907, 16712190, 130813, 65028, 131840, 264449, 133377, 65790, 61697, 15917319,
            14801924, 16317698, 591614, 394748, 16711935, 131072, 65792, 16711679, 328704, 656896, 132607,
            328703, 197120, 66563, 16646657, 196607, 130815, 16711936, 131587, 131326, 66049, 261632, 16711936,
            16776960, 3, 511, 65792, 16711938, 16580612, 65535, 65019, 327425, 16516097, 261377, 196861, 66049,
            16711680, 65027, 16712962
        ];

        Vp8LMultipliers m = new()
        {
            GreenToBlue = 240,
            GreenToRed = 232,
            RedToBlue = 0
        };

        uint[] expectedOutput =
        [
            5998579, 65790, 130301, 16646653, 196350, 130565, 16712702, 16583164, 16452092, 65790, 782600,
            647446, 16571414, 16448771, 263931, 132601, 16711935, 131072, 511, 16711679, 132350, 329469,
            16647676, 132093, 66303, 16647169, 16515584, 196607, 196096, 16646655, 514, 131326, 16712192,
            327169, 16646655, 16776960, 3, 16712190, 511, 16646401, 16580612, 65535, 196092, 327425, 16319743,
            392450, 196861, 16712192, 16711680, 130564, 16451071
        ];

        LosslessUtils.TransformColorInverse(m, pixelData);

        Assert.Equal(expectedOutput, pixelData);
    }

    private static void RunPredictor11Test()
    {
        // arrange
        uint[] topData = [4278258949, 4278258949];
        const uint left = 4294839812;
        short[] scratch = new short[8];
        const uint expectedResult = 4294839812;

        // act
        unsafe
        {
            fixed (uint* top = &topData[1])
            {
                uint actual = LosslessUtils.Predictor11(left, top, scratch);

                // assert
                Assert.Equal(expectedResult, actual);
            }
        }
    }

    private static void RunPredictor12Test()
    {
        // arrange
        uint[] topData = [4294844413, 4294779388];
        const uint left = 4294844413;
        const uint expectedResult = 4294779388;

        // act
        unsafe
        {
            fixed (uint* top = &topData[1])
            {
                uint actual = LosslessUtils.Predictor12(left, top);

                // assert
                Assert.Equal(expectedResult, actual);
            }
        }
    }

    private static void RunPredictor13Test()
    {
        // arrange
        uint[] topData0 = [4278193922, 4278193666];
        const uint left0 = 4278193410;
        const uint expectedResult0 = 4278193154;
        uint[] topData1 = [4294933015, 4278219803];
        const uint left1 = 4278236686;
        const uint expectedResult1 = 4278231571;
        uint actual0 = 0;
        uint actual1 = 0;

        // act
        unsafe
        {
            fixed (uint* top = &topData0[1])
            {
                actual0 = LosslessUtils.Predictor13(left0, top);
            }

            fixed (uint* top = &topData1[1])
            {
                actual1 = LosslessUtils.Predictor13(left1, top);
            }
        }

        // assert
        Assert.Equal(expectedResult0, actual0);
        Assert.Equal(expectedResult1, actual1);
    }

    [Fact]
    public void BundleColorMap_WithXbitsZero_Works()
    {
        // arrange
        byte[] row = [238, 238, 238, 238, 238, 238, 240, 237, 240, 235, 223, 223, 218, 220, 226, 219, 220, 204, 218, 211, 218, 221, 254, 255
        ];
        int xBits = 0;
        uint[] actual = new uint[row.Length];
        uint[] expected =
        [
            4278251008, 4278251008, 4278251008, 4278251008, 4278251008,
            4278251008, 4278251520, 4278250752, 4278251520, 4278250240,
            4278247168, 4278247168, 4278245888, 4278246400, 4278247936,
            4278246144, 4278246400, 4278242304, 4278245888, 4278244096,
            4278245888, 4278246656, 4278255104, 4278255360
        ];

        // act
        LosslessUtils.BundleColorMap(row, actual.Length, xBits, actual);

        // assert
        Assert.True(actual.SequenceEqual(expected));
    }

    [Fact]
    public void BundleColorMap_WithXbitsNoneZero_Works()
    {
        // arrange
        byte[] row =
        [
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3
        ];
        int xBits = 2;
        uint[] actual = new uint[row.Length];
        uint[] expected =
        [
            4278233600, 4278233600, 4278233600, 4278233600, 4278255360, 4278255360, 4278255360, 4278255360, 4278233600, 4278233600, 4278233600, 4278233600,
            4278255360, 4278255360, 4278255360, 4278255360, 4278211840, 4278211840, 4278211840, 4278211840, 4278255360, 4278255360, 4278255360, 4278255360,
            4278255360, 4278255360, 4278255360, 4278255360, 4278255360, 4278255360, 4278255360, 4278206208, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        ];

        // act
        LosslessUtils.BundleColorMap(row, actual.Length, xBits, actual);

        // assert
        Assert.True(actual.SequenceEqual(expected));
    }

    [Fact]
    public void CombinedShannonEntropy_Works() => RunCombinedShannonEntropyTest();

    [Fact]
    public void Predictor11_Works() => RunPredictor11Test();

    [Fact]
    public void Predictor12_Works() => RunPredictor12Test();

    [Fact]
    public void Predictor13_Works() => RunPredictor13Test();

    [Fact]
    public void AddGreenToBlueAndRed_Works() => RunAddGreenToBlueAndRedTest();

    [Fact]
    public void TransformColor_Works() => RunTransformColorTest();

    [Fact]
    public void TransformColorInverse_Works() => RunTransformColorInverseTest();

    [Fact]
    public void CombinedShannonEntropy_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCombinedShannonEntropyTest, HwIntrinsics.AllowAll);

    [Fact]
    public void CombinedShannonEntropy_WithoutAVX2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunCombinedShannonEntropyTest, HwIntrinsics.DisableAVX2);

    [Fact]
    public void Predictor11_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPredictor11Test, HwIntrinsics.AllowAll);

    [Fact]
    public void Predictor11_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPredictor11Test, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void Predictor12_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPredictor12Test, HwIntrinsics.AllowAll);

    [Fact]
    public void Predictor12_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPredictor12Test, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void Predictor13_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPredictor13Test, HwIntrinsics.AllowAll);

    [Fact]
    public void Predictor13_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPredictor13Test, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void SubtractGreen_Works() => RunSubtractGreenTest();

    [Fact]
    public void SubtractGreen_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSubtractGreenTest, HwIntrinsics.AllowAll);

    [Fact]
    public void SubtractGreen_WithoutAVX2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSubtractGreenTest, HwIntrinsics.DisableAVX2);

    [Fact]
    public void SubtractGreen_Scalar_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSubtractGreenTest, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void SubtractGreen_WithoutAvxOrSSSE3_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSubtractGreenTest, HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void AddGreenToBlueAndRed_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunAddGreenToBlueAndRedTest, HwIntrinsics.AllowAll);

    [Fact]
    public void AddGreenToBlueAndRed_WithoutAVX2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunAddGreenToBlueAndRedTest, HwIntrinsics.DisableAVX2);

    [Fact]
    public void AddGreenToBlueAndRed_WithoutAVX2OrSSSE3_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunAddGreenToBlueAndRedTest, HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void TransformColor_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformColorTest, HwIntrinsics.AllowAll);

    [Fact]
    public void TransformColor_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformColorTest, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void TransformColor_WithoutAVX2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformColorTest, HwIntrinsics.DisableAVX2);

    [Fact]
    public void TransformColorInverse_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformColorInverseTest, HwIntrinsics.AllowAll);

    [Fact]
    public void TransformColorInverse_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformColorInverseTest, HwIntrinsics.DisableHWIntrinsic);

    [Fact]
    public void TransformColorInverse_WithoutAVX2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformColorInverseTest, HwIntrinsics.DisableAVX2);
}
