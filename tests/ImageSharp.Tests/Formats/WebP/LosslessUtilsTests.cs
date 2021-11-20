// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Webp
{
    [Trait("Format", "Webp")]
    public class LosslessUtilsTests
    {
        private static void RunSubtractGreenTest()
        {
            uint[] pixelData =
            {
                4293035898, 4293101432, 4292903793, 4292838511, 4292837995, 4292771950, 4292903791, 4293299316,
                4293563769, 4293629303, 4293363312, 4291913575, 4289282905, 4288692313, 4289349210, 4289809240,
                4289743703, 4289874775, 4289940567, 4289743701, 4290137943, 4290860378, 4291058267, 4291386715,
                4291583836, 4291715937, 4291585379, 4291650657, 4291650143, 4291584863, 4291716451, 4291847521,
                4291913571, 4292044130, 4291978850, 4291847521, 4291847524, 4291847779, 4291913571, 4291848293,
                4291651689, 4291585895, 4291519584, 4291715936, 4291520355, 4291650658, 4291847263, 4291913313,
                4291847777, 4291781731, 4291783015
            };

            uint[] expectedOutput =
            {
                4284188659, 4284254193, 4284318702, 4284187883, 4284318441, 4284383470, 4284318700, 4284124392,
                4283799012, 4283864546, 4284581610, 4285163264, 4284891926, 4284497945, 4284761620, 4284893965,
                4284828428, 4284959500, 4284959755, 4284828426, 4284960520, 4285289733, 4285159937, 4285292030,
                4285358077, 4285228030, 4284966398, 4285097213, 4285227773, 4285096956, 4285097470, 4285228540,
                4285163516, 4285425149, 4285294332, 4285228540, 4285228543, 4285163261, 4285163516, 4285032701,
                4284835841, 4284835584, 4284966140, 4285228029, 4284770300, 4285097214, 4285293819, 4285228795,
                4285163259, 4285228287, 4284901886
            };

            LosslessUtils.SubtractGreenFromBlueAndRed(pixelData);

            Assert.Equal(expectedOutput, pixelData);
        }

        private static void RunAddGreenToBlueAndRedTest()
        {
            uint[] pixelData =
            {
                4284188659, 4284254193, 4284318702, 4284187883, 4284318441, 4284383470, 4284318700, 4284124392,
                4283799012, 4283864546, 4284581610, 4285163264, 4284891926, 4284497945, 4284761620, 4284893965,
                4284828428, 4284959500, 4284959755, 4284828426, 4284960520, 4285289733, 4285159937, 4285292030,
                4285358077, 4285228030, 4284966398, 4285097213, 4285227773, 4285096956, 4285097470, 4285228540,
                4285163516, 4285425149, 4285294332, 4285228540, 4285228543, 4285163261, 4285163516, 4285032701,
                4284835841, 4284835584, 4284966140, 4285228029, 4284770300, 4285097214, 4285293819, 4285228795,
                4285163259, 4285228287, 4284901886
            };

            uint[] expectedOutput =
            {
                4293035898, 4293101432, 4292903793, 4292838511, 4292837995, 4292771950, 4292903791, 4293299316,
                4293563769, 4293629303, 4293363312, 4291913575, 4289282905, 4288692313, 4289349210, 4289809240,
                4289743703, 4289874775, 4289940567, 4289743701, 4290137943, 4290860378, 4291058267, 4291386715,
                4291583836, 4291715937, 4291585379, 4291650657, 4291650143, 4291584863, 4291716451, 4291847521,
                4291913571, 4292044130, 4291978850, 4291847521, 4291847524, 4291847779, 4291913571, 4291848293,
                4291651689, 4291585895, 4291519584, 4291715936, 4291520355, 4291650658, 4291847263, 4291913313,
                4291847777, 4291781731, 4291783015
            };

            LosslessUtils.AddGreenToBlueAndRed(pixelData);

            Assert.Equal(expectedOutput, pixelData);
        }

        private static void RunTransformColorTest()
        {
            uint[] pixelData =
            {
                5998579, 65790, 130301, 16646653, 196350, 130565, 16712702, 16583164, 16452092, 65790, 782600,
                647446, 16571414, 16448771, 263931, 132601, 16711935, 131072, 511, 16711679, 132350, 329469,
                16647676, 132093, 66303, 16647169, 16515584, 196607, 196096, 16646655, 514, 131326, 16712192,
                327169, 16646655, 16776960, 3, 16712190, 511, 16646401, 16580612, 65535, 196092, 327425, 16319743,
                392450, 196861, 16712192, 16711680, 130564, 16451071
            };

            var m = new Vp8LMultipliers()
            {
                GreenToBlue = 240,
                GreenToRed = 232,
                RedToBlue = 0
            };

            uint[] expectedOutput =
            {
                100279, 65790, 16710907, 16712190, 130813, 65028, 131840, 264449, 133377, 65790, 61697, 15917319,
                14801924, 16317698, 591614, 394748, 16711935, 131072, 65792, 16711679, 328704, 656896, 132607,
                328703, 197120, 66563, 16646657, 196607, 130815, 16711936, 131587, 131326, 66049, 261632, 16711936,
                16776960, 3, 511, 65792, 16711938, 16580612, 65535, 65019, 327425, 16516097, 261377, 196861, 66049,
                16711680, 65027, 16712962
            };

            LosslessUtils.TransformColor(m, pixelData, pixelData.Length);

            Assert.Equal(expectedOutput, pixelData);
        }

        private static void RunTransformColorInverseTest()
        {
            uint[] pixelData =
            {
                100279, 65790, 16710907, 16712190, 130813, 65028, 131840, 264449, 133377, 65790, 61697, 15917319,
                14801924, 16317698, 591614, 394748, 16711935, 131072, 65792, 16711679, 328704, 656896, 132607,
                328703, 197120, 66563, 16646657, 196607, 130815, 16711936, 131587, 131326, 66049, 261632, 16711936,
                16776960, 3, 511, 65792, 16711938, 16580612, 65535, 65019, 327425, 16516097, 261377, 196861, 66049,
                16711680, 65027, 16712962
            };

            var m = new Vp8LMultipliers()
            {
                GreenToBlue = 240,
                GreenToRed = 232,
                RedToBlue = 0
            };

            uint[] expectedOutput =
            {
                5998579, 65790, 130301, 16646653, 196350, 130565, 16712702, 16583164, 16452092, 65790, 782600,
                647446, 16571414, 16448771, 263931, 132601, 16711935, 131072, 511, 16711679, 132350, 329469,
                16647676, 132093, 66303, 16647169, 16515584, 196607, 196096, 16646655, 514, 131326, 16712192,
                327169, 16646655, 16776960, 3, 16712190, 511, 16646401, 16580612, 65535, 196092, 327425, 16319743,
                392450, 196861, 16712192, 16711680, 130564, 16451071
            };

            LosslessUtils.TransformColorInverse(m, pixelData);

            Assert.Equal(expectedOutput, pixelData);
        }

        private static void RunPredictor11Test()
        {
            // arrange
            uint[] topData = { 4278258949, 4278258949 };
            uint left = 4294839812;
            short[] scratch = new short[8];
            uint expectedResult = 4294839812;

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
            uint[] topData = { 4294844413, 4294779388 };
            uint left = 4294844413;
            uint expectedResult = 4294779388;

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
            uint[] topData = { 4278193922, 4278193666 };
            uint left = 4278193410;
            uint expectedResult = 4278193154;

            // act
            unsafe
            {
                fixed (uint* top = &topData[1])
                {
                    uint actual = LosslessUtils.Predictor13(left, top);

                    // assert
                    Assert.Equal(expectedResult, actual);
                }
            }
        }

        [Fact]
        public void Predictor11_Works() => RunPredictor11Test();

        [Fact]
        public void Predictor12_Works() => RunPredictor12Test();

        [Fact]
        public void Predictor13_Works() => RunPredictor13Test();

        [Fact]
        public void SubtractGreen_Works() => RunSubtractGreenTest();

        [Fact]
        public void AddGreenToBlueAndRed_Works() => RunAddGreenToBlueAndRedTest();

        [Fact]
        public void TransformColor_Works() => RunTransformColorTest();

        [Fact]
        public void TransformColorInverse_Works() => RunTransformColorInverseTest();

#if SUPPORTS_RUNTIME_INTRINSICS
        [Fact]
        public void Predictor11_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPredictor11Test, HwIntrinsics.AllowAll);

        [Fact]
        public void Predictor11_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPredictor11Test, HwIntrinsics.DisableSSE2);

        [Fact]
        public void Predictor12_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPredictor12Test, HwIntrinsics.AllowAll);

        [Fact]
        public void Predictor12_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPredictor12Test, HwIntrinsics.DisableSSE2);

        [Fact]
        public void Predictor13_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPredictor13Test, HwIntrinsics.AllowAll);

        [Fact]
        public void Predictor13_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunPredictor13Test, HwIntrinsics.DisableSSE2);

        [Fact]
        public void SubtractGreen_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSubtractGreenTest, HwIntrinsics.AllowAll);

        [Fact]
        public void SubtractGreen_WithoutAvx_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSubtractGreenTest, HwIntrinsics.DisableAVX);

        [Fact]
        public void SubtractGreen_WithoutAvxOrSSSE3_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSubtractGreenTest, HwIntrinsics.DisableAVX | HwIntrinsics.DisableSSSE3);

        [Fact]
        public void AddGreenToBlueAndRed_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunAddGreenToBlueAndRedTest, HwIntrinsics.AllowAll);

        [Fact]
        public void AddGreenToBlueAndRed_WithoutAvx_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunAddGreenToBlueAndRedTest, HwIntrinsics.DisableAVX);

        [Fact]
        public void AddGreenToBlueAndRed_WithoutAvxOrSSSE3_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunAddGreenToBlueAndRedTest, HwIntrinsics.DisableAVX | HwIntrinsics.DisableSSE2 | HwIntrinsics.DisableSSSE3);

        [Fact]
        public void TransformColor_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformColorTest, HwIntrinsics.AllowAll);

        [Fact]
        public void TransformColor_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformColorTest, HwIntrinsics.DisableSSE2);

        [Fact]
        public void TransformColor_WithoutAVX2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformColorTest, HwIntrinsics.DisableAVX2);

        [Fact]
        public void TransformColorInverse_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformColorInverseTest, HwIntrinsics.AllowAll);

        [Fact]
        public void TransformColorInverse_WithoutSSE2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformColorInverseTest, HwIntrinsics.DisableSSE2);

        [Fact]
        public void TransformColorInverse_WithoutAVX2_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTransformColorInverseTest, HwIntrinsics.DisableAVX2);
#endif
    }
}
