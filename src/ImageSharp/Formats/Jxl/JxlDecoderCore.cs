// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Owns one JPEG XL read operation and applies the general decoder policies.
/// </summary>
internal sealed class JxlDecoderCore : ImageDecoderCore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The JPEG XL decoder options.</param>
    public JxlDecoderCore(JxlDecoderOptions options)
        : base(options.GeneralOptions)
    {
    }

    /// <inheritdoc/>
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The shared decoder base owns the buffered input. The codestream reader borrows it and follows
        // container fragments without copying their payloads. The header owns the decoded ICC profile.
        JxlCodestreamReader codestream = new(stream);
        JxlBitReader reader = new(codestream);
        JxlHeader header = this.ReadHeader(ref reader);

        ImageMetadata metadata = new() { IccProfile = header.IccProfile };
        metadata.SetFormatMetadata(JxlFormat.Instance, header.Metadata);

        List<ImageFrameMetadata> frames = this.IdentifyFrames(ref reader, header, cancellationToken);

        // EXIF and XMP can follow the codestream. Skip pixel payloads while retaining those bounded box ranges.
        // Container structure remains mandatory even when optional profile errors can be ignored.
        this.CompleteMetadata(codestream, header, metadata, cancellationToken);

        return new ImageInfo(header.Size, metadata, frames);
    }

    /// <summary>
    /// Completes container traversal and applies optional metadata under the existing ancillary policy.
    /// </summary>
    /// <param name="codestream">The borrowed container and codestream reader.</param>
    /// <param name="header">The image-wide orientation and profile declarations.</param>
    /// <param name="metadata">The final image metadata.</param>
    /// <param name="cancellationToken">The token checked between metadata boxes.</param>
    private void CompleteMetadata(JxlCodestreamReader codestream, JxlHeader header, ImageMetadata metadata, CancellationToken cancellationToken)
    {
        codestream.Complete();
        if (!this.Options.SkipMetadata)
        {
            foreach ((uint Type, long Offset, long Length) box in codestream.Boxes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                this.ExecuteAncillarySegmentAction(() => codestream.ApplyMetadataBox(box, metadata, this.Options.Configuration, cancellationToken));
            }

            // JPEG XL's image header defines orientation. It takes precedence over an EXIF copy of that value.
            // A default orientation alone does not require creating an otherwise absent EXIF profile.
            if (header.Orientation != ExifOrientationMode.TopLeft || metadata.ExifProfile is not null)
            {
                metadata.ExifProfile ??= new ExifProfile();
                metadata.ExifProfile.SetValue(ExifTag.Orientation, header.Orientation);
            }
        }
    }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JxlCodestreamReader codestream = new(stream);
        JxlBitReader reader = new(codestream);
        JxlHeader header = this.ReadHeader(ref reader);

        return this.DecodeFrame<TPixel>(ref reader, codestream, header, cancellationToken);
    }

    /// <summary>
    /// Parses the image header once and establishes the dimensions used for allocation-error reporting.
    /// </summary>
    /// <param name="reader">The reader at the codestream signature.</param>
    /// <returns>The retained image header.</returns>
    private JxlHeader ReadHeader(ref JxlBitReader reader)
    {
        JxlHeader header = JxlHeader.Read(ref reader, this.Options.Configuration.MemoryAllocator, this.Options.SegmentIntegrityHandling);
        this.Dimensions = header.Size;
        return header;
    }

    /// <summary>
    /// Reconstructs one supported modular frame and packs its native channels into the final image.
    /// </summary>
    /// <typeparam name="TPixel">The requested destination pixel format.</typeparam>
    /// <param name="reader">The reader following the already parsed image header.</param>
    /// <param name="codestream">The borrowed codestream and container reader.</param>
    /// <param name="header">The retained image-wide sample and color declarations.</param>
    /// <param name="cancellationToken">The token checked during reconstruction and output packing.</param>
    /// <returns>The final image. Failure disposes its complete destination allocation.</returns>
    private Image<TPixel> DecodeFrame<TPixel>(
        ref JxlBitReader reader, JxlCodestreamReader codestream, JxlHeader header, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        MemoryAllocator allocator = this.Options.Configuration.MemoryAllocator;
        if (header.PreviewSize.HasValue)
        {
            JxlFrameHeader preview = JxlFrameHeader.Read(ref reader, header, true, this.Options.SegmentIntegrityHandling);
            if (!JxlFrameSections.Skip(ref reader, allocator, preview.SectionCount, this.Options.SegmentIntegrityHandling, cancellationToken))
            {
                throw new InvalidImageContentException("The JPEG XL preview ends before the main image frame.");
            }
        }

        JxlFrameHeader frame = JxlFrameHeader.Read(ref reader, header, false, this.Options.SegmentIntegrityHandling);
        if (!frame.Modular || frame.SectionCount != 1 || !frame.IsLast || frame.Type != JxlFrameHeader.FrameType.Regular
            || frame.HasCrop || frame.Size != header.Size || frame.Upsampling != 1 || (frame.Flags & ~JxlFrameHeader.SkipAdaptiveDcSmoothing) != 0
            || frame.RestorationFilter.Gaborish || frame.RestorationFilter.Iterations != 0)
        {
            throw new NotSupportedException(
                "JPEG XL decoding currently requires a final full-canvas modular frame with one coding group, no upsampling and no filters or image features.");
        }

        // Native compact RGB must already use sRGB primaries and D65. A compact gamma changes only
        // the transfer curve, so shared companders can convert it without a matrix or another image buffer.
        // Other primaries, ICC, XYB and YCbCr require reconstruction or color conversion not implemented here.
        if (header.UsesXybTransform || frame.YCbCr || header.HasIccProfile || header.Metadata.BitDepth.ExponentBits != 0
            || (!header.Gamma.HasValue && header.TransferCharacteristics != CicpTransferCharacteristics.Iec61966_2_1)
            || header.WhitePoint != KnownIlluminants.D65Chromaticity
            || !header.Primaries.Equals(KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates))
        {
            throw new NotSupportedException("JPEG XL color transformation and floating-point reconstruction are not yet supported.");
        }

        int colorCount = header.ProfileColorType == JxlColorType.Luminance ? 1 : 3;
        int alphaChannel = -1;
        JxlBitDepth alphaDepth = header.Metadata.BitDepth;
        PixelAlphaRepresentation alphaRepresentation = PixelAlphaRepresentation.Unassociated;
        if (header.Metadata.ExtraChannels.Count > 1)
        {
            throw new NotSupportedException("JPEG XL reconstruction of multiple extra channels is not yet supported.");
        }

        if (header.Metadata.ExtraChannels.Count == 1)
        {
            JxlExtraChannelMetadata alpha = header.Metadata.ExtraChannels[0];
            if (alpha.Type != JxlExtraChannelType.Alpha || alpha.BitDepth.ExponentBits != 0 || frame.ExtraChannelUpsampling[0] != 1)
            {
                throw new NotSupportedException("JPEG XL reconstruction currently supports only full-resolution integer alpha.");
            }

            alphaChannel = colorCount;
            alphaDepth = alpha.BitDepth;
            alphaRepresentation = alpha.AlphaRepresentation;
        }

        using JxlFrameSections sections = JxlFrameSections.Read(ref reader, allocator, frame.SectionCount, cancellationToken);
        reader.BeginSection(sections.Sizes.Span[0]);
        using JxlModularDecoder modular = JxlModularDecoder.Read(ref reader, header, frame, allocator, cancellationToken);
        reader.EndSection();

        ImageMetadata metadata = new() { IccProfile = header.IccProfile };
        metadata.SetFormatMetadata(JxlFormat.Instance, header.Metadata);
        Image<TPixel> image = new(this.Options.Configuration, header.Size.Width, header.Size.Height, metadata);
        try
        {
            ImageFrame<TPixel> destination = image.Frames.RootFrame;
            JxlFrameMetadata frameMetadata = destination.Metadata.GetFormatMetadata(JxlFormat.Instance);
            if (header.TicksPerSecond is Rational rate)
            {
                frameMetadata.FrameDelay = new LongRational((long)frame.Duration * rate.Denominator, rate.Numerator).Simplify();
            }

            // Integer channels retain their native precision through prediction and inverse transforms.
            // Convert and normalize once into a reusable vector row, then let the pixel API own storage bounds and rounding.
            float colorMaximum = (1U << header.Metadata.BitDepth.BitsPerSample) - 1;
            float alphaMaximum = (1U << alphaDepth.BitsPerSample) - 1;
            Vector4 maximum = new(colorMaximum, colorMaximum, colorMaximum, alphaMaximum);
            bool convertGamma = header.Gamma.HasValue;
            double expansionGamma = 1D;
            if (header.Gamma is Rational compressionGamma)
            {
                // JPEG XL stores the compression exponent. GammaCompanding.Expand takes its reciprocal,
                // the exponent that restores linear RGB. Keep the encoded ratio until this shared API boundary.
                expansionGamma = (double)compressionGamma.Denominator / compressionGamma.Numerator;
            }

            using IMemoryOwner<Vector4> rowOwner = allocator.Allocate<Vector4>(header.Size.Width);
            Span<Vector4> vectors = rowOwner.Memory.Span;
            PixelOperations<TPixel> operations = PixelOperations<TPixel>.Instance;
            for (int y = 0; y < image.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReadOnlySpan<int> red = modular.GetChannel(0).DangerousGetRowSpan(y);
                ReadOnlySpan<int> green = colorCount == 1 ? red : modular.GetChannel(1).DangerousGetRowSpan(y);
                ReadOnlySpan<int> blue = colorCount == 1 ? red : modular.GetChannel(2).DangerousGetRowSpan(y);
                ReadOnlySpan<int> alpha = alphaChannel == -1 ? [] : modular.GetChannel(alphaChannel).DangerousGetRowSpan(y);
                SimdUtils.PackedNormalizeInterleave4(red, green, blue, alpha, MemoryMarshal.Cast<Vector4, float>(vectors), maximum);

                if (convertGamma)
                {
                    // Transfer curves act on straight RGB. The shared alpha operation handles zero alpha
                    // and finite result bounds; both companders leave alpha unchanged in the same vector row.
                    if (alphaRepresentation == PixelAlphaRepresentation.Associated)
                    {
                        PixelOperations.UnPremultiply(vectors, vectors);
                    }

                    GammaCompanding.Expand(vectors, expansionGamma);
                    SRgbCompanding.Compress(vectors);
                }

                Span<TPixel> pixels = destination.PixelBuffer.DangerousGetRowSpan(y);
                if (!convertGamma && alphaRepresentation == PixelAlphaRepresentation.Associated)
                {
                    operations.FromAssociatedScaledVector4Destructive(this.Options.Configuration, vectors, pixels);
                }
                else
                {
                    operations.FromUnassociatedScaledVector4Destructive(this.Options.Configuration, vectors, pixels);
                }
            }

            this.CompleteMetadata(codestream, header, metadata, cancellationToken);
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads displayed-frame metadata and validates section tables before skipping their payloads.
    /// </summary>
    /// <param name="reader">The bit reader positioned after the image header.</param>
    /// <param name="header">The image-wide coding and animation fields.</param>
    /// <param name="cancellationToken">The token used to cancel frame and section traversal.</param>
    /// <returns>The displayed frames, excluding previews and intermediate reference frames.</returns>
    private List<ImageFrameMetadata> IdentifyFrames(ref JxlBitReader reader, JxlHeader header, CancellationToken cancellationToken)
    {
        MemoryAllocator allocator = this.Options.Configuration.MemoryAllocator;
        List<ImageFrameMetadata> frames = [];

        // A preview has its own header and section table. It precedes the main frames but does not
        // contribute to their displayed count or consume the caller's MaxFrames allowance.
        if (header.PreviewSize.HasValue)
        {
            JxlFrameHeader preview = JxlFrameHeader.Read(ref reader, header, true, this.Options.SegmentIntegrityHandling);
            if (!JxlFrameSections.Skip(ref reader, allocator, preview.SectionCount, this.Options.SegmentIntegrityHandling, cancellationToken))
            {
                // A short preview leaves no main-frame header to read. Identify can still return the
                // image-wide fields, without adding the preview as a displayed frame.
                return frames;
            }
        }

        JxlFrameHeader frame;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            frame = JxlFrameHeader.Read(ref reader, header, false, this.Options.SegmentIntegrityHandling);
            bool complete = JxlFrameSections.Skip(ref reader, allocator, frame.SectionCount, this.Options.SegmentIntegrityHandling, cancellationToken);

            // Zero-duration intermediate frames and reference-only frames still occupy sections.
            // Parse them to reach the next frame, but report only frames that produce displayed output.
            if (frame.IsDisplayed)
            {
                JxlFrameMetadata frameMetadata = new();
                if (header.TicksPerSecond is Rational rate)
                {
                    // Seconds = native ticks * rate denominator / rate numerator. The encoded
                    // denominator is at most 1024, so widening the uint tick count to long is sufficient.
                    // Retain the 64-bit fraction in public metadata. Its reduced numerator need not fit in a uint.
                    frameMetadata.FrameDelay = new LongRational((long)frame.Duration * rate.Denominator, rate.Numerator).Simplify();
                }

                ImageFrameMetadata metadata = new();
                metadata.SetFormatMetadata(JxlFormat.Instance, frameMetadata);
                frames.Add(metadata);
            }

            if (!complete)
            {
                // The header and table are complete, so their metadata remains valid. The reader is at
                // the final logical end; attempting another frame would reinterpret missing payload as header bits.
                break;
            }
        }
        while (!frame.IsLast && (uint)frames.Count < this.Options.MaxFrames);

        return frames;
    }
}
