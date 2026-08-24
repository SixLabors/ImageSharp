// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Decodes one AV1 still-image elementary stream into an ImageSharp image.
/// </summary>
internal class Av1Decoder : IAv1TileReader
{
    /// <summary>
    /// The open-bitstream-unit parser for the current image item.
    /// </summary>
    private readonly ObuReader obuReader;

    /// <summary>
    /// The configuration used for decoded image and scratch-memory allocation.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The tile parser shared by all tile groups in the current frame.
    /// </summary>
    private Av1TileReader? tileReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Decoder"/> class.
    /// </summary>
    /// <param name="configuration">The configuration used for image and scratch-memory allocation.</param>
    public Av1Decoder(Configuration configuration)
    {
        this.configuration = configuration;
        this.obuReader = new();
    }

    /// <summary>
    /// Gets the decoded frame header, or <see langword="null"/> before the stream provides one.
    /// </summary>
    public ObuFrameHeader? FrameHeader { get; private set; }

    /// <summary>
    /// Gets the decoded sequence header, or <see langword="null"/> before the stream provides one.
    /// </summary>
    public ObuSequenceHeader? SequenceHeader { get; private set; }

    /// <summary>
    /// Gets the tile and superblock state for the decoded frame, or <see langword="null"/> before tile parsing completes.
    /// </summary>
    public Av1FrameInfo? FrameInfo { get; private set; }

    /// <summary>
    /// Decodes an AV1 still-image elementary stream.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="buffer">The complete AV1 elementary-stream payload.</param>
    /// <param name="containerColorProfile">
    /// The container color description that overrides matching sequence-header color information.
    /// </param>
    /// <returns>The decoded image.</returns>
    public Image<TPixel> Decode<TPixel>(Span<byte> buffer, CicpProfile? containerColorProfile = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Av1BitStreamReader reader = new(buffer);
        this.obuReader.ReadAll(ref reader, buffer.Length, () => this, false);
        Guard.NotNull(this.tileReader, nameof(this.tileReader));
        Guard.NotNull(this.SequenceHeader, nameof(this.SequenceHeader));
        Guard.NotNull(this.FrameHeader, nameof(this.FrameHeader));

        if (containerColorProfile is not null)
        {
            // ISO BMFF color information takes precedence over the matching bitstream fields. Apply only the four
            // CICP values; chroma layout and bit depth remain properties of the AV1 coded image itself.
            ObuColorConfig colorConfig = this.SequenceHeader.ColorConfig;
            colorConfig.ColorPrimaries = (ObuColorPrimaries)containerColorProfile.ColorPrimaries;
            colorConfig.TransferCharacteristics = (ObuTransferCharacteristics)containerColorProfile.TransferCharacteristics;
            colorConfig.MatrixCoefficients = (ObuMatrixCoefficients)containerColorProfile.MatrixCoefficients;
            colorConfig.ColorRange = containerColorProfile.FullRange;
        }

        this.FrameInfo = this.tileReader.FrameInfo;
        using Av1FrameBuffer<byte> frameBuffer = new(
            this.configuration,
            this.SequenceHeader,
            this.SequenceHeader.ColorConfig.GetColorFormat(),
            false);

        Av1FrameDecoder frameDecoder = new(this.SequenceHeader, this.FrameHeader, this.FrameInfo, frameBuffer);
        frameDecoder.DecodeFrame();

        Image<TPixel>? resultImage = null;
        try
        {
            resultImage = new Image<TPixel>(
                this.configuration,
                this.FrameHeader.FrameSize.SuperResolutionUpscaledWidth,
                this.FrameHeader.FrameSize.FrameHeight,
                null);

            ImageFrame<TPixel> resultFrame = resultImage.Frames.RootFrame;
            Av1YuvConverter.ConvertToRgb(this.configuration, frameBuffer, resultFrame);
            return resultImage;
        }
        catch
        {
            resultImage?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Parses one entropy-coded tile payload into the current frame state.
    /// </summary>
    /// <param name="tileData">The entropy-coded tile payload.</param>
    /// <param name="tileNum">The raster-order tile index.</param>
    public void ReadTile(Span<byte> tileData, int tileNum)
    {
        if (this.tileReader is null)
        {
            this.SequenceHeader = this.obuReader.SequenceHeader;
            this.FrameHeader = this.obuReader.FrameHeader;
            Guard.NotNull(this.SequenceHeader, nameof(this.SequenceHeader));
            Guard.NotNull(this.FrameHeader, nameof(this.FrameHeader));

            // Every tile group in a frame contributes to the same mode-info and coefficient state.
            this.tileReader = new Av1TileReader(this.configuration, this.SequenceHeader, this.FrameHeader);
        }

        this.tileReader.ReadTile(tileData, tileNum);
    }
}
