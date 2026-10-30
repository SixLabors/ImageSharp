// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

/// <summary>
/// Writes final planar RGB and alpha samples into the destination frame's pixel buffer.
/// </summary>
/// <typeparam name="TPixel">The destination pixel type.</typeparam>
/// <remarks>
/// The pipeline supplies three RGB planes, including for grayscale images. Extra channels start at channel three.
/// Color conversion and frame blending precede this stage. The destination pixel operations own alpha conversion, clamping and packing.
/// </remarks>
internal sealed class WriteToOutputStage<TPixel> : RenderPipelineStageBase
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// The borrowed destination. The decoded image owns and disposes this buffer.
    /// </summary>
    private readonly Buffer2D<TPixel> destination;

    /// <summary>
    /// The visible source width before orientation, excluding pipeline border storage.
    /// </summary>
    private readonly int sourceWidth;

    /// <summary>
    /// The visible source height before orientation, excluding pipeline border storage.
    /// </summary>
    private readonly int sourceHeight;

    /// <summary>
    /// The pipeline alpha channel, or minus one for opaque input.
    /// </summary>
    private readonly int alphaChannel;

    /// <summary>
    /// Whether the final source RGB planes already include multiplication by alpha.
    /// </summary>
    private readonly bool associatedAlpha;

    /// <summary>
    /// Whether source columns run backwards before any coordinate transposition.
    /// </summary>
    private readonly bool flipX;

    /// <summary>
    /// Whether source rows run backwards before any coordinate transposition.
    /// </summary>
    private readonly bool flipY;

    /// <summary>
    /// Whether source rows become destination columns.
    /// </summary>
    private readonly bool transpose;

    /// <summary>
    /// Initializes a new instance of the <see cref="WriteToOutputStage{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration used by the destination pixel operations.</param>
    /// <param name="destination">The frame's pixel buffer, allocated with the oriented image dimensions.</param>
    /// <param name="alphaChannel">The pipeline alpha-channel index, or minus one when no alpha channel is present.</param>
    /// <param name="associatedAlpha">Whether the final source RGB planes are associated with the supplied alpha plane.</param>
    /// <param name="orientation">The encoded image orientation to apply to final pixel coordinates.</param>
    public WriteToOutputStage(Configuration configuration, Buffer2D<TPixel> destination, int alphaChannel, bool associatedAlpha, JxlExifOrientation orientation)
        : base(configuration)
    {
        this.destination = destination;
        this.alphaChannel = alphaChannel;
        this.associatedAlpha = associatedAlpha;

        // JPEG XL first reverses the source axes, then transposes them. These combinations cover all eight header orientations.
        // Retain source extents here because the destination has exchanged width and height for transposed orientations.
        this.flipX = orientation is JxlExifOrientation.FlipHorizontal or JxlExifOrientation.Rotate180
            or JxlExifOrientation.Rotate270 or JxlExifOrientation.AntiTranspose;

        this.flipY = orientation is JxlExifOrientation.FlipVertical or JxlExifOrientation.Rotate180
            or JxlExifOrientation.Rotate90 or JxlExifOrientation.AntiTranspose;

        this.transpose = orientation is JxlExifOrientation.Transpose or JxlExifOrientation.Rotate90
            or JxlExifOrientation.Rotate270 or JxlExifOrientation.AntiTranspose;

        this.sourceWidth = this.transpose ? destination.Height : destination.Width;
        this.sourceHeight = this.transpose ? destination.Width : destination.Height;
    }

    /// <inheritdoc />
    public override string Name => "WritePixels";

    /// <inheritdoc />
    public override RenderPipelineChannelMode GetChannelMode(int channel)
        => channel < 3 || channel == this.alphaChannel ? RenderPipelineChannelMode.Input : RenderPipelineChannelMode.Ignored;

    /// <summary>
    /// Writes one final source-row segment into its oriented destination coordinates.
    /// </summary>
    /// <param name="inputRows">The planar source rows with the pipeline's fixed left prefix.</param>
    /// <param name="outputRows">Unused by this terminal stage, which writes the destination pixel buffer.</param>
    /// <param name="xExtraLeft">The terminal stage's left border, which is zero.</param>
    /// <param name="xExtraRight">The terminal stage's right border, which is zero.</param>
    /// <param name="width">The submitted sample count, including any right padding beyond the visible image.</param>
    /// <param name="xPos">The source column before orientation.</param>
    /// <param name="yPos">The source row before orientation.</param>
    public override void ProcessRow(
        Buffer2D<Memory<float>> inputRows,
        Buffer2D<Memory<float>> outputRows,
        int xExtraLeft,
        int xExtraRight,
        int width,
        int xPos,
        int yPos)
    {
        // The pipeline can submit padded rows and columns outside the visible image. They do not belong in the destination.
        // This terminal stage has no filter border; xExtraLeft and xExtraRight do not carry thread indices or output samples.
        if (yPos >= this.sourceHeight || xPos >= this.sourceWidth)
        {
            return;
        }

        int length = Math.Min(width, this.sourceWidth - xPos);

        // Borrow each plane once. GetInputRow removes the pipeline's fixed left prefix; sample zero is already the submitted xPos.
        // Grayscale enters the render pipeline as three color planes. Do not infer source channels from the destination pixel format.
        ReadOnlySpan<float> red = this.GetInputRow(inputRows, 0, 0)[..length];
        ReadOnlySpan<float> green = this.GetInputRow(inputRows, 1, 0)[..length];
        ReadOnlySpan<float> blue = this.GetInputRow(inputRows, 2, 0)[..length];
        ReadOnlySpan<float> alpha = this.alphaChannel < 0 ? default : this.GetInputRow(inputRows, this.alphaChannel, 0)[..length];
        int orientedY = this.flipY ? this.sourceHeight - 1 - yPos : yPos;
        Configuration configuration = this.GetConfiguration();
        PixelOperations<TPixel> operations = PixelOperations<TPixel>.Instance;

        if (!this.transpose)
        {
            // A horizontal reversal still occupies one contiguous destination slice. Pack directly, then reverse the final pixels in place.
            // Vertical reversal only changes the selected row. Neither case needs copied planes or an additional pixel buffer.
            int destinationX = this.flipX ? this.sourceWidth - xPos - length : xPos;
            Span<TPixel> pixels = this.destination.DangerousGetRowSpan(orientedY).Slice(destinationX, length);

            // Association describes the source planes, even when the destination does not store alpha.
            // The shared operation owns interleaving, alpha conversion, destination bounds and final packing.
            if (this.associatedAlpha)
            {
                operations.PackFromAssociatedScaledFloatPlanes(configuration, red, green, blue, alpha, pixels);
            }
            else
            {
                operations.PackFromUnassociatedScaledFloatPlanes(configuration, red, green, blue, alpha, pixels);
            }

            if (this.flipX)
            {
                pixels.Reverse();
            }

            return;
        }

        // A transposed source row is a destination column, so the final stores cannot use one contiguous span.
        // Pack bounded blocks through the same bulk API, then scatter the completed pixels. Pixel arithmetic stays in the shared bulk conversion.
        // The block matches the shared planar packer's 64-pixel block. It uses stack storage and needs no allocator or thread-specific state.
        const int BlockSize = 64;
        Span<TPixel> packed = stackalloc TPixel[BlockSize];
        int destinationRow = this.flipX ? this.sourceWidth - 1 - xPos : xPos;
        int rowStep = this.flipX ? -1 : 1;

        for (int offset = 0; offset < length; offset += BlockSize)
        {
            int count = Math.Min(BlockSize, length - offset);
            Span<TPixel> pixels = packed[..count];
            ReadOnlySpan<float> redBlock = red.Slice(offset, count);
            ReadOnlySpan<float> greenBlock = green.Slice(offset, count);
            ReadOnlySpan<float> blueBlock = blue.Slice(offset, count);
            ReadOnlySpan<float> alphaBlock = alpha.IsEmpty ? default : alpha.Slice(offset, count);

            // Use the same conversion for column output. Do not add a separate scalar packer for the scattered stores.
            if (this.associatedAlpha)
            {
                operations.PackFromAssociatedScaledFloatPlanes(configuration, redBlock, greenBlock, blueBlock, alphaBlock, pixels);
            }
            else
            {
                operations.PackFromUnassociatedScaledFloatPlanes(configuration, redBlock, greenBlock, blueBlock, alphaBlock, pixels);
            }

            // After transposition, the possibly reversed source y is the fixed destination x.
            // Each source x advances one destination row; the same mapping applies to a final short block.
            for (int i = 0; i < count; i++)
            {
                this.destination.DangerousGetRowSpan(destinationRow)[orientedY] = pixels[i];
                destinationRow += rowStep;
            }
        }
    }
}
