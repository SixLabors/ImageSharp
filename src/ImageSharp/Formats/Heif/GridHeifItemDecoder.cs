// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decodes the image items referenced by a HEIF grid derived-image item.
/// </summary>
/// <typeparam name="TPixel">The destination pixel type.</typeparam>
internal class GridHeifItemDecoder<TPixel> : IHeifItemDecoder<TPixel>, IHeifAlphaItemDecoder<TPixel>
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
        GridDescriptor descriptor = ParseGridDescriptor(data);
        int rows = descriptor.Rows;
        int columns = descriptor.Columns;
        int outputWidth = descriptor.OutputSize.Width;
        int outputHeight = descriptor.OutputSize.Height;
        List<uint> linked = this.GetLinkedTileIds(gridItem, descriptor);

        // Each compressed tile decoder returns an owned Image. Keep every tile alive until
        // the final grid has copied its pixels, then dispose all intermediates together.
        using DisposableList<Image<TPixel>> gridTiles = new(linked.Count);
        Heif4CharCode tileType = default;
        Av1CodecConfiguration? av1GridConfiguration = null;
        foreach (uint id in linked)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HeifItem item = this.items.First(item => item.Id == id);
            ValidateTileConfiguration(item, ref tileType, ref av1GridConfiguration);

            IHeifItemDecoder<TPixel>? decoder = HeifCompressionFactory.GetDecoder<TPixel>(item.Type)
                ?? throw new ImageFormatException($"The HEIF image grid uses unsupported tile type '{item.Type}'.");

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

        Image<TPixel> result = new(options.Configuration, outputWidth, outputHeight, firstTile.Metadata.DeepClone());
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
            int copyWidth = Math.Min(tileWidth, outputWidth - destinationX);
            int copyHeight = Math.Min(tileHeight, outputHeight - destinationY);
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

    /// <inheritdoc/>
    public void DecodeAlphaItemData(
        DecoderOptions options,
        HeifItem gridItem,
        Span<byte> data,
        ImageFrame<TPixel> destination,
        Size outputSize,
        Rectangle destinationRectangle,
        bool premultiplied,
        CancellationToken cancellationToken)
    {
        GridDescriptor descriptor = ParseGridDescriptor(data);
        List<uint> linked = this.GetLinkedTileIds(gridItem, descriptor);
        Heif4CharCode tileType = default;
        Av1CodecConfiguration? av1GridConfiguration = null;
        Size tileSize = default;

        // Validate the complete grid before mutating the color frame. IgnoreImageData can then omit a failed alpha
        // grid without leaving a partially composed prefix in the returned image.
        foreach (uint id in linked)
        {
            HeifItem item = this.items.First(item => item.Id == id);
            ValidateTileConfiguration(item, ref tileType, ref av1GridConfiguration);
            if (HeifCompressionFactory.GetDecoder<TPixel>(item.Type) is not IHeifAlphaItemDecoder<TPixel>)
            {
                throw new ImageFormatException($"The HEIF alpha grid uses unsupported tile type '{item.Type}'.");
            }

            if (!this.buffers.ContainsKey(item.Id))
            {
                throw new InvalidImageContentException($"HEIF alpha grid tile {item.Id} has no data extents.");
            }

            if (item.Extent == default)
            {
                throw new InvalidImageContentException($"HEIF alpha grid tile {item.Id} has no spatial extent.");
            }

            if (tileSize == default)
            {
                tileSize = item.Extent;
            }
            else if (item.Extent != tileSize)
            {
                throw new InvalidImageContentException("The HEIF alpha grid contains tiles with mismatched dimensions.");
            }
        }

        int gridWidth = descriptor.OutputSize.Width;
        int gridHeight = descriptor.OutputSize.Height;
        if (((long)tileSize.Width * descriptor.Columns) < gridWidth || ((long)tileSize.Height * descriptor.Rows) < gridHeight)
        {
            throw new InvalidImageContentException("The HEIF alpha grid tiles do not cover the output canvas.");
        }

        if (((long)tileSize.Width * (descriptor.Columns - 1)) >= gridWidth ||
            ((long)tileSize.Height * (descriptor.Rows - 1)) >= gridHeight)
        {
            throw new InvalidImageContentException("The HEIF alpha grid edge tiles do not overlap the output canvas.");
        }

        if (descriptor.OutputSize != outputSize || destinationRectangle.Size != outputSize)
        {
            throw new InvalidImageContentException("The HEIF alpha grid dimensions do not match the color grid dimensions.");
        }

        for (int tileIndex = 0; tileIndex < linked.Count; tileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HeifItem item = this.items.First(item => item.Id == linked[tileIndex]);
            IHeifAlphaItemDecoder<TPixel> decoder = (IHeifAlphaItemDecoder<TPixel>)HeifCompressionFactory.GetDecoder<TPixel>(item.Type)!;
            IMemoryOwner<byte> itemMemory = this.buffers[item.Id];
            int column = tileIndex % descriptor.Columns;
            int row = tileIndex / descriptor.Columns;
            int destinationX = destinationRectangle.X + (column * tileSize.Width);
            int destinationY = destinationRectangle.Y + (row * tileSize.Height);
            int copyWidth = Math.Min(tileSize.Width, destinationRectangle.Right - destinationX);
            int copyHeight = Math.Min(tileSize.Height, destinationRectangle.Bottom - destinationY);
            Rectangle tileDestination = new(destinationX, destinationY, copyWidth, copyHeight);

            decoder.DecodeAlphaItemData(
                options,
                item,
                itemMemory.GetSpan(),
                destination,
                tileSize,
                tileDestination,
                premultiplied,
                cancellationToken);
        }
    }

    /// <summary>
    /// Parses and validates the fixed HEIF image-grid descriptor fields used by both color and alpha composition.
    /// </summary>
    /// <param name="data">The complete image-grid descriptor payload.</param>
    /// <returns>The validated row, column, and output dimensions.</returns>
    private static GridDescriptor ParseGridDescriptor(ReadOnlySpan<byte> data)
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

        bool usesLargeDimensions = (data[1] & 1) != 0;
        int descriptorLength = usesLargeDimensions ? 12 : 8;
        if (data.Length != descriptorLength)
        {
            throw new InvalidImageContentException("The HEIF image grid descriptor has an invalid length.");
        }

        uint outputWidth = usesLargeDimensions
            ? BinaryPrimitives.ReadUInt32BigEndian(data[4..])
            : BinaryPrimitives.ReadUInt16BigEndian(data[4..]);

        uint outputHeight = usesLargeDimensions
            ? BinaryPrimitives.ReadUInt32BigEndian(data[8..])
            : BinaryPrimitives.ReadUInt16BigEndian(data[6..]);

        if (outputWidth is 0 or > int.MaxValue || outputHeight is 0 or > int.MaxValue)
        {
            throw new InvalidImageContentException("The HEIF image grid descriptor has invalid output dimensions.");
        }

        return new GridDescriptor(data[2] + 1, data[3] + 1, new Size((int)outputWidth, (int)outputHeight));
    }

    /// <summary>
    /// Resolves and validates the row-major tile identifiers for a grid descriptor.
    /// </summary>
    /// <param name="gridItem">The grid item whose derived-image references are being resolved.</param>
    /// <param name="descriptor">The validated grid dimensions.</param>
    /// <returns>The exact row-major tile identifiers required by the descriptor.</returns>
    private List<uint> GetLinkedTileIds(HeifItem gridItem, in GridDescriptor descriptor)
    {
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

        int tileCount = descriptor.Rows * descriptor.Columns;
        if (linked.Count != tileCount)
        {
            string message = $"The HEIF image grid requires {tileCount} tiles, but its derived-image references contain {linked.Count}.";
            throw new InvalidImageContentException(message);
        }

        return linked;
    }

    /// <summary>
    /// Validates the coding format and common AV1 sample layout of one grid tile.
    /// </summary>
    /// <param name="item">The coded grid tile being validated.</param>
    /// <param name="tileType">The common coding type established by the first grid tile.</param>
    /// <param name="av1GridConfiguration">The common AV1 sample layout established by the first AV1 grid tile.</param>
    private static void ValidateTileConfiguration(
        HeifItem item,
        ref Heif4CharCode tileType,
        ref Av1CodecConfiguration? av1GridConfiguration)
    {
        if (tileType == default)
        {
            tileType = item.Type;
        }
        else if (item.Type != tileType)
        {
            throw new InvalidImageContentException("All HEIF image grid tiles must use the same coding format.");
        }

        if (item.Type != Heif4CharCode.Av01)
        {
            return;
        }

        Av1CodecConfiguration itemConfiguration = item.Av1CodecConfiguration
            ?? throw new InvalidImageContentException($"AV1 image grid tile {item.Id} has no codec configuration property.");

        if (av1GridConfiguration is null)
        {
            av1GridConfiguration = itemConfiguration;
        }
        else if (!av1GridConfiguration.HasMatchingImageConfiguration(itemConfiguration))
        {
            // All grid cells share one output sample layout. Reject differing AV1 descriptions before allocating
            // or composing tiles so channel precision and chroma geometry cannot change between cells.
            throw new InvalidImageContentException("All AV1 image grid tiles must use matching codec configurations.");
        }
    }

    /// <summary>
    /// Contains the bounded row, column, and output dimensions from one image-grid descriptor.
    /// </summary>
    private readonly struct GridDescriptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GridDescriptor"/> struct.
        /// </summary>
        /// <param name="rows">The number of grid rows.</param>
        /// <param name="columns">The number of grid columns.</param>
        /// <param name="outputSize">The output canvas dimensions.</param>
        public GridDescriptor(int rows, int columns, Size outputSize)
        {
            this.Rows = rows;
            this.Columns = columns;
            this.OutputSize = outputSize;
        }

        /// <summary>
        /// Gets the number of grid rows.
        /// </summary>
        public int Rows { get; }

        /// <summary>
        /// Gets the number of grid columns.
        /// </summary>
        public int Columns { get; }

        /// <summary>
        /// Gets the output canvas dimensions.
        /// </summary>
        public Size OutputSize { get; }
    }
}
