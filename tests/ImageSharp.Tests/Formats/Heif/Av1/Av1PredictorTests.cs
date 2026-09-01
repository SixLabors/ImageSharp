// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 intra prediction against scalar definitions across the supported hardware-intrinsic configurations.
/// </summary>
[Trait("Format", "Heif")]
public class Av1PredictorTests
{
    /// <summary>
    /// The offset within directional reference storage that leaves readable samples before both edge origins.
    /// </summary>
    private const int ReferenceOrigin = 128;

    /// <summary>
    /// The hardware configurations required to exercise each SIMD tier and the complete scalar fallback.
    /// </summary>
    private const HwIntrinsics PredictorConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Gets the cardinal, base, and adjusted angles covering every directional projection zone.
    /// </summary>
    private static ReadOnlySpan<int> DirectionalAngles => [36, 45, 54, 67, 90, 104, 113, 126, 135, 148, 157, 166, 180, 194, 203, 212];

    /// <summary>
    /// Gets the complete set of AV1 filter-intra coefficient modes.
    /// </summary>
    private static ReadOnlySpan<Av1FilterIntraMode> FilterIntraModes =>
    [
        Av1FilterIntraMode.DC,
        Av1FilterIntraMode.Vertical,
        Av1FilterIntraMode.Horizontal,
        Av1FilterIntraMode.Directional157,
        Av1FilterIntraMode.Paeth,
    ];

    /// <summary>
    /// Verifies DC prediction with each register-width tier and the scalar fallback.
    /// </summary>
    [Fact]
    public void DcPredictorsMatchReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateDcPredictors, PredictorConfigurations);

    /// <summary>
    /// Verifies horizontal prediction with each register-width tier and the scalar fallback.
    /// </summary>
    [Fact]
    public void HorizontalPredictorMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateHorizontalPredictor, PredictorConfigurations);

    /// <summary>
    /// Verifies vertical prediction with each register-width tier and the scalar fallback.
    /// </summary>
    [Fact]
    public void VerticalPredictorMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateVerticalPredictor, PredictorConfigurations);

    /// <summary>
    /// Verifies Paeth prediction with each register-width tier and the scalar fallback.
    /// </summary>
    [Fact]
    public void PaethPredictorMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidatePaethPredictor, PredictorConfigurations);

    /// <summary>
    /// Verifies smooth prediction with each register-width tier and the scalar fallback.
    /// </summary>
    [Fact]
    public void SmoothPredictorMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateSmoothPredictor, PredictorConfigurations);

    /// <summary>
    /// Verifies horizontal smooth prediction with each register-width tier and the scalar fallback.
    /// </summary>
    [Fact]
    public void SmoothHorizontalPredictorMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateSmoothHorizontalPredictor, PredictorConfigurations);

    /// <summary>
    /// Verifies vertical smooth prediction with each register-width tier and the scalar fallback.
    /// </summary>
    [Fact]
    public void SmoothVerticalPredictorMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateSmoothVerticalPredictor, PredictorConfigurations);

    /// <summary>
    /// Verifies directional prediction with each register-width tier and the scalar fallback.
    /// </summary>
    [Fact]
    public void DirectionalPredictorsMatchReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateDirectionalPredictors, PredictorConfigurations);

    /// <summary>
    /// Verifies filter-intra prediction with each register-width tier and the scalar fallback.
    /// </summary>
    [Fact]
    public void FilterIntraPredictorsMatchReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateFilterIntraPredictors, PredictorConfigurations);

    /// <summary>
    /// Verifies intra-edge upsampling with Vector128 and the scalar fallback.
    /// </summary>
    [Fact]
    public void EdgeUpsamplingMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateEdgeUpsampling, HwIntrinsics.AllowAll | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies intra-edge filtering with Vector128 and the scalar fallback.
    /// </summary>
    [Fact]
    public void EdgeFilteringMatchesReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateEdgeFiltering, HwIntrinsics.AllowAll | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies the traversal-order bits that distinguish current libaom's mixed-vertical square tables.
    /// </summary>
    [Fact]
    public void MixedVerticalAvailabilityUsesDedicatedSquareTables()
    {
        Assert.True(Av1BottomRightTopLeftConstants.HasTopRight(Av1PartitionType.Split, Av1BlockSize.Block8x8, 16));
        Assert.False(Av1BottomRightTopLeftConstants.HasTopRight(Av1PartitionType.VerticalA, Av1BlockSize.Block8x8, 16));
        Assert.False(Av1BottomRightTopLeftConstants.HasTopRight(Av1PartitionType.VerticalB, Av1BlockSize.Block8x8, 16));

        Assert.False(Av1BottomRightTopLeftConstants.HasBottomLeft(Av1PartitionType.Split, Av1BlockSize.Block8x8, 1));
        Assert.True(Av1BottomRightTopLeftConstants.HasBottomLeft(Av1PartitionType.VerticalA, Av1BlockSize.Block8x8, 1));
        Assert.True(Av1BottomRightTopLeftConstants.HasBottomLeft(Av1PartitionType.VerticalB, Av1BlockSize.Block8x8, 1));
    }

    /// <summary>
    /// Verifies that mixed-vertical rectangles use current libaom's ordinary rectangle tables.
    /// </summary>
    [Theory]
    [InlineData((int)Av1BlockSize.Block4x8)]
    [InlineData((int)Av1BlockSize.Block8x16)]
    [InlineData((int)Av1BlockSize.Block16x32)]
    [InlineData((int)Av1BlockSize.Block32x64)]
    [InlineData((int)Av1BlockSize.Block64x128)]
    public void MixedVerticalAvailabilityReusesVerticalRectangleTables(int blockSizeValue)
    {
        Av1BlockSize blockSize = (Av1BlockSize)blockSizeValue;
        bool expectedTopRight = Av1BottomRightTopLeftConstants.HasTopRight(Av1PartitionType.Split, blockSize, 0);
        bool expectedBottomLeft = Av1BottomRightTopLeftConstants.HasBottomLeft(Av1PartitionType.Split, blockSize, 0);

        Assert.Equal(expectedTopRight, Av1BottomRightTopLeftConstants.HasTopRight(Av1PartitionType.VerticalA, blockSize, 0));
        Assert.Equal(expectedTopRight, Av1BottomRightTopLeftConstants.HasTopRight(Av1PartitionType.VerticalB, blockSize, 0));
        Assert.Equal(expectedBottomLeft, Av1BottomRightTopLeftConstants.HasBottomLeft(Av1PartitionType.VerticalA, blockSize, 0));
        Assert.Equal(expectedBottomLeft, Av1BottomRightTopLeftConstants.HasBottomLeft(Av1PartitionType.VerticalB, blockSize, 0));
    }

    /// <summary>
    /// Verifies all four DC neighbor-availability combinations at every AV1 transform size.
    /// </summary>
    private static void ValidateDcPredictors()
    {
        for (int sizeIndex = 0; sizeIndex < (int)Av1TransformSize.AllSizes; sizeIndex++)
        {
            Av1TransformSize transformSize = (Av1TransformSize)sizeIndex;
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int stride = width + 5;
            byte[] above = CreateByteSamples(width, 17);
            byte[] left = CreateByteSamples(height, 43);
            short[] aboveHigh = CreateHighBitDepthSamples(width, 17);
            short[] leftHigh = CreateHighBitDepthSamples(height, 43);

            for (int availability = 0; availability < 4; availability++)
            {
                bool hasLeft = (availability & 1) != 0;
                bool hasAbove = (availability & 2) != 0;
                byte[] expected = CreateByteDestination(stride, height);
                byte[] actual = CreateByteDestination(stride, height);
                short[] expectedHigh = CreateHighBitDepthDestination(stride, height);
                short[] actualHigh = CreateHighBitDepthDestination(stride, height);

                Av1DcIntraPredictor.PredictScalar(hasLeft, hasAbove, expected, stride, above, left, width, height);
                Av1DcIntraPredictor.Predict(hasLeft, hasAbove, actual, stride, above, left, width, height);
                Av1DcIntraPredictor.PredictScalar(hasLeft, hasAbove, expectedHigh, stride, aboveHigh, leftHigh, width, height, 12);
                Av1DcIntraPredictor.Predict(hasLeft, hasAbove, actualHigh, stride, aboveHigh, leftHigh, width, height, 12);

                Assert.Equal(expected, actual);
                Assert.Equal(expectedHigh, actualHigh);
            }
        }
    }

    /// <summary>
    /// Verifies horizontal prediction at every AV1 transform size and sample precision.
    /// </summary>
    private static void ValidateHorizontalPredictor() => ValidateNonDirectionalPredictor(Av1PredictionMode.Horizontal);

    /// <summary>
    /// Verifies vertical prediction at every AV1 transform size and sample precision.
    /// </summary>
    private static void ValidateVerticalPredictor() => ValidateNonDirectionalPredictor(Av1PredictionMode.Vertical);

    /// <summary>
    /// Verifies Paeth prediction at every AV1 transform size and sample precision.
    /// </summary>
    private static void ValidatePaethPredictor() => ValidateNonDirectionalPredictor(Av1PredictionMode.Paeth);

    /// <summary>
    /// Verifies smooth prediction at every AV1 transform size and sample precision.
    /// </summary>
    private static void ValidateSmoothPredictor() => ValidateNonDirectionalPredictor(Av1PredictionMode.Smooth);

    /// <summary>
    /// Verifies horizontal smooth prediction at every AV1 transform size and sample precision.
    /// </summary>
    private static void ValidateSmoothHorizontalPredictor() => ValidateNonDirectionalPredictor(Av1PredictionMode.SmoothHorizontal);

    /// <summary>
    /// Verifies vertical smooth prediction at every AV1 transform size and sample precision.
    /// </summary>
    private static void ValidateSmoothVerticalPredictor() => ValidateNonDirectionalPredictor(Av1PredictionMode.SmoothVertical);

    /// <summary>
    /// Verifies one closed non-directional operator at every AV1 transform size and sample precision.
    /// </summary>
    /// <param name="mode">The prediction mode to verify.</param>
    private static void ValidateNonDirectionalPredictor(Av1PredictionMode mode)
    {
        Av1NonDirectionalIntraPredictorBase predictor = Av1NonDirectionalIntraPredictorBase.GetPredictor(mode);
        for (int sizeIndex = 0; sizeIndex < (int)Av1TransformSize.AllSizes; sizeIndex++)
        {
            Av1TransformSize transformSize = (Av1TransformSize)sizeIndex;
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            int stride = width + 5;
            byte[] aboveStorage = CreateByteSamples(width + 1, 19);
            byte[] left = CreateByteSamples(height, 71);
            short[] aboveHighStorage = CreateHighBitDepthSamples(width + 1, 19);
            short[] leftHigh = CreateHighBitDepthSamples(height, 71);
            byte[] expected = CreateByteDestination(stride, height);
            byte[] actual = CreateByteDestination(stride, height);
            short[] expectedHigh = CreateHighBitDepthDestination(stride, height);
            short[] actualHigh = CreateHighBitDepthDestination(stride, height);

            predictor.PredictScalar(expected, stride, aboveStorage.AsSpan(1), left, width, height);
            predictor.Predict(actual, stride, aboveStorage.AsSpan(1), left, width, height);
            predictor.PredictScalar(expectedHigh, stride, aboveHighStorage.AsSpan(1), leftHigh, width, height);
            predictor.Predict(actualHigh, stride, aboveHighStorage.AsSpan(1), leftHigh, width, height);

            Assert.Equal(expected, actual);
            Assert.Equal(expectedHigh, actualHigh);
        }

        ValidateKnownNonDirectionalVector(mode, predictor);
    }

    /// <summary>
    /// Verifies one non-directional operator against a byte-exact reference block and its translated high-bit-depth equivalent.
    /// </summary>
    /// <param name="mode">The prediction mode being verified.</param>
    /// <param name="predictor">The closed operator-driven predictor.</param>
    private static void ValidateKnownNonDirectionalVector(Av1PredictionMode mode, Av1NonDirectionalIntraPredictorBase predictor)
    {
        byte[] aboveStorage;
        byte[] left;
        byte[] expected;

        if (mode == Av1PredictionMode.Paeth)
        {
            aboveStorage = [50, 60, 10, 90, 40];
            left = [20, 80, 30, 100];
            expected =
            [
                20, 10, 50, 20,
                80, 50, 90, 80,
                30, 10, 90, 30,
                100, 50, 100, 100,
            ];
        }
        else
        {
            aboveStorage = [0, 20, 40, 60, 80];
            left = [20, 40, 60, 80];
            expected = mode switch
            {
                Av1PredictionMode.Horizontal =>
                [
                    20, 20, 20, 20,
                    40, 40, 40, 40,
                    60, 60, 60, 60,
                    80, 80, 80, 80,
                ],
                Av1PredictionMode.Vertical =>
                [
                    20, 40, 60, 80,
                    20, 40, 60, 80,
                    20, 40, 60, 80,
                    20, 40, 60, 80,
                ],
                Av1PredictionMode.Smooth =>
                [
                    20, 43, 60, 73,
                    43, 57, 68, 75,
                    60, 68, 73, 78,
                    73, 75, 78, 80,
                ],
                Av1PredictionMode.SmoothHorizontal =>
                [
                    20, 45, 60, 65,
                    40, 57, 67, 70,
                    60, 68, 73, 75,
                    80, 80, 80, 80,
                ],
                _ =>
                [
                    20, 40, 60, 80,
                    45, 57, 68, 80,
                    60, 67, 73, 80,
                    65, 70, 75, 80,
                ],
            };
        }

        byte[] actual = new byte[16];
        predictor.Predict(actual, 4, aboveStorage.AsSpan(1), left, 4, 4);

        Assert.Equal(expected, actual);

        const int offset = 512;
        short[] aboveHighStorage = new short[aboveStorage.Length];
        short[] leftHigh = new short[left.Length];
        short[] expectedHigh = new short[expected.Length];
        short[] actualHigh = new short[16];

        for (int i = 0; i < aboveStorage.Length; i++)
        {
            aboveHighStorage[i] = (short)(aboveStorage[i] + offset);
        }

        for (int i = 0; i < left.Length; i++)
        {
            leftHigh[i] = (short)(left[i] + offset);
        }

        for (int i = 0; i < expected.Length; i++)
        {
            expectedHigh[i] = (short)(expected[i] + offset);
        }

        predictor.Predict(actualHigh, 4, aboveHighStorage.AsSpan(1), leftHigh, 4, 4);

        Assert.Equal(expectedHigh, actualHigh);
    }

    /// <summary>
    /// Verifies every directional zone, rectangular transpose, and edge-upsampling index rule.
    /// </summary>
    private static void ValidateDirectionalPredictors()
    {
        byte[] aboveStorage = CreateByteSamples(512, 23);
        byte[] leftStorage = CreateByteSamples(512, 89);
        short[] aboveHighStorage = CreateHighBitDepthSamples(512, 23);
        short[] leftHighStorage = CreateHighBitDepthSamples(512, 89);
        ReadOnlySpan<byte> above = aboveStorage.AsSpan(ReferenceOrigin);
        ReadOnlySpan<byte> left = leftStorage.AsSpan(ReferenceOrigin);
        ReadOnlySpan<short> aboveHigh = aboveHighStorage.AsSpan(ReferenceOrigin);
        ReadOnlySpan<short> leftHigh = leftHighStorage.AsSpan(ReferenceOrigin);

        foreach (int angle in DirectionalAngles)
        {
            for (int sizeIndex = 0; sizeIndex < (int)Av1TransformSize.AllSizes; sizeIndex++)
            {
                ValidateDirectionalCase((Av1TransformSize)sizeIndex, angle, false, false, above, left, aboveHigh, leftHigh);
            }
        }

        // Edge upsampling is permitted only for small blocks. These cases exercise top-only, both-edge,
        // and left-only indexing without asking an invalid large transform to consume an upsampled edge.
        ValidateDirectionalCase(Av1TransformSize.Size4x4, 45, true, false, above, left, aboveHigh, leftHigh);
        ValidateDirectionalCase(Av1TransformSize.Size4x4, 135, true, true, above, left, aboveHigh, leftHigh);
        ValidateDirectionalCase(Av1TransformSize.Size4x4, 203, false, true, above, left, aboveHigh, leftHigh);
        ValidateKnownDirectionalVectors();
    }

    /// <summary>
    /// Verifies all three projection zones against byte-exact reference blocks.
    /// </summary>
    private static void ValidateKnownDirectionalVectors()
    {
        ValidateKnownDirectionalVector(
            45,
            [0, 10, 20, 30, 40, 50, 60, 70, 80, 90],
            [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [
                20, 30, 40, 50,
                30, 40, 50, 60,
                40, 50, 60, 70,
                50, 60, 70, 80,
            ]);

        ValidateKnownDirectionalVector(
            135,
            [5, 10, 20, 30, 40, 50, 60, 70, 80, 90],
            [5, 50, 60, 70, 80, 90, 100, 110, 120, 130],
            [
                5, 10, 20, 30,
                50, 5, 10, 20,
                60, 50, 5, 10,
                70, 60, 50, 5,
            ]);

        ValidateKnownDirectionalVector(
            203,
            [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            [0, 10, 20, 30, 40, 50, 60, 70, 80, 90],
            [
                14, 18, 23, 27,
                24, 28, 33, 37,
                34, 38, 43, 47,
                44, 48, 53, 57,
            ]);
    }

    /// <summary>
    /// Verifies one directional projection and its translated high-bit-depth equivalent.
    /// </summary>
    /// <param name="angle">The adjusted directional angle.</param>
    /// <param name="aboveStorage">The top-left prefix followed by the top reference.</param>
    /// <param name="leftStorage">The top-left prefix followed by the left reference.</param>
    /// <param name="expected">The byte-exact predicted block.</param>
    private static void ValidateKnownDirectionalVector(
        int angle,
        ReadOnlySpan<byte> aboveStorage,
        ReadOnlySpan<byte> leftStorage,
        ReadOnlySpan<byte> expected)
    {
        ReadOnlySpan<byte> above = aboveStorage[1..];
        ReadOnlySpan<byte> left = leftStorage[1..];

        byte[] actual = new byte[16];
        byte[] scratch = new byte[Av1DirectionalIntraPredictor.ScratchLength];
        Av1DirectionalIntraPredictor.Predict(actual, 4, Av1TransformSize.Size4x4, above, left, false, false, angle, scratch);

        Assert.Equal(expected, actual);

        const int offset = 512;
        short[] aboveHighStorage = new short[aboveStorage.Length];
        short[] leftHighStorage = new short[leftStorage.Length];
        short[] expectedHigh = new short[expected.Length];
        short[] actualHigh = new short[16];
        short[] scratchHigh = new short[Av1DirectionalIntraPredictor.ScratchLength];

        for (int i = 0; i < aboveStorage.Length; i++)
        {
            aboveHighStorage[i] = (short)(aboveStorage[i] + offset);
        }

        for (int i = 0; i < leftStorage.Length; i++)
        {
            leftHighStorage[i] = (short)(leftStorage[i] + offset);
        }

        for (int i = 0; i < expected.Length; i++)
        {
            expectedHigh[i] = (short)(expected[i] + offset);
        }

        Av1DirectionalIntraPredictor.Predict(
            actualHigh,
            4,
            Av1TransformSize.Size4x4,
            aboveHighStorage.AsSpan(1),
            leftHighStorage.AsSpan(1),
            false,
            false,
            angle,
            scratchHigh);

        Assert.Equal(expectedHigh, actualHigh);
    }

    /// <summary>
    /// Verifies one directional prediction configuration for both native sample representations.
    /// </summary>
    private static void ValidateDirectionalCase(
        Av1TransformSize transformSize,
        int angle,
        bool upsampleAbove,
        bool upsampleLeft,
        ReadOnlySpan<byte> above,
        ReadOnlySpan<byte> left,
        ReadOnlySpan<short> aboveHigh,
        ReadOnlySpan<short> leftHigh)
    {
        int width = transformSize.GetWidth();
        int height = transformSize.GetHeight();
        int stride = width + 5;
        byte[] expected = CreateByteDestination(stride, height);
        byte[] actual = CreateByteDestination(stride, height);
        short[] expectedHigh = CreateHighBitDepthDestination(stride, height);
        short[] actualHigh = CreateHighBitDepthDestination(stride, height);
        byte[] scratch = new byte[Av1DirectionalIntraPredictor.ScratchLength];
        short[] scratchHigh = new short[Av1DirectionalIntraPredictor.ScratchLength];

        Av1DirectionalIntraPredictor.PredictScalar(expected, stride, transformSize, above, left, upsampleAbove, upsampleLeft, angle);
        Av1DirectionalIntraPredictor.Predict(actual, stride, transformSize, above, left, upsampleAbove, upsampleLeft, angle, scratch);
        Av1DirectionalIntraPredictor.PredictScalar(expectedHigh, stride, transformSize, aboveHigh, leftHigh, upsampleAbove, upsampleLeft, angle);
        Av1DirectionalIntraPredictor.Predict(actualHigh, stride, transformSize, aboveHigh, leftHigh, upsampleAbove, upsampleLeft, angle, scratchHigh);

        Assert.Equal(expected, actual);
        Assert.Equal(expectedHigh, actualHigh);
    }

    /// <summary>
    /// Verifies every filter-intra operator at each transform size permitted by the AV1 syntax.
    /// </summary>
    private static void ValidateFilterIntraPredictors()
    {
        foreach (Av1FilterIntraMode mode in FilterIntraModes)
        {
            Av1FilterIntraPredictorBase predictor = Av1FilterIntraPredictorBase.GetPredictor(mode);
            for (int sizeIndex = 0; sizeIndex < (int)Av1TransformSize.AllSizes; sizeIndex++)
            {
                Av1TransformSize transformSize = (Av1TransformSize)sizeIndex;
                int width = transformSize.GetWidth();
                int height = transformSize.GetHeight();
                if (width > 32 || height > 32)
                {
                    continue;
                }

                int stride = width + 5;
                byte[] aboveStorage = CreateByteSamples(width + 1, 29);
                byte[] left = CreateByteSamples(height, 97);
                short[] aboveHighStorage = CreateHighBitDepthSamples(width + 1, 29);
                short[] leftHigh = CreateHighBitDepthSamples(height, 97);
                byte[] expected = CreateByteDestination(stride, height);
                byte[] actual = CreateByteDestination(stride, height);
                short[] expectedHigh = CreateHighBitDepthDestination(stride, height);
                short[] actualHigh = CreateHighBitDepthDestination(stride, height);
                byte[] expectedScratch = new byte[Av1FilterIntraPredictorBase.ScratchLength];
                byte[] actualScratch = new byte[Av1FilterIntraPredictorBase.ScratchLength];
                short[] expectedHighScratch = new short[Av1FilterIntraPredictorBase.ScratchLength];
                short[] actualHighScratch = new short[Av1FilterIntraPredictorBase.ScratchLength];

                predictor.PredictScalar(expected, stride, aboveStorage.AsSpan(1), left, width, height, expectedScratch);
                predictor.Predict(actual, stride, aboveStorage.AsSpan(1), left, width, height, actualScratch);
                predictor.PredictScalar(expectedHigh, stride, aboveHighStorage.AsSpan(1), leftHigh, width, height, 12, expectedHighScratch);
                predictor.Predict(actualHigh, stride, aboveHighStorage.AsSpan(1), leftHigh, width, height, 12, actualHighScratch);

                Assert.Equal(expected, actual);
                Assert.Equal(expectedHigh, actualHigh);
            }
        }

        ValidateKnownFilterIntraVectors();
    }

    /// <summary>
    /// Retains byte-exact reference vectors so scalar and SIMD code cannot share the same mistranslation unnoticed.
    /// </summary>
    private static void ValidateKnownFilterIntraVectors()
    {
        byte[][] expectedByMode =
        [
            [42, 65, 89, 123, 72, 77, 91, 110, 105, 100, 104, 112, 142, 128, 123, 124],
            [44, 79, 116, 153, 69, 94, 126, 158, 94, 109, 136, 163, 119, 124, 146, 168],
            [47, 67, 87, 107, 83, 93, 103, 113, 122, 127, 132, 137, 161, 163, 166, 168],
            [38, 55, 81, 111, 64, 62, 73, 92, 97, 83, 81, 86, 134, 113, 103, 100],
            [49, 81, 114, 148, 82, 105, 132, 159, 117, 132, 153, 174, 152, 159, 177, 190],
        ];

        // These edge values are the input to the five reference vectors above. The leading top value is the
        // shared top-left sample addressed through above[-1] by the normative recursive filter process.
        byte[] aboveStorage = [17, 30, 70, 110, 150];
        byte[] left = [40, 80, 120, 160];
        short[] aboveHighStorage = [529, 542, 582, 622, 662];
        short[] leftHigh = [552, 592, 632, 672];

        for (int modeIndex = 0; modeIndex < FilterIntraModes.Length; modeIndex++)
        {
            byte[] actual = new byte[16];
            byte[] scratch = new byte[Av1FilterIntraPredictorBase.ScratchLength];
            short[] actualHigh = new short[16];
            short[] expectedHigh = new short[16];
            short[] scratchHigh = new short[Av1FilterIntraPredictorBase.ScratchLength];
            Av1FilterIntraPredictorBase predictor = Av1FilterIntraPredictorBase.GetPredictor(FilterIntraModes[modeIndex]);

            predictor.Predict(actual, 4, aboveStorage.AsSpan(1), left, 4, 4, scratch);

            for (int i = 0; i < expectedHigh.Length; i++)
            {
                expectedHigh[i] = (short)(expectedByMode[modeIndex][i] + 512);
            }

            predictor.Predict(actualHigh, 4, aboveHighStorage.AsSpan(1), leftHigh, 4, 4, 10, scratchHigh);

            Assert.Equal(expectedByMode[modeIndex], actual);
            Assert.Equal(expectedHigh, actualHigh);
        }
    }

    /// <summary>
    /// Verifies vector interleaving, endpoint extension, clamping, and scalar tails in edge upsampling.
    /// </summary>
    private static void ValidateEdgeUpsampling()
    {
        ReadOnlySpan<int> counts = [4, 8, 12, 16];
        foreach (int count in counts)
        {
            byte[] actual = CreateUpsampleByteEdge(count);
            byte[] expected = (byte[])actual.Clone();
            byte[] scratch = new byte[160];

            UpsampleEdgeScalar(expected, count, 8);
            Av1PredictionDecoder.UpsampleIntraEdge(actual.AsSpan(2), count, scratch);

            Assert.Equal(expected, actual);

            ReadOnlySpan<int> bitDepths = [10, 12];

            foreach (int bitDepth in bitDepths)
            {
                short[] actualHigh = CreateUpsampleHighBitDepthEdge(count, bitDepth);
                short[] expectedHigh = (short[])actualHigh.Clone();
                short[] scratchHigh = new short[160];

                UpsampleEdgeScalar(expectedHigh, count, bitDepth);
                Av1PredictionDecoder.UpsampleIntraEdge(actualHigh.AsSpan(2), count, bitDepth, scratchHigh);

                Assert.Equal(expectedHigh, actualHigh);
            }
        }
    }

    /// <summary>
    /// Verifies all three edge-filter kernels across vector boundaries and the maximum normative edge length.
    /// </summary>
    private static void ValidateEdgeFiltering()
    {
        ReadOnlySpan<int> counts = [4, 8, 9, 16, 31, 64, 129];
        foreach (int count in counts)
        {
            for (int strength = 1; strength <= 3; strength++)
            {
                byte[] actual = CreateByteSamples(count, 31);
                byte[] expected = (byte[])actual.Clone();
                byte[] source = (byte[])actual.Clone();
                byte[] scratch = new byte[160];

                FilterEdgeScalar(source, expected, strength);
                Av1PredictionDecoder.FilterIntraEdge(ref actual[0], count, strength, scratch);

                Assert.Equal(expected, actual);

                short[] actualHigh = CreateHighBitDepthSamples(count, 31);
                short[] expectedHigh = (short[])actualHigh.Clone();
                short[] sourceHigh = (short[])actualHigh.Clone();
                short[] scratchHigh = new short[160];

                FilterEdgeScalar(sourceHigh, expectedHigh, strength);
                Av1PredictionDecoder.FilterIntraEdge(ref actualHigh[0], count, strength, scratchHigh);

                Assert.Equal(expectedHigh, actualHigh);
            }
        }
    }

    /// <summary>
    /// Creates deterministic 8-bit samples with enough variation to expose lane-order mistakes.
    /// </summary>
    private static byte[] CreateByteSamples(int length, int seed)
    {
        byte[] samples = new byte[length];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)(((i * 73) + (seed * 29) + ((i * i) * 7)) & 255);
        }

        return samples;
    }

    /// <summary>
    /// Creates deterministic 12-bit samples with values spanning the full reconstructed range.
    /// </summary>
    private static short[] CreateHighBitDepthSamples(int length, int seed)
    {
        short[] samples = new short[length];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)(((i * 977) + (seed * 131) + ((i * i) * 37)) & 4095);
        }

        return samples;
    }

    /// <summary>
    /// Creates a strided byte destination initialized with a padding sentinel.
    /// </summary>
    private static byte[] CreateByteDestination(int stride, int height)
    {
        byte[] destination = new byte[stride * height];
        Array.Fill(destination, (byte)0xCD);
        return destination;
    }

    /// <summary>
    /// Creates a strided high-bit-depth destination initialized with a padding sentinel.
    /// </summary>
    private static short[] CreateHighBitDepthDestination(int stride, int height)
    {
        short[] destination = new short[stride * height];
        Array.Fill(destination, (short)-1234);
        return destination;
    }

    /// <summary>
    /// Creates an 8-bit edge with two prefix samples and room for all interleaved outputs.
    /// </summary>
    private static byte[] CreateUpsampleByteEdge(int count)
    {
        byte[] edge = new byte[(2 * count) + 4];
        Array.Fill(edge, (byte)0xA5);
        edge[1] = 231;
        for (int i = 0; i < count; i++)
        {
            edge[i + 2] = (byte)(((i * 97) + 41) & 255);
        }

        return edge;
    }

    /// <summary>
    /// Creates a high-bit-depth edge containing extrema that exercise interpolation clamping.
    /// </summary>
    private static short[] CreateUpsampleHighBitDepthEdge(int count, int bitDepth)
    {
        int maximum = (1 << bitDepth) - 1;
        short[] edge = new short[(2 * count) + 4];
        Array.Fill(edge, (short)-1);
        edge[1] = (short)maximum;
        for (int i = 0; i < count; i++)
        {
            edge[i + 2] = (short)((i & 1) == 0 ? 0 : maximum);
        }

        return edge;
    }

    /// <summary>
    /// Applies the normative four-tap upsampling formula to an edge stored at index two.
    /// </summary>
    private static void UpsampleEdgeScalar<T>(T[] edge, int count, int bitDepth)
        where T : unmanaged, IBinaryInteger<T>
    {
        T[] input = new T[count + 3];
        input[0] = edge[1];
        input[1] = edge[1];
        for (int i = 0; i < count; i++)
        {
            input[i + 2] = edge[i + 2];
        }

        input[count + 2] = input[count + 1];
        edge[0] = input[0];
        int maximum = (1 << bitDepth) - 1;
        for (int i = 0; i < count; i++)
        {
            int value = -int.CreateChecked(input[i])
                + (9 * int.CreateChecked(input[i + 1]))
                + (9 * int.CreateChecked(input[i + 2]))
                - int.CreateChecked(input[i + 3]);

            edge[(2 * i) + 1] = T.CreateChecked(Math.Clamp((value + 8) >> 4, 0, maximum));
            edge[(2 * i) + 2] = input[i + 2];
        }
    }

    /// <summary>
    /// Applies the normative AV1 edge-filter definition to an independent source copy.
    /// </summary>
    private static void FilterEdgeScalar<T>(T[] source, T[] destination, int strength)
        where T : unmanaged, IBinaryInteger<T>
    {
        ReadOnlySpan<int> kernel = strength switch
        {
            1 => [0, 4, 8, 4, 0],
            2 => [0, 5, 6, 5, 0],
            _ => [2, 4, 4, 4, 2],
        };

        for (int i = 1; i < source.Length; i++)
        {
            int sum = 0;
            for (int tap = 0; tap < kernel.Length; tap++)
            {
                int sourceIndex = Math.Clamp(i - 2 + tap, 0, source.Length - 1);
                sum += int.CreateChecked(source[sourceIndex]) * kernel[tap];
            }

            destination[i] = T.CreateChecked((sum + 8) >> 4);
        }
    }
}
