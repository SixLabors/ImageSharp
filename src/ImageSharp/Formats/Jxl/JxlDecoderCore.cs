// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Owns one JPEG XL read operation and applies the general decoder policies.
/// </summary>
internal sealed partial class JxlDecoderCore : ImageDecoderCore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The JPEG XL decoder options.</param>
    public JxlDecoderCore(JxlDecoderOptions options)
        : base(options.GeneralOptions)
    {
    }

    /// <summary>
    /// Reads the DC matrix factors shared by modular and VarDCT frames.
    /// </summary>
    /// <param name="reader">The reader after image features and before VarDCT quantization or the global modular tree.</param>
    /// <returns>The finite positive factors in X, Y, B component order.</returns>
    public static Vector3 ReadDcQuantization(ref JxlBitReader reader)
    {
        const float EncodedFactorScale = 1F / 128F;
        const float MinimumFactor = 1E-8F;

        // DC factors precede both coding modes. Defaults are exact powers of two; explicit half values
        // use units of 1/128. Retain those fractions until coefficient dequantization.
        if (reader.ReadBoolean())
        {
            return new Vector3(1F / 4096F, 1F / 512F, 1F / 256F);
        }

        Vector3 factors = new Vector3(reader.ReadHalf(), reader.ReadHalf(), reader.ReadHalf()) * EncodedFactorScale;
        if (factors.X < MinimumFactor || factors.Y < MinimumFactor || factors.Z < MinimumFactor)
        {
            throw new InvalidImageContentException("The JPEG XL DC dequantization factor is invalid.");
        }

        return factors;
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
    /// Reconstructs LF dependencies and packs the final frame through the shared pixel APIs.
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

        // Slots hold native float planes, not displayed pixel images. LF producers replace one slot after success;
        // their dependent frames borrow it without a copy. The whole decode call owns all four slot lifetimes.
        InlineArray4<Buffer2D<float>[]?> lowFrequencyFrames = default;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                JxlFrameHeader frame = JxlFrameHeader.Read(ref reader, header, false, this.Options.SegmentIntegrityHandling);
                bool lowFrequency = frame.Type == JxlFrameHeader.FrameType.LowFrequency;
                if ((!lowFrequency && (!frame.IsLast || frame.Type != JxlFrameHeader.FrameType.Regular))
                    || frame.HasCrop || frame.Size != header.Size
                    || (frame.Flags & ~(JxlFrameHeader.SkipAdaptiveDcSmoothing | JxlFrameHeader.UseLowFrequencyFrame | JxlFrameHeader.Splines)) != 0)
                {
                    throw new NotSupportedException(
                        "JPEG XL decoding currently requires full-canvas LF dependencies followed by a final frame with supported reconstruction flags.");
                }

                Buffer2D<float>[]? dcReference = null;
                if ((frame.Flags & JxlFrameHeader.UseLowFrequencyFrame) != 0)
                {
                    // A level n consumer reads slot n, which a preceding level n+1 producer saved.
                    // There are four LF levels. Level four cannot request a fifth level, and an absent slot is malformed input.
                    if (frame.LowFrequencyLevel == 4 || lowFrequencyFrames[frame.LowFrequencyLevel] is null)
                    {
                        throw new InvalidImageContentException("The JPEG XL frame requests a missing low-frequency dependency.");
                    }

                    dcReference = lowFrequencyFrames[frame.LowFrequencyLevel];
                }

                using JxlFrameSections sections = JxlFrameSections.Read(ref reader, allocator, frame.SectionCount, cancellationToken);
                using JxlFrame reconstructed = this.DecodeFrameData(ref reader, header, frame, sections, dcReference, cancellationToken);
                reconstructed.Reconstruct(allocator, cancellationToken);

                if (lowFrequency)
                {
                    // Both LF and displayed output complete the same native spatial operations. The LF slot takes
                    // ownership before color conversion, alpha unassociation or pixel packing can change these samples.
                    Buffer2D<float>[] planes = reconstructed.TakeReconstructedChannels();

                    // Publish only a complete replacement. A level n producer borrows slot n and replaces slot n-1.
                    // Dispose the previous contents of the replaced slot after the new planes are complete.
                    int slot = frame.LowFrequencyLevel - 1;
                    Buffer2D<float>[]? previous = lowFrequencyFrames[slot];
                    lowFrequencyFrames[slot] = planes;
                    if (previous is not null)
                    {
                        foreach (Buffer2D<float> plane in previous)
                        {
                            plane.Dispose();
                        }
                    }

                    continue;
                }

                ImageMetadata metadata = new() { IccProfile = header.IccProfile };
                metadata.SetFormatMetadata(JxlFormat.Instance, header.Metadata);

                // Initialize output so a partly written frame cannot expose data retained in pooled memory.
                Image<TPixel> image = new(this.Options.Configuration, header.Size.Width, header.Size.Height, metadata);
                try
                {
                    ImageFrame<TPixel> destination = image.Frames.RootFrame;
                    JxlFrameMetadata frameMetadata = destination.Metadata.GetFormatMetadata(JxlFormat.Instance);
                    if (header.TicksPerSecond is Rational rate)
                    {
                        frameMetadata.FrameDelay = new LongRational((long)frame.Duration * rate.Denominator, rate.Numerator).Simplify();
                    }

                    this.PackFrame(header, reconstructed, destination, cancellationToken);

                    this.CompleteMetadata(codestream, header, metadata, cancellationToken);
                    return image;
                }
                catch
                {
                    image.Dispose();
                    throw;
                }
            }
        }
        finally
        {
            for (int slot = 0; slot < 4; slot++)
            {
                if (lowFrequencyFrames[slot] is Buffer2D<float>[] planes)
                {
                    foreach (Buffer2D<float> plane in planes)
                    {
                        plane.Dispose();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Reconstructs frame sections in dependency order and completes their native component planes.
    /// </summary>
    /// <param name="reader">The reader at the frame's first section payload.</param>
    /// <param name="header">The native channel declarations.</param>
    /// <param name="frame">The coding dimensions and pass descriptions.</param>
    /// <param name="sections">The retained physical section sizes and logical ordering.</param>
    /// <param name="dcReference">The borrowed saved LF planes, or an empty span for coded DC.</param>
    /// <param name="cancellationToken">The token checked between sections and within reconstruction.</param>
    /// <returns>The owner of the decoded native components. Spatial reconstruction has not yet been applied.</returns>
    private JxlFrame DecodeFrameData(
        ref JxlBitReader reader,
        JxlHeader header,
        JxlFrameHeader frame,
        JxlFrameSections sections,
        ReadOnlySpan<Buffer2D<float>> dcReference,
        CancellationToken cancellationToken)
    {
        ReadOnlySpan<uint> sizes = sections.Sizes.Span;
        ReadOnlySpan<uint> order = sections.Order.Span;
        bool combined = sizes.Length == 1;
        using IMemoryOwner<long>? offsetsOwner = combined ? null : this.Options.Configuration.MemoryAllocator.Allocate<long>(sizes.Length);
        Span<long> offsets = offsetsOwner is null ? [] : offsetsOwner.Memory.Span;
        long position = sections.PayloadOffset;
        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] = position;
            position += sizes[i];
        }

        // Logical dependencies determine processing order. The table can put those sections in another physical order.
        // Cache prefix sums once so a permuted frame needs neither repeated table scans nor copied payloads.
        if (combined)
        {
            reader.BeginSection(sizes[0]);
        }
        else
        {
            SelectSection(ref reader, sizes, order, offsets, 0);
        }

        MemoryAllocator allocator = this.Options.Configuration.MemoryAllocator;
        JxlSplines? splines = null;
        JxlVarDctDecoder? varDct = null;
        JxlModularDecoder? modular = null;
        try
        {
            // Frame features precede DC factors in the DC-global section. Their entropy state ends
            // before VarDCT metadata and the shared modular tree, including in a combined single-group section.
            if ((frame.Flags & JxlFrameHeader.Splines) != 0)
            {
                splines = JxlSplines.Read(ref reader, frame, allocator, cancellationToken);
            }

            Vector3 dcFactors = ReadDcQuantization(ref reader);
            varDct = frame.Modular ? null : JxlVarDctDecoder.ReadGlobal(ref reader, header, frame, allocator, dcFactors, dcReference);
            modular = JxlModularDecoder.Read(ref reader, header, frame, allocator, cancellationToken);
            if (!combined)
            {
                reader.EndSection();
            }

            Size size = frame.CodingSize;
            int groupSize = frame.GroupSize;
            int dcGroupSize = groupSize * 8;
            int groupColumns = (int)(((uint)size.Width + (uint)groupSize - 1) / (uint)groupSize);
            int groupRows = (int)(((uint)size.Height + (uint)groupSize - 1) / (uint)groupSize);
            int dcGroupColumns = (int)(((uint)size.Width + (uint)dcGroupSize - 1) / (uint)dcGroupSize);
            int dcGroupRows = (int)(((uint)size.Height + (uint)dcGroupSize - 1) / (uint)dcGroupSize);

            // The section-table parser already bounds both group products through the complete section count.
            int groupCount = groupColumns * groupRows;
            int dcGroupCount = dcGroupColumns * dcGroupRows;

            // DC groups carry channels whose smaller dimension shift is at least three.
            // Keep full grid rectangles until the modular owner shifts and clips each individual channel.
            for (int group = 0; group < dcGroupCount; group++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!combined)
                {
                    SelectSection(ref reader, sizes, order, offsets, 1 + group);
                }

                Rectangle rectangle = new((group % dcGroupColumns) * dcGroupSize, (group / dcGroupColumns) * dcGroupSize, dcGroupSize, dcGroupSize);

                // Referenced DC is already reconstructed float data. It has neither a precision prefix nor a coded color DC stream.
                // Extra-channel groups and AC metadata still follow in their normal positions.
                if (dcReference.IsEmpty)
                {
                    varDct?.ReadDcGroup(ref reader, modular, group, cancellationToken);
                }

                modular.ReadGroup(ref reader, rectangle, 3, 1000, 1 + dcGroupCount + group, cancellationToken);
                varDct?.ReadAcMetadata(ref reader, modular, group, cancellationToken);
                if (!combined)
                {
                    reader.EndSection();
                }
            }

            // Smoothing reads the complete padded DC grid. It must run after every DC group and before
            // any inverse transform copies those values into its low-frequency coefficient positions.
            if (varDct is not null && (frame.Flags & (JxlFrameHeader.SkipAdaptiveDcSmoothing | JxlFrameHeader.UseLowFrequencyFrame)) == 0)
            {
                varDct.SmoothDc(allocator, cancellationToken);
            }

            // Modular-only frames have no AC-global fields, but their physical section remains bounded.
            if (!combined)
            {
                SelectSection(ref reader, sizes, order, offsets, 1 + dcGroupCount);
            }

            if (varDct is not null)
            {
                varDct.ReadGlobalAc(ref reader, frame, modular, allocator, cancellationToken);
                varDct.PrepareReconstruction(allocator, header, frame);
            }

            if (!combined)
            {
                reader.EndSection();
            }

            InlineArray16<int> minimumShifts = default;
            InlineArray16<int> maximumShifts = default;
            int maximumShift = 2;
            int minimumShift = 3;
            ReadOnlySpan<byte> downsampling = frame.Downsampling;
            ReadOnlySpan<byte> lastPass = frame.LastDownsamplingPass;
            for (int pass = 0; pass < frame.PassCount; pass++)
            {
                for (int i = 0; i < downsampling.Length; i++)
                {
                    if (lastPass[i] == pass)
                    {
                        minimumShift = BitOperations.Log2(downsampling[i]);
                    }
                }

                if (pass == frame.PassCount - 1)
                {
                    minimumShift = 0;
                }

                minimumShifts[pass] = minimumShift;
                maximumShifts[pass] = maximumShift;
                maximumShift = minimumShift - 1;
            }

            // Accumulate every pass for one group before reusing its coefficient buffer. The final pass
            // reconstructs each block immediately. Section seeking handles physical order without full-image coefficients.
            // Modular groups use the same pass-specific resolution ranges and complete their global transforms below.
            for (int group = 0; group < groupCount; group++)
            {
                for (int pass = 0; pass < frame.PassCount; pass++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!combined)
                    {
                        SelectSection(ref reader, sizes, order, offsets, 2 + dcGroupCount + (pass * groupCount) + group);
                    }

                    // Tree property one includes reserved DC, AC-metadata and quantization-table stream IDs.
                    // These IDs identify entropy contexts; they are different from physical section numbers.
                    int streamId = 1 + (3 * dcGroupCount) + JxlConstants.QuantizationTableCount + (pass * groupCount) + group;
                    Rectangle rectangle = new((group % groupColumns) * groupSize, (group / groupColumns) * groupSize, groupSize, groupSize);
                    varDct?.ReadAcGroup(ref reader, group, pass, cancellationToken);
                    modular.ReadGroup(ref reader, rectangle, minimumShifts[pass], maximumShifts[pass], streamId, cancellationToken);
                    if (!combined)
                    {
                        reader.EndSection();
                    }
                }
            }

            if (combined)
            {
                reader.EndSection();
            }

            modular.Complete(cancellationToken);

            // The last logical section need not end at the frame's physical end. Restore it before any following frame or metadata.
            reader.SeekBytePosition(sections.PayloadOffset + sections.TotalLength);

            // The constructor transfers ready owners only after it succeeds. Until then this catch retains
            // all parsed resources, including a failed final validation or allocation of the runtime frame object.
            return new JxlFrame(header, frame, modular, varDct, dcFactors, splines);
        }
        catch
        {
            modular?.Dispose();
            varDct?.Dispose();
            splines?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Selects one bounded payload from a logical section index without copying its bytes.
    /// </summary>
    /// <param name="reader">The frame bit reader.</param>
    /// <param name="sizes">The physical payload lengths.</param>
    /// <param name="order">The logical-to-physical permutation, or empty for identity order.</param>
    /// <param name="offsets">The precomputed physical payload starts in logical codestream byte units.</param>
    /// <param name="logicalIndex">The section required by the current decoding stage.</param>
    private static void SelectSection(
        ref JxlBitReader reader, ReadOnlySpan<uint> sizes, ReadOnlySpan<uint> order, ReadOnlySpan<long> offsets, int logicalIndex)
    {
        int physicalIndex = order.IsEmpty ? logicalIndex : (int)order[logicalIndex];
        reader.SeekBytePosition(offsets[physicalIndex]);
        reader.BeginSection(sizes[physicalIndex]);
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
