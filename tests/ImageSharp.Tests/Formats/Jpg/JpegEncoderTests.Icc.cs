// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

public partial class JpegEncoderTests
{
    /// <summary>
    /// Checks ICC encoding through both spectral paths, including padding, metadata omission and unchanged source pixels.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="provider">A real JPEG with an embedded profile.</param>
    /// <param name="colorType">The encoded JPEG component layout.</param>
    /// <param name="compatibleProfile">Whether the fixture profile matches the encoded components.</param>
    [Theory]
    [WithFile(TestImages.Jpeg.ICC.AdobeRgb, PixelTypes.Rgb24, JpegColorType.Rgb, true)]
    [WithFile(TestImages.Jpeg.ICC.AdobeRgb, PixelTypes.RgbaVector, JpegColorType.YCbCrRatio420, true)]
    [WithFile(TestImages.Jpeg.ICC.CMYK, PixelTypes.Rgb24, JpegColorType.Cmyk, true)]
    [WithFile(TestImages.Jpeg.ICC.YCCK, PixelTypes.Rgb24, JpegColorType.Ycck, true)]
    [WithFile(TestImages.Jpeg.ICC.SRgbGray, PixelTypes.L8, JpegColorType.Luminance, false)]
    public void Encode_IccProfile_MatchesBaselineAndProgressive<TPixel>(TestImageProvider<TPixel> provider, JpegColorType colorType, bool compatibleProfile)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> source = provider.GetImage();

        // Crop real pixels to cross complete MCU blocks and leave both horizontal and vertical padding.
        source.Mutate(context => context.Crop(new Rectangle(0, 0, Math.Min(65, source.Width), Math.Min(17, source.Height))));

        IccProfile profile = Assert.IsType<IccProfile>(source.Metadata.IccProfile);
        using Image<TPixel> unchanged = source.Clone();
        JpegEncoder baselineEncoder = new() { Quality = 100, ColorType = colorType };
        JpegEncoder progressiveEncoder = new() { Quality = 100, ColorType = colorType, Progressive = true, SkipMetadata = true };

        using MemoryStream baselineStream = new();
        using MemoryStream progressiveStream = new();

        source.Save(baselineStream, baselineEncoder);
        source.Save(progressiveStream, progressiveEncoder);

        baselineStream.Position = 0;
        progressiveStream.Position = 0;
        DecoderOptions convertOptions = new(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Convert, SkipMetadata = true };

        using Image<TPixel> baseline = Image.Load<TPixel>(baselineStream);
        using Image<TPixel> progressive = Image.Load<TPixel>(convertOptions, progressiveStream);

        // Streaming and full-image DCT must produce the same pixels after applying the retained profile.
        ImageComparer.Exact.VerifySimilarity(baseline, progressive);

        Assert.Null(progressive.Metadata.IccProfile);

        progressiveStream.Position = 0;
        ImageInfo info = Image.Identify(progressiveStream);
        if (compatibleProfile)
        {
            Assert.Equal(profile.ToByteArray(), Assert.IsType<IccProfile>(baseline.Metadata.IccProfile).ToByteArray());
            Assert.Equal(profile.ToByteArray(), Assert.IsType<IccProfile>(info.Metadata.IccProfile).ToByteArray());
        }
        else
        {
            // sRGB_Gray.jpg carries an RGB profile. A one-channel JPEG cannot retain that three-channel profile.
            Assert.Equal(IccColorSpaceType.Rgb, profile.Header.DataColorSpace);
            Assert.Null(baseline.Metadata.IccProfile);
            Assert.Null(info.Metadata.IccProfile);
        }

        ImageComparer.Exact.VerifySimilarity(unchanged, source);
        Assert.Same(profile, source.Metadata.IccProfile);

        source.DebugSave(provider, testOutputDetails: colorType, extension: "jpg", encoder: baselineEncoder);
        source.DebugSave(provider, testOutputDetails: $"{colorType}_Progressive", extension: "jpg", encoder: progressiveEncoder);
    }
}
