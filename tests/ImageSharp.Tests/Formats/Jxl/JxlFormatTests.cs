// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

[Trait("Format", "Jxl")]
[ValidateDisposedMemoryAllocations]
public class JxlFormatTests
{
    /// <summary>
    /// Verifies every reversible color operation, including signed rounding and overflow, at each SIMD width and scalar fallback.
    /// </summary>
    [Fact]
    public void InverseModularColorTransformMatchesNativeArithmetic()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateInverseModularColorTransform,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies the first complete modular path through format discovery, explicit decoding and automatic pixel selection.
    /// </summary>
    /// <param name="file">The unchanged native lossless fixture.</param>
    /// <param name="hasAlpha">Whether its independently decoded alpha samples are 128 rather than implicit opacity.</param>
    [Theory]
    [InlineData(TestImages.Jxl.LosslessRgb3x3, false)]
    [InlineData(TestImages.Jxl.LosslessRgba3x3, true)]
    public void DecodeLosslessMatchesNativePixels(string file, bool hasAlpha)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration);
        byte[] data = TestFile.Create(file).Bytes;
        using MemoryStream discoveredInput = new(data, false);
        using MemoryStream explicitInput = new(data, false);
        using MemoryStream automaticInput = new(data, false);
        using Image<Rgba32> discovered = Image.Load<Rgba32>(options, discoveredInput);
        using Image<Rgba32> explicitImage = JxlDecoder.Instance.Decode<Rgba32>(options, explicitInput);
        using Image automatic = Image.Load(options, automaticInput);

        // The native decoder returns the original gamma-encoded samples: 0, 64, 128 and 255.
        // The file declares compression gamma 0.45455. Convert x to linear with x^(1/0.45455), then apply the sRGB curve.
        // In eight-bit sRGB, those samples become 0, 62, 129 and 255. Alpha retains its independent value of 128.
        ReadOnlySpan<Rgb24> expected =
        [
            new(255, 0, 0), new(0, 255, 0), new(0, 0, 255),
            new(129, 62, 62), new(62, 129, 62), new(62, 62, 129),
            new(255, 255, 255), new(129, 129, 129), new(0, 0, 0)
        ];

        Assert.Equal(new Size(3, 3), discovered.Size);
        Assert.Equal(1, discovered.Frames.Count);
        Assert.Equal(JxlColorType.Rgb, discovered.Metadata.GetFormatMetadata(JxlFormat.Instance).ColorType);
        for (int i = 0; i < expected.Length; i++)
        {
            Rgb24 color = expected[i];
            Rgba32 pixel = new(color.R, color.G, color.B, hasAlpha ? (byte)128 : byte.MaxValue);

            Assert.Equal(pixel, discovered[i % 3, i / 3]);
            Assert.Equal(pixel, explicitImage[i % 3, i / 3]);
        }

        if (hasAlpha)
        {
            Assert.IsType<Image<Rgba32>>(automatic);
        }
        else
        {
            Assert.IsType<Image<Rgb24>>(automatic);
        }

        Assert.True(discoveredInput.CanRead);
        Assert.True(explicitInput.CanRead);
        Assert.True(automaticInput.CanRead);
    }

    /// <summary>
    /// Verifies that a short modular payload returns all temporary allocations and leaves the caller's stream open.
    /// </summary>
    [Fact]
    public void DecodeRejectsTruncatedLosslessPayload()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = new(new JxlConfigurationModule()) { MemoryAllocator = allocator };
        DecoderOptions options = new(configuration);
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.LosslessRgb3x3Truncated).Bytes, false);

        // This stored fixture omits the original file's final payload byte. The native decoder also rejects it as truncated.
        // Allocation logging checks cleanup immediately, without relying on finalizers to release failed reconstruction buffers.
        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, stream);
        });

        Assert.Equal(0, allocator.LiveBufferBytes);
        Assert.True(stream.CanRead);
    }

    /// <summary>
    /// Verifies cancellation through the public asynchronous decoder without transferring stream ownership.
    /// </summary>
    [Fact]
    public async Task DecodeLosslessHonorsCancellation()
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration);
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.LosslessRgb3x3).Bytes, false);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            using Image<Rgba32> image = await JxlDecoder.Instance.DecodeAsync<Rgba32>(options, stream, cancellation.Token);
        });

        Assert.True(stream.CanRead);
    }

    /// <summary>
    /// Verifies displayed animation timing and MaxFrames against the native reference inspector.
    /// </summary>
    /// <param name="file">The stored animation input.</param>
    /// <param name="maxFrames">The displayed frame limit.</param>
    /// <param name="firstSeconds">The first frame's duration reported by the native inspector, in seconds.</param>
    /// <param name="shortSeconds">The shorter later frame's duration, in seconds.</param>
    [Theory]
    [InlineData(TestImages.Jxl.CroppedTrafficLight, 1, 3L, 1L)]
    [InlineData(TestImages.Jxl.CroppedTrafficLight, 4, 3L, 1L)]
    [InlineData(TestImages.Jxl.CroppedTrafficLightLongDuration, 4, 4398046510080L, 102400L)]
    public void IdentifyReportsDisplayedAnimationFrames(string file, int maxFrames, long firstSeconds, long shortSeconds)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { MaxFrames = (uint)maxFrames };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        ImageInfo info = Image.Identify(options, stream);

        // jxlinspect reports the exact durations for both files. The long variant uses the maximum native
        // tick count and a 1/1024 tick rate, which exceeds both uint seconds and TimeSpan's duration range.
        // Cropped intermediate content must not change the canvas size or add displayed frames.
        ReadOnlySpan<long> seconds = [firstSeconds, shortSeconds, 3 * shortSeconds, shortSeconds];

        Assert.Equal(new Size(50, 80), info.Size);
        Assert.Equal(maxFrames, info.FrameCount);
        Assert.Equal(0U, info.Metadata.GetFormatMetadata(JxlFormat.Instance).RepeatCount);

        for (int i = 0; i < maxFrames; i++)
        {
            JxlFrameMetadata frame = info.FrameMetadataCollection[i].GetFormatMetadata(JxlFormat.Instance);

            Assert.Equal(new LongRational(seconds[i], 1), frame.FrameDelay);
        }
    }

    /// <summary>
    /// Verifies that the optional preview is traversed without being counted as a displayed image.
    /// </summary>
    [Fact]
    public void IdentifyDoesNotCountPreviewAsDisplayedFrame()
    {
        Configuration configuration = new(new JxlConfigurationModule());
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.WithPreview).Bytes, false);

        ImageInfo info = Image.Identify(new DecoderOptions(configuration), stream);

        Assert.Equal(new Size(64, 64), info.Size);
        Assert.Equal(1, info.FrameCount);
    }

    /// <summary>
    /// Verifies that invalid bounded Brotli wrappers and payloads follow the shared ancillary error policy.
    /// </summary>
    /// <param name="file">The stored input with invalid optional metadata.</param>
    /// <param name="handling">The selected segment error policy.</param>
    [Theory]
    [InlineData(TestImages.Jxl.ExifBrotliTruncated, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.ExifBrotliTruncated, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.ExifBrotliTruncated, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(TestImages.Jxl.ExifBrotliShortType, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.ExifBrotliShortType, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.ExifBrotliShortType, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(TestImages.Jxl.ExifBrotliForbiddenType, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.ExifBrotliForbiddenType, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.ExifBrotliForbiddenType, SegmentIntegrityHandling.IgnoreImageData)]
    public void IdentifyAppliesAncillaryPolicyToCompressedExif(string file, SegmentIntegrityHandling handling)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        // Each damaged payload has a valid outer box range. Ignoring it must retain the following image,
        // without consuming a type byte from the next box or accepting the malformed EXIF profile.
        if (handling == SegmentIntegrityHandling.Strict)
        {
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, stream));
        }
        else
        {
            ImageInfo info = Image.Identify(options, stream);

            Assert.Equal(new Size(256, 128), info.Size);
            Assert.Null(info.Metadata.ExifProfile);
            Assert.Same(JxlFormat.Instance, info.Metadata.DecodedImageFormat);
        }
    }

    /// <summary>
    /// Verifies that skipped optional boxes do not validate their payloads, even with strict integrity handling.
    /// </summary>
    /// <param name="file">The stored input with an invalid bounded wrapper.</param>
    [Theory]
    [InlineData(TestImages.Jxl.ExifBrotliShortType)]
    [InlineData(TestImages.Jxl.ExifBrotliForbiddenType)]
    public void IdentifySkipsInvalidOptionalBoxes(string file)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SkipMetadata = true, SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        ImageInfo info = Image.Identify(options, stream);

        Assert.Equal(new Size(256, 128), info.Size);
        Assert.Null(info.Metadata.ExifProfile);
    }

    /// <summary>
    /// Verifies that invalid optional names do not prevent parsing the fields and image sections that follow them.
    /// </summary>
    /// <param name="file">The stored input with one invalid UTF-8 label.</param>
    /// <param name="handling">The selected segment error policy.</param>
    [Theory]
    [InlineData(TestImages.Jxl.InvalidFrameName, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.InvalidFrameName, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.InvalidFrameName, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(TestImages.Jxl.InvalidExtraChannelName, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.InvalidExtraChannelName, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.InvalidExtraChannelName, SegmentIntegrityHandling.IgnoreImageData)]
    public void IdentifyAppliesAncillaryPolicyToNames(string file, SegmentIntegrityHandling handling)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        if (handling == SegmentIntegrityHandling.Strict)
        {
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, stream));
        }
        else
        {
            ImageInfo info = Image.Identify(options, stream);

            Assert.Equal(1, info.FrameCount);
            if (file == TestImages.Jxl.InvalidExtraChannelName)
            {
                // The next valid label and the spot mixture must survive recovery of the preceding invalid label.
                JxlMetadata metadata = info.Metadata.GetFormatMetadata(JxlFormat.Instance);

                Assert.Equal(new Size(600, 400), info.Size);
                Assert.Equal(3, metadata.ExtraChannels.Count);
                Assert.Empty(metadata.ExtraChannels[1].Name);
                Assert.Equal("A", metadata.ExtraChannels[2].Name);
                Assert.Equal(JxlExtraChannelType.SpotColor, metadata.ExtraChannels[2].Type);
                Assert.NotNull(info.Metadata.IccProfile);
            }
            else
            {
                Assert.Equal(new Size(8, 8), info.Size);
            }
        }
    }

    /// <summary>
    /// Verifies that a complete but invalid ICC prediction can be omitted only when XYB supplies the native color transform.
    /// </summary>
    /// <param name="file">The stored input with a complete entropy stream and invalid prediction preamble.</param>
    /// <param name="handling">The selected segment error policy.</param>
    [Theory]
    [InlineData(TestImages.Jxl.XybInvalidIccPrediction, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.XybInvalidIccPrediction, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.XybInvalidIccPrediction, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(TestImages.Jxl.RgbInvalidIccPrediction, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.RgbInvalidIccPrediction, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.RgbInvalidIccPrediction, SegmentIntegrityHandling.IgnoreImageData)]
    public void IdentifyRequiresNativeComponentIcc(string file, SegmentIntegrityHandling handling)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        if (handling == SegmentIntegrityHandling.Strict || file == TestImages.Jxl.RgbInvalidIccPrediction)
        {
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, stream));
        }
        else
        {
            ImageInfo info = Image.Identify(options, stream);

            Assert.Equal(new Size(8, 8), info.Size);
            Assert.Equal(1, info.FrameCount);
            Assert.Equal(JxlColorType.Xyb, info.Metadata.GetFormatMetadata(JxlFormat.Instance).ColorType);
            Assert.Null(info.Metadata.IccProfile);
        }
    }

    /// <summary>
    /// Verifies that missing name bytes and incomplete ICC entropy remain fatal in every integrity mode.
    /// </summary>
    /// <param name="file">The stored truncated field input.</param>
    /// <param name="handling">The selected segment error policy.</param>
    [Theory]
    [InlineData(TestImages.Jxl.TruncatedFrameName, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.TruncatedFrameName, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.TruncatedFrameName, SegmentIntegrityHandling.IgnoreImageData)]
    [InlineData(TestImages.Jxl.XybTruncatedIcc, SegmentIntegrityHandling.Strict)]
    [InlineData(TestImages.Jxl.XybTruncatedIcc, SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(TestImages.Jxl.XybTruncatedIcc, SegmentIntegrityHandling.IgnoreImageData)]
    public void IdentifyRejectsTruncatedHeaderFields(string file, SegmentIntegrityHandling handling)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        // Select the decoder explicitly so even an input shorter than the format-detection header reaches its field parser.
        Assert.Throws<InvalidImageContentException>(() => JxlDecoder.Instance.Identify(options, stream));
    }

    /// <summary>
    /// Verifies that codestream structure remains mandatory when optional metadata is skipped.
    /// </summary>
    /// <param name="file">The stored input with invalid level or fragment control.</param>
    [Theory]
    [InlineData(TestImages.Jxl.SquareInvalidLevel)]
    [InlineData(TestImages.Jxl.SquareDuplicateLevel)]
    [InlineData(TestImages.Jxl.SquareLateLevel)]
    [InlineData(TestImages.Jxl.SplinesDuplicateFragment)]
    [InlineData(TestImages.Jxl.SplinesMissingFinalFlag)]
    public void IdentifyRejectsMalformedStructure(string file)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SkipMetadata = true, SegmentIntegrityHandling = SegmentIntegrityHandling.IgnoreImageData };
        using MemoryStream stream = new(TestFile.Create(file).Bytes, false);

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, stream));
    }

    /// <summary>
    /// Verifies that a complete table and frame header survive a short pixel payload only under the image-data recovery policy.
    /// </summary>
    /// <param name="handling">The selected segment error policy.</param>
    [Theory]
    [InlineData(SegmentIntegrityHandling.Strict)]
    [InlineData(SegmentIntegrityHandling.IgnoreAncillary)]
    [InlineData(SegmentIntegrityHandling.IgnoreImageData)]
    public void IdentifyRetainsFrameMetadataForTruncatedPayload(SegmentIntegrityHandling handling)
    {
        Configuration configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(configuration) { SegmentIntegrityHandling = handling };
        using MemoryStream stream = new(TestFile.Create(TestImages.Jxl.TruncatedSections).Bytes, false);

        if (handling != SegmentIntegrityHandling.IgnoreImageData)
        {
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, stream));
        }
        else
        {
            // The original conformance header reports 2143 by 1050 pixels with 16-bit samples.
            // This stored variant removes only the final payload byte; its frame header and all 49 sizes remain intact.
            ImageInfo info = Image.Identify(options, stream);

            Assert.Equal(new Size(2143, 1050), info.Size);
            Assert.Equal(1, info.FrameCount);
            Assert.Equal(new JxlBitDepth(16, 0), info.Metadata.GetFormatMetadata(JxlFormat.Instance).BitDepth);
        }
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
    [InlineData(TestImages.Jxl.PermutedSections, 2143, 1050, 16)]
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

    /// <summary>
    /// Checks native reversible color equations against fixed integer results with complete vector blocks and a scalar tail.
    /// </summary>
    private static void ValidateInverseModularColorTransform()
    {
        ReadOnlySpan<int> firstValues = [-7, int.MaxValue, int.MinValue, 9, 0, -1, 42];
        ReadOnlySpan<int> secondValues = [10, 2, -3, -5, int.MinValue, -1, 17];
        ReadOnlySpan<int> thirdValues = [2, 2, -1, -7, int.MaxValue, -1, -103];

        // libjxl InvRCTRow defines wrapping 32-bit sums and signed right shifts, including before the average in operations four and five.
        // Operation five uses the restored third channel in that average. Operation six reverses YCoCg in two successive half-shift steps.
        // Each operation below lists seven first-channel results, seven second-channel results, then seven third-channel results.
        ReadOnlySpan<int> expected =
        [
            // No arithmetic.
            -7, int.MaxValue, int.MinValue, 9, 0, -1, 42,
            10, 2, -3, -5, int.MinValue, -1, 17,
            2, 2, -1, -7, int.MaxValue, -1, -103,

            // Add the first channel to the third.
            -7, int.MaxValue, int.MinValue, 9, 0, -1, 42,
            10, 2, -3, -5, int.MinValue, -1, 17,
            -5, -2147483647, int.MaxValue, 2, int.MaxValue, -2, -61,

            // Add the first channel to the second.
            -7, int.MaxValue, int.MinValue, 9, 0, -1, 42,
            3, -2147483647, 2147483645, 4, int.MinValue, -2, 59,
            2, 2, -1, -7, int.MaxValue, -1, -103,

            // Add the first channel to both other channels.
            -7, int.MaxValue, int.MinValue, 9, 0, -1, 42,
            3, -2147483647, 2147483645, 4, int.MinValue, -2, 59,
            -5, -2147483647, int.MaxValue, 2, int.MaxValue, -2, -61,

            // Add the signed average of the first and third channels to the second.
            -7, int.MaxValue, int.MinValue, 9, 0, -1, 42,
            7, -1073741822, 1073741820, -4, -1073741825, -2, -14,
            2, 2, -1, -7, int.MaxValue, -1, -103,

            // Restore the third channel, then use it in the signed average added to the second.
            -7, int.MaxValue, int.MinValue, 9, 0, -1, 42,
            4, 2, -4, 0, -1073741825, -3, 7,
            -5, -2147483647, int.MaxValue, 2, int.MaxValue, -2, -61,

            // Reverse YCoCg, with first = Y, second = Co and third = Cg.
            -3, int.MaxValue, int.MinValue, 11, -2147483647, 0, 103,
            -6, int.MinValue, int.MinValue, 6, 1073741824, -1, -9,
            -13, 2147483645, -2147483645, 16, 1, 1, 86
        ];

        // Thirty-one columns contain complete 512-, 256- and 128-bit blocks, followed by three scalar columns.
        // A seven-value period puts different samples at each block boundary instead of repeating vector-sized groups.
        int length = (2 * Vector512<int>.Count) - 1;
        int[] first = new int[length];
        int[] second = new int[length];
        int[] third = new int[length];
        int sampleCount = firstValues.Length;

        for (int operation = 0; operation <= 6; operation++)
        {
            for (int i = 0; i < length; i++)
            {
                int sample = i % sampleCount;
                first[i] = firstValues[sample];
                second[i] = secondValues[sample];
                third[i] = thirdValues[sample];
            }

            JxlModularColorTransform.Apply(operation, first, second, third);

            int offset = operation * 3 * sampleCount;
            for (int i = 0; i < length; i++)
            {
                int sample = i % sampleCount;

                Assert.Equal(expected[offset + sample], first[i]);
                Assert.Equal(expected[offset + sampleCount + sample], second[i]);
                Assert.Equal(expected[offset + (2 * sampleCount) + sample], third[i]);
            }
        }
    }
}
