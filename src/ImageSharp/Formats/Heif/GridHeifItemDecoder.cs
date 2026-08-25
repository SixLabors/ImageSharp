// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decodes the image items referenced by a HEIF grid derived-image item.
/// </summary>
/// <typeparam name="TPixel">The destination pixel type.</typeparam>
internal class GridHeifItemDecoder<TPixel> : IHeifItemDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// The item definitions available to the grid.
    /// </summary>
    private readonly IList<HeifItem> items;

    /// <summary>
    /// The item-reference relationships used to locate the grid's tiles.
    /// </summary>
    private readonly IList<HeifItemLink> itemLinks;

    /// <summary>
    /// The assembled encoded payload for each referenced image item.
    /// </summary>
    private readonly IDictionary<uint, IMemoryOwner<byte>> buffers;

    /// <summary>
    /// The optional row-major tile identifiers supplied for an auxiliary grid plane.
    /// </summary>
    private readonly IReadOnlyList<uint>? tileItemIds;

    /// <summary>
    /// Initializes a new instance of the <see cref="GridHeifItemDecoder{TPixel}"/> class.
    /// </summary>
    /// <param name="items">The item definitions in the containing HEIF file.</param>
    /// <param name="itemLinks">The item-reference relationships in the containing HEIF file.</param>
    /// <param name="buffers">The assembled encoded payload for each image item.</param>
    /// <param name="tileItemIds">
    /// Optional row-major tile identifiers that replace the grid item's own derived-image references.
    /// </param>
    public GridHeifItemDecoder(
        IList<HeifItem> items,
        IList<HeifItemLink> itemLinks,
        IDictionary<uint, IMemoryOwner<byte>> buffers,
        IReadOnlyList<uint>? tileItemIds = null)
    {
        this.items = items;
        this.itemLinks = itemLinks;
        this.buffers = buffers;
        this.tileItemIds = tileItemIds;
    }

    /// <summary>
    /// Gets the grid derived-image item type.
    /// </summary>
    public Heif4CharCode Type => Heif4CharCode.Grid;

    /// <summary>
    /// Gets the compression method used by the decoded grid tiles.
    /// </summary>
    public HeifCompressionMethod CompressionMethod { get; private set; }

    /// <summary>
    /// Decodes the tiles referenced by a grid derived-image item.
    /// </summary>
    /// <param name="options">The general options governing the containing HEIF decode.</param>
    /// <param name="gridItem">The grid derived-image item.</param>
    /// <param name="data">The grid descriptor payload.</param>
    /// <param name="colorProfile">The container color description inherited by tiles that do not declare one.</param>
    /// <param name="cancellationToken">The token used to cancel between tile payloads.</param>
    /// <returns>The image reconstructed from the referenced grid tiles.</returns>
    public Image<TPixel> DecodeItemData(
        DecoderOptions options,
        HeifItem gridItem,
        Span<byte> data,
        CicpProfile? colorProfile,
        CancellationToken cancellationToken)
    {
        if (data.Length < 8)
        {
            throw new InvalidImageContentException("The HEIF image grid descriptor is truncated.");
        }

        byte version = data[0];
        if (version != 0)
        {
            throw new InvalidImageContentException($"The HEIF image grid descriptor has unsupported version {version}.");
        }

        byte flags = data[1];
        int rows = data[2] + 1;
        int columns = data[3] + 1;
        bool usesLargeDimensions = (flags & 1) != 0;
        int descriptorLength = usesLargeDimensions ? 12 : 8;
        if (data.Length != descriptorLength)
        {
            throw new InvalidImageContentException("The HEIF image grid descriptor has an invalid length.");
        }

        uint outputWidth;
        uint outputHeight;
        if (usesLargeDimensions)
        {
            outputWidth = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
            outputHeight = BinaryPrimitives.ReadUInt32BigEndian(data[8..]);
        }
        else
        {
            outputWidth = BinaryPrimitives.ReadUInt16BigEndian(data[4..]);
            outputHeight = BinaryPrimitives.ReadUInt16BigEndian(data[6..]);
        }

        if (outputWidth is 0 or > int.MaxValue || outputHeight is 0 or > int.MaxValue)
        {
            throw new InvalidImageContentException("The HEIF image grid descriptor has invalid output dimensions.");
        }

        List<uint> linked = this.tileItemIds is null ? [] : new(this.tileItemIds);
        if (this.tileItemIds is null)
        {
            foreach (HeifItemLink link in this.itemLinks)
            {
                if (link.Type == Heif4CharCode.Dimg && link.SourceId == gridItem.Id)
                {
                    // The order of dimg destinations is the normative row-major order of the grid cells.
                    linked.AddRange(link.DestinationIds);
                }
            }
        }

        int tileCount = rows * columns;
        if (linked.Count != tileCount)
        {
            string message = $"The HEIF image grid requires {tileCount} tiles, but its derived-image references contain {linked.Count}.";
            throw new InvalidImageContentException(message);
        }

        // Each compressed tile decoder returns an owned Image. Keep every tile alive until
        // the final grid has copied its pixels, then dispose all intermediates together.
        using DisposableList<Image<TPixel>> gridTiles = new(linked.Count);
        Heif4CharCode tileType = default;
        Av1CodecConfiguration? av1GridConfiguration = null;
        foreach (uint id in linked)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HeifItem item = this.items.First(item => item.Id == id);
            if (tileType == default)
            {
                tileType = item.Type;
            }
            else if (item.Type != tileType)
            {
                throw new InvalidImageContentException("All HEIF image grid tiles must use the same coding format.");
            }

            if (item.Type == Heif4CharCode.Av01)
            {
                Av1CodecConfiguration itemConfiguration = item.Av1CodecConfiguration
                    ?? throw new InvalidImageContentException($"AV1 image grid tile {item.Id} has no codec configuration property.");

                if (av1GridConfiguration is null)
                {
                    av1GridConfiguration = itemConfiguration;
                }
                else if (!av1GridConfiguration.HasMatchingImageConfiguration(itemConfiguration))
                {
                    // All grid cells share one output sample layout. Reject differing AV1 descriptions before
                    // allocating and copying tiles so channel precision or chroma geometry cannot change by cell.
                    throw new InvalidImageContentException("All AV1 image grid tiles must use matching codec configurations.");
                }
            }

            IHeifItemDecoder<TPixel>? decoder = HeifCompressionFactory.GetDecoder<TPixel>(item.Type);
            if (decoder is null)
            {
                throw new ImageFormatException($"The HEIF image grid uses unsupported tile type '{item.Type}'.");
            }

            if (!this.buffers.TryGetValue(item.Id, out IMemoryOwner<byte>? itemMemory))
            {
                throw new InvalidImageContentException($"HEIF image grid tile {item.Id} has no data extents.");
            }

            this.CompressionMethod = decoder.CompressionMethod;
            Image<TPixel> tile = decoder.DecodeItemData(
                options,
                item,
                itemMemory.GetSpan(),
                item.CicpProfile ?? colorProfile,
                cancellationToken);

            try
            {
                HeifItemDecoderUtilities.ScaleToItemExtent(tile, item);
                gridTiles.Add(tile);
            }
            catch
            {
                tile.Dispose();
                throw;
            }
        }

        Image<TPixel> firstTile = gridTiles[0];
        int tileWidth = firstTile.Width;
        int tileHeight = firstTile.Height;
        if (((long)tileWidth * columns) < outputWidth || ((long)tileHeight * rows) < outputHeight)
        {
            throw new InvalidImageContentException("The HEIF image grid tiles do not cover the output canvas.");
        }

        if (((long)tileWidth * (columns - 1)) >= outputWidth || ((long)tileHeight * (rows - 1)) >= outputHeight)
        {
            throw new InvalidImageContentException("The HEIF image grid edge tiles do not overlap the output canvas.");
        }

        Image<TPixel> result = new(options.Configuration, (int)outputWidth, (int)outputHeight, firstTile.Metadata.DeepClone());
        ImageFrame<TPixel> destination = result.Frames.RootFrame;
        for (int tileIndex = 0; tileIndex < gridTiles.Count; tileIndex++)
        {
            Image<TPixel> tile = gridTiles[tileIndex];
            if (tile.Width != tileWidth || tile.Height != tileHeight)
            {
                result.Dispose();
                throw new InvalidImageContentException("The HEIF image grid contains tiles with mismatched dimensions.");
            }

            int column = tileIndex % columns;
            int row = tileIndex / columns;
            int destinationX = column * tileWidth;
            int destinationY = row * tileHeight;
            int copyWidth = Math.Min(tileWidth, (int)outputWidth - destinationX);
            int copyHeight = Math.Min(tileHeight, (int)outputHeight - destinationY);
            ImageFrame<TPixel> source = tile.Frames.RootFrame;

            // The descriptor may crop only the rightmost column and bottom row. Copying bounded row spans
            // applies that crop without allocating derived-image views or invoking the processing pipeline.
            for (int y = 0; y < copyHeight; y++)
            {
                Span<TPixel> destinationRow = destination.PixelBuffer.DangerousGetRowSpan(destinationY + y).Slice(destinationX, copyWidth);
                source.PixelBuffer.DangerousGetRowSpan(y)[..copyWidth].CopyTo(destinationRow);
            }
        }

        return result;
    }
}
