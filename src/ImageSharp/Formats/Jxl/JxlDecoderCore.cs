// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

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
        JxlHeader header = JxlHeader.Read(ref reader, this.Options.Configuration.MemoryAllocator, this.Options.SegmentIntegrityHandling);
        this.Dimensions = header.Size;

        ImageMetadata metadata = new() { IccProfile = header.IccProfile };
        metadata.SetFormatMetadata(JxlFormat.Instance, header.Metadata);

        List<ImageFrameMetadata> frames = this.IdentifyFrames(ref reader, header, cancellationToken);

        // EXIF and XMP can follow the codestream. Skip pixel payloads while retaining those bounded box ranges.
        // Container structure remains mandatory even when optional profile errors can be ignored.
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

        return new ImageInfo(header.Size, metadata, frames);
    }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        // Pixel reconstruction belongs to this core. Never return an empty image as decoded content.
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("JPEG XL pixel decoding is not yet supported.");
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
