// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Common;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.PhotometricInterpretation;

[Trait("Format", "Tiff")]
public class RgbaPlanarTiffColorTests
{
    /// <summary>
    /// Checks straight-alpha byte planes retain fractional components in RgbaVector.
    /// </summary>
    /// <param name="length">The row width, including scalar and vector-tail cases.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(33)]
    [InlineData(64)]
    [InlineData(65)]
    public void Decode_Aligned8Bit_PreservesUnassociatedVectorPrecision(int length) => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        AssertUnassociatedVectorPrecision,
        length,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks all straight-alpha byte components in the decoded vector row.
    /// </summary>
    /// <param name="serialized">The serialized row width.</param>
    private static void AssertUnassociatedVectorPrecision(string serialized)
    {
        int length = FeatureTestRunner.Deserialize<int>(serialized);
        using IMemoryOwner<byte> red = Configuration.Default.MemoryAllocator.Allocate<byte>(length);
        using IMemoryOwner<byte> green = Configuration.Default.MemoryAllocator.Allocate<byte>(length);
        using IMemoryOwner<byte> blue = Configuration.Default.MemoryAllocator.Allocate<byte>(length);
        using IMemoryOwner<byte> alpha = Configuration.Default.MemoryAllocator.Allocate<byte>(length);
        IMemoryOwner<byte>[] planes = [red, green, blue, alpha];
        using Image<RgbaVector> image = new(length, 1);
        Span<byte> redSamples = red.GetSpan();
        Span<byte> greenSamples = green.GetSpan();
        Span<byte> blueSamples = blue.GetSpan();
        Span<byte> alphaSamples = alpha.GetSpan();

        // A 17-pixel period shifts colors across 16-, 32-, and 64-pixel groups; endpoints and hidden RGB expose alpha mistakes.
        ReadOnlySpan<Rgba32> samples =
        [
            new(0, 255, 1, 255), new(17, 63, 201, 129), new(255, 0, 128, 0), new(255, 255, 255, 255),
            new(3, 31, 157, 211), new(41, 5, 233, 67), new(91, 177, 13, 53), new(222, 19, 109, 241),
            new(72, 213, 44, 188), new(151, 62, 7, 35), new(11, 99, 201, 111), new(192, 8, 73, 160),
            new(64, 144, 224, 32), new(29, 209, 117, 71), new(187, 54, 96, 204), new(237, 83, 16, 125),
            new(103, 147, 231, 199)
        ];

        for (int i = 0; i < length; i++)
        {
            Rgba32 sample = samples[i % samples.Length];
            redSamples[i] = sample.R;
            greenSamples[i] = sample.G;
            blueSamples[i] = sample.B;
            alphaSamples[i] = sample.A;
        }

        new RgbaPlanarTiffColor<RgbaVector>(
            Configuration.Default,
            TiffExtraSampleType.UnassociatedAlphaData,
            new TiffBitsPerSample(8, 8, 8, 8)).Decode(planes, image.Frames.RootFrame.PixelBuffer, 0, 0, length, 1);

        // Widths 1/17/33/64/65 cover scalar output, smaller SIMD groups, a full 64-pixel byte group, and its tail.
        for (int i = 0; i < length; i++)
        {
            // Each stored byte maps to its exact scaled channel value, including alpha and hidden RGB.
            Vector4 expected = new(redSamples[i] / 255F, greenSamples[i] / 255F, blueSamples[i] / 255F, alphaSamples[i] / 255F);
            Assert.Equal(expected, image[i, 0].ToScaledVector4());
        }
    }

    /// <summary>
    /// Checks premultiplied byte planes retain their stored components in RgbaVectorP.
    /// </summary>
    /// <param name="length">The row width, including scalar and vector-tail cases.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    [InlineData(33)]
    [InlineData(64)]
    [InlineData(65)]
    public void Decode_Aligned8Bit_PreservesAssociatedVectorStorage(int length) => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        AssertAssociatedVectorStorage,
        length,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks premultiplied byte components in the associated destination row.
    /// </summary>
    /// <param name="serialized">The serialized row width.</param>
    private static void AssertAssociatedVectorStorage(string serialized)
    {
        int length = FeatureTestRunner.Deserialize<int>(serialized);
        using IMemoryOwner<byte> red = Configuration.Default.MemoryAllocator.Allocate<byte>(length);
        using IMemoryOwner<byte> green = Configuration.Default.MemoryAllocator.Allocate<byte>(length);
        using IMemoryOwner<byte> blue = Configuration.Default.MemoryAllocator.Allocate<byte>(length);
        using IMemoryOwner<byte> alpha = Configuration.Default.MemoryAllocator.Allocate<byte>(length);
        IMemoryOwner<byte>[] planes = [red, green, blue, alpha];
        using Image<RgbaVectorP> image = new(length, 1);
        Span<byte> redSamples = red.GetSpan();
        Span<byte> greenSamples = green.GetSpan();
        Span<byte> blueSamples = blue.GetSpan();
        Span<byte> alphaSamples = alpha.GetSpan();

        // Every RGB code is at most alpha; 17 distinct samples shift across groups without invalid premultiplication.
        ReadOnlySpan<Rgba32> samples =
        [
            new(13, 51, 91, 127), new(0, 0, 0, 0), new(255, 1, 128, 255), new(2, 5, 7, 64),
            new(31, 7, 17, 63), new(11, 72, 93, 127), new(3, 41, 6, 95), new(144, 18, 87, 191),
            new(221, 19, 128, 239), new(52, 84, 13, 159), new(1, 2, 3, 31), new(97, 64, 21, 127),
            new(170, 30, 80, 191), new(25, 15, 40, 63), new(7, 99, 117, 127), new(200, 39, 5, 223),
            new(61, 3, 8, 95)
        ];

        for (int i = 0; i < length; i++)
        {
            Rgba32 sample = samples[i % samples.Length];
            redSamples[i] = sample.R;
            greenSamples[i] = sample.G;
            blueSamples[i] = sample.B;
            alphaSamples[i] = sample.A;
        }

        new RgbaPlanarTiffColor<RgbaVectorP>(
            Configuration.Default,
            TiffExtraSampleType.AssociatedAlphaData,
            new TiffBitsPerSample(8, 8, 8, 8)).Decode(planes, image.Frames.RootFrame.PixelBuffer, 0, 0, length, 1);

        // Widths 1/17/33/64/65 cover scalar output, smaller SIMD groups, a full 64-pixel byte group, and its tail.
        for (int i = 0; i < length; i++)
        {
            // Associated TIFF RGB is already multiplied by alpha; RgbaVectorP stores those byte/255F values.
            Vector4 expected = new(redSamples[i] / 255F, greenSamples[i] / 255F, blueSamples[i] / 255F, alphaSamples[i] / 255F);
            Assert.Equal(RgbaVectorP.FromAssociatedScaledVector4(expected), image[i, 0]);
        }
    }
}
