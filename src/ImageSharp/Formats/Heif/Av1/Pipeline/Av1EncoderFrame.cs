// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Provides non-owning visible and coded views over operation-scoped AV1 component planes.
/// </summary>
/// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
internal readonly struct Av1EncoderFrame<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// The base-two alignment exponent applied to coded frame dimensions.
    /// </summary>
    private const int CodedDimensionAlignmentLog2 = 3;

    /// <summary>
    /// The base-two alignment exponent applied to the physical luma row stride.
    /// </summary>
    private const int LumaStrideAlignmentLog2 = 5;

    /// <summary>
    /// The physical luma border required by non-resized all-intra encoding.
    /// </summary>
    public const int LumaBorder = 64;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderFrame{TSample}"/> struct for a monochrome frame.
    /// </summary>
    /// <param name="luma">The coded luma region inside the bordered plane owned by the encode operation.</param>
    /// <param name="width">The visible luma width.</param>
    /// <param name="height">The visible luma height.</param>
    /// <param name="bitDepth">The native component precision.</param>
    public Av1EncoderFrame(Buffer2DRegion<TSample> luma, int width, int height, int bitDepth)
        : this(luma, default, default, width, height, bitDepth, Av1ColorFormat.Yuv400, 0, 0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1EncoderFrame{TSample}"/> struct for a color frame.
    /// </summary>
    /// <param name="luma">The coded luma region inside the bordered plane owned by the encode operation.</param>
    /// <param name="chromaBlue">The coded blue-difference region inside its bordered plane.</param>
    /// <param name="chromaRed">The coded red-difference region inside its bordered plane.</param>
    /// <param name="width">The visible luma width.</param>
    /// <param name="height">The visible luma height.</param>
    /// <param name="bitDepth">The native component precision.</param>
    /// <param name="colorFormat">The native luma and chroma sampling layout.</param>
    /// <param name="chromaPositionX">The horizontal chroma position in half-luma-sample units.</param>
    /// <param name="chromaPositionY">The vertical chroma position in half-luma-sample units.</param>
    public Av1EncoderFrame(
        Buffer2DRegion<TSample> luma,
        Buffer2DRegion<TSample> chromaBlue,
        Buffer2DRegion<TSample> chromaRed,
        int width,
        int height,
        int bitDepth,
        Av1ColorFormat colorFormat,
        int chromaPositionX,
        int chromaPositionY)
    {
        this.Width = width;
        this.Height = height;
        this.LumaBitDepth = bitDepth;
        this.ChromaBitDepth = bitDepth;
        this.IsMonochrome = colorFormat == Av1ColorFormat.Yuv400;
        this.ChromaSubsamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422 ? 1 : 0;
        this.ChromaSubsamplingY = colorFormat == Av1ColorFormat.Yuv420 ? 1 : 0;
        this.ChromaPositionX = chromaPositionX;
        this.ChromaPositionY = chromaPositionY;
        this.CodedWidth = luma.Width;
        this.CodedHeight = luma.Height;
        int visibleChromaWidth = (width + this.ChromaSubsamplingX) >> this.ChromaSubsamplingX;
        int visibleChromaHeight = (height + this.ChromaSubsamplingY) >> this.ChromaSubsamplingY;
        Buffer2DRegion<TSample> visibleChromaBlue = this.IsMonochrome
            ? default
            : chromaBlue.GetSubRegion(0, 0, visibleChromaWidth, visibleChromaHeight);

        Buffer2DRegion<TSample> visibleChromaRed = this.IsMonochrome
            ? default
            : chromaRed.GetSubRegion(0, 0, visibleChromaWidth, visibleChromaHeight);

        this.View = new PlanarView(
            luma.GetSubRegion(0, 0, width, height),
            visibleChromaBlue,
            visibleChromaRed,
            width,
            height,
            bitDepth,
            colorFormat,
            chromaPositionX,
            chromaPositionY);

        this.CodedView = new PlanarView(
            luma,
            chromaBlue,
            chromaRed,
            this.CodedWidth,
            this.CodedHeight,
            bitDepth,
            colorFormat,
            chromaPositionX,
            chromaPositionY);
    }

    /// <summary>
    /// Gets the writable component-plane view used by closed generic conversion and coding operations.
    /// </summary>
    public PlanarView View { get; }

    /// <summary>
    /// Gets the writable coded component planes used by block coding and reconstruction.
    /// </summary>
    public PlanarView CodedView { get; }

    /// <summary>
    /// Gets the visible luma width.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the visible luma height.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the luma width rounded up to the fixed coding-block boundary.
    /// </summary>
    public int CodedWidth { get; }

    /// <summary>
    /// Gets the luma height rounded up to the fixed coding-block boundary.
    /// </summary>
    public int CodedHeight { get; }

    /// <summary>
    /// Gets the native luma sample precision.
    /// </summary>
    public int LumaBitDepth { get; }

    /// <summary>
    /// Gets the native chroma sample precision.
    /// </summary>
    public int ChromaBitDepth { get; }

    /// <summary>
    /// Gets a value indicating whether the frame contains only luma samples.
    /// </summary>
    public bool IsMonochrome { get; }

    /// <summary>
    /// Gets the horizontal chroma subsampling shift.
    /// </summary>
    public int ChromaSubsamplingX { get; }

    /// <summary>
    /// Gets the vertical chroma subsampling shift.
    /// </summary>
    public int ChromaSubsamplingY { get; }

    /// <summary>
    /// Gets the horizontal chroma position in half-luma-sample units.
    /// </summary>
    public int ChromaPositionX { get; }

    /// <summary>
    /// Gets the vertical chroma position in half-luma-sample units.
    /// </summary>
    public int ChromaPositionY { get; }

    /// <summary>
    /// Calculates the coded luma dimensions used by the fixed all-intra frame layout.
    /// </summary>
    /// <param name="width">The visible luma width.</param>
    /// <param name="height">The visible luma height.</param>
    /// <returns>The visible dimensions rounded up to the coding alignment.</returns>
    public static Size GetCodedSize(int width, int height)
        => new(
            Av1Math.AlignPowerOf2(width, CodedDimensionAlignmentLog2),
            Av1Math.AlignPowerOf2(height, CodedDimensionAlignmentLog2));

    /// <summary>
    /// Calculates the physical dimensions required for an all-intra component plane.
    /// </summary>
    /// <param name="width">The visible luma width.</param>
    /// <param name="height">The visible luma height.</param>
    /// <param name="subsamplingX">The plane's horizontal subsampling shift.</param>
    /// <param name="subsamplingY">The plane's vertical subsampling shift.</param>
    /// <returns>The physical plane dimensions, including its complete border and row padding.</returns>
    public static Size GetPlaneBufferSize(int width, int height, int subsamplingX, int subsamplingY)
    {
        Size codedSize = GetCodedSize(width, height);

        // libaom aligns the complete luma row before deriving a subsampled plane's stride.
        // Aligning chroma independently would produce a different physical layout for narrow or odd-sized frames.
        int lumaStride = Av1Math.AlignPowerOf2(codedSize.Width + (2 * LumaBorder), LumaStrideAlignmentLog2);
        int planeStride = lumaStride >> subsamplingX;
        int planeBorderHeight = LumaBorder >> subsamplingY;
        return new Size(planeStride, (codedSize.Height >> subsamplingY) + (2 * planeBorderHeight));
    }

    /// <summary>
    /// Extends the visible edge samples through the coded padding.
    /// </summary>
    public void ExtendBorders()
        => this.CodedView.ExtendBorders(this.Width, this.Height);

    /// <summary>
    /// Replicates the visible edge samples through a plane's complete physical border.
    /// </summary>
    private static void ExtendPlane(Buffer2DRegion<TSample> plane, int visibleWidth, int visibleHeight)
    {
        Buffer2D<TSample> buffer = plane.Buffer;
        Rectangle bounds = plane.Bounds;
        for (int y = 0; y < visibleHeight; y++)
        {
            Span<TSample> row = buffer.DangerousGetRowSpan(bounds.Y + y);

            // libaom fills both physical borders and the right-hand coded alignment from the nearest visible sample.
            row[..bounds.X].Fill(row[bounds.X]);
            row[(bounds.X + visibleWidth)..].Fill(row[bounds.X + visibleWidth - 1]);
        }

        // Horizontal extension runs first so copying the first and last visible rows also initializes both corners.
        ReadOnlySpan<TSample> firstVisibleRow = buffer.DangerousGetRowSpan(bounds.Y);
        for (int y = 0; y < bounds.Y; y++)
        {
            firstVisibleRow.CopyTo(buffer.DangerousGetRowSpan(y));
        }

        ReadOnlySpan<TSample> finalVisibleRow = buffer.DangerousGetRowSpan(bounds.Y + visibleHeight - 1);
        for (int y = bounds.Y + visibleHeight; y < buffer.Height; y++)
        {
            finalVisibleRow.CopyTo(buffer.DangerousGetRowSpan(y));
        }
    }

    /// <summary>
    /// Provides a non-owning component-plane view for generic hot-path operations.
    /// </summary>
    internal readonly struct PlanarView : IHeifPlanarSampleBuffer<TSample>
    {
        /// <summary>
        /// The writable luma plane.
        /// </summary>
        private readonly Buffer2DRegion<TSample> luma;

        /// <summary>
        /// The writable blue-difference plane, or the default region for monochrome frames.
        /// </summary>
        private readonly Buffer2DRegion<TSample> chromaBlue;

        /// <summary>
        /// The writable red-difference plane, or the default region for monochrome frames.
        /// </summary>
        private readonly Buffer2DRegion<TSample> chromaRed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlanarView"/> struct.
        /// </summary>
        public PlanarView(
            Buffer2DRegion<TSample> luma,
            Buffer2DRegion<TSample> chromaBlue,
            Buffer2DRegion<TSample> chromaRed,
            int width,
            int height,
            int bitDepth,
            Av1ColorFormat colorFormat,
            int chromaPositionX,
            int chromaPositionY)
        {
            this.luma = luma;
            this.chromaBlue = chromaBlue;
            this.chromaRed = chromaRed;
            this.Width = width;
            this.Height = height;
            this.LumaBitDepth = bitDepth;
            this.ChromaBitDepth = bitDepth;
            this.IsMonochrome = colorFormat == Av1ColorFormat.Yuv400;
            this.ChromaSubsamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422 ? 1 : 0;
            this.ChromaSubsamplingY = colorFormat == Av1ColorFormat.Yuv420 ? 1 : 0;
            this.ChromaPositionX = chromaPositionX;
            this.ChromaPositionY = chromaPositionY;
        }

        /// <inheritdoc/>
        public int Width { get; }

        /// <inheritdoc/>
        public int Height { get; }

        /// <inheritdoc/>
        public int LumaBitDepth { get; }

        /// <inheritdoc/>
        public int ChromaBitDepth { get; }

        /// <inheritdoc/>
        public bool IsMonochrome { get; }

        /// <inheritdoc/>
        public int ChromaSubsamplingX { get; }

        /// <inheritdoc/>
        public int ChromaSubsamplingY { get; }

        /// <inheritdoc/>
        public int ChromaPositionX { get; }

        /// <inheritdoc/>
        public int ChromaPositionY { get; }

        /// <summary>
        /// Replicates the visible component edges through the coded padding.
        /// </summary>
        /// <param name="visibleWidth">The visible luma width.</param>
        /// <param name="visibleHeight">The visible luma height.</param>
        public void ExtendBorders(int visibleWidth, int visibleHeight)
        {
            ExtendPlane(this.luma, visibleWidth, visibleHeight);

            if (!this.IsMonochrome)
            {
                int visibleChromaWidth = (visibleWidth + this.ChromaSubsamplingX) >> this.ChromaSubsamplingX;
                int visibleChromaHeight = (visibleHeight + this.ChromaSubsamplingY) >> this.ChromaSubsamplingY;
                ExtendPlane(this.chromaBlue, visibleChromaWidth, visibleChromaHeight);
                ExtendPlane(this.chromaRed, visibleChromaWidth, visibleChromaHeight);
            }
        }

        /// <summary>
        /// Gets a writable coded component plane.
        /// </summary>
        /// <param name="plane">The requested component plane.</param>
        /// <returns>The complete coded plane region.</returns>
        public Buffer2DRegion<TSample> GetPlane(Av1Plane plane)
            => plane switch
            {
                Av1Plane.Y => this.luma,
                Av1Plane.U => this.chromaBlue,
                _ => this.chromaRed
            };

        /// <inheritdoc/>
        public Span<TSample> GetLumaRowSpan(int row) => this.luma.DangerousGetRowSpan(row);

        /// <inheritdoc/>
        public Span<TSample> GetChromaBlueRowSpan(int row) => this.chromaBlue.DangerousGetRowSpan(row);

        /// <inheritdoc/>
        public Span<TSample> GetChromaRedRowSpan(int row) => this.chromaRed.DangerousGetRowSpan(row);
    }
}
