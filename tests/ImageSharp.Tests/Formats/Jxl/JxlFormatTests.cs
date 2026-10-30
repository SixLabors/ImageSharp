// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

[Trait("Format", "Jxl")]
public class JxlFormatTests
{
    /// <summary>
    /// Verifies that an incomplete optional Brotli payload follows the shared ancillary error policy.
    /// </summary>
    /// <param name="handling">The selected segment error policy.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    public void IdentifyAppliesAncillaryPolicyToCompressedExif(SegmentIntegrityHandling handling)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.ExifBrotliTruncated).Bytes, false);

        // The stored input removes the last compressed byte and updates the outer box length.
        // Its container and image header remain intact; only the optional EXIF compression is incomplete.
        if (handling == SegmentIntegrityHandling.Strict)
        {
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, stream));
        }
        else
        {
            ImageInfo info = Image.Identify(options, stream);

            Assert.Null(info.Metadata.ExifProfile);
            Assert.Same(JxlFormat.Instance, info.Metadata.DecodedImageFormat);
        }
    }

    /// <summary>
    /// Verifies that codestream level control remains mandatory when optional metadata is skipped.
    /// </summary>
    /// <param name="file">The stored container with a malformed level box.</param>
    [Theory]
    [InlineData(TestImages.Jxl.SquareInvalidLevel)]
    [InlineData(TestImages.Jxl.SquareDuplicateLevel)]
    [InlineData(TestImages.Jxl.SquareLateLevel)]
    public void IdentifyRejectsInvalidContainerLevel(string file)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SkipMetadata = true };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, stream));
    }

    /// <summary>
    /// Verifies plain and Brotli-compressed EXIF from native fixtures, including trailing and open-ended boxes.
    /// </summary>
    /// <param name="file">The stored auxiliary-box fixture.</param>
    [Theory]
    [InlineData(TestImages.Jxl.Exif)]
    [InlineData(TestImages.Jxl.ExifBrotli)]
    [InlineData(TestImages.Jxl.ExifBrotliTrailingFinite)]
    [InlineData(TestImages.Jxl.ExifBrotliTrailingInfinite)]
    public void IdentifyReadsReferenceExif(string file)
    {
        // One-byte I/O buffers force partial compressed input and output. The expected software name
        // comes from the original uncompressed EXIF fixture, not from our Brotli or EXIF writer.
        Configuration configuration = new(new JxlConfigurationModule()) { StreamProcessingBufferSize = 1 };
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        ImageInfo info = Image.Identify(options, stream);

        Assert.NotNull(info.Metadata.ExifProfile);
        Assert.True(info.Metadata.ExifProfile.TryGetValue(ExifTag.Software, out IExifValue<string> software));
        Assert.Equal("Pixelmator Pro 3.6.16", software.Value);
    }

    /// <summary>
    /// Verifies format registration and public identification using stored raw and container inputs.
    /// </summary>
    /// <param name="file">The stored reference input.</param>
    /// <param name="width">The width reported by the reference inspector.</param>
    /// <param name="height">The height reported by the reference inspector.</param>
    /// <param name="precision">The encoded color sample precision.</param>
    [Theory]
    [InlineData(TestImages.Jxl.Square, 8, 8, 8)]
    [InlineData(TestImages.Jxl.SquareExtendedSizeContainer, 8, 8, 8)]
    [InlineData(TestImages.Jxl.SquareLevel5, 8, 8, 8)]
    [InlineData(TestImages.Jxl.SplinesFragmentedV1, 2048, 2048, 8)]
    [InlineData(TestImages.Jxl.LosslessFloat, 500, 500, 32)]
    public void IdentifyUsesRegisteredDecoder(string file, int width, int height, int precision)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration);
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        ImageInfo info = Image.Identify(options, stream);

        Assert.Equal(new Size(width, height), info.Size);
        Assert.Same(JxlFormat.Instance, info.Metadata.DecodedImageFormat);
        Assert.Equal((byte)precision, info.Metadata.GetFormatMetadata(JxlFormat.Instance).BitDepth.BitsPerSample);
        Assert.True(stream.CanRead);
    }

    /// <summary>
    /// Verifies optional profiles before and after the codestream through the shared nonseekable-input wrapper.
    /// </summary>
    /// <param name="file">The stored container input.</param>
    /// <param name="skipMetadata">Whether optional EXIF and XMP profiles are omitted.</param>
    [Theory]
    [InlineData(TestImages.Jxl.ExifXmp, false)]
    [InlineData(TestImages.Jxl.ExifXmp, true)]
    [InlineData(TestImages.Jxl.ExifXmpAfterCodestream, false)]
    [InlineData(TestImages.Jxl.ExifXmpAfterCodestream, true)]
    public void IdentifyReadsOptionalMetadata(string file, bool skipMetadata)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SkipMetadata = skipMetadata };
        using MemoryStream data = new(TestFile.Create(file).Bytes, false);
        using NonSeekableStream stream = new(data);

        ImageInfo info = Image.Identify(options, stream);

        Assert.Equal(new Size(1, 1), info.Size);
        Assert.Equal(!skipMetadata, info.Metadata.ExifProfile is not null);
        Assert.Equal(!skipMetadata, info.Metadata.XmpProfile is not null);
    }

    /// <summary>
    /// Verifies that SkipMetadata keeps required ICC color data when the caller selects Preserve.
    /// </summary>
    [Fact]
    public void IdentifyPreservesIccWhenOptionalMetadataIsSkipped()
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SkipMetadata = true, ColorProfileHandling = ColorProfileHandling.Preserve };
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.WithIcc).Bytes, false);

        ImageInfo info = JxlDecoder.Instance.Identify(options, stream);

        Assert.Equal(new Size(169, 118), info.Size);
        Assert.NotNull(info.Metadata.IccProfile);
        Assert.Equal(544, info.Metadata.IccProfile.ToByteArray().Length);
        Assert.Equal(PixelAlphaRepresentation.Unassociated, info.PixelType.AlphaRepresentation);
    }

    /// <summary>
    /// Verifies the public format identifiers.
    /// </summary>
    [Fact]
    public void FormatProperties_AreAsExpected()
    {
        JxlFormat format = JxlFormat.Instance;

        Assert.Equal("JPEG XL", format.Name);
        Assert.Equal("image/jxl", format.DefaultMimeType);
        Assert.Contains("image/jxl", format.MimeTypes);
        Assert.Contains("jxl", format.FileExtensions);
    }

    /// <summary>
    /// Verifies both signatures, partial headers and headers with trailing bytes.
    /// </summary>
    /// <param name="hex">The header bytes in hexadecimal notation.</param>
    /// <param name="expected">Whether the header identifies JPEG XL.</param>
    [Theory]
    [InlineData("", false)]
    [InlineData("FF", false)]
    [InlineData("FFD8FF", false)]
    [InlineData("FF0A", true)]
    [InlineData("FF0A123456", true)]
    [InlineData("0000000C4A584C200D0A87", false)]
    [InlineData("0000000C4A584C200D0A870A", true)]
    [InlineData("0000000C4A584C200D0A870A0000001466747970", true)]
    [InlineData("0000000C4A584C200D0A870B", false)]
    public void DetectorRecognizesOnlyCompleteSignatures(string hex, bool expected)
    {
        // These are format signatures, not generated image fixtures. No image header or pixel data is constructed.
        byte[] header = Convert.FromHexString(hex);
        JxlImageFormatDetector detector = new();

        bool detected = detector.TryDetectFormat(header, out IImageFormat format);

        Assert.Equal(expected, detected);
        Assert.Same(expected ? JxlFormat.Instance : null, format);
    }

    /// <summary>
    /// Verifies that specialized options retain the caller's configuration and policies.
    /// </summary>
    [Fact]
    public void DecoderOptionsRetainGeneralOptions()
    {
        DecoderOptions generalOptions = new(new Configuration())
        {
            MaxFrames = 2,
            SkipMetadata = true,
            TargetSize = new Size(64, 32)
        };

        JxlDecoderOptions options = new(generalOptions);

        Assert.Same(generalOptions, options.GeneralOptions);
    }

    /// <summary>
    /// Verifies the specialized options' public constructor boundary.
    /// </summary>
    [Fact]
    public void DecoderOptionsRejectNullGeneralOptions()
        => Assert.Throws<ArgumentNullException>(() => new JxlDecoderOptions(null));
}
