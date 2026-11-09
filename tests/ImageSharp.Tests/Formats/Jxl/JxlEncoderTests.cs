// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

[Trait("Format", "Jxl")]
[ValidateDisposedMemoryAllocations]
public class JxlEncoderTests
{
    /// <summary>
    /// Checks default encoding from decoded color formats and retains or omits stored orientation as requested.
    /// </summary>
    /// <param name="provider">The original JPEG, palette or oriented JPEG XL input.</param>
    /// <param name="skipMetadata">Whether to omit ancillary metadata and its orientation.</param>
    /// <param name="orientation">The expected displayed orientation in the output header.</param>
    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Jpeg420Small, PixelTypes.Rgba32, false, ExifOrientationMode.TopLeft)]
    [WithFile(TestImages.Jpeg.Baseline.Ycck, PixelTypes.Rgba32, false, ExifOrientationMode.TopLeft)]
    [WithFile(TestImages.Gif.StaticNontransparent, PixelTypes.Rgba32, false, ExifOrientationMode.TopLeft)]
    [WithFile(TestImages.Png.PalettedFourColor, PixelTypes.Rgba32, false, ExifOrientationMode.TopLeft)]
    [WithFile(TestImages.Jxl.Orientation6, PixelTypes.Rgba32, false, ExifOrientationMode.RightTop)]
    [WithFile(TestImages.Jxl.Orientation6, PixelTypes.Rgba32, true, ExifOrientationMode.TopLeft)]
    public void EncodeDecodedFormatsMatchesNativePixels(TestImageProvider<Rgba32> provider, bool skipMetadata, ushort orientation)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateDecodedFormatPixels,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider,
            FormattableString.Invariant($"{skipMetadata},{orientation}"));

    /// <summary>
    /// Checks compact RGB and grayscale encodings against native output at each SIMD width.
    /// </summary>
    /// <param name="provider">The original reference pixels decoded by libjxl.</param>
    /// <param name="encoding">The compact color description selected for encoding.</param>
    [Theory]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, "p3")]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, "rec2020")]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, "pq")]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, "hlg")]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, "linear")]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, "custom")]
    [WithFile(TestImages.Jxl.GrayscalePatches, PixelTypes.RgbaVector, "gray")]
    public void EncodeCompactColorMatchesNativePixels(TestImageProvider<RgbaVector> provider, string encoding)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateCompactColorPixels,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider,
            encoding);

    /// <summary>
    /// Checks supplied extra-channel samples, spatial resolution and source ownership against native encoding.
    /// </summary>
    /// <param name="provider">The stored input decoded by libjxl.</param>
    /// <param name="type">The extra-channel type.</param>
    /// <param name="shift">The base-two reduction in sample dimensions.</param>
    /// <param name="bits">The stored sample precision.</param>
    /// <param name="exponentBits">The stored floating-point exponent width, or zero for integers.</param>
    /// <param name="progressive">Whether the managed file uses two passes.</param>
    /// <param name="associated">Whether the supplied primary alpha associates the image color components.</param>
    /// <param name="multiple">Whether to supply a second channel with different native sample values.</param>
    [Theory]
    [WithFile(TestImages.Jxl.PermutedSections, PixelTypes.Rgb24, JxlExtraChannelType.Depth, 0, 8, 0, false, false, false)]
    [WithFile(TestImages.Jxl.PermutedSections, PixelTypes.Rgb24, JxlExtraChannelType.Depth, 0, 8, 0, false, false, true)]
    [WithFile(TestImages.Jxl.OddSizeUpsampling, PixelTypes.Rgb24, JxlExtraChannelType.SpotColor, 1, 16, 0, true, false, false)]
    [WithFile(TestImages.Jxl.OddSizeUpsampling, PixelTypes.Rgb24, JxlExtraChannelType.SelectionMask, 2, 12, 0, false, false, false)]
    [WithFile(TestImages.Jxl.OddSizeUpsampling, PixelTypes.Rgb24, JxlExtraChannelType.ColorFilterArray, 3, 16, 0, true, false, false)]
    [WithFile(TestImages.Jxl.OddSizeUpsampling, PixelTypes.Rgb24, JxlExtraChannelType.Thermal, 1, 16, 5, false, false, false)]
    [WithFile(TestImages.Jxl.CroppedTrafficLight, PixelTypes.Rgb24, JxlExtraChannelType.Optional, 2, 24, 0, true, false, false)]
    [WithFile(TestImages.Jxl.OddSizeUpsampling, PixelTypes.Rgb24, JxlExtraChannelType.Alpha, 1, 16, 0, false, true, false)]
    [WithFile(TestImages.Jxl.OddSizeUpsampling, PixelTypes.Rgb24, JxlExtraChannelType.Alpha, 3, 32, 8, true, false, false)]
    [WithFile(TestImages.Jxl.OddSizeUpsampling, PixelTypes.Rgb24, JxlExtraChannelType.Black, 0, 8, 0, false, false, false)]
    [WithFile(TestImages.Jxl.OddSizeUpsampling, PixelTypes.Rgb24, JxlExtraChannelType.Black, 2, 16, 0, true, false, false)]
    public void EncodeExtraChannelsMatchNativePixels(
        TestImageProvider<Rgb24> provider,
        JxlExtraChannelType type,
        int shift,
        int bits,
        int exponentBits,
        bool progressive,
        bool associated,
        bool multiple)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateExtraChannelPixels,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider,
            FormattableString.Invariant($"{(int)type},{shift},{bits},{exponentBits},{progressive},{associated},{multiple}"));

    /// <summary>
    /// Encodes native reference pixels through Image.Save and checks native and managed reconstruction at each SIMD width.
    /// </summary>
    /// <typeparam name="TPixel">The source and comparison pixel type.</typeparam>
    /// <param name="provider">The stored reference image.</param>
    /// <param name="bits">The declared sample precision.</param>
    /// <param name="progressive">Whether to split the encoded components across two resolution passes.</param>
    [Theory]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.Rgba32 | PixelTypes.L8, 8, false)]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.Rgba64 | PixelTypes.L16, 16, false)]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, 32, false)]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.Rgba32, 12, false)]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.Rgba32, 24, false)]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.Rgba32, 24, false)]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.Rgba32, 31, false)]
    [WithFile(TestImages.Jxl.Dice, PixelTypes.Rgba32, 8, false)]
    [WithFile(TestImages.Jxl.PermutedSections, PixelTypes.Rgb24, 8, false)]
    [WithFile(TestImages.Jxl.OddSizeUpsampling, PixelTypes.Rgba32 | PixelTypes.L8, 8, true)]
    [WithFile(TestImages.Jxl.PermutedSections, PixelTypes.Rgb24, 8, true)]
    [WithFile(TestImages.Jxl.PermutedSections, PixelTypes.Rgba64, 16, true)]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, 32, true)]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.Rgba32, 31, true)]
    public void EncodeLosslessMatchesNativePixels<TPixel>(TestImageProvider<TPixel> provider, int bits, bool progressive)
        where TPixel : unmanaged, IPixel<TPixel>
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            PixelTests<TPixel>.ValidateEncodedPixels,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider,
            FormattableString.Invariant($"{bits},{progressive}"));

    /// <summary>
    /// Checks native reconstruction of lossy XYB output across SIMD widths and scalar tails.
    /// </summary>
    /// <param name="provider">The original native pixel input.</param>
    /// <param name="distance">The requested reconstruction distance.</param>
    /// <param name="progressive">Whether to split the encoded components across two resolution passes.</param>
    [Theory]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.RgbaVector, 1F, false)]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.RgbaVector, 3F, false)]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, 1F, false)]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, 3F, false)]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.RgbaVector, 1F, true)]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, 1F, true)]
    public void EncodeLossyMatchesNativePixels(TestImageProvider<RgbaVector> provider, float distance, bool progressive)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateLossyPixels,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider,
            FormattableString.Invariant($"{distance},{progressive}"));

    /// <summary>
    /// Checks binary16 rounding against native output and checks other float layouts with both decoders at each SIMD width.
    /// </summary>
    /// <param name="provider">The original floating-point reference image.</param>
    /// <param name="bits">The declared sample precision.</param>
    /// <param name="exponentBits">The declared exponent precision.</param>
    [Theory]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, 16, 5)]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, 24, 8)]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, 16, 6)]
    public void EncodeFloatingPointMatchesNativePixels(TestImageProvider<RgbaVector> provider, int bits, int exponentBits)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateFloatingPointPixels,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider,
            FormattableString.Invariant($"{bits},{exponentBits}"));

    /// <summary>
    /// Checks integer output bounds for native extended RGB pixels at each SIMD width.
    /// </summary>
    /// <param name="provider">The original reference image with negative and extended RGB samples.</param>
    /// <param name="bits">The declared integer precision.</param>
    [Theory]
    [WithFile(TestImages.Jxl.SpotColor, PixelTypes.RgbaVector, 8)]
    [WithFile(TestImages.Jxl.SpotColor, PixelTypes.RgbaVector, 31)]
    public void EncodeIntegerBoundsMatchesNativePixels(TestImageProvider<RgbaVector> provider, int bits)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidateIntegerBounds,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider,
            bits.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// Checks that retained XYB metadata does not change the source pixel layout during lossless encoding.
    /// </summary>
    /// <param name="provider">The original XYB reference image.</param>
    [Theory]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.Rgba32)]
    public void EncodeDecodedXybMatchesNativePixels(TestImageProvider<Rgba32> provider)
    {
        provider.Configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(provider.Configuration) { MaxFrames = 1, ColorProfileHandling = ColorProfileHandling.Convert, SkipMetadata = true };

        TestFile input = TestFile.Create(provider.SourceFileOrDescription);
        using MemoryStream inputStream = new(input.Bytes, false);
        ImageInfo info = Image.Identify(options, inputStream);

        using Image<Rgba32> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options);
        source.Metadata.SetFormatMetadata(JxlFormat.Instance, info.Metadata.GetFormatMetadata(JxlFormat.Instance).DeepClone());
        JxlMetadata metadata = source.Metadata.GetFormatMetadata(JxlFormat.Instance);
        JxlColorType colorType = metadata.ColorType;
        JxlEncoder encoder = new() { SkipMetadata = true };
        string path = provider.Utility.SaveTestOutputFile(source, "jxl", encoder);

        using FileStream stream = File.OpenRead(path);
        using Image<Rgba32> native = LibJxlReferenceDecoder.Instance.Decode<Rgba32>(options, stream);

        Assert.Equal(JxlColorType.Xyb, colorType);
        ImageComparer.Exact.VerifySimilarity(source, native);
        Assert.Equal(colorType, metadata.ColorType);
    }

    /// <summary>
    /// Checks profile retention and the existing SkipMetadata contract with original reference profiles.
    /// </summary>
    /// <typeparam name="TPixel">The native source pixel type and its alpha representation.</typeparam>
    /// <param name="provider">The original profile-bearing image.</param>
    /// <param name="skipMetadata">Whether to omit ancillary metadata.</param>
    /// <param name="associated">Whether the caller selects associated alpha for the encoded components.</param>
    /// <param name="requiresIcc">Whether the stored input must supply an ICC profile for this case.</param>
    [Theory]
    [WithFile(TestImages.Jxl.WithIcc, PixelTypes.RgbaVector, false, false, true)]
    [WithFile(TestImages.Jxl.Exif, PixelTypes.RgbaVector, false, false, false)]
    [WithFile(TestImages.Jxl.Exif, PixelTypes.RgbaVector, true, false, false)]
    [WithFile(TestImages.Jxl.LinearRgbaWithIcc, PixelTypes.RgbaVectorP, false, true, true)]
    public void EncodeRetainsProfiles<TPixel>(TestImageProvider<TPixel> provider, bool skipMetadata, bool associated, bool requiresIcc)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.Configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(provider.Configuration) { MaxFrames = 1, ColorProfileHandling = ColorProfileHandling.Preserve };

        TestFile input = TestFile.Create(provider.SourceFileOrDescription);
        using MemoryStream inputStream = new(input.Bytes, false);
        ImageInfo info = Image.Identify(options, inputStream);

        if (requiresIcc)
        {
            Assert.NotNull(info.Metadata.IccProfile);
        }

        using Image<TPixel> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options);
        source.Metadata.SetFormatMetadata(JxlFormat.Instance, info.Metadata.GetFormatMetadata(JxlFormat.Instance).DeepClone());
        source.Metadata.IccProfile = info.Metadata.IccProfile;
        source.Metadata.ExifProfile = info.Metadata.ExifProfile;
        source.Metadata.XmpProfile = info.Metadata.XmpProfile;

        if (associated)
        {
            JxlMetadata metadata = source.Metadata.GetFormatMetadata(JxlFormat.Instance);

            // The caller selects associated float alpha. The associated pixel type also removes undefined
            // hidden RGB at zero alpha from the comparison, through the normal pixel conversion contract.
            metadata.ExtraChannels.Clear();
            metadata.ExtraChannels.Add(new JxlExtraChannelMetadata(
                JxlExtraChannelType.Alpha,
                new JxlBitDepth(32, 8),
                PixelAlphaRepresentation.Associated));
        }

        JxlEncoder encoder = new() { BitDepth = new JxlBitDepth(32, 8), SkipMetadata = skipMetadata };
        string path = provider.Utility.SaveTestOutputFile(
            source, "jxl", encoder, testOutputDetails: FormattableString.Invariant($"{skipMetadata}_{associated}"));

        using Image<TPixel> actual = Image.Load<TPixel>(options, path);
        using FileStream stream = File.OpenRead(path);
        using Image<TPixel> native = LibJxlReferenceDecoder.Instance.Decode<TPixel>(options, stream);

        ImageComparer.Tolerant(0.00052F).VerifySimilarity(native, actual);
        ImageComparer.Tolerant(0.00052F).VerifySimilarity(source, native);

        if (source.Metadata.IccProfile is not null)
        {
            Assert.Equal(source.Metadata.IccProfile.ToByteArray(), Assert.IsType<IccProfile>(actual.Metadata.IccProfile).ToByteArray());
        }

        if (skipMetadata)
        {
            Assert.Null(actual.Metadata.ExifProfile);
            Assert.Null(actual.Metadata.XmpProfile);
        }
        else
        {
            Assert.Equal(source.Metadata.ExifProfile?.ToByteArray(), actual.Metadata.ExifProfile?.ToByteArray());
            Assert.Equal(source.Metadata.XmpProfile?.Data, actual.Metadata.XmpProfile?.Data);
        }
    }

    /// <summary>
    /// Checks native acceptance and retained CMYK metadata with separate color, black and alpha precisions.
    /// </summary>
    /// <param name="provider">The original CMYK profile and native displayed pixels.</param>
    [Theory]
    [WithFile(TestImages.Jxl.CmykLayers, PixelTypes.RgbaVector)]
    public void EncodeCmykRetainsIndependentChannelPrecisions(TestImageProvider<RgbaVector> provider)
    {
        provider.Configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(provider.Configuration) { MaxFrames = 1, ColorProfileHandling = ColorProfileHandling.Preserve };
        TestFile input = TestFile.Create(provider.SourceFileOrDescription);
        using MemoryStream inputStream = new(input.Bytes, false);
        ImageInfo info = Image.Identify(options, inputStream);
        IccProfile profile = Assert.IsType<IccProfile>(info.Metadata.IccProfile);

        using Image<RgbaVector> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options);
        source.Metadata.IccProfile = profile;
        JxlMetadata metadata = source.Metadata.GetFormatMetadata(JxlFormat.Instance);
        metadata.BitDepth = new JxlBitDepth(32, 8);
        metadata.ExtraChannels.Add(new JxlExtraChannelMetadata(JxlExtraChannelType.Black, new JxlBitDepth(16, 0), PixelAlphaRepresentation.None));
        metadata.ExtraChannels.Add(new JxlExtraChannelMetadata(JxlExtraChannelType.Alpha, new JxlBitDepth(8, 0), PixelAlphaRepresentation.Unassociated));
        JxlEncoder encoder = new() { SkipMetadata = true };
        string path = provider.Utility.SaveTestOutputFile(source, "jxl", encoder);

        using Image<RgbaVector> actual = Image.Load<RgbaVector>(options, path);
        using FileStream stream = File.OpenRead(path);

        // The native CMYK pixel comparison remains disabled for the profile-scale error in
        // https://github.com/libjxl/libjxl/issues/5113. Native decoding must still accept the complete emitted file.
        using Image<RgbaVector> native = LibJxlReferenceDecoder.Instance.Decode<RgbaVector>(options, stream);

        Assert.Equal(profile.ToByteArray(), Assert.IsType<IccProfile>(actual.Metadata.IccProfile).ToByteArray());
        JxlMetadata encoded = actual.Metadata.GetFormatMetadata(JxlFormat.Instance);
        Assert.Equal(new JxlBitDepth(32, 8), encoded.BitDepth);
        Assert.Collection(
            encoded.ExtraChannels,
            black =>
            {
                Assert.Equal(JxlExtraChannelType.Black, black.Type);
                Assert.Equal(new JxlBitDepth(16, 0), black.BitDepth);
            },
            alpha =>
            {
                Assert.Equal(JxlExtraChannelType.Alpha, alpha.Type);
                Assert.Equal(new JxlBitDepth(8, 0), alpha.BitDepth);
                Assert.Equal(PixelAlphaRepresentation.Unassociated, alpha.AlphaRepresentation);
            });
    }

    /// <summary>
    /// Checks the pixels and exact positive delays of original animation frames after a full-canvas encoding.
    /// </summary>
    /// <param name="provider">The original animation reference.</param>
    [Theory]
    [WithFile(TestImages.Jxl.CroppedTrafficLight, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jxl.CroppedTrafficLightLongDuration, PixelTypes.Rgba32)]
    [WithFile(TestImages.Jxl.AnimationIcos4d, PixelTypes.Rgba32)]
    public void EncodeAnimationMatchesNativePixels(TestImageProvider<Rgba32> provider)
    {
        provider.Configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(provider.Configuration) { MaxFrames = 2, ColorProfileHandling = ColorProfileHandling.Convert, SkipMetadata = true };

        TestFile input = TestFile.Create(provider.SourceFileOrDescription);
        using MemoryStream inputStream = new(input.Bytes, false);
        ImageInfo info = Image.Identify(options, inputStream);

        using Image<Rgba32> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options);
        source.Metadata.SetFormatMetadata(JxlFormat.Instance, info.Metadata.GetFormatMetadata(JxlFormat.Instance).DeepClone());

        for (int i = 0; i < source.Frames.Count; i++)
        {
            source.Frames[i].Metadata.SetFormatMetadata(JxlFormat.Instance, info.FrameMetadataCollection[i].GetFormatMetadata(JxlFormat.Instance).DeepClone());
        }

        JxlEncoder encoder = new() { ColorType = JxlColorType.Rgb, BitDepth = new JxlBitDepth(8, 0), SkipMetadata = true };
        string path = provider.Utility.SaveTestOutputFile(source, "jxl", encoder);

        using Image<Rgba32> actual = Image.Load<Rgba32>(options, path);
        using FileStream stream = File.OpenRead(path);
        using Image<Rgba32> native = LibJxlReferenceDecoder.Instance.Decode<Rgba32>(options, stream);

        Assert.Equal(2, source.Frames.Count);
        ImageComparer.Exact.VerifySimilarity(source, native);
        ImageComparer.Exact.VerifySimilarity(source, actual);
        Assert.Equal(source.Metadata.GetFormatMetadata(JxlFormat.Instance).RepeatCount, actual.Metadata.GetFormatMetadata(JxlFormat.Instance).RepeatCount);

        for (int i = 0; i < source.Frames.Count; i++)
        {
            Assert.Equal(
                info.FrameMetadataCollection[i].GetFormatMetadata(JxlFormat.Instance).FrameDelay,
                actual.Frames[i].Metadata.GetFormatMetadata(JxlFormat.Instance).FrameDelay);

            Assert.Equal(
                info.FrameMetadataCollection[i].GetFormatMetadata(JxlFormat.Instance).FrameDelay,
                source.Frames[i].Metadata.GetFormatMetadata(JxlFormat.Instance).FrameDelay);
        }
    }

    /// <summary>
    /// Checks that zero-delay source frames remain displayed without changing their source metadata.
    /// </summary>
    /// <param name="provider">The original animation that supplies two distinct canvases.</param>
    [Theory]
    [WithFile(TestImages.Jxl.CroppedTrafficLight, PixelTypes.Rgba32)]
    public void EncodeZeroDelayAnimationMatchesNativePixels(TestImageProvider<Rgba32> provider)
    {
        provider.Configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(provider.Configuration) { MaxFrames = 2, ColorProfileHandling = ColorProfileHandling.Convert, SkipMetadata = true };

        using Image<Rgba32> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options);

        foreach (ImageFrame<Rgba32> frame in source.Frames)
        {
            frame.Metadata.GetFormatMetadata(JxlFormat.Instance).FrameDelay = new LongRational(0, 1);
        }

        JxlEncoder encoder = new() { ColorType = JxlColorType.Rgb, BitDepth = new JxlBitDepth(8, 0), SkipMetadata = true };
        string path = provider.Utility.SaveTestOutputFile(source, "jxl", encoder);

        using Image<Rgba32> actual = Image.Load<Rgba32>(options, path);
        using FileStream stream = File.OpenRead(path);
        using Image<Rgba32> native = LibJxlReferenceDecoder.Instance.Decode<Rgba32>(options, stream);

        Assert.Equal(2, source.Frames.Count);
        ImageComparer.Exact.VerifySimilarity(source, native);
        ImageComparer.Exact.VerifySimilarity(source, actual);

        // JPEG XL hides a non-final zero-duration frame. A positive encoded duration must keep the
        // first source canvas displayed; the final canvas can retain a zero duration.
        LongRational firstDelay = actual.Frames[0].Metadata.GetFormatMetadata(JxlFormat.Instance).FrameDelay;
        Assert.True(firstDelay.Numerator > 0);
        Assert.True(firstDelay.Denominator > 0);
        Assert.Equal(new LongRational(0, 1), actual.Frames[1].Metadata.GetFormatMetadata(JxlFormat.Instance).FrameDelay);

        foreach (ImageFrame<Rgba32> frame in source.Frames)
        {
            Assert.Equal(new LongRational(0, 1), frame.Metadata.GetFormatMetadata(JxlFormat.Instance).FrameDelay);
        }
    }

    /// <summary>
    /// Checks that failed workspace rents release encoder buffers and retain the caller's image and stream.
    /// </summary>
    /// <param name="provider">The stored native pixel input.</param>
    /// <param name="failedAllocation">The encoder allocation attempt that fails, counted after caller-owned inputs are prepared.</param>
    /// <param name="progressive">Whether to prepare retained progressive channel planes.</param>
    /// <param name="reducedAlpha">Whether to supply a reduced primary alpha channel that requires expansion.</param>
    [Theory]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.Rgba32, 2, false, false)]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.Rgba32, 8, false, false)]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.Rgba32, 6, true, false)]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.Rgba32, 5, false, true)]
    public void EncodeReturnsBuffersAfterAllocationFailure(
        TestImageProvider<Rgba32> provider,
        int failedAllocation,
        bool progressive,
        bool reducedAlpha)
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        provider.Configuration = new(new JxlConfigurationModule()) { MemoryAllocator = allocator };
        DecoderOptions options = new(provider.Configuration) { MaxFrames = 1, ColorProfileHandling = ColorProfileHandling.Convert, SkipMetadata = true };
        using MemoryStream stream = new();

        using (Image<Rgba32> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options))
        {
            using Buffer2D<float> alpha = reducedAlpha
                ? allocator.Allocate2D<float>(((source.Width - 1) >> 1) + 1, ((source.Height - 1) >> 1) + 1)
                : null;

            JxlExtraChannel[] channels = [];

            if (alpha is not null)
            {
                for (int y = 0; y < alpha.Height; y++)
                {
                    Span<float> row = alpha.DangerousGetRowSpan(y);

                    for (int x = 0; x < row.Length; x++)
                    {
                        row[x] = source[x << 1, y << 1].ToUnassociatedScaledVector4().W;
                    }
                }

                JxlExtraChannelMetadata metadata = new(JxlExtraChannelType.Alpha, new JxlBitDepth(8, 0), PixelAlphaRepresentation.Associated)
                {
                    DimensionShift = 1
                };

                channels = [new JxlExtraChannel(metadata, [alpha])];
            }

            // Retain caller alpha in this baseline. Progressive rent six follows two retained channel rents;
            // reduced-alpha rent five follows its expanded plane and fails while preparing the filter workspace.
            long sourceBytes = allocator.LiveBufferBytes;
            allocator.FailAtAllocation = allocator.AllocationLog[^1].AllocationId + failedAllocation;
            JxlEncoder encoder = new() { Progressive = progressive, ExtraChannels = channels, SkipMetadata = true };

            Assert.Throws<InvalidMemoryOperationException>(() => source.Save(stream, encoder));
            Assert.Equal(sourceBytes, allocator.LiveBufferBytes);
            Assert.True(stream.CanWrite);

            if (alpha is not null)
            {
                Assert.False(alpha.IsDisposed);
            }
        }

        Assert.Equal(0, allocator.LiveBufferBytes);
    }

    /// <summary>
    /// Checks cancellation before encoding and during output, including cleanup of allocator-owned workspace.
    /// </summary>
    /// <param name="provider">The stored native pixel input.</param>
    /// <param name="beforeStart">Whether to cancel before the encoder creates its workspace.</param>
    [Theory]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.Rgba32, true)]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.Rgba32, false)]
    public async Task EncodeHonorsCancellation(TestImageProvider<Rgba32> provider, bool beforeStart)
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        provider.Configuration = new(new JxlConfigurationModule()) { MemoryAllocator = allocator };
        DecoderOptions options = new(provider.Configuration) { MaxFrames = 1, ColorProfileHandling = ColorProfileHandling.Convert, SkipMetadata = true };
        using PausedStream stream = new(new MemoryStream());
        using CancellationTokenSource cancellation = new();

        stream.OnWaiting(() =>
        {
            // The first underlying output write occurs after the encoder acquires its working buffers.
            // Release disables further pauses so the next cancellation check can return every owner.
            cancellation.Cancel();
            stream.Release();
        });

        if (beforeStart)
        {
            cancellation.Cancel();
        }

        using (Image<Rgba32> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options))
        {
            long sourceBytes = allocator.LiveBufferBytes;
            JxlEncoder encoder = new() { SkipMetadata = true };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.SaveAsync(stream, encoder, cancellation.Token));

            Assert.Equal(sourceBytes, allocator.LiveBufferBytes);
            Assert.True(stream.CanWrite);
        }

        Assert.Equal(0, allocator.LiveBufferBytes);
    }

    /// <summary>
    /// Checks that explicit XYB coding rejects a lossless reconstruction distance.
    /// </summary>
    /// <param name="provider">The stored native pixel input.</param>
    [Theory]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.RgbaVector)]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector)]
    public void EncodeRejectsIncompatibleOptions(TestImageProvider<RgbaVector> provider)
    {
        provider.Configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(provider.Configuration) { MaxFrames = 1, ColorProfileHandling = ColorProfileHandling.Convert, SkipMetadata = true };
        using Image<RgbaVector> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options);
        JxlEncoder encoder = new() { ColorType = JxlColorType.Xyb, Distance = 0F, BitDepth = new JxlBitDepth(32, 8) };
        using MemoryStream stream = new();

        Assert.Throws<ArgumentException>(() => source.Save(stream, encoder));
    }

    /// <summary>
    /// Checks that explicit color and precision options take precedence without changing source metadata.
    /// </summary>
    /// <typeparam name="TPixel">The source and comparison pixel type.</typeparam>
    /// <param name="provider">The stored image decoded by libjxl.</param>
    /// <param name="colorType">The explicit output color model.</param>
    /// <param name="distance">The reconstruction distance.</param>
    /// <param name="bits">The explicit sample precision.</param>
    [Theory]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.L16, JxlColorType.Luminance, 0F, 16)]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.RgbaVector, JxlColorType.Rgb, 0F, 32)]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.RgbaVector, JxlColorType.Luminance, 0F, 32)]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.RgbaVector, JxlColorType.Rgb, 1F, 32)]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.RgbaVector, JxlColorType.Xyb, 1F, 32)]
    [WithFile(TestImages.Jxl.LossyRgb3x3, PixelTypes.RgbaVector, JxlColorType.Luminance, 1F, 32)]
    [WithFile(TestImages.Jxl.LosslessFloat, PixelTypes.RgbaVector, JxlColorType.Luminance, 1F, 32)]
    [WithFile(TestImages.Jxl.WithIcc, PixelTypes.RgbaVector, JxlColorType.Other, 0F, 32)]
    public void EncoderOptionsOverrideMetadata<TPixel>(TestImageProvider<TPixel> provider, JxlColorType colorType, float distance, int bits)
        where TPixel : unmanaged, IPixel<TPixel>
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            PixelTests<TPixel>.ValidateEncoderOptions,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic,
            provider,
            FormattableString.Invariant($"{(int)colorType},{distance},{bits}"));

    /// <summary>
    /// Checks default JPEG XL encoding of decoded RGB pixels and their retained display orientation.
    /// </summary>
    /// <param name="providerDump">The serialized original-file provider.</param>
    /// <param name="encodingDump">The metadata policy and expected orientation, separated by a comma.</param>
    private static void ValidateDecodedFormatPixels(string providerDump, string encodingDump)
    {
        TestImageProvider<Rgba32> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgba32>>(providerDump);
        string[] encoding = encodingDump.Split(',');
        bool skipMetadata = FeatureTestRunner.Deserialize<bool>(encoding[0]);
        ushort orientation = FeatureTestRunner.Deserialize<ushort>(encoding[1]);
        provider.Configuration = Configuration.Default.Clone();
        DecoderOptions options = new(provider.Configuration) { MaxFrames = 1, ColorProfileHandling = ColorProfileHandling.Convert };
        TestFile input = TestFile.Create(provider.SourceFileOrDescription);
        using MemoryStream inputStream = new(input.Bytes, false);
        using Image<Rgba32> source = Image.Load<Rgba32>(options, inputStream);
        using Image<Rgba32> original = source.Clone();
        JxlBitDepth depth = source.Metadata.DecodedImageFormat is JxlFormat
            ? source.Metadata.GetFormatMetadata(JxlFormat.Instance).BitDepth : new JxlBitDepth(8, 0);

        ushort sourceOrientation = source.Metadata.ExifProfile?.GetValue(ExifTag.Orientation)?.Value ?? ExifOrientationMode.TopLeft;
        JxlEncoder encoder = new() { SkipMetadata = skipMetadata };
        string details = FormattableString.Invariant($"{skipMetadata}_{orientation}");
        string path = provider.Utility.SaveTestOutputFile(source, "jxl", encoder, testOutputDetails: details);

        using FileStream nativeStream = File.OpenRead(path);
        using Image<Rgba32> native = LibJxlReferenceDecoder.Instance.Decode<Rgba32>(options, nativeStream);
        using Image<Rgba32> actual = Image.Load<Rgba32>(options, path);
        using MemoryStream referenceStream = new();

        // The input pixels and metadata come from normal file loading. Native encoding supplies the
        // independent expected pixels, including all eight palette color bits and the retained alpha values.
        source.Save(referenceStream, new LibJxlReferenceEncoder { BitDepth = depth });
        referenceStream.Position = 0;
        using Image<Rgba32> expected = LibJxlReferenceDecoder.Instance.Decode<Rgba32>(options, referenceStream);

        ImageComparer.Exact.VerifySimilarity(expected, native);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
        ImageComparer.Exact.VerifySimilarity(original, source);

        using FileStream headerStream = File.OpenRead(path);
        using BufferedReadStream buffered = new(provider.Configuration, headerStream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, provider.Configuration.MemoryAllocator, SegmentIntegrityHandling.Strict, CancellationToken.None);

        Assert.Equal(orientation, header.Orientation);
        Assert.Equal(JxlColorType.Rgb, header.ProfileColorType);
        Assert.Equal(depth, header.Metadata.BitDepth);
        Assert.Equal(sourceOrientation, source.Metadata.ExifProfile?.GetValue(ExifTag.Orientation)?.Value ?? ExifOrientationMode.TopLeft);

        if (skipMetadata)
        {
            Assert.Null(actual.Metadata.ExifProfile);
        }
        else
        {
            Assert.Equal(orientation, actual.Metadata.ExifProfile?.GetValue(ExifTag.Orientation)?.Value ?? ExifOrientationMode.TopLeft);
        }
    }

    /// <summary>
    /// Checks compact encoding, native pixel reconstruction and the output color labels.
    /// </summary>
    /// <param name="providerDump">The serialized original-image provider.</param>
    /// <param name="encoding">The compact color description selected by the theory.</param>
    private static void ValidateCompactColorPixels(string providerDump, string encoding)
    {
        TestImageProvider<RgbaVector> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<RgbaVector>>(providerDump);
        provider.Configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(provider.Configuration) { MaxFrames = 1, ColorProfileHandling = ColorProfileHandling.Convert };
        using Image<RgbaVector> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options);

        // The source pixels always come from native working-sRGB output. These options select the
        // file's original encoding; the encoder must convert those pixels before writing its components.
        source.Metadata.IccProfile = null;
        source.Metadata.CicpProfile = encoding switch
        {
            "p3" => new(12, 13, 0, true),
            "rec2020" => new(9, 1, 0, true),
            "pq" => new(9, 16, 0, true),
            "hlg" => new(9, 18, 0, true),
            _ => null
        };

        JxlMetadata metadata = new() { BitDepth = new JxlBitDepth(32, 8) };
        if (encoding is "linear" or "gray")
        {
            metadata.Gamma = new Rational(1, 1);
            metadata.ColorType = encoding == "gray" ? JxlColorType.Luminance : JxlColorType.Rgb;
        }
        else if (encoding == "custom")
        {
            metadata.WhitePoint = new CieXyChromaticityCoordinates(0.31F, 0.33F);
            metadata.Primaries = new RgbPrimariesChromaticityCoordinates(
                new CieXyChromaticityCoordinates(0.63F, 0.33F),
                new CieXyChromaticityCoordinates(0.30F, 0.60F),
                new CieXyChromaticityCoordinates(0.15F, 0.06F));
        }

        source.Metadata.SetFormatMetadata(JxlFormat.Instance, metadata);
        JxlEncoder encoder = new() { SkipMetadata = true };
        string path = provider.Utility.SaveTestOutputFile(source, "jxl", encoder, testOutputDetails: encoding);

        using Image<RgbaVector> actual = Image.Load<RgbaVector>(options, path);
        using FileStream stream = File.OpenRead(path);
        using Image<RgbaVector> native = LibJxlReferenceDecoder.Instance.Decode<RgbaVector>(options, stream);

        ImageComparer.Tolerant(0.00052F).VerifySimilarity(native, actual);
        ImageComparer.Tolerant(0.00052F).VerifySimilarity(source, native);

        // Decode returns sRGB pixels even when the file declares another original encoding.
        CicpProfile output = Assert.IsType<CicpProfile>(actual.Metadata.CicpProfile);
        Assert.Equal(CicpColorPrimaries.ItuRBt709_6, output.ColorPrimaries);
        Assert.Equal(CicpTransferCharacteristics.Iec61966_2_1, output.TransferCharacteristics);

        stream.Position = 0;
        ImageInfo info = Image.Identify(options, stream);
        JxlMetadata encoded = info.Metadata.GetFormatMetadata(JxlFormat.Instance);
        Assert.Equal(encoding == "gray" ? JxlColorType.Luminance : JxlColorType.Rgb, encoded.ColorType);

        if (source.Metadata.CicpProfile is CicpProfile input)
        {
            CicpProfile retained = Assert.IsType<CicpProfile>(info.Metadata.CicpProfile);
            Assert.Equal(input.ColorPrimaries, retained.ColorPrimaries);
            Assert.Equal(input.TransferCharacteristics, retained.TransferCharacteristics);
        }
        else if (encoding is "linear" or "gray")
        {
            Assert.Equal(new Rational(1, 1), encoded.Gamma);
        }
        else
        {
            Assert.Null(info.Metadata.CicpProfile);
        }
    }

    /// <summary>
    /// Compares native and managed decoding of lossy output and checks source preservation.
    /// </summary>
    /// <param name="providerDump">The serialized original-image provider.</param>
    /// <param name="encodingDump">The serialized reconstruction distance and progressive setting.</param>
    private static void ValidateLossyPixels(string providerDump, string encodingDump)
    {
        TestImageProvider<RgbaVector> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<RgbaVector>>(providerDump);
        string[] encoding = encodingDump.Split(',');
        float distance = FeatureTestRunner.Deserialize<float>(encoding[0]);
        bool progressive = FeatureTestRunner.Deserialize<bool>(encoding[1]);
        provider.Configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(provider.Configuration) { MaxFrames = 1, ColorProfileHandling = ColorProfileHandling.Convert, SkipMetadata = true };

        using Image<RgbaVector> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options);

        if (provider.SourceFileOrDescription == TestImages.Jxl.LosslessFloat)
        {
            // The final 241-column group exercises a scalar tail after the widest SIMD conversion.
            source.Mutate(x => x.Crop(source.Width - 1, source.Height));
        }

        using Image<RgbaVector> original = source.Clone();
        JxlMetadata metadata = source.Metadata.GetFormatMetadata(JxlFormat.Instance);
        JxlColorType sourceColorType = metadata.ColorType;
        JxlBitDepth sourceBitDepth = metadata.BitDepth;
        JxlEncoder encoder = new() { Distance = distance, Progressive = progressive, BitDepth = new JxlBitDepth(32, 8), SkipMetadata = true };
        string path = provider.Utility.SaveTestOutputFile(source, "jxl", encoder, testOutputDetails: FormattableString.Invariant($"{distance}_{progressive}"));

        VerifyPassCount(path, provider.Configuration, progressive);

        using FileStream stream = File.OpenRead(path);
        using Image<RgbaVector> native = LibJxlReferenceDecoder.Instance.Decode<RgbaVector>(options, stream);
        using Image<RgbaVector> actual = Image.Load<RgbaVector>(options, path);
        using MemoryStream referenceStream = new();
        LibJxlReferenceEncoder referenceEncoder = new() { BitDepth = new JxlBitDepth(32, 8), Distance = distance };
        source.Save(referenceStream, referenceEncoder);
        referenceStream.Position = 0;

        using Image<RgbaVector> expected = LibJxlReferenceDecoder.Instance.Decode<RgbaVector>(options, referenceStream);

        ImageComparer.Tolerant(0.00052F).VerifySimilarity(expected, native);
        ImageComparer.Tolerant(0.00052F).VerifySimilarity(native, actual);
        ImageComparer.Exact.VerifySimilarity(original, source);
        Assert.Equal(JxlColorType.Xyb, actual.Metadata.GetFormatMetadata(JxlFormat.Instance).ColorType);
        Assert.Equal(sourceColorType, metadata.ColorType);
        Assert.Equal(sourceBitDepth, metadata.BitDepth);
    }

    /// <summary>
    /// Checks precision packing through both independent decoding and native binary16 reference encoding.
    /// </summary>
    /// <param name="providerDump">The serialized original-image provider.</param>
    /// <param name="precisionDump">The sample and exponent precisions, separated by a comma.</param>
    private static void ValidateFloatingPointPixels(string providerDump, string precisionDump)
    {
        TestImageProvider<RgbaVector> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<RgbaVector>>(providerDump);
        string[] precision = precisionDump.Split(',');
        byte bits = FeatureTestRunner.Deserialize<byte>(precision[0]);
        byte exponentBits = FeatureTestRunner.Deserialize<byte>(precision[1]);
        JxlBitDepth depth = new(bits, exponentBits);
        provider.Configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(provider.Configuration) { MaxFrames = 1, ColorProfileHandling = ColorProfileHandling.Convert, SkipMetadata = true };

        using Image<RgbaVector> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options);

        // Remove one column through the image API so the final group has a scalar tail after SIMD conversion.
        source.Mutate(x => x.Crop(source.Width - 1, source.Height));

        JxlEncoder encoder = new() { ColorType = JxlColorType.Rgb, BitDepth = depth, SkipMetadata = true };
        string path = provider.Utility.SaveTestOutputFile(source, "jxl", encoder, testOutputDetails: FormattableString.Invariant($"{bits}_{exponentBits}"));

        using FileStream stream = File.OpenRead(path);
        using Image<RgbaVector> native = LibJxlReferenceDecoder.Instance.Decode<RgbaVector>(options, stream);
        using Image<RgbaVector> actual = Image.Load<RgbaVector>(options, path);

        ImageComparer.Exact.VerifySimilarity(native, actual);
        Assert.Equal(depth, actual.Metadata.GetFormatMetadata(JxlFormat.Instance).BitDepth);

        if (depth == new JxlBitDepth(16, 5))
        {
            // Libjxl requires already prepared samples for shortened float layouts. The reference encoder
            // uses the established RgbaHalf pixel conversion and native FLOAT16 input to supply that precision.
            using MemoryStream referenceStream = new();
            source.Save(referenceStream, LibJxlReferenceEncoder.Instance);
            referenceStream.Position = 0;

            using Image<RgbaVector> expected = LibJxlReferenceDecoder.Instance.Decode<RgbaVector>(options, referenceStream);
            ImageComparer.Exact.VerifySimilarity(expected, native);
        }
    }

    /// <summary>
    /// Checks that declared unsigned samples bound extended source colors before integer packing.
    /// </summary>
    /// <param name="providerDump">The serialized original-image provider.</param>
    /// <param name="bitsDump">The declared integer precision.</param>
    private static void ValidateIntegerBounds(string providerDump, string bitsDump)
    {
        TestImageProvider<RgbaVector> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<RgbaVector>>(providerDump);
        byte bits = FeatureTestRunner.Deserialize<byte>(bitsDump);
        provider.Configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(provider.Configuration) { MaxFrames = 1, ColorProfileHandling = ColorProfileHandling.Convert, SkipMetadata = true };

        using Image<RgbaVector> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options);
        TestFile file = TestFile.Create(provider.SourceFileOrDescription);
        using MemoryStream originalStream = new(file.Bytes, false);
        using Image<Rgba32> expected = LibJxlReferenceDecoder.Instance.Decode<Rgba32>(options, originalStream);
        JxlEncoder encoder = new() { ColorType = JxlColorType.Rgb, BitDepth = new JxlBitDepth(bits, 0), SkipMetadata = true };
        string path = provider.Utility.SaveTestOutputFile(source, "jxl", encoder, bits);

        using FileStream stream = File.OpenRead(path);
        using Image<Rgba32> native = LibJxlReferenceDecoder.Instance.Decode<Rgba32>(options, stream);
        using Image<Rgba32> actual = Image.Load<Rgba32>(options, path);

        ImageComparer.Exact.VerifySimilarity(expected, native);
        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Compares native extra-channel reconstruction and displayed pixels without reference-side color or upsampling math.
    /// </summary>
    /// <param name="providerDump">The serialized stored-image provider.</param>
    /// <param name="encodingDump">The serialized type, reduction, precision, progressive setting, alpha association and second-channel flag.</param>
    private static void ValidateExtraChannelPixels(string providerDump, string encodingDump)
    {
        TestImageProvider<Rgb24> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgb24>>(providerDump);
        string[] encoding = encodingDump.Split(',');
        JxlExtraChannelType type = (JxlExtraChannelType)FeatureTestRunner.Deserialize<int>(encoding[0]);
        byte shift = FeatureTestRunner.Deserialize<byte>(encoding[1]);
        JxlBitDepth depth = new(FeatureTestRunner.Deserialize<byte>(encoding[2]), FeatureTestRunner.Deserialize<byte>(encoding[3]));
        bool progressive = FeatureTestRunner.Deserialize<bool>(encoding[4]);
        bool associated = FeatureTestRunner.Deserialize<bool>(encoding[5]);
        bool multiple = FeatureTestRunner.Deserialize<bool>(encoding[6]);
        provider.Configuration = new(new JxlConfigurationModule());
        DecoderOptions options = new(provider.Configuration) { ColorProfileHandling = ColorProfileHandling.Convert, SkipMetadata = true };

        using Image<Rgb24> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options);
        using Image<Rgb24> original = source.Clone();
        using IMemoryOwner<Vector4> rowOwner = provider.Configuration.MemoryAllocator.Allocate<Vector4>(source.Width);

        Span<Vector4> row = rowOwner.Memory.Span[..source.Width];
        List<Buffer2D<float>> planes = [];
        List<float[]> snapshots = [];
        List<Buffer2D<float>> secondPlanes = [];
        List<float[]> secondSnapshots = [];

        try
        {
            int width = ((source.Width - 1) >> shift) + 1;
            int height = ((source.Height - 1) >> shift) + 1;

            foreach (ImageFrame<Rgb24> frame in source.Frames)
            {
                Buffer2D<float> plane = provider.Configuration.MemoryAllocator.Allocate2D<float>(width, height);
                planes.Add(plane);
                float[] snapshot = new float[checked(width * height)];
                snapshots.Add(snapshot);
                Buffer2D<float> secondPlane = multiple ? provider.Configuration.MemoryAllocator.Allocate2D<float>(width, height) : null;

                if (secondPlane is not null)
                {
                    secondPlanes.Add(secondPlane);
                    secondSnapshots.Add(new float[checked(width * height)]);
                }

                for (int y = 0; y < height; y++)
                {
                    // The native source supplies every sample value. Select its grid positions without a
                    // reference-side filter; native decoding owns the expected spatial reconstruction.
                    PixelOperations<Rgb24>.Instance.ToUnassociatedScaledVector4(
                        provider.Configuration, frame.PixelBuffer.DangerousGetRowSpan(y << shift), row);

                    Span<float> samples = plane.DangerousGetRowSpan(y);
                    Span<float> secondSamples = secondPlane is null ? [] : secondPlane.DangerousGetRowSpan(y);

                    for (int x = 0; x < width; x++)
                    {
                        samples[x] = row[x << shift].X;

                        if (multiple)
                        {
                            secondSamples[x] = row[x << shift].Y;
                        }
                    }

                    samples.CopyTo(snapshot.AsSpan(y * width, width));

                    if (multiple)
                    {
                        secondSamples.CopyTo(secondSnapshots[^1].AsSpan(y * width, width));
                    }
                }
            }

            PixelAlphaRepresentation representation = type == JxlExtraChannelType.Alpha
                ? associated ? PixelAlphaRepresentation.Associated : PixelAlphaRepresentation.Unassociated
                : PixelAlphaRepresentation.None;

            JxlExtraChannelMetadata metadata = new(type, depth, representation)
            {
                DimensionShift = shift,
                Name = $"extra_{type}",
                SpotColor = new Vector4(0.25F, 0.5F, 0.75F, 0.5F),
                ColorFilterArrayChannel = type == JxlExtraChannelType.ColorFilterArray ? (ushort)2 : (ushort)0
            };

            List<JxlExtraChannel> suppliedChannels = [new JxlExtraChannel(metadata, planes)];
            JxlExtraChannelMetadata secondMetadata = new(JxlExtraChannelType.SelectionMask, depth, PixelAlphaRepresentation.None)
            {
                DimensionShift = shift,
                Name = "extra_SelectionMask"
            };

            if (multiple)
            {
                suppliedChannels.Add(new JxlExtraChannel(secondMetadata, secondPlanes));
            }

            // Float color storage isolates association from an additional integer color quantization.
            JxlEncoder encoder = new()
            {
                ColorType = JxlColorType.Rgb,
                BitDepth = new JxlBitDepth(32, 8),
                Progressive = progressive,
                ExtraChannels = suppliedChannels,
                TransparentColorMode = TransparentColorMode.Preserve,
                SkipMetadata = true
            };

            string path = provider.Utility.SaveTestOutputFile(source, "jxl", encoder, testOutputDetails: encodingDump.Replace(',', '_'));

            // Association changes the stored color components, but not their displayed color. Native
            // unassociated encoding supplies the same displayed pixels without a reference-side multiply.
            JxlExtraChannelMetadata referenceMetadata = associated
                ? new(type, depth, PixelAlphaRepresentation.Unassociated) { DimensionShift = shift, Name = metadata.Name }
                : metadata;

            LibJxlReferenceEncoder referenceEncoder = new()
            {
                BitDepth = new JxlBitDepth(32, 8),
                ExtraChannel = new JxlExtraChannel(referenceMetadata, planes)
            };

            using MemoryStream referenceStream = new();
            source.Save(referenceStream, referenceEncoder);
            referenceStream.Position = 0;

            using Image<RgbaVectorP> expected = LibJxlReferenceDecoder.Instance.Decode<RgbaVectorP>(options, referenceStream);
            using FileStream stream = File.OpenRead(path);
            using Image<RgbaVectorP> native = LibJxlReferenceDecoder.Instance.Decode<RgbaVectorP>(options, stream);
            using Image<RgbaVectorP> actual = Image.Load<RgbaVectorP>(options, path);

            ImageComparer.Tolerant(0.00052F).VerifySimilarity(expected, native);
            ImageComparer.Tolerant(0.00052F).VerifySimilarity(native, actual);
            ImageComparer.Exact.VerifySimilarity(original, source);

            referenceStream.Position = 0;
            stream.Position = 0;

            using Image<RgbaVector> expectedExtra =
                LibJxlReferenceDecoder.Instance.DecodeExtraChannel(options, referenceStream, 0, CancellationToken.None);

            using Image<RgbaVector> actualExtra =
                LibJxlReferenceDecoder.Instance.DecodeExtraChannel(options, stream, 0, CancellationToken.None);

            ImageComparer.Exact.VerifySimilarity(expectedExtra, actualExtra);

            if (multiple)
            {
                // Encode each plane independently through native index zero. The combined managed file
                // must retain each plane's samples at its own encoded channel index after scratch reuse.
                using MemoryStream secondReference = new();
                source.Save(secondReference, new LibJxlReferenceEncoder
                {
                    BitDepth = new JxlBitDepth(32, 8),
                    ExtraChannel = new JxlExtraChannel(secondMetadata, secondPlanes)
                });

                secondReference.Position = 0;
                stream.Position = 0;

                using Image<RgbaVector> expectedSecond =
                    LibJxlReferenceDecoder.Instance.DecodeExtraChannel(options, secondReference, 0, CancellationToken.None);

                using Image<RgbaVector> actualSecond =
                    LibJxlReferenceDecoder.Instance.DecodeExtraChannel(options, stream, 1, CancellationToken.None);

                ImageComparer.Exact.VerifySimilarity(expectedSecond, actualSecond);
            }

            JxlExtraChannelMetadata decoded = actual.Metadata.GetFormatMetadata(JxlFormat.Instance).ExtraChannels[0];
            Assert.Equal(type, decoded.Type);
            Assert.Equal(depth, decoded.BitDepth);
            Assert.Equal(shift, decoded.DimensionShift);
            Assert.Equal(metadata.Name, decoded.Name);
            Assert.Equal(representation, decoded.AlphaRepresentation);

            if (type == JxlExtraChannelType.ColorFilterArray)
            {
                Assert.Equal(metadata.ColorFilterArrayChannel, decoded.ColorFilterArrayChannel);
            }

            if (type == JxlExtraChannelType.SpotColor)
            {
                Assert.Equal(metadata.SpotColor, decoded.SpotColor);
            }

            for (int i = 0; i < planes.Count; i++)
            {
                Buffer2D<float> plane = planes[i];
                Assert.False(plane.IsDisposed);

                for (int y = 0; y < height; y++)
                {
                    Assert.True(snapshots[i].AsSpan(y * width, width).SequenceEqual(plane.DangerousGetRowSpan(y)));
                }
            }

            for (int i = 0; i < secondPlanes.Count; i++)
            {
                Buffer2D<float> plane = secondPlanes[i];
                Assert.False(plane.IsDisposed);

                for (int y = 0; y < height; y++)
                {
                    Assert.True(secondSnapshots[i].AsSpan(y * width, width).SequenceEqual(plane.DangerousGetRowSpan(y)));
                }
            }
        }
        finally
        {
            foreach (Buffer2D<float> plane in planes)
            {
                plane.Dispose();
            }

            foreach (Buffer2D<float> plane in secondPlanes)
            {
                plane.Dispose();
            }
        }
    }

    /// <summary>
    /// Checks the pass count in the file produced through Image.Save.
    /// </summary>
    /// <param name="path">The emitted file in the normal test output directory.</param>
    /// <param name="configuration">The configuration used to encode and decode the image.</param>
    /// <param name="progressive">Whether the requested output has two passes.</param>
    private static void VerifyPassCount(string path, Configuration configuration, bool progressive)
    {
        using FileStream stream = File.OpenRead(path);
        using BufferedReadStream buffered = new(configuration, stream);
        JxlBitReader reader = new(new JxlCodestreamReader(buffered));
        JxlHeader header = JxlHeader.Read(ref reader, configuration.MemoryAllocator, SegmentIntegrityHandling.Strict, CancellationToken.None);
        JxlFrameHeader frame = JxlFrameHeader.Read(ref reader, header, false, SegmentIntegrityHandling.Strict);

        Assert.Equal(progressive ? 2 : 1, frame.PassCount);
    }

    /// <summary>
    /// Keeps each remote callback on a closed pixel type, as required by FeatureTestRunner.
    /// </summary>
    /// <typeparam name="TPixel">The source and comparison pixel type.</typeparam>
    private static class PixelTests<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Checks explicit color and precision options through native decoding and independent native encoding.
        /// </summary>
        /// <param name="providerDump">The serialized native-image provider.</param>
        /// <param name="encodingDump">The explicit color model, distance and precision, separated by commas.</param>
        public static void ValidateEncoderOptions(string providerDump, string encodingDump)
        {
            TestImageProvider<TPixel> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<TPixel>>(providerDump);
            string[] encoding = encodingDump.Split(',');
            JxlColorType colorType = (JxlColorType)FeatureTestRunner.Deserialize<int>(encoding[0]);
            float distance = FeatureTestRunner.Deserialize<float>(encoding[1]);
            byte bits = FeatureTestRunner.Deserialize<byte>(encoding[2]);
            JxlBitDepth depth = new(bits, bits == 32 ? (byte)8 : (byte)0);
            provider.Configuration = new(new JxlConfigurationModule());
            DecoderOptions options = new(provider.Configuration) { MaxFrames = 1, ColorProfileHandling = ColorProfileHandling.Convert };
            using Image<TPixel> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options);

            if (colorType == JxlColorType.Other)
            {
                // Native decoding supplies working-sRGB pixels. Identification retains the original ICC bytes
                // needed to declare Other output; the encoder owns the conversion back to that device profile.
                TestFile input = TestFile.Create(provider.SourceFileOrDescription);
                using MemoryStream inputStream = new(input.Bytes, false);
                DecoderOptions identifyOptions = new(provider.Configuration) { MaxFrames = 1, ColorProfileHandling = ColorProfileHandling.Preserve };
                ImageInfo info = Image.Identify(identifyOptions, inputStream);
                source.Metadata.IccProfile = Assert.IsType<IccProfile>(info.Metadata.IccProfile);
            }

            if (source.Width > 16)
            {
                // An odd width leaves scalar samples after each enabled SIMD width on the larger float input.
                source.Mutate(x => x.Crop(source.Width - 1, source.Height));
            }

            using Image<TPixel> original = source.Clone();
            JxlMetadata metadata = source.Metadata.GetFormatMetadata(JxlFormat.Instance);
            JxlColorType metadataColorType = colorType == JxlColorType.Luminance ? JxlColorType.Rgb : JxlColorType.Luminance;
            metadata.ColorType = metadataColorType;
            metadata.BitDepth = new JxlBitDepth(8, 0);
            JxlEncoder encoder = new() { ColorType = colorType, Distance = distance, BitDepth = depth, SkipMetadata = true };
            string details = FormattableString.Invariant($"{colorType}_{distance}_{bits}");
            string path = provider.Utility.SaveTestOutputFile(source, "jxl", encoder, testOutputDetails: details);

            using FileStream stream = File.OpenRead(path);
            using Image<TPixel> native = LibJxlReferenceDecoder.Instance.Decode<TPixel>(options, stream);
            using Image<TPixel> actual = Image.Load<TPixel>(options, path);

            ImageComparer.Tolerant(0.00052F).VerifySimilarity(native, actual);

            using FileStream headerStream = File.OpenRead(path);
            using BufferedReadStream buffered = new(provider.Configuration, headerStream);
            JxlBitReader reader = new(new JxlCodestreamReader(buffered));
            JxlHeader header = JxlHeader.Read(ref reader, provider.Configuration.MemoryAllocator, SegmentIntegrityHandling.Strict, CancellationToken.None);

            Assert.Equal(colorType == JxlColorType.Xyb ? JxlColorType.Rgb : colorType, header.ProfileColorType);
            Assert.Equal(distance > 0, header.UsesXybTransform);
            Assert.Equal(depth, header.Metadata.BitDepth);

            if (typeof(TPixel) == typeof(L16))
            {
                // Retain the original exact luminance test while extending the option matrix.
                ImageComparer.Exact.VerifySimilarity(source, native);
            }
            else
            {
                using Image<TPixel> referenceInput = source.Clone();

                if (colorType == JxlColorType.Luminance)
                {
                    // Supply grayscale pixels through the public processing API. The reference encoder receives
                    // those pixels unchanged; libjxl alone produces the expected reconstructed output.
                    referenceInput.Mutate(x => x.Grayscale());
                }

                using MemoryStream referenceStream = new();
                referenceInput.Save(referenceStream, new LibJxlReferenceEncoder { BitDepth = depth, Distance = distance });
                referenceStream.Position = 0;
                using Image<TPixel> expected = LibJxlReferenceDecoder.Instance.Decode<TPixel>(options, referenceStream);

                ImageComparer.Tolerant(0.00052F).VerifySimilarity(expected, native);
            }

            ImageComparer.Exact.VerifySimilarity(original, source);
            Assert.Equal(metadataColorType, metadata.ColorType);
            Assert.Equal(new JxlBitDepth(8, 0), metadata.BitDepth);
        }

        /// <summary>
        /// Checks emitted pixels and source preservation through the public encoder path.
        /// </summary>
        /// <param name="providerDump">The serialized stored-image provider.</param>
        /// <param name="encodingDump">The serialized sample precision and progressive setting.</param>
        public static void ValidateEncodedPixels(string providerDump, string encodingDump)
        {
            TestImageProvider<TPixel> provider = FeatureTestRunner.DeserializeForXunit<TestImageProvider<TPixel>>(providerDump);
            string[] encoding = encodingDump.Split(',');
            int bits = FeatureTestRunner.Deserialize<int>(encoding[0]);
            bool progressive = FeatureTestRunner.Deserialize<bool>(encoding[1]);
            provider.Configuration = new(new JxlConfigurationModule());
            DecoderOptions options = new(provider.Configuration) { MaxFrames = 1, ColorProfileHandling = ColorProfileHandling.Convert, SkipMetadata = true };

            using Image<TPixel> source = provider.GetImage(LibJxlReferenceDecoder.Instance, options);
            using Image<TPixel> original = source.Clone();
            JxlColorType colorType = (TPixel.GetPixelTypeInfo().ColorType & ~PixelColorType.Alpha) == PixelColorType.Luminance
                ? JxlColorType.Luminance : JxlColorType.Rgb;

            JxlBitDepth depth = new((byte)bits, bits == 32 ? (byte)8 : (byte)0);
            JxlMetadata metadata = source.Metadata.GetFormatMetadata(JxlFormat.Instance);
            metadata.ColorType = colorType;
            metadata.BitDepth = depth;
            JxlEncoder encoder = new() { Progressive = progressive, SkipMetadata = true };

            // Keep the emitted file in the normal test output location for independent jxl-rs verification.
            string path = provider.Utility.SaveTestOutputFile(source, "jxl", encoder, FormattableString.Invariant($"{bits}_{progressive}"));

            VerifyPassCount(path, provider.Configuration, progressive);
            using FileStream nativeStream = File.OpenRead(path);
            using Image<TPixel> native = LibJxlReferenceDecoder.Instance.Decode<TPixel>(options, nativeStream);
            using Image<TPixel> actual = Image.Load<TPixel>(options, path);

            ImageComparer.Exact.VerifySimilarity(source, native);
            ImageComparer.Exact.VerifySimilarity(source, actual);
            ImageComparer.Exact.VerifySimilarity(original, source);
            Assert.Equal(depth, actual.Metadata.GetFormatMetadata(JxlFormat.Instance).BitDepth);
            Assert.Equal(colorType, actual.Metadata.GetFormatMetadata(JxlFormat.Instance).ColorType);
            Assert.Equal(depth, metadata.BitDepth);
            Assert.Equal(colorType, metadata.ColorType);
        }
    }
}
