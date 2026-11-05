// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Converts completed JPEG XL component planes to destination pixels with one reusable row allocation.
/// </summary>
/// <typeparam name="TPixel">The destination pixel format.</typeparam>
/// <remarks>
/// The decoder retains ownership of component planes, expanded extras and the destination frame.
/// This operation owns only its row storage. Color operators own device conversion; PixelOperations owns final pixel packing.
/// </remarks>
internal sealed class JxlPixelConverter<TPixel> : IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// The configuration used by the final pixel conversion.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The borrowed image-wide sample and color declarations.
    /// </summary>
    private readonly JxlHeader header;

    /// <summary>
    /// The borrowed frame that owns native components during decoding and output.
    /// </summary>
    private readonly JxlFrame frame;

    /// <summary>
    /// The initialized destination frame. The decoder retains its ownership.
    /// </summary>
    private readonly ImageFrame<TPixel> destination;

    /// <summary>
    /// The absolute modular alpha channel, or minus one for opaque output.
    /// </summary>
    private readonly int alphaChannel;

    /// <summary>
    /// The absolute modular black channel, or minus one for models without black.
    /// </summary>
    private readonly int blackChannel;

    /// <summary>
    /// The borrowed full-resolution alpha plane, or null when the native row needs normalization.
    /// </summary>
    private readonly Buffer2D<float>? upsampledAlpha;

    /// <summary>
    /// The borrowed full-resolution black plane, or null when the native row needs normalization.
    /// </summary>
    private readonly Buffer2D<float>? upsampledBlack;

    /// <summary>
    /// The color operation selected once for this frame.
    /// </summary>
    private readonly JxlColorConverterBase colorConverter;

    /// <summary>
    /// Restores coding components before pointwise image features, or null when the ordinary path can finish directly.
    /// </summary>
    private readonly JxlColorConverterBase? featureConverter;

    /// <summary>
    /// The paired ICC transform, or null when native operators can produce sRGB directly.
    /// </summary>
    private readonly ColorProfileConverter? profileConverter;

    /// <summary>
    /// The shared destination packing and alpha conversion operations.
    /// </summary>
    private readonly PixelOperations<TPixel> pixelOperations;

    /// <summary>
    /// The native alpha representation, independent of the color-channel precision.
    /// </summary>
    private readonly JxlBitDepth alphaDepth;

    /// <summary>
    /// The native black representation, independent of alpha and color-channel precision.
    /// </summary>
    private readonly JxlBitDepth blackDepth;

    /// <summary>
    /// Indicates whether the shared pixel API must unassociate the converted color.
    /// </summary>
    private readonly bool associated;

    // Lengths are float counts, not pixel counts. Together they partition the single row allocation
    // into [pixel vectors | normalized alpha | normalized black | optional spot rows].
    private readonly int vectorLength;
    private readonly int alphaLength;
    private readonly int blackLength;
    private readonly int spotLength;

    /// <summary>
    /// The only allocation owned by this operation. Source planes and destination pixels are borrowed.
    /// </summary>
    private readonly IMemoryOwner<float> rowOwner;

    /// <summary>
    /// The exact row slice. ConvertRows acquires its span once before traversing image rows.
    /// </summary>
    private readonly Memory<float> rowMemory;

    /// <summary>
    /// Indicates whether the owned conversion row has been released.
    /// </summary>
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlPixelConverter{TPixel}"/> class with prepared source and destination owners.
    /// </summary>
    /// <param name="configuration">The destination pixel configuration and tracked allocator.</param>
    /// <param name="header">The native sample, extra-channel and association declarations.</param>
    /// <param name="frame">The borrowed frame. Convert only completed rows; component owners remain valid until conversion ends.</param>
    /// <param name="destination">The initialized destination frame.</param>
    public JxlPixelConverter(Configuration configuration, JxlHeader header, JxlFrame frame, ImageFrame<TPixel> destination)
    {
        this.configuration = configuration;
        this.header = header;
        this.frame = frame;
        this.destination = destination;
        this.upsampledAlpha = frame.UpsampledAlpha;
        this.upsampledBlack = frame.UpsampledBlack;
        this.colorConverter = JxlColorConverterBase.Create(header, frame);
        this.featureConverter = frame.HasSpotColors ? JxlColorConverterBase.CreateForImageFeatures(header, frame) : null;
        this.pixelOperations = PixelOperations<TPixel>.Instance;

        // Coding XYB reconstructs sRGB without its suggested output profile. Compact color descriptions
        // also use direct operators. Only native components with an embedded non-sRGB profile need ICC.
        if (!header.UsesXybTransform && IccProfile.TryGetIccProfileForColorConversion(header.IccProfile, out IccProfile? sourceProfile))
        {
            this.profileConverter = new ColorProfileConverter(new ColorConversionOptions
            {
                MemoryAllocator = configuration.MemoryAllocator,
                SourceIccProfile = sourceProfile,
                TargetIccProfile = CompactSrgbV4Profile.Profile
            });
        }

        int alphaExtra = frame.AlphaExtra;
        int blackExtra = frame.BlackExtra;
        int extraOffset = frame.Modular.ColorChannelCount;
        this.alphaChannel = alphaExtra < 0 ? -1 : extraOffset + alphaExtra;
        this.blackChannel = blackExtra < 0 ? -1 : extraOffset + blackExtra;
        this.alphaDepth = alphaExtra < 0 ? default : header.Metadata.ExtraChannels[alphaExtra].BitDepth;
        this.blackDepth = blackExtra < 0 ? default : header.Metadata.ExtraChannels[blackExtra].BitDepth;
        this.associated = alphaExtra >= 0 && header.Metadata.ExtraChannels[alphaExtra].AlphaRepresentation == PixelAlphaRepresentation.Associated;

        int width = destination.Width;
        this.alphaLength = alphaExtra >= 0 && this.upsampledAlpha is null ? width : 0;
        this.blackLength = blackExtra >= 0 && this.upsampledBlack is null ? width : 0;
        long spotLength = this.featureConverter is null ? 0 : 6L * width;
        long rowLength = (4L * width) + this.alphaLength + this.blackLength + spotLength;
        if (rowLength > int.MaxValue)
        {
            throw new InvalidMemoryOperationException("The JPEG XL color conversion workspace exceeds the supported buffer length.");
        }

        // A segmented destination can exceed a single float Span. Bound the complete row before narrowing
        // its nonnegative offsets. Acquire the only owned allocation last, after all other preparation succeeds.
        this.vectorLength = (int)(4L * width);
        this.spotLength = (int)spotLength;
        this.rowOwner = configuration.MemoryAllocator.Allocate<float>((int)rowLength);
        this.rowMemory = this.rowOwner.Memory[..(int)rowLength];
    }

    /// <summary>
    /// Gets the reusable pixel-vector row for a complete group-row conversion sequence.
    /// </summary>
    /// <remarks>Borrow once before row traversal. This operation retains ownership until disposal.</remarks>
    public Span<Vector4> PixelRow => MemoryMarshal.Cast<float, Vector4>(this.rowMemory.Span[..this.vectorLength]);

    /// <summary>
    /// Converts the completed frame's native color planes and packs the destination pixels.
    /// </summary>
    /// <param name="cancellationToken">The token checked before each output row.</param>
    public void Convert(CancellationToken cancellationToken)
    {
        if (!this.frame.ReconstructedColors.IsEmpty)
        {
            // Spatial reconstruction already restored full-resolution float components. Borrow these
            // planes directly and do not normalize them as native integer samples a second time.
            ReadOnlySpan<Buffer2D<float>> colors = this.frame.ReconstructedColors;
            Buffer2DRegion<float> first = colors[0].GetRegion();
            Buffer2DRegion<float> second = colors.Length == 1 ? first : colors[1].GetRegion();
            Buffer2DRegion<float> third = colors.Length == 1 ? first : colors[2].GetRegion();

            this.ConvertRows(first, second, third, default(JxlSampleConverter.IdentityOperator), cancellationToken);
        }
        else
        {
            this.ConvertModular(cancellationToken);
        }
    }

    /// <summary>
    /// Converts and packs one restored native color row with no pending extra-channel reconstruction.
    /// </summary>
    /// <param name="row">The global destination row.</param>
    /// <param name="component0">The restored first native component.</param>
    /// <param name="component1">The restored second native component.</param>
    /// <param name="component2">The restored third native component.</param>
    /// <param name="vectors">The pixel row borrowed before traversal.</param>
    public void ConvertRow(int row, ReadOnlySpan<float> component0, ReadOnlySpan<float> component1, ReadOnlySpan<float> component2, Span<Vector4> vectors)
    {
        JxlColorConverterBase.ComponentValues<float> source = new(component0, component1, component2, [], []);
        this.ConvertRow(row, in source, vectors, [], default(JxlSampleConverter.IdentityOperator));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.rowOwner.Dispose();
        this.isDisposed = true;
    }

    /// <summary>
    /// Converts native modular color with its integer or floating-point carrier format selected before row traversal.
    /// </summary>
    /// <param name="cancellationToken">The token checked before each output row.</param>
    private void ConvertModular(CancellationToken cancellationToken)
    {
        JxlModularDecoder modular = this.frame.Modular;
        JxlModularChannel first = modular.GetChannel(0);
        bool grayscale = modular.ColorChannelCount == 1;
        JxlModularChannel second = grayscale ? first : modular.GetChannel(1);
        JxlModularChannel third = grayscale ? first : modular.GetChannel(2);
        JxlBitDepth depth = this.header.Metadata.BitDepth;
        if (depth.ExponentBits == 0)
        {
            if (depth.BitsPerSample < 23)
            {
                this.ConvertModularRows(first, second, third, new JxlSampleConverter.IntegerOperator(depth.BitsPerSample), cancellationToken);
            }
            else
            {
                this.ConvertModularRows(first, second, third, new JxlSampleConverter.AccurateIntegerOperator(depth.BitsPerSample), cancellationToken);
            }
        }
        else if (depth.BitsPerSample == 32)
        {
            this.ConvertModularRows(first, second, third, default(JxlSampleConverter.Binary32Operator), cancellationToken);
        }
        else if (depth.ExponentBits == 8)
        {
            this.ConvertModularRows(first, second, third, new JxlSampleConverter.Exponent8Operator(depth.BitsPerSample), cancellationToken);
        }
        else
        {
            this.ConvertModularRows(first, second, third, new JxlSampleConverter.CustomFloatOperator(depth), cancellationToken);
        }
    }

    /// <summary>
    /// Selects the modular storage width before converting all rows with the prepared sample operation.
    /// </summary>
    /// <typeparam name="TSampleOperator">The existing operation over signed 32-bit carriers.</typeparam>
    /// <param name="first">The first borrowed color channel.</param>
    /// <param name="second">The second channel, or the repeated grayscale channel.</param>
    /// <param name="third">The third channel, or the repeated grayscale channel.</param>
    /// <param name="sampleOperator">The prepared integer normalization or floating-point decoding operation.</param>
    /// <param name="cancellationToken">The token checked before each output row.</param>
    private void ConvertModularRows<TSampleOperator>(
        JxlModularChannel first,
        JxlModularChannel second,
        JxlModularChannel third,
        TSampleOperator sampleOperator,
        CancellationToken cancellationToken)
        where TSampleOperator : struct, JxlSampleConverter.IConversionOperator<int>
    {
        // All color channels use the image's modular storage width. Short storage widens in registers;
        // it uses the same sample equations without allocating or copying full-width carrier planes.
        if (first.Uses16BitStorage)
        {
            this.ConvertRows(
                first.Region16,
                second.Region16,
                third.Region16,
                new JxlSampleConverter.Int16Operator<TSampleOperator>(sampleOperator),
                cancellationToken);
        }
        else
        {
            this.ConvertRows(first.Region, second.Region, third.Region, sampleOperator, cancellationToken);
        }
    }

    /// <summary>
    /// Converts component rows and packs the destination with the prepared sample and color operations.
    /// </summary>
    /// <typeparam name="TSample">The modular integer carrier or restored float sample.</typeparam>
    /// <typeparam name="TSampleOperator">The representation selected before row traversal.</typeparam>
    /// <param name="component0">The first full-resolution component plane.</param>
    /// <param name="component1">The second plane, or the repeated luminance plane.</param>
    /// <param name="component2">The third plane, or the repeated luminance plane.</param>
    /// <param name="sampleOperator">The prepared sample representation.</param>
    /// <param name="cancellationToken">The token checked before each output row.</param>
    private void ConvertRows<TSample, TSampleOperator>(
        Buffer2DRegion<TSample> component0,
        Buffer2DRegion<TSample> component1,
        Buffer2DRegion<TSample> component2,
        TSampleOperator sampleOperator,
        CancellationToken cancellationToken)
        where TSample : unmanaged
        where TSampleOperator : struct, JxlSampleConverter.IConversionOperator<TSample>
    {
        int width = this.destination.Width;
        Span<float> row = this.rowMemory.Span;
        Span<Vector4> vectors = MemoryMarshal.Cast<float, Vector4>(row[..this.vectorLength]);
        Span<float> alpha = row.Slice(this.vectorLength, this.alphaLength);
        Span<float> black = row.Slice(this.vectorLength + this.alphaLength, this.blackLength);
        Span<float> spotWorkspace = row.Slice(this.vectorLength + this.alphaLength + this.blackLength, this.spotLength);
        JxlModularChannel nativeAlpha = this.alphaChannel < 0 ? default : this.frame.Modular.GetChannel(this.alphaChannel);
        JxlModularChannel nativeBlack = this.blackChannel < 0 ? default : this.frame.Modular.GetChannel(this.blackChannel);

        // Pixel vectors and native extra rows share one owner. Borrow its Span once.
        // Expanded extras borrow final float planes instead; they need no row storage or second normalization.
        for (int y = 0; y < this.destination.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<float> rowAlpha = [];
            if (this.upsampledAlpha is not null)
            {
                rowAlpha = this.upsampledAlpha.DangerousGetRowSpan(y)[..width];
            }
            else if (this.alphaChannel >= 0)
            {
                if (nativeAlpha.Uses16BitStorage)
                {
                    JxlSampleConverter.Convert(nativeAlpha.Region16.DangerousGetRowSpan(y), alpha, this.alphaDepth);
                }
                else
                {
                    JxlSampleConverter.Convert(nativeAlpha.Region.DangerousGetRowSpan(y), alpha, this.alphaDepth);
                }

                rowAlpha = alpha;
            }

            ReadOnlySpan<float> rowBlack = [];
            if (this.upsampledBlack is not null)
            {
                rowBlack = this.upsampledBlack.DangerousGetRowSpan(y)[..width];
            }
            else if (this.blackChannel >= 0)
            {
                if (nativeBlack.Uses16BitStorage)
                {
                    JxlSampleConverter.Convert(nativeBlack.Region16.DangerousGetRowSpan(y), black, this.blackDepth);
                }
                else
                {
                    JxlSampleConverter.Convert(nativeBlack.Region.DangerousGetRowSpan(y), black, this.blackDepth);
                }

                rowBlack = black;
            }

            JxlColorConverterBase.ComponentValues<TSample> source = new(
                component0.DangerousGetRowSpan(y), component1.DangerousGetRowSpan(y), component2.DangerousGetRowSpan(y), rowBlack, rowAlpha);

            this.ConvertRow(y, in source, vectors, spotWorkspace, in sampleOperator);
        }
    }

    /// <summary>
    /// Shares native color conversion and final pixel packing between retained-plane and sequential-row traversal.
    /// </summary>
    /// <typeparam name="TSample">The native modular carrier or restored float sample.</typeparam>
    /// <typeparam name="TSampleOperator">The representation selected before traversal.</typeparam>
    /// <param name="row">The global destination row.</param>
    /// <param name="source">The complete native component rows, including ready alpha and black when present.</param>
    /// <param name="vectors">The reusable pixel-vector row.</param>
    /// <param name="spotWorkspace">The borrowed spot rows, or empty on the ordinary path.</param>
    /// <param name="sampleOperator">The prepared native sample representation.</param>
    private void ConvertRow<TSample, TSampleOperator>(
        int row,
        in JxlColorConverterBase.ComponentValues<TSample> source,
        Span<Vector4> vectors,
        Span<float> spotWorkspace,
        in TSampleOperator sampleOperator)
        where TSample : unmanaged
        where TSampleOperator : struct, JxlSampleConverter.IConversionOperator<TSample>
    {
        if (this.featureConverter is not null)
        {
            // Features consume native color components before output transfer or ICC conversion.
            // Only feature-bearing rows need separate component ranges for the spot calculation.
            this.featureConverter.ConvertComponents(in source, vectors, in sampleOperator);
            this.ApplySpotColors(row, vectors, spotWorkspace, source.Black, source.Alpha);
        }
        else if (this.profileConverter is null)
        {
            this.colorConverter.ConvertComponents(in source, vectors, in sampleOperator);
            this.colorConverter.ApplyOutputConversion(vectors);
        }
        else
        {
            this.colorConverter.ConvertToRgb(in source, MemoryMarshal.Cast<Vector4, float>(vectors), this.profileConverter, in sampleOperator);
        }

        if (this.profileConverter is not null)
        {
            this.PackRgb(row, vectors, source.Alpha);
            return;
        }

        // Both traversal modes use the same color operator and PixelOperations packing. Normalized alpha
        // bounds and association therefore do not depend on whether reconstruction retained whole planes.
        Span<TPixel> pixels = this.destination.PixelBuffer.DangerousGetRowSpan(row);
        if (this.associated)
        {
            this.pixelOperations.FromAssociatedScaledVector4Destructive(this.configuration, vectors, pixels);
        }
        else
        {
            this.pixelOperations.FromUnassociatedScaledVector4Destructive(this.configuration, vectors, pixels);
        }
    }

    /// <summary>
    /// Mixes native spot samples in reusable rows, then completes the existing output color conversion.
    /// </summary>
    /// <param name="row">The completed native image row.</param>
    /// <param name="vectors">The restored native colors with their separate image alpha.</param>
    /// <param name="workspace">Six exact row ranges: red, green, blue and three mixing ranges.</param>
    /// <param name="black">Original remaining-light black samples for CMYK, or empty for other device models.</param>
    /// <param name="alpha">The unchanged normalized alpha row, or empty for opaque output.</param>
    private void ApplySpotColors(int row, Span<Vector4> vectors, Span<float> workspace, ReadOnlySpan<float> black, ReadOnlySpan<float> alpha)
    {
        int width = vectors.Length;
        Span<float> red = workspace[..width];
        Span<float> green = workspace.Slice(width, width);
        Span<float> blue = workspace.Slice(2 * width, width);

        // Split the native colors without copying alpha. The shared traversal handles complete
        // SIMD blocks and scalar tails; the original alpha row remains available for pixel packing.
        SimdUtils.DeinterleaveFloatPlanes(vectors, red, green, blue, []);

        this.frame.ApplySpotColors(row, red, green, blue, workspace.Slice(3 * width, 3 * width));

        if ((!this.header.UsesXybTransform || this.frame.CodingColorTransformApplied)
            && !this.header.HasIccProfile && this.header.ProfileColorType == JxlColorType.Xyb)
        {
            // Declared XYB is an original device encoding, unlike coding XYB. Its image features
            // precede the device-to-linear-RGB transform, just as they precede a device ICC transform.
            JxlColorConverterBase.ConvertInPlace(
                red, green, blue, new JxlColorConverterBase.XybDeviceOperator<JxlColorConverterBase.RgbOperator>(default));
        }

        if (this.profileConverter is null)
        {
            // Rejoin the unchanged alpha row after the RGB-only feature calculation. The shared
            // interleaver supplies opaque alpha for an empty row and retains floating-point components.
            SimdUtils.InterleaveFloatPlanes(red, green, blue, alpha, vectors);
            this.colorConverter.ApplyOutputConversion(vectors);
        }
        else
        {
            Span<float> storage = MemoryMarshal.Cast<Vector4, float>(vectors);
            Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(storage[..(3 * width)]);
            if (this.blackChannel >= 0)
            {
                // Features use remaining light. Invert all four components to ink amounts before
                // the typed CMYK transform. Its RGB output reduces four floats per pixel to three.
                SimdUtils.PackedInvertNormalizeInterleave4(red, green, blue, black, storage, 1F);
                this.profileConverter.Convert<Cmyk, Rgb>(MemoryMarshal.Cast<float, Cmyk>(storage), rgb);
            }
            else if (this.profileConverter.Options.SourceIccProfile is { Header.DataColorSpace: IccColorSpaceType.Gray })
            {
                this.profileConverter.Convert<Y, Rgb>(MemoryMarshal.Cast<float, Y>(red), rgb);
            }
            else
            {
                SimdUtils.PackedNormalizeInterleave3(red, green, blue, storage[..(3 * width)], 1F);
                this.profileConverter.Convert<Rgb, Rgb>(rgb, rgb);
            }
        }
    }

    /// <summary>
    /// Packs typed RGB output and its separate image alpha through the shared bulk pixel operations.
    /// </summary>
    /// <param name="row">The destination image row.</param>
    /// <param name="vectors">Four floats per pixel. Its first 3 * width floats hold the contiguous converted Rgb row.</param>
    /// <param name="alpha">The unchanged normalized alpha row, or empty for opaque pixels.</param>
    private void PackRgb(int row, Span<Vector4> vectors, ReadOnlySpan<float> alpha)
    {
        int width = vectors.Length;
        Span<float> storage = MemoryMarshal.Cast<Vector4, float>(vectors);
        ReadOnlySpan<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(storage[..(3 * width)]);
        Span<TPixel> pixels = this.destination.PixelBuffer.DangerousGetRowSpan(row);

        // RGB has three floats per pixel; pixel vectors have four. Keep this one full Vector512
        // batch separate from the packed RGB source so expansion and destructive packing cannot
        // overwrite unread colors. The fixed inline storage uses no allocator or managed allocation.
        InlineArray16<Vector4> blockBuffer = default;
        Span<Vector4> blockStorage = blockBuffer;

        for (int x = 0; x < width; x += blockStorage.Length)
        {
            int count = Math.Min(blockStorage.Length, width - x);
            Span<Vector4> block = blockStorage[..count];
            Rgb.ToScaledVector4(rgb.Slice(x, count), block);
            if (!alpha.IsEmpty)
            {
                Numerics.WithW(block, alpha.Slice(x, count));
            }

            Span<TPixel> destination = pixels.Slice(x, count);
            if (this.associated)
            {
                this.pixelOperations.FromAssociatedScaledVector4Destructive(this.configuration, block, destination);
            }
            else
            {
                this.pixelOperations.FromUnassociatedScaledVector4Destructive(this.configuration, block, destination);
            }
        }
    }
}
