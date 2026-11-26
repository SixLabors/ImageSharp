// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Owns one JPEG XL read operation and applies the general decoder policies.
/// </summary>
internal sealed class JxlDecoderCore : ImageDecoderCore
{
    /// <summary>
    /// Limits identification to image-wide fields when selecting the destination pixel format.
    /// </summary>
    private readonly bool colorMetadataOnly;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The JPEG XL decoder options.</param>
    public JxlDecoderCore(JxlDecoderOptions options)
        : this(options, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlDecoderCore"/> class with an identification scope.
    /// </summary>
    /// <param name="options">The JPEG XL decoder options.</param>
    /// <param name="colorMetadataOnly">Whether identification stops after the image header used for pixel selection.</param>
    public JxlDecoderCore(JxlDecoderOptions options, bool colorMetadataOnly)
        : base(options.GeneralOptions)
    {
        this.colorMetadataOnly = colorMetadataOnly;
    }

    /// <summary>
    /// Reads the DC matrix factors shared by modular and VarDCT frames.
    /// </summary>
    /// <param name="reader">The reader after image features and before VarDCT quantization or the global modular tree.</param>
    /// <returns>The finite positive factors in X, Y, B component order.</returns>
    public static Vector3 ReadDcQuantization(ref JxlBitReader reader)
    {
        // Explicit half values store DC factors in units of 1/128. Factors below 1E-8 after this normalization are invalid.
        // This limit is a format validity bound, not a rounding tolerance.
        const float encodedFactorScale = 1F / 128F;
        const float minimumFactor = 1E-8F;

        // DC factors precede both coding modes. Defaults are exact powers of two; explicit half values
        // use units of 1/128. Retain those fractions until coefficient dequantization.
        if (reader.ReadBoolean())
        {
            return JxlQuantization.DefaultDcFactors;
        }

        Vector3 factors = new Vector3(reader.ReadHalf(), reader.ReadHalf(), reader.ReadHalf()) * encodedFactorScale;
        if (factors.X < minimumFactor || factors.Y < minimumFactor || factors.Z < minimumFactor)
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
        JxlCodestreamReader codestream = new(stream, this.Options.SegmentIntegrityHandling);
        JxlBitReader reader = new(codestream);
        JxlHeader header = this.ReadHeader(ref reader, cancellationToken);

        ImageMetadata metadata = new()
        {
            IccProfile = header.IccProfile,
            CicpProfile = this.Options.SkipMetadata ? null : header.GetCicpProfile()
        };

        metadata.SetFormatMetadata(JxlFormat.Instance, header.Metadata);

        if (this.colorMetadataOnly)
        {
            // Pixel selection needs no frame headers or trailing boxes. The typed decoder reads them under
            // the caller's integrity policy and owns the output profiles, orientation and configuration.
            return new ImageInfo(header.Size, metadata);
        }

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
            // The container traversal limits each box range to the file. ApplyMetadataBox checks each payload length before it reads. It also reports damaged
            // payloads with the ancillary policy.
            foreach ((uint Type, long Offset, long Length) box in codestream.Boxes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                codestream.ApplyMetadataBox(box, metadata, this.Options.Configuration, cancellationToken);
            }

            // JPEG XL's image header defines orientation. It takes precedence over an EXIF copy of that value.
            // A default orientation alone does not require creating an otherwise absent EXIF profile.
            if (header.Orientation != ExifOrientationMode.TopLeft || metadata.ExifProfile is not null)
            {
                ExifProfile exifProfile = metadata.ExifProfile ?? new ExifProfile();
                exifProfile.SetValue(ExifTag.Orientation, header.Orientation);
                metadata.ExifProfile = exifProfile;
            }
        }
    }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JxlCodestreamReader codestream = new(stream, this.Options.SegmentIntegrityHandling);
        JxlBitReader reader = new(codestream);
        JxlHeader header = this.ReadHeader(ref reader, cancellationToken);

        return this.DecodeFrames<TPixel>(ref reader, codestream, header, cancellationToken);
    }

    /// <summary>
    /// Parses the image header once and establishes the dimensions used for allocation-error reporting.
    /// </summary>
    /// <param name="reader">The reader at the codestream signature.</param>
    /// <param name="cancellationToken">The token checked during ICC reconstruction.</param>
    /// <returns>The retained image header.</returns>
    private JxlHeader ReadHeader(ref JxlBitReader reader, CancellationToken cancellationToken)
    {
        JxlHeader header = JxlHeader.Read(ref reader, this.Options.Configuration.MemoryAllocator, this.Options.SegmentIntegrityHandling, cancellationToken);
        this.Dimensions = header.Size;
        return header;
    }

    /// <summary>
    /// Creates the cleared output image and its image-wide metadata.
    /// </summary>
    /// <typeparam name="TPixel">The requested destination pixel format.</typeparam>
    /// <param name="header">The image-wide size, profile and format declarations.</param>
    /// <returns>The image with one cleared root frame.</returns>
    private Image<TPixel> CreateImage<TPixel>(JxlHeader header)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Final color conversion produces working sRGB. Keep the original compact
        // description in JXL metadata without applying its label to converted pixels.
        ImageMetadata metadata = new()
        {
            IccProfile = header.IccProfile,
            CicpProfile = this.Options.SkipMetadata ? null : new(1, 13, 0, true)
        };

        metadata.SetFormatMetadata(JxlFormat.Instance, header.Metadata);

        // Sequential output writes completed rows during AC traversal. Initialize the destination
        // first so malformed or cancelled input cannot expose samples retained in pooled memory.
        // Image-data recovery also returns this cleared value for each pixel that it cannot decode.
        return new Image<TPixel>(this.Options.Configuration, header.Size.Width, header.Size.Height, metadata);
    }

    /// <summary>
    /// Reconstructs native dependencies and packs each displayed canvas through the shared pixel APIs.
    /// </summary>
    /// <typeparam name="TPixel">The requested destination pixel format.</typeparam>
    /// <param name="reader">The reader following the already parsed image header.</param>
    /// <param name="codestream">The borrowed codestream and container reader.</param>
    /// <param name="header">The retained image-wide sample and color declarations.</param>
    /// <param name="cancellationToken">The token checked during reconstruction and output packing.</param>
    /// <returns>The displayed frames, limited by MaxFrames. Failure disposes every destination frame.</returns>
    private Image<TPixel> DecodeFrames<TPixel>(
        ref JxlBitReader reader, JxlCodestreamReader codestream, JxlHeader header, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        MemoryAllocator allocator = this.Options.Configuration.MemoryAllocator;

        // The ICC profile belongs to the image. All displayed frames share its transform and black point.
        ColorProfileConverter? profileConverter = null;

        if (!header.UsesXybTransform && IccProfile.TryGetIccProfileForColorConversion(header.IccProfile, out IccProfile? sourceProfile))
        {
            // Libjxl's skcms backend applies ICC curves to component magnitudes and restores the input signs.
            profileConverter = new ColorProfileConverter(new ColorConversionOptions
            {
                MemoryAllocator = allocator,
                SourceIccProfile = sourceProfile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
                UseSymmetricIccCurves = true,
                UseBlackPointCompensation = true
            });
        }

        // LF dependencies and ordinary references use separate four-slot tables. Each slot owns
        // completed native planes, without retaining a frame's entropy or reconstruction workspace.
        // Displayed TPixel frames have separate storage and remain attached to one image throughout decoding.
        InlineArray4<Buffer2D<float>[]?> lowFrequencyFrames = default;
        InlineArray4<JxlReferenceFrame?> referenceFrames = default;
        Image<TPixel>? image = null;
        uint visibleFrame = 0;
        uint nonvisibleFrame = 0;
        SegmentIntegrityHandling handling = this.Options.SegmentIntegrityHandling;
        try
        {
            // A truncated preview leaves no main frame data. The IgnoreImageData mode then skips the loop.
            bool decodeFrames = !header.PreviewSize.HasValue || this.SkipPreview(ref reader, header, cancellationToken);
            while (decodeFrames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Before the first displayed frame exists, a damaged frame header or section table is a structural error, and the decoder throws. After that
                // frame, the damage is image data. Each check reports it once. A false or null result means that the IgnoreImageData mode keeps the frames that
                // the decoder decoded before the damage.
                bool imageExists = image is not null;
                JxlFrameHeader? frame = JxlFrameHeader.Read(ref reader, header, false, handling, imageExists);
                if (frame is null || !this.ValidateFrame(frame, lowFrequencyFrames, referenceFrames, header.Size, imageExists))
                {
                    break;
                }

                bool lowFrequency = frame.Type == JxlFrameHeader.FrameType.LowFrequency;
                bool originalComposition = frame.NeedsBlending;

                // MaxFrames counts displayed canvases, not previews, LF dependencies or hidden references.
                // The final requested canvas still reads earlier references, but no later consumer needs
                // a copy of its native samples. Avoid reserving a pre-transform snapshot for that case.
                bool lastRequestedFrame = frame.IsDisplayed && (uint)(image?.Frames.Count ?? 0) + 1 >= this.Options.MaxFrames;
                bool saveReference = frame.CanBeReferenced && !lastRequestedFrame;

                // A level n consumer reads slot n, which a preceding level n+1 producer saved. ValidateFrame checks the slot.
                Buffer2D<float>[]? dcReference = (frame.Flags & JxlFrameHeader.UseLowFrequencyFrame) != 0 ? lowFrequencyFrames[frame.LowFrequencyLevel] : null;

                // The first displayed frame uses visible seed one. Hidden frames advance the other counter;
                // a displayed frame resets it. Native noise generation consumes the low 32 bits of each counter.
                if (frame.IsDisplayed)
                {
                    visibleFrame++;
                    nonvisibleFrame = 0;
                }
                else
                {
                    nonvisibleFrame++;
                }

                using JxlFrameSections? sections = JxlFrameSections.Read(ref reader, allocator, frame.SectionCount, handling, imageExists, cancellationToken);
                if (sections is null)
                {
                    break;
                }

                using JxlFrame? reconstructed = this.DecodeFrameData(
                    ref reader,
                    codestream.Length,
                    header,
                    frame,
                    sections,
                    dcReference,
                    referenceFrames,
                    ref image,
                    profileConverter,
                    out ImageFrame<TPixel>? destination,
                    cancellationToken);

                if (reconstructed is null)
                {
                    break;
                }

                bool pixelsWritten = reconstructed.VarDct is JxlVarDctDecoder decodedVarDct && decodedVarDct.UsesGroupRows;
                if (!pixelsWritten)
                {
                    reconstructed.Reconstruct(allocator, visibleFrame, nonvisibleFrame, cancellationToken);
                }

                if (destination is not null && image is not null)
                {
                    JxlReferenceFrame? preservedReference = null;
                    try
                    {
                        if (saveReference && frame.SaveBeforeColorTransform && originalComposition)
                        {
                            // This frame saves every color and extra channel before the coding transform.
                            // The color replacement can still blend extras, so composition must not overwrite
                            // those earlier samples. Only this case needs separate native reference storage.
                            preservedReference = reconstructed.CopyReferenceFrame(this.Options.Configuration, cancellationToken);
                        }

                        if (!pixelsWritten)
                        {
                            // Post-transform reference frames reach the original encoding before spot rendering,
                            // even when MaxFrames omits their later consumers. Retain that conversion order;
                            // only the unused reference allocation and publication can be omitted.
                            if (originalComposition || (frame.CanBeReferenced && !frame.SaveBeforeColorTransform))
                            {
                                reconstructed.TransformToOriginalColor(allocator, cancellationToken);
                                if (originalComposition)
                                {
                                    reconstructed.Compose(referenceFrames, header.Size, allocator, cancellationToken);
                                }
                            }

                            using JxlPixelConverter<TPixel> pixelConverter = new(
                                this.Options.Configuration, header, reconstructed, destination, profileConverter);

                            pixelConverter.Convert(cancellationToken);
                        }

                        if (saveReference)
                        {
                            // Pixel conversion and spot mixing borrow rows without changing the native planes.
                            // Transfer those owners after output, unless composition required an earlier snapshot.
                            // Publish only now: this frame can read the previous contents of its own reference slot.
                            JxlReferenceFrame saved = preservedReference ?? reconstructed.TakeReferenceFrame(frame.SaveBeforeColorTransform);
                            int slot = frame.ReferenceSlot;
                            JxlReferenceFrame? previous = referenceFrames[slot];
                            referenceFrames[slot] = saved;
                            preservedReference = null;
                            previous?.Dispose();
                        }
                    }
                    finally
                    {
                        // A snapshot remains local until both display conversion and reference publication succeed.
                        preservedReference?.Dispose();
                    }

                    if (frame.IsLast || lastRequestedFrame)
                    {
                        break;
                    }
                }
                else if (lowFrequency)
                {
                    // LF storage retains native float samples. Transfer completed planes before any output
                    // color conversion, and release the preceding contents of the same reference slot.
                    Buffer2D<float>[] planes = reconstructed.TakeReconstructedChannels();
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
                }
                else
                {
                    if (!frame.SaveBeforeColorTransform)
                    {
                        // Hidden post-transform references retain the original encoded colors after composition.
                        // Pre-transform references instead retain the untouched coding colors and extras for patches.
                        reconstructed.TransformToOriginalColor(allocator, cancellationToken);
                        if (originalComposition)
                        {
                            reconstructed.Compose(referenceFrames, header.Size, allocator, cancellationToken);
                        }
                    }

                    // Every consumer has finished reading the preceding slot. Transfer the
                    // completed planes before releasing those preceding reference owners.
                    JxlReferenceFrame saved = reconstructed.TakeReferenceFrame(frame.SaveBeforeColorTransform);
                    int slot = frame.ReferenceSlot;
                    JxlReferenceFrame? previous = referenceFrames[slot];
                    referenceFrames[slot] = saved;
                    previous?.Dispose();
                }
            }

            // The loop ends at the last displayed frame, at MaxFrames, or at the one report of damaged image data that the IgnoreImageData mode ignored. That
            // mode keeps the frames and the streamed rows that the decoder decoded before the damage. Frame constructors clear their pixels, so each pixel that
            // the decoder did not read keeps that value. Damage before the first displayed frame leaves no canvas, so this code returns a cleared canvas.
            image ??= this.CreateImage<TPixel>(header);

            // The container traversal skips the frames that MaxFrames omits, and it still reads the later metadata boxes. A physical pixel EOF does not cancel
            // the container structure rules or the metadata policies.
            this.CompleteMetadata(codestream, header, image.Metadata, cancellationToken);
            return image;
        }
        catch
        {
            image?.Dispose();
            throw;
        }
        finally
        {
            for (int slot = 0; slot < 4; slot++)
            {
                referenceFrames[slot]?.Dispose();

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
    /// Reconstructs the frame sections in dependency order and completes their native component planes.
    /// </summary>
    /// <param name="reader">The reader at the first section payload of the frame.</param>
    /// <param name="codestreamLength">The number of logical codestream bytes that the input holds.</param>
    /// <param name="header">The native channel declarations.</param>
    /// <param name="frame">The coding dimensions and the pass descriptions.</param>
    /// <param name="sections">The retained physical section sizes and the logical order.</param>
    /// <param name="dcReference">The borrowed saved LF planes, or an empty span for coded DC.</param>
    /// <param name="references">The borrowed ordinary slots. Patches read their completed pre-transform planes.</param>
    /// <param name="image">
    /// The output image, or null before the first displayed frame. A displayed frame creates the image or appends a frame after its global sections, so a frame
    /// that ends before them adds no frame.
    /// </param>
    /// <param name="profileConverter">The shared ICC converter, or null for direct color conversion.</param>
    /// <param name="destination">The initialized displayed frame, or null for a hidden frame or an incomplete frame.</param>
    /// <param name="cancellationToken">The token that the method checks between sections and in the reconstruction.</param>
    /// <returns>
    /// The native component owner, or null if the IgnoreImageData mode ignores an incomplete section. The group-row output already wrote the destination
    /// pixels. The retained planes still need spatial reconstruction.
    /// </returns>
    private JxlFrame? DecodeFrameData<TPixel>(
        ref JxlBitReader reader,
        long codestreamLength,
        JxlHeader header,
        JxlFrameHeader frame,
        JxlFrameSections sections,
        ReadOnlySpan<Buffer2D<float>> dcReference,
        ReadOnlySpan<JxlReferenceFrame?> references,
        ref Image<TPixel>? image,
        ColorProfileConverter? profileConverter,
        out ImageFrame<TPixel>? destination,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        destination = null;
        ReadOnlySpan<uint> sizes = sections.Sizes.Span;
        ReadOnlySpan<uint> order = sections.Order.Span;
        bool combined = sizes.Length == 1;

        // The logical dependencies set the process order. The table can put those sections in another physical order. The code caches the prefix sums once, so
        // a permuted frame needs no repeated table scans and no copied payloads. A combined section starts at the payload offset and needs no rented storage.
        long payloadOffset = sections.PayloadOffset;
        using IMemoryOwner<long>? offsetsOwner = combined ? null : this.Options.Configuration.MemoryAllocator.Allocate<long>(sizes.Length);
        Span<long> offsets = offsetsOwner is null ? new Span<long>(ref payloadOffset) : offsetsOwner.Memory.Span;
        long position = payloadOffset;
        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] = position;
            position += sizes[i];
        }

        if (!this.TrySelectSection(ref reader, sizes, order, offsets, 0, codestreamLength))
        {
            return null;
        }

        MemoryAllocator allocator = this.Options.Configuration.MemoryAllocator;
        JxlPatches? patches = null;
        JxlSplines? splines = null;
        JxlVarDctDecoder? varDct = null;
        JxlModularDecoder? modular = null;
        JxlFrame? reconstructed = null;
        JxlFrame? result = null;
        try
        {
            // Frame features precede DC factors in the DC-global section. Their entropy state ends
            // before VarDCT metadata and the shared modular tree, including in a combined single-group section.
            if ((frame.Flags & JxlFrameHeader.Patches) != 0)
            {
                int physicalSection = order.IsEmpty ? 0 : (int)order[0];
                patches = JxlPatches.Read(
                    ref reader, frame, header.Metadata.ExtraChannels.Count, references, sizes[physicalSection], allocator, cancellationToken);

                if (!patches.HasAny)
                {
                    patches.Dispose();
                    patches = null;
                }
            }

            if ((frame.Flags & JxlFrameHeader.Splines) != 0)
            {
                splines = JxlSplines.Read(ref reader, frame, allocator, cancellationToken);
            }

            JxlNoise noise = (frame.Flags & JxlFrameHeader.Noise) != 0 ? JxlNoise.Read(ref reader) : default;
            Vector3 dcFactors = ReadDcQuantization(ref reader);
            varDct = frame.Modular ? null : JxlVarDctDecoder.ReadGlobal(ref reader, header, frame, allocator, dcFactors, dcReference);
            modular = JxlModularDecoder.Read(ref reader, header, frame, allocator, cancellationToken);

            // Global modular inverses can make alpha and black unavailable until all groups finish.
            // Saved native frames and the existing spatial-feature passes also consume retained planes.
            // Ordinary final color can instead be restored and packed as each group row becomes complete.
            bool useGroupRows = frame.IsDisplayed && varDct is not null
                && frame.Type == JxlFrameHeader.FrameType.Regular && !frame.CanBeReferenced && !frame.NeedsBlending
                && header.Metadata.ExtraChannels.Count == 0 && frame.Upsampling == 1 && patches is null && splines is null && !noise.HasAny;
            for (int component = 0; component < frame.HorizontalSamplingShifts.Length; component++)
            {
                useGroupRows &= frame.HorizontalSamplingShifts[component] == 0 && frame.VerticalSamplingShifts[component] == 0;
            }

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
                if (!combined && !this.TrySelectSection(ref reader, sizes, order, offsets, 1 + group, codestreamLength))
                {
                    return null;
                }

                Rectangle rectangle = new((group % dcGroupColumns) * dcGroupSize, (group / dcGroupColumns) * dcGroupSize, dcGroupSize, dcGroupSize);

                // Referenced DC is already reconstructed float data. It has neither a precision prefix nor a coded color DC stream.
                // Extra-channel groups and AC metadata still follow in their normal positions.
                if (dcReference.IsEmpty)
                {
                    varDct?.ReadDcGroup(ref reader, modular, group, cancellationToken);
                }

                modular.ReadGroup(ref reader, rectangle, 3, 1000, 1 + dcGroupCount + group, cancellationToken);
                varDct?.ReadAcMetadata(ref reader, modular, group, this.Options.SegmentIntegrityHandling, image is not null, cancellationToken);
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
            if (!combined && !this.TrySelectSection(ref reader, sizes, order, offsets, 1 + dcGroupCount, codestreamLength))
            {
                return null;
            }

            if (varDct is not null)
            {
                varDct.ReadGlobalAc(ref reader, frame, modular, allocator, cancellationToken);
                varDct.PrepareReconstruction(allocator, header, frame, useGroupRows);
            }

            if (!combined)
            {
                reader.EndSection();
            }

            if (frame.IsDisplayed)
            {
                // This decoder reads DC-global, the DC groups and AC-global before it allocates the output.
                // Thus each global check runs before the image exists and stays structural in each mode.
                if (image is null)
                {
                    image = this.CreateImage<TPixel>(header);
                    destination = image.Frames.RootFrame;
                }
                else
                {
                    // Native composition makes a complete canvas, so a new initialized frame needs no copy of the previous TPixel frame. The reference
                    // backgrounds stay in native planes.
                    destination = image.Frames.CreateFrame();
                }

                JxlFrameMetadata frameMetadata = destination.Metadata.GetFormatMetadata(JxlFormat.Instance);
                if (header.TicksPerSecond is Rational rate)
                {
                    // Seconds = duration ticks * timebase denominator / timebase numerator.
                    // The code widens the values before the multiplication and keeps the exact fraction for each displayed frame.
                    frameMetadata.FrameDelay = new LongRational((long)frame.Duration * rate.Denominator, rate.Numerator).Simplify();
                }
            }

            // The frame takes ownership before traversal so both streamed and retained output share failure
            // cleanup. No other owner disposes its component planes after this construction succeeds.
            reconstructed = new JxlFrame(header, frame, modular, varDct, dcFactors, patches, splines, noise);
            using JxlPixelConverter<TPixel>? pixelConverter = varDct is not null && varDct.UsesGroupRows && destination is not null
                ? new JxlPixelConverter<TPixel>(this.Options.Configuration, header, reconstructed, destination, profileConverter)
                : null;

            Span<Vector4> pixelRow = pixelConverter is null ? [] : pixelConverter.PixelRow;
            bool restoreRows = pixelConverter is not null && (frame.RestorationFilter.Gaborish || frame.RestorationFilter.Iterations != 0);
            JxlRestoration.RowProcessor<JxlVarDctDecoder.VarDctSigmaSource> restoration = default;
            if (restoreRows && varDct is not null)
            {
                restoration = varDct.CreateRestorationRows(frame);
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
                if (pixelConverter is not null && varDct is not null && group % groupColumns == 0)
                {
                    varDct.BeginGroupRow((group / groupColumns) * groupSize);
                }

                for (int pass = 0; pass < frame.PassCount; pass++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int section = 2 + dcGroupCount + (pass * groupCount) + group;
                    if (!combined && !this.TrySelectSection(ref reader, sizes, order, offsets, section, codestreamLength))
                    {
                        return null;
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

                if (pixelConverter is not null && varDct is not null && ((group + 1) % groupColumns == 0 || group == groupCount - 1))
                {
                    // Every horizontal group in this strip now has all progressive passes. Filters retain
                    // their pending neighboring rows before the next group row reuses the same native storage.
                    int top = (group / groupColumns) * groupSize;
                    int bottom = Math.Min(top + groupSize, size.Height);
                    int componentWidth = varDct.GroupRowWidth;
                    for (int y = top; y < bottom; y++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ReadOnlySpan<float> row = varDct.GetGroupRow(y);
                        ReadOnlySpan<float> rowX = row[..componentWidth];
                        ReadOnlySpan<float> rowY = row.Slice(componentWidth, componentWidth);
                        ReadOnlySpan<float> rowB = row.Slice(2 * componentWidth, componentWidth);
                        if (restoreRows)
                        {
                            restoration.AppendRows(rowX, rowY, rowB);
                            while (restoration.TryReadRow(out int outputY, out ReadOnlySpan<float> x, out ReadOnlySpan<float> luma, out ReadOnlySpan<float> b))
                            {
                                pixelConverter.ConvertRow(outputY, x, luma, b, pixelRow);
                            }
                        }
                        else
                        {
                            pixelConverter.ConvertRow(y, rowX[..size.Width], rowY[..size.Width], rowB[..size.Width], pixelRow);
                        }
                    }
                }
            }

            if (restoreRows && pixelConverter is not null)
            {
                // Final image reflection supplies the remaining filter neighborhoods. It does not create
                // a strip boundary: global row coordinates remain unchanged through every restoration pass.
                restoration.Complete();
                while (restoration.TryReadRow(out int outputY, out ReadOnlySpan<float> x, out ReadOnlySpan<float> luma, out ReadOnlySpan<float> b))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pixelConverter.ConvertRow(outputY, x, luma, b, pixelRow);
                }
            }

            if (combined)
            {
                reader.EndSection();
            }

            modular.Complete(cancellationToken);

            // The last logical section need not end at the frame's physical end. Restore it before any following frame or metadata.
            reader.SeekBytePosition(sections.PayloadOffset + sections.TotalLength);

            result = reconstructed;
            return result;
        }
        finally
        {
            // An exception or an ignored incomplete section returns no frame. Thus this code releases each owner.
            if (result is null)
            {
                if (reconstructed is not null)
                {
                    reconstructed.Dispose();
                }
                else
                {
                    modular?.Dispose();
                    varDct?.Dispose();
                    patches?.Dispose();
                    splines?.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Checks the frame flags and the frame dependencies once, before the section table and the payloads.
    /// </summary>
    /// <param name="frame">The parsed frame header.</param>
    /// <param name="lowFrequencyFrames">The saved LF planes for each LF level.</param>
    /// <param name="references">The ordinary reference slots before this frame.</param>
    /// <param name="canvasSize">The image canvas dimensions.</param>
    /// <param name="imageExists">Whether the decoder created the image before this frame. Before that, each error throws.</param>
    /// <returns>Whether the frame is valid. False means that the IgnoreImageData mode ignored the error.</returns>
    private bool ValidateFrame(
        JxlFrameHeader frame,
        ReadOnlySpan<Buffer2D<float>[]?> lowFrequencyFrames,
        ReadOnlySpan<JxlReferenceFrame?> references,
        Size canvasSize,
        bool imageExists)
    {
        SegmentIntegrityHandling handling = this.Options.SegmentIntegrityHandling;
        if ((frame.Flags & ~(JxlFrameHeader.SkipAdaptiveDcSmoothing | JxlFrameHeader.UseLowFrequencyFrame
            | JxlFrameHeader.Patches | JxlFrameHeader.Splines | JxlFrameHeader.Noise)) != 0)
        {
            // An unsupported feature is not damage, so it throws in each mode.
            throw new NotSupportedException("The JPEG XL frame uses unsupported reconstruction flags.");
        }

        // A level n consumer reads slot n, which a preceding level n+1 producer saved. There are four LF levels. Level four cannot request a fifth level. An
        // absent slot is malformed input.
        if ((frame.Flags & JxlFrameHeader.UseLowFrequencyFrame) != 0 && (frame.LowFrequencyLevel == 4 || lowFrequencyFrames[frame.LowFrequencyLevel] is null))
        {
            handling.ThrowOrIgnoreImageDataError(imageExists, "The JPEG XL frame requests a missing low-frequency dependency.");
            return false;
        }

        // Composition runs for each displayed frame that needs a background, and for each hidden frame that saves its samples after the color transform. It
        // reads the reference slots as they are before this frame.
        if (frame.NeedsBlending && (frame.IsDisplayed || !frame.SaveBeforeColorTransform))
        {
            uint usedSources = 1U << frame.Blending.Source;
            foreach (JxlFrameHeader.BlendingInfo extra in frame.ExtraChannelBlending)
            {
                usedSources |= 1U << extra.Source;
            }

            // An absent slot is an all-zero background. Each populated source must cover the canvas at its origin. Only the color source must obey the coding
            // color transform. That transform does not change the extra samples, so they can also come from a pre-transform reference.
            for (int slot = 0; slot < references.Length; slot++)
            {
                JxlReferenceFrame? reference = references[slot];
                if ((usedSources & (1U << slot)) != 0 && reference is not null
                    && ((slot == frame.Blending.Source && reference.SavedBeforeColorTransform) || reference.Origin != Point.Empty
                        || reference.Size.Width < canvasSize.Width || reference.Size.Height < canvasSize.Height))
                {
                    handling.ThrowOrIgnoreImageDataError(imageExists, "The JPEG XL composition background is not a complete original-color canvas.");
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Selects one complete payload from a logical section index. The method does not copy the payload bytes.
    /// </summary>
    /// <param name="reader">The frame bit reader.</param>
    /// <param name="sizes">The physical payload lengths.</param>
    /// <param name="order">The logical-to-physical permutation, or empty for identity order.</param>
    /// <param name="offsets">The precomputed physical payload starts in logical codestream byte units.</param>
    /// <param name="logicalIndex">The section that the current decode stage needs.</param>
    /// <param name="codestreamLength">The number of logical codestream bytes that the input holds.</param>
    /// <returns>Whether the section is complete. False means that the IgnoreImageData mode ignored the truncation.</returns>
    private bool TrySelectSection(
        ref JxlBitReader reader, ReadOnlySpan<uint> sizes, ReadOnlySpan<uint> order, ReadOnlySpan<long> offsets, int logicalIndex, long codestreamLength)
    {
        int physicalIndex = order.IsEmpty ? logicalIndex : (int)order[logicalIndex];
        long offset = offsets[physicalIndex];
        if (offset + sizes[physicalIndex] > codestreamLength)
        {
            // The frame decoder receives only complete sections. The first incomplete section is the one report of truncated image data.
            // The IgnoreImageData mode keeps the frames and the streamed rows that the decoder decoded before it.
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("The JPEG XL section payload is truncated.");
            return false;
        }

        reader.SeekBytePosition(offset);
        reader.BeginSection(sizes[physicalIndex]);
        return true;
    }

    /// <summary>
    /// Reads the preview frame header and skips its section payloads. The decoder does not decode the preview.
    /// </summary>
    /// <param name="reader">The bit reader, at the position after the image header.</param>
    /// <param name="header">The image-wide header that declares the preview.</param>
    /// <param name="cancellationToken">The token that cancels the section traversal.</param>
    /// <returns>Whether the preview payloads are complete. False means that the IgnoreImageData mode ignored the truncation.</returns>
    private bool SkipPreview(ref JxlBitReader reader, JxlHeader header, CancellationToken cancellationToken)
    {
        // The preview comes before each main frame, so no image exists. Thus each header and table error throws, and Read returns a frame. Only a short preview
        // payload uses the image data policy.
        SegmentIntegrityHandling handling = this.Options.SegmentIntegrityHandling;
        JxlFrameHeader preview = JxlFrameHeader.Read(ref reader, header, true, handling, false)!;
        return JxlFrameSections.Skip(ref reader, this.Options.Configuration.MemoryAllocator, preview.SectionCount, handling, false, cancellationToken);
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
        SegmentIntegrityHandling handling = this.Options.SegmentIntegrityHandling;
        List<ImageFrameMetadata> frames = [];

        // A preview has its own header and section table. It comes before the main frames. It does not add to the displayed frame count, and it does not use
        // the MaxFrames allowance of the caller. If the preview is short, no main-frame header remains to read. Identify can still return the image-wide
        // fields, but it does not add the preview as a displayed frame.
        if (header.PreviewSize.HasValue && !this.SkipPreview(ref reader, header, cancellationToken))
        {
            return frames;
        }

        JxlFrameHeader? frame;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            // As in Decode, a damaged header or table after the first displayed frame is image data. A null result means that the IgnoreImageData mode keeps
            // the frames that Identify found before the damage.
            bool imageExists = frames.Count != 0;
            frame = JxlFrameHeader.Read(ref reader, header, false, handling, imageExists);
            if (frame is null)
            {
                break;
            }

            bool complete = JxlFrameSections.Skip(ref reader, allocator, frame.SectionCount, handling, imageExists, cancellationToken);

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
                // The mode ignored a short payload or a damaged table. The header is complete, so its metadata stays valid. An attempt to read another frame
                // reads the missing payload or table bits as header bits.
                break;
            }
        }
        while (!frame.IsLast && (uint)frames.Count < this.Options.MaxFrames);

        return frames;
    }
}
