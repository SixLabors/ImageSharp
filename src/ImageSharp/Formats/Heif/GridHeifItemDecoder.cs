// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decodes the image items referenced by a HEIF grid derived-image item.
/// </summary>
/// <typeparam name="TPixel">The destination pixel type.</typeparam>
internal sealed class GridHeifItemDecoder<TPixel> : IHeifItemDecoder<TPixel>, IHeifAlphaItemDecoder
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// The row-major tiles of the grid.
    /// </summary>
    private readonly HeifItem[] tiles;

    /// <summary>
    /// For each color tile, a value that is <see langword="true"/> if an alpha item of the tile premultiplies its color.
    /// </summary>
    private readonly bool[] tilePremultiplied;

    /// <summary>
    /// Reads one selected encoded item payload on demand.
    /// </summary>
    private readonly Func<HeifItem, IMemoryOwner<byte>> itemDataReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="GridHeifItemDecoder{TPixel}"/> class.
    /// </summary>
    /// <param name="tiles">
    /// The row-major tiles from the decode plan. <see cref="HeifItemGraphValidator"/> validated that they are AV1 items. It also validated that their
    /// codec configurations and spatial extents agree.
    /// </param>
    /// <param name="tilePremultiplied">
    /// For each color tile, a value that is <see langword="true"/> if an alpha item of the tile premultiplies its color. An alpha grid decoder does not
    /// use this array, and the caller gives an empty array.
    /// </param>
    /// <param name="itemDataReader">Reads one validated encoded image payload when the decoder needs it.</param>
    public GridHeifItemDecoder(HeifItem[] tiles, bool[] tilePremultiplied, Func<HeifItem, IMemoryOwner<byte>> itemDataReader)
    {
        this.tiles = tiles;
        this.tilePremultiplied = tilePremultiplied;
        this.itemDataReader = itemDataReader;
    }

    /// <summary>
    /// Gets the grid derived-image item type.
    /// </summary>
    public Heif4CharCode Type => Heif4CharCode.Grid;

    /// <inheritdoc/>
    public void DecodeItemData(
        DecoderOptions options,
        HeifChromaUpsampling chromaUpsampling,
        HeifItem gridItem,
        Span<byte> data,
        CicpProfile? colorProfile,
        IccProfile? profile,
        Av1FrameBuffer<byte>? alphaFrame,
        Size alphaOutputSize,
        Rectangle alphaRectangle,
        bool premultiplied,
        Rectangle sourceRectangle,
        HeifPixelTransform transform,
        Func<Buffer2DRegion<TPixel>> destination,
        ImageMetadata metadata,
        CancellationToken cancellationToken)
    {
        string? error = HeifGridDescriptor.Parse(data, out HeifGridDescriptor descriptor);
        if (error is not null)
        {
            throw new InvalidImageContentException(error);
        }

        // The image spatial extents property gives the size of the reconstructed image, before the clean aperture,
        // rotation, and mirror properties apply. The output width and height of the grid descriptor give the size of
        // the canvas on which the tiles are placed, which is the reconstructed grid image. The caller sizes the output
        // image from the spatial extents, so a larger extent would allocate pixels that no tile can fill. Reject it
        // before the output image is allocated. A smaller extent allocates no extra pixels, and the tiles are clipped
        // to it as before.
        if (gridItem.Extent.Width > descriptor.OutputSize.Width || gridItem.Extent.Height > descriptor.OutputSize.Height)
        {
            throw new InvalidImageContentException(
                $"HEIF image grid {gridItem.Id} declares spatial extents {gridItem.Extent.Width}x{gridItem.Extent.Height}, " +
                $"which exceed the output size {descriptor.OutputSize.Width}x{descriptor.OutputSize.Height} of its grid descriptor.");
        }

        // The method validates the layout and the placement of every tile before the destination allocates the canvas. Thus, a tile that the decoder
        // cannot place is a structural error. This error throws before the decoder allocates pixels.
        error = descriptor.Validate(this.tiles);
        if (error is not null)
        {
            throw new InvalidImageContentException(error);
        }

        // The item graph validator validated that every tile is an AV1 item. Thus, one decoder decodes all tiles. The canvas exists before the first
        // tile decodes. Thus, in the IgnoreImageData mode, a damaged tile payload that holds no frame writes no pixels. Its region keeps the fill value,
        // and the other tiles continue to decode.
        Size tileSize = this.tiles[0].Extent;
        Av1HeifItemDecoder<TPixel> decoder = new(true);
        Buffer2DRegion<TPixel> gridDestination = destination();
        for (int tileIndex = 0; tileIndex < this.tiles.Length; tileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HeifItem item = this.tiles[tileIndex];

            // Edge tiles retain their coded extent while conversion writes only the visible grid region.
            // The region refers to the final frame, so no decoded tile image or pixel copy is needed.
            Rectangle tileBounds = descriptor.GetTileBounds(tileSize, tileIndex);
            Rectangle visibleSource = Rectangle.Intersect(tileBounds, sourceRectangle);
            if (visibleSource.IsEmpty)
            {
                continue;
            }

            Rectangle relativeSource = new(visibleSource.X - sourceRectangle.X, visibleSource.Y - sourceRectangle.Y, visibleSource.Width, visibleSource.Height);
            Rectangle outputBounds = transform.TransformRectangle(relativeSource, sourceRectangle.Size);
            Buffer2DRegion<TPixel> tileDestination = gridDestination.GetSubRegion(outputBounds.X, outputBounds.Y, outputBounds.Width, outputBounds.Height);
            Rectangle tileSource = new(visibleSource.X - tileBounds.X, visibleSource.Y - tileBounds.Y, visibleSource.Width, visibleSource.Height);

            // A grid can link alpha to each coded tile instead of to the derived image. The decode plan finds the premultiplication of each tile. Thus,
            // the decoder removes the premultiplication from the RGB values of the tile before the ICC conversion.
            bool tilePremultiplied = premultiplied || (alphaFrame is not null && this.tilePremultiplied[tileIndex]);

            // The caller validated the extents of every tile before the canvas existed. The item data reader gives the bytes that a truncated tile holds.
            using IMemoryOwner<byte> itemMemory = this.itemDataReader(item);
            decoder.DecodeItemData(
                options,
                chromaUpsampling,
                item,
                itemMemory.GetSpan(),
                item.CicpProfile ?? colorProfile,
                profile,
                alphaFrame,
                alphaOutputSize,
                new Rectangle(alphaRectangle.X + relativeSource.X, alphaRectangle.Y + relativeSource.Y, relativeSource.Width, relativeSource.Height),
                tilePremultiplied,
                tileSource,
                transform,
                () => tileDestination,
                metadata,
                cancellationToken);
        }
    }

    /// <inheritdoc/>
    public Av1FrameBuffer<byte>? DecodeAlphaItemData(DecoderOptions options, HeifItem gridItem, Span<byte> data, CancellationToken cancellationToken)
    {
        // HeifItemGraphValidator.ValidateAlphaGrid validated the descriptor against the alpha tiles before the decode. Thus, the parse finds no error here.
        string? error = HeifGridDescriptor.Parse(data, out HeifGridDescriptor descriptor);
        DebugGuard.IsTrue(error is null, nameof(data), "The item graph validator validated the alpha grid descriptor.");

        Size tileSize = this.tiles[0].Extent;

        // The grid needs one assembled native alpha plane, because its tile boundaries can be different from the boundaries of the color grid. The
        // method releases each decoded tile after it copies the tile into the plane. The color image exists without the alpha plane. Thus, in the
        // IgnoreImageData mode, a damaged tile payload that holds no frame removes the full alpha grid, and the image is opaque.
        Av1HeifItemDecoder<TPixel> decoder = new(true);
        Av1FrameBuffer<byte>? result = null;
        try
        {
            for (int tileIndex = 0; tileIndex < this.tiles.Length; tileIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                HeifItem item = this.tiles[tileIndex];
                using IMemoryOwner<byte> itemMemory = this.itemDataReader(item);
                using Av1FrameBuffer<byte>? frame = decoder.DecodeAlphaItemData(options, item, itemMemory.GetSpan(), cancellationToken);
                if (frame is null)
                {
                    result?.Dispose();
                    return null;
                }

                // The first decoded tile sets the sample format of the plane.
                result ??= Av1FrameBuffer<byte>.CreateAuxiliary(options.Configuration, frame.ColorConfig, descriptor.OutputSize);
                CopyAlphaTile(options.Configuration, frame, result, item.Extent, descriptor, tileSize, tileIndex);
            }

            return result;
        }
        catch
        {
            result?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Writes one decoded auxiliary tile into the native grid plane.
    /// </summary>
    /// <param name="configuration">The configuration providing the normalized row buffer.</param>
    /// <param name="source">The decoded tile samples.</param>
    /// <param name="destination">The native grid plane.</param>
    /// <param name="extent">The tile's presentation extent.</param>
    /// <param name="descriptor">The validated grid layout.</param>
    /// <param name="tileSize">The nominal grid-cell size.</param>
    /// <param name="tileIndex">The tile's row-major index.</param>
    private static void CopyAlphaTile(
        Configuration configuration,
        Av1FrameBuffer<byte> source,
        Av1FrameBuffer<byte> destination,
        Size extent,
        in HeifGridDescriptor descriptor,
        Size tileSize,
        int tileIndex)
    {
        Rectangle tileBounds = descriptor.GetTileBounds(tileSize, tileIndex);
        using HeifAlphaRowSource rows = Av1YuvConverter.CreateAlphaRowSource(configuration, source, extent, new Rectangle(Point.Empty, tileBounds.Size));

        HeifColorConversionParameters parameters = Av1YuvConverter.GetConversionParameters(destination.ColorConfig, destination.ColorConfig.ColorRange, out _);
        if (destination.BitDepth == Av1BitDepth.EightBit)
        {
            Av1PlanarSampleBuffer<byte> buffer = new(destination);
            for (int y = 0; y < tileBounds.Height; y++)
            {
                HeifSampleConversion.WriteSamples<byte, HeifByteSampleConverter>(
                    rows.ReadRow(y),
                    buffer.GetLumaRowSpan(tileBounds.Y + y).Slice(tileBounds.X, tileBounds.Width),
                    parameters.LumaScale,
                    parameters.LumaBias,
                    parameters.LumaSampleMaximum);
            }

            return;
        }

        Av1PlanarSampleBuffer<ushort> highBitDepthBuffer = new(destination);
        for (int y = 0; y < tileBounds.Height; y++)
        {
            HeifSampleConversion.WriteSamples<ushort, HeifUShortSampleConverter>(
                rows.ReadRow(y),
                highBitDepthBuffer.GetLumaRowSpan(tileBounds.Y + y).Slice(tileBounds.X, tileBounds.Width),
                parameters.LumaScale,
                parameters.LumaBias,
                parameters.LumaSampleMaximum);
        }
    }
}
