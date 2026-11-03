// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;
using SixLabors.ImageSharp.Formats.Jxl.Components.Modular;
using SixLabors.ImageSharp.Memory;
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
    /// The borrowed owner of native modular color and extra-channel samples.
    /// </summary>
    private readonly JxlModularDecoder modular;

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
    // into [pixel vectors | color scratch | normalized alpha | normalized black].
    private readonly int vectorLength;
    private readonly int colorLength;
    private readonly int alphaLength;
    private readonly int blackLength;

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
    /// Initializes a new instance of the <see cref="JxlPixelConverter{TPixel}"/> class with ready source and destination owners.
    /// </summary>
    /// <param name="configuration">The destination pixel configuration and tracked allocator.</param>
    /// <param name="header">The native sample, extra-channel and association declarations.</param>
    /// <param name="modular">The borrowed completed native channels and their color/extra layout.</param>
    /// <param name="destination">The initialized destination frame.</param>
    /// <param name="alphaExtra">The first alpha extra-channel index, or minus one for opaque output.</param>
    /// <param name="blackExtra">The required black extra-channel index, or minus one for other device models.</param>
    /// <param name="upsampledAlpha">The borrowed expanded alpha plane, or null for a native-resolution row.</param>
    /// <param name="upsampledBlack">The borrowed expanded black plane, or null for a native-resolution row.</param>
    /// <param name="colorConverter">The prepared native-component to working-sRGB conversion.</param>
    public JxlPixelConverter(
        Configuration configuration,
        JxlHeader header,
        JxlModularDecoder modular,
        ImageFrame<TPixel> destination,
        int alphaExtra,
        int blackExtra,
        Buffer2D<float>? upsampledAlpha,
        Buffer2D<float>? upsampledBlack,
        JxlColorConverterBase colorConverter)
    {
        this.configuration = configuration;
        this.header = header;
        this.modular = modular;
        this.destination = destination;
        this.upsampledAlpha = upsampledAlpha;
        this.upsampledBlack = upsampledBlack;
        this.colorConverter = colorConverter;
        this.pixelOperations = PixelOperations<TPixel>.Instance;

        int extraOffset = modular.ColorChannelCount;
        this.alphaChannel = alphaExtra < 0 ? -1 : extraOffset + alphaExtra;
        this.blackChannel = blackExtra < 0 ? -1 : extraOffset + blackExtra;
        this.alphaDepth = alphaExtra < 0 ? default : header.Metadata.ExtraChannels[alphaExtra].BitDepth;
        this.blackDepth = blackExtra < 0 ? default : header.Metadata.ExtraChannels[blackExtra].BitDepth;
        this.associated = alphaExtra >= 0 && header.Metadata.ExtraChannels[alphaExtra].AlphaRepresentation == PixelAlphaRepresentation.Associated;

        int width = destination.Width;
        long colorLength64 = colorConverter.GetBufferLength(width);
        this.alphaLength = alphaExtra >= 0 && upsampledAlpha is null ? width : 0;
        this.blackLength = blackExtra >= 0 && upsampledBlack is null ? width : 0;
        long rowLength = (4L * width) + colorLength64 + this.alphaLength + this.blackLength;
        if (rowLength > int.MaxValue)
        {
            throw new InvalidMemoryOperationException("The JPEG XL color conversion workspace exceeds the supported buffer length.");
        }

        // A segmented destination can exceed a single float Span. Bound the complete row before narrowing
        // its nonnegative offsets. Acquire the only owned allocation last, after all other preparation succeeds.
        this.vectorLength = (int)(4L * width);
        this.colorLength = (int)colorLength64;
        this.rowOwner = configuration.MemoryAllocator.Allocate<float>((int)rowLength);
        this.rowMemory = this.rowOwner.Memory[..(int)rowLength];
    }

    /// <summary>
    /// Converts native modular color with its integer or floating-point carrier format selected before row traversal.
    /// </summary>
    /// <param name="cancellationToken">The token checked before each output row.</param>
    public void ConvertModular(CancellationToken cancellationToken)
    {
        Buffer2DRegion<int> first = this.modular.GetChannel(0);
        bool grayscale = this.modular.ColorChannelCount == 1;
        Buffer2DRegion<int> second = grayscale ? first : this.modular.GetChannel(1);
        Buffer2DRegion<int> third = grayscale ? first : this.modular.GetChannel(2);
        JxlBitDepth depth = this.header.Metadata.BitDepth;
        if (depth.ExponentBits == 0)
        {
            if (depth.BitsPerSample < 23)
            {
                this.ConvertRows(first, second, third, new JxlSampleConverter.IntegerOperator(depth.BitsPerSample), cancellationToken);
            }
            else
            {
                this.ConvertRows(first, second, third, new JxlSampleConverter.AccurateIntegerOperator(depth.BitsPerSample), cancellationToken);
            }
        }
        else if (depth.BitsPerSample == 32)
        {
            this.ConvertRows(first, second, third, default(JxlSampleConverter.Binary32Operator), cancellationToken);
        }
        else if (depth.ExponentBits == 8)
        {
            this.ConvertRows(first, second, third, new JxlSampleConverter.Exponent8Operator(depth.BitsPerSample), cancellationToken);
        }
        else
        {
            this.ConvertRows(first, second, third, new JxlSampleConverter.CustomFloatOperator(depth), cancellationToken);
        }
    }

    /// <summary>
    /// Converts restored full-resolution float color without another sample normalization.
    /// </summary>
    /// <param name="component0">The first completed component plane.</param>
    /// <param name="component1">The second plane, or the repeated luminance plane.</param>
    /// <param name="component2">The third plane, or the repeated luminance plane.</param>
    /// <param name="cancellationToken">The token checked before each output row.</param>
    public void ConvertFloat(
        Buffer2DRegion<float> component0,
        Buffer2DRegion<float> component1,
        Buffer2DRegion<float> component2,
        CancellationToken cancellationToken)
        => this.ConvertRows(component0, component1, component2, default(JxlSampleConverter.IdentityOperator), cancellationToken);

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
        Span<float> color = row.Slice(this.vectorLength, this.colorLength);
        Span<float> alpha = row.Slice(this.vectorLength + this.colorLength, this.alphaLength);
        Span<float> black = row.Slice(this.vectorLength + this.colorLength + this.alphaLength, this.blackLength);
        Buffer2DRegion<int> nativeAlpha = this.alphaChannel < 0 ? default : this.modular.GetChannel(this.alphaChannel);
        Buffer2DRegion<int> nativeBlack = this.blackChannel < 0 ? default : this.modular.GetChannel(this.blackChannel);

        // Vector output, packed color scratch and native extra rows share one owner. Borrow its Span once.
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
                JxlSampleConverter.Convert(nativeAlpha.DangerousGetRowSpan(y), alpha, this.alphaDepth);
                rowAlpha = alpha;
            }

            ReadOnlySpan<float> rowBlack = [];
            if (this.upsampledBlack is not null)
            {
                rowBlack = this.upsampledBlack.DangerousGetRowSpan(y)[..width];
            }
            else if (this.blackChannel >= 0)
            {
                JxlSampleConverter.Convert(nativeBlack.DangerousGetRowSpan(y), black, this.blackDepth);
                rowBlack = black;
            }

            JxlColorConverterBase.ComponentValues<TSample> source = new(
                component0.DangerousGetRowSpan(y), component1.DangerousGetRowSpan(y), component2.DangerousGetRowSpan(y), rowBlack, rowAlpha);

            this.colorConverter.ConvertToRgb(in source, vectors, color, in sampleOperator);

            // Native color conversion precedes final unassociation. PixelOperations owns normalized alpha
            // bounds, association and destination rounding; device conversion never treats K as alpha.
            Span<TPixel> pixels = this.destination.PixelBuffer.DangerousGetRowSpan(y);
            if (this.associated)
            {
                this.pixelOperations.FromAssociatedScaledVector4Destructive(this.configuration, vectors, pixels);
            }
            else
            {
                this.pixelOperations.FromUnassociatedScaledVector4Destructive(this.configuration, vectors, pixels);
            }
        }
    }
}
