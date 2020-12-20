// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Experimental.Webp.Lossless;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.WebP
{
    [Trait("Format", "Webp")]
    public class LosslessUtilsTests
    {
        private static void RunSubstractGreenTest()
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

        [Fact]
        public void SubtractGreen_Works()
        {
            RunSubstractGreenTest();
        }

        [Fact]
        public void AddGreenToBlueAndRed_Works()
        {
            RunAddGreenToBlueAndRedTest();
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Fact]
        public void SubtractGreen_WithHardwareIntrinsics_Works()
        {
            FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSubstractGreenTest, HwIntrinsics.AllowAll);
        }

        [Fact]
        public void SubtractGreen_WithoutAvx_Works()
        {
            FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSubstractGreenTest, HwIntrinsics.DisableAVX);
        }

        [Fact]
        public void SubtractGreen_WithoutAvxOrSSSE3_Works()
        {
            FeatureTestRunner.RunWithHwIntrinsicsFeature(RunSubstractGreenTest, HwIntrinsics.DisableAVX | HwIntrinsics.DisableSSSE3);
        }

        [Fact]
        public void AddGreenToBlueAndRed_WithHardwareIntrinsics_Works()
        {
            FeatureTestRunner.RunWithHwIntrinsicsFeature(RunAddGreenToBlueAndRedTest, HwIntrinsics.AllowAll);
        }

        [Fact]
        public void AddGreenToBlueAndRed_WithoutAvx_Works()
        {
            FeatureTestRunner.RunWithHwIntrinsicsFeature(RunAddGreenToBlueAndRedTest, HwIntrinsics.DisableAVX);
        }

        [Fact]
        public void AddGreenToBlueAndRed_WithoutAvxOrSSSE3_Works()
        {
            FeatureTestRunner.RunWithHwIntrinsicsFeature(RunAddGreenToBlueAndRedTest, HwIntrinsics.DisableAVX | HwIntrinsics.DisableSSE2 | HwIntrinsics.DisableSSSE3);
        }
#endif
    }
}
