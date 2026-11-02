// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;
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
                    || (frame.Flags & ~(JxlFrameHeader.SkipAdaptiveDcSmoothing | JxlFrameHeader.UseLowFrequencyFrame)) != 0
                    || (frame.Upsampling != 1 && frame.Modular && !lowFrequency)
                    || (frame.Modular && (frame.RestorationFilter.Gaborish || frame.RestorationFilter.Iterations != 0)))
                {
                    throw new NotSupportedException(
                        "JPEG XL decoding currently requires full-canvas LF dependencies followed by a final frame without image features. "
                        + "Upsampling of modular frames is not yet supported.");
                }

                // Native compact RGB must already use sRGB primaries and D65. A compact gamma changes only
                // the transfer curve, so shared companders can convert it without a matrix or another image buffer.
                // XYB has its own transform to sRGB, independent of the original profile's suggested output space.
                // Native RGB with other primaries, ICC, floating representations and YCbCr need their own output conversion.
                if ((frame.Modular && header.UsesXybTransform && !lowFrequency) || frame.YCbCr
                    || (lowFrequency && frame.Modular && !header.UsesXybTransform)
                    || (!header.UsesXybTransform && (header.HasIccProfile || header.Metadata.BitDepth.ExponentBits != 0
                    || (!header.Gamma.HasValue && header.TransferCharacteristics != CicpTransferCharacteristics.Iec61966_2_1)
                    || header.WhitePoint != KnownIlluminants.D65Chromaticity
                    || !header.Primaries.Equals(KnownRgbWorkingSpaces.SRgb.ChromaticityCoordinates))))
                {
                    throw new NotSupportedException("JPEG XL color transformation and floating-point reconstruction are not yet supported.");
                }

                int colorCount = !header.UsesXybTransform && header.ProfileColorType == JxlColorType.Luminance ? 1 : 3;
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
                    if (alpha.Type != JxlExtraChannelType.Alpha || alpha.BitDepth.ExponentBits != 0
                        || (frame.Modular && frame.ExtraChannelUpsampling[0] != 1))
                    {
                        throw new NotSupportedException("JPEG XL reconstruction requires integer alpha. Modular alpha must have full resolution.");
                    }

                    alphaChannel = frame.Modular ? colorCount : 0;
                    alphaDepth = alpha.BitDepth;
                    alphaRepresentation = alpha.AlphaRepresentation;
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
                (JxlModularDecoder Modular, JxlVarDctDecoder? VarDct, Vector3 DcFactors) reconstructed = this.DecodeFrameData(
                    ref reader, header, frame, sections, dcReference, cancellationToken);

                using JxlModularDecoder modular = reconstructed.Modular;
                using JxlVarDctDecoder? varDct = reconstructed.VarDct;
                using Buffer2D<float>? upsampledAlpha = varDct is null
                    ? null
                    : this.UpsampleVarDctFrame(header, frame, varDct, modular, lowFrequency ? -1 : alphaChannel, cancellationToken);

                if (lowFrequency)
                {
                    // Native LF slots contain restored component samples before color conversion, alpha unassociation or pixel packing.
                    // Transfer the VarDCT planes directly. Modular XYB first restores its signed integer channels to these same float units.
                    Buffer2D<float>[] planes = varDct is not null
                        ? varDct.TakeReconstructedChannels()
                        : modular.ReconstructXyb(frame, reconstructed.DcFactors, cancellationToken);

                    try
                    {
                        if (varDct is null && frame.Upsampling > 1)
                        {
                            using JxlUpsampler upsampler = new(header, frame.OutputSize, frame.CodingSize, frame.Upsampling, allocator);
                            foreach (Buffer2D<float> plane in planes)
                            {
                                upsampler.Apply(plane, frame.CodingSize, frame.Upsampling, cancellationToken);
                            }
                        }
                    }
                    catch
                    {
                        foreach (Buffer2D<float> plane in planes)
                        {
                            plane.Dispose();
                        }

                        throw;
                    }

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
                Image<TPixel> image = new(this.Options.Configuration, header.Size.Width, header.Size.Height, metadata);
                try
                {
                    ImageFrame<TPixel> destination = image.Frames.RootFrame;
                    JxlFrameMetadata frameMetadata = destination.Metadata.GetFormatMetadata(JxlFormat.Instance);
                    if (header.TicksPerSecond is Rational rate)
                    {
                        frameMetadata.FrameDelay = new LongRational((long)frame.Duration * rate.Denominator, rate.Numerator).Simplify();
                    }

                    if (varDct is not null)
                    {
                        this.PackVarDctFrame(
                            header,
                            varDct,
                            modular,
                            destination,
                            upsampledAlpha?.GetRegion(),
                            alphaChannel,
                            alphaDepth,
                            alphaRepresentation,
                            cancellationToken);

                        this.CompleteMetadata(codestream, header, metadata, cancellationToken);
                        return image;
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
    /// <returns>The modular owner, optional VarDCT owner and DC factors needed to restore modular XYB.</returns>
    private (JxlModularDecoder Modular, JxlVarDctDecoder? VarDct, Vector3 DcFactors) DecodeFrameData(
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

        // Transform-coded color and modular extra channels share the global tree. Read the VarDCT fields
        // before that tree; its owner also supplies the quantization matrices and reusable group storage.
        Vector3 dcFactors = ReadDcQuantization(ref reader);
        MemoryAllocator allocator = this.Options.Configuration.MemoryAllocator;
        JxlVarDctDecoder? varDct = frame.Modular ? null : JxlVarDctDecoder.ReadGlobal(ref reader, header, frame, allocator, dcFactors, dcReference);
        JxlModularDecoder? modular = null;
        try
        {
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

            // Accumulate every pass for one group before reusing its coefficient buffer. Section seeking
            // handles any physical order without retaining coefficients for the complete image.
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

                varDct?.ReconstructGroup(group, cancellationToken);
            }

            if (combined)
            {
                reader.EndSection();
            }

            modular.Complete(cancellationToken);
            varDct?.ApplyRestoration(frame, allocator, cancellationToken);

            // The last logical section need not end at the frame's physical end. Restore it before any following frame or metadata.
            reader.SeekBytePosition(sections.PayloadOffset + sections.TotalLength);
            return (modular, varDct, dcFactors);
        }
        catch
        {
            modular?.Dispose();
            varDct?.Dispose();
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
