// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
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
internal sealed class GridHeifItemDecoder<TPixel> : IHeifItemDecoder<TPixel>, IHeifAlphaItemDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// The image-grid descriptor version defined by HEIF.
    /// </summary>
    private const byte GridDescriptorVersion = 0;

    /// <summary>
    /// The descriptor flag that selects 32-bit output dimensions instead of 16-bit dimensions.
    /// </summary>
    private const byte LargeDimensionsFlag = 1;

    /// <summary>
    /// The descriptor length when output dimensions use 16-bit fields.
    /// </summary>
    private const int ShortGridDescriptorLength = 8;

    /// <summary>
    /// The descriptor length when output dimensions use 32-bit fields.
    /// </summary>
    private const int LongGridDescriptorLength = 12;

    /// <summary>
    /// The minimum width and height of the first cell in a MIAF image grid.
    /// </summary>
    private const int MinimumGridCellDimension = 64;

    /// <summary>
    /// The item definitions available to the grid, indexed by item identifier.
    /// </summary>
    private readonly Dictionary<uint, HeifItem> items;

    /// <summary>
    /// The item-reference relationships used to locate the grid's tiles.
    /// </summary>
    private readonly IList<HeifItemLink> itemLinks;

    /// <summary>
    /// Reads one selected encoded item payload on demand.
    /// </summary>
    private readonly Func<HeifItem, IMemoryOwner<byte>> itemDataReader;

    /// <summary>
    /// The optional row-major tile identifiers supplied for an auxiliary grid plane.
    /// </summary>
    private readonly IReadOnlyList<uint>? tileItemIds;

    /// <summary>
    /// Initializes a new instance of the <see cref="GridHeifItemDecoder{TPixel}"/> class.
    /// </summary>
    /// <param name="items">The item definitions in the containing HEIF file.</param>
    /// <param name="itemLinks">The item-reference relationships in the containing HEIF file.</param>
    /// <param name="itemDataReader">Reads one selected encoded image payload on demand.</param>
    /// <param name="tileItemIds">
    /// Optional row-major tile identifiers that replace the grid item's own derived-image references.
    /// </param>
    public GridHeifItemDecoder(
        IList<HeifItem> items,
        IList<HeifItemLink> itemLinks,
        Func<HeifItem, IMemoryOwner<byte>> itemDataReader,
        IReadOnlyList<uint>? tileItemIds = null)
    {
        Dictionary<uint, HeifItem> itemLookup = new(items.Count);
        foreach (HeifItem item in items)
        {
            itemLookup.Add(item.Id, item);
        }

        this.items = itemLookup;
        this.itemLinks = itemLinks;
        this.itemDataReader = itemDataReader;
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
        IReadOnlyList<uint> linked = this.GetLinkedTileIds(gridItem, descriptor);

        Heif4CharCode tileType = default;
        Av1CodecConfiguration? av1GridConfiguration = null;
        Image<TPixel> result = this.CreateGridResult(
            options,
            descriptor,
            linked[0],
            colorProfile,
            ref tileType,
            ref av1GridConfiguration,
            cancellationToken,
            out int tileWidth,
            out int tileHeight);

        try
        {
            for (int tileIndex = 1; tileIndex < linked.Count; tileIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                HeifItem item = this.items[linked[tileIndex]];
                using Image<TPixel> tile = this.DecodeGridTile(
                    options,
                    item,
                    colorProfile,
                    ref tileType,
                    ref av1GridConfiguration,
                    cancellationToken);

                Size copySize = GetGridTileCopySize(
                    descriptor,
                    tileWidth,
                    tileHeight,
                    tileIndex);

                if (!IsGridTileExtentValid(tile.Size, copySize, tileWidth, tileHeight))
                {
                    throw new InvalidImageContentException(
                        $"HEIF image grid tile {item.Id} has dimensions {tile.Size}, which cannot cover its {copySize} grid region.");
                }

                CopyGridTile(tile, result, descriptor, tileIndex, tileWidth, tileHeight);
            }

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Decodes the first validated grid tile, establishes the common tile geometry, and creates the output canvas.
    /// </summary>
    private Image<TPixel> CreateGridResult(
        DecoderOptions options,
        in GridDescriptor descriptor,
        uint firstTileId,
        CicpProfile? colorProfile,
        ref Heif4CharCode tileType,
        ref Av1CodecConfiguration? av1GridConfiguration,
        CancellationToken cancellationToken,
        out int tileWidth,
        out int tileHeight)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HeifItem item = this.items[firstTileId];
        using Image<TPixel> tile = this.DecodeGridTile(
            options,
            item,
            colorProfile,
            ref tileType,
            ref av1GridConfiguration,
            cancellationToken);

        tileWidth = tile.Width;
        tileHeight = tile.Height;
        ValidateGridCoverage(descriptor, tileWidth, tileHeight);
        ValidateGridDimensions(descriptor, tile.Size, av1GridConfiguration);
        Size copySize = GetGridTileCopySize(descriptor, tileWidth, tileHeight, 0);
        if (!IsGridTileExtentValid(tile.Size, copySize, tileWidth, tileHeight))
        {
            throw new InvalidImageContentException(
                $"HEIF image grid tile {item.Id} has dimensions {tile.Size}, which cannot cover its {copySize} grid region.");
        }

        Image<TPixel> result = new(
            options.Configuration,
            descriptor.OutputSize.Width,
            descriptor.OutputSize.Height,
            tile.Metadata.DeepClone());

        try
        {
            CopyGridTile(tile, result, descriptor, 0, tileWidth, tileHeight);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Decodes and scales one grid tile while its encoded payload owner is active.
    /// </summary>
    private Image<TPixel> DecodeGridTile(
        DecoderOptions options,
        HeifItem item,
        CicpProfile? colorProfile,
        ref Heif4CharCode tileType,
        ref Av1CodecConfiguration? av1GridConfiguration,
        CancellationToken cancellationToken)
    {
        ValidateTileConfiguration(item, ref tileType, ref av1GridConfiguration);
        IHeifItemDecoder<TPixel>? decoder = HeifCompressionFactory.GetDecoder<TPixel>(item.Type)
            ?? throw new ImageFormatException($"The HEIF image grid uses unsupported tile type '{item.Type}'.");

        using IMemoryOwner<byte> itemMemory = this.itemDataReader(item);
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
            return tile;
        }
        catch
        {
            // Ownership transfers to the caller only after extent normalization succeeds.
            tile.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Copies one decoded tile into its cropped row-major grid position.
    /// </summary>
    private static void CopyGridTile(
        Image<TPixel> tile,
        Image<TPixel> result,
        in GridDescriptor descriptor,
        int tileIndex,
        int tileWidth,
        int tileHeight)
    {
        int column = tileIndex % descriptor.Columns;
        int row = tileIndex / descriptor.Columns;
        int destinationX = column * tileWidth;
        int destinationY = row * tileHeight;
        int copyWidth = Math.Min(tileWidth, descriptor.OutputSize.Width - destinationX);
        int copyHeight = Math.Min(tileHeight, descriptor.OutputSize.Height - destinationY);
        ImageFrame<TPixel> source = tile.Frames.RootFrame;
        ImageFrame<TPixel> destination = result.Frames.RootFrame;

        // Copy before disposing this decoded tile, keeping peak tile storage independent of grid cell count.
        // The descriptor may crop only the rightmost column and bottom row.
        for (int y = 0; y < copyHeight; y++)
        {
            Span<TPixel> destinationRow = destination.PixelBuffer
                .DangerousGetRowSpan(destinationY + y)
                .Slice(destinationX, copyWidth);

            source.PixelBuffer.DangerousGetRowSpan(y)[..copyWidth].CopyTo(destinationRow);
        }
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
        IReadOnlyList<uint> linked = this.GetLinkedTileIds(gridItem, descriptor);
        Heif4CharCode tileType = default;
        Av1CodecConfiguration? av1GridConfiguration = null;
        HeifItem firstItem = this.items[linked[0]];
        if (firstItem.Extent == default)
        {
            throw new InvalidImageContentException($"HEIF alpha grid tile {firstItem.Id} has no spatial extent.");
        }

        Size tileSize = firstItem.Extent;
        ValidateGridCoverage(descriptor, tileSize.Width, tileSize.Height);

        // Validate the complete grid before mutating the color frame. IgnoreImageData can then omit a failed alpha
        // grid without leaving a partially composed prefix in the returned image.
        for (int tileIndex = 0; tileIndex < linked.Count; tileIndex++)
        {
            uint id = linked[tileIndex];
            HeifItem item = this.items[id];
            ValidateTileConfiguration(item, ref tileType, ref av1GridConfiguration);
            if (HeifCompressionFactory.GetDecoder<TPixel>(item.Type) is not IHeifAlphaItemDecoder<TPixel>)
            {
                throw new ImageFormatException($"The HEIF alpha grid uses unsupported tile type '{item.Type}'.");
            }

            if (item.Extent == default)
            {
                throw new InvalidImageContentException($"HEIF alpha grid tile {item.Id} has no spatial extent.");
            }

            Size copySize = GetGridTileCopySize(
                descriptor,
                tileSize.Width,
                tileSize.Height,
                tileIndex);

            if (!IsGridTileExtentValid(item.Extent, copySize, tileSize.Width, tileSize.Height))
            {
                throw new InvalidImageContentException(
                    $"HEIF alpha grid tile {item.Id} has dimensions {item.Extent}, which cannot cover its {copySize} grid region.");
            }
        }

        ValidateGridDimensions(descriptor, tileSize, av1GridConfiguration);

        int gridWidth = descriptor.OutputSize.Width;
        int gridHeight = descriptor.OutputSize.Height;
        if (descriptor.OutputSize != outputSize || destinationRectangle.Size != outputSize)
        {
            throw new InvalidImageContentException("The HEIF alpha grid dimensions do not match the color grid dimensions.");
        }

        for (int tileIndex = 0; tileIndex < linked.Count; tileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HeifItem item = this.items[linked[tileIndex]];
            IHeifItemDecoder<TPixel>? itemDecoder = HeifCompressionFactory.GetDecoder<TPixel>(item.Type);
            if (itemDecoder is not IHeifAlphaItemDecoder<TPixel> decoder)
            {
                throw new InvalidImageContentException($"HEIF alpha grid tile {item.Id} uses unsupported coding format '{item.Type}'.");
            }

            using IMemoryOwner<byte> itemMemory = this.itemDataReader(item);
            int column = tileIndex % descriptor.Columns;
            int row = tileIndex / descriptor.Columns;
            int destinationX = destinationRectangle.X + (column * tileSize.Width);
            int destinationY = destinationRectangle.Y + (row * tileSize.Height);
            Size copySize = GetGridTileCopySize(
                descriptor,
                tileSize.Width,
                tileSize.Height,
                tileIndex);

            Rectangle tileDestination = new(destinationX, destinationY, copySize.Width, copySize.Height);

            decoder.DecodeAlphaItemData(
                options,
                item,
                itemMemory.GetSpan(),
                destination,
                item.Extent,
                tileDestination,
                premultiplied,
                cancellationToken);
        }
    }

    /// <summary>
    /// Validates that the first cell dimensions cover the grid while leaving a nonempty final row and column.
    /// </summary>
    private static void ValidateGridCoverage(in GridDescriptor descriptor, int tileWidth, int tileHeight)
    {
        if (((long)tileWidth * descriptor.Columns) < descriptor.OutputSize.Width ||
            ((long)tileHeight * descriptor.Rows) < descriptor.OutputSize.Height)
        {
            throw new InvalidImageContentException("The HEIF image grid tiles do not cover the output canvas.");
        }

        if (((long)tileWidth * (descriptor.Columns - 1)) >= descriptor.OutputSize.Width ||
            ((long)tileHeight * (descriptor.Rows - 1)) >= descriptor.OutputSize.Height)
        {
            throw new InvalidImageContentException("The HEIF image grid edge tiles do not overlap the output canvas.");
        }
    }

    /// <summary>
    /// Gets the portion of one cell that overlaps the output canvas.
    /// </summary>
    private static Size GetGridTileCopySize(
        in GridDescriptor descriptor,
        int tileWidth,
        int tileHeight,
        int tileIndex)
    {
        int column = tileIndex % descriptor.Columns;
        int row = tileIndex / descriptor.Columns;
        int copyWidth = column == descriptor.Columns - 1
            ? descriptor.OutputSize.Width - (tileWidth * (descriptor.Columns - 1))
            : tileWidth;

        int copyHeight = row == descriptor.Rows - 1
            ? descriptor.OutputSize.Height - (tileHeight * (descriptor.Rows - 1))
            : tileHeight;

        return new Size(copyWidth, copyHeight);
    }

    /// <summary>
    /// Determines whether a cell can cover its output region without exceeding the first cell's dimensions.
    /// </summary>
    private static bool IsGridTileExtentValid(Size extent, Size copySize, int tileWidth, int tileHeight)
        => extent.Width >= copySize.Width
            && extent.Width <= tileWidth
            && extent.Height >= copySize.Height
            && extent.Height <= tileHeight;

    /// <summary>
    /// Validates the MIAF cell-size and chroma-alignment rules established by the first grid cell.
    /// </summary>
    private static void ValidateGridDimensions(
        in GridDescriptor descriptor,
        Size tileSize,
        Av1CodecConfiguration? av1GridConfiguration)
    {
        if (tileSize.Width < MinimumGridCellDimension || tileSize.Height < MinimumGridCellDimension)
        {
            throw new InvalidImageContentException(
                $"HEIF image grid cells must be at least {MinimumGridCellDimension} samples wide and high.");
        }

        if (av1GridConfiguration is null || av1GridConfiguration.IsMonochrome)
        {
            return;
        }

        if (av1GridConfiguration.ChromaSubsamplingX &&
            (((descriptor.OutputSize.Width & 1) != 0) || ((tileSize.Width & 1) != 0)))
        {
            throw new InvalidImageContentException(
                "HEIF image grid widths must be even when AV1 chroma is horizontally subsampled.");
        }

        if (av1GridConfiguration.ChromaSubsamplingY &&
            (((descriptor.OutputSize.Height & 1) != 0) || ((tileSize.Height & 1) != 0)))
        {
            throw new InvalidImageContentException(
                "HEIF image grid heights must be even when AV1 chroma is vertically subsampled.");
        }
    }

    /// <summary>
    /// Parses and validates the fixed HEIF image-grid descriptor fields used by both color and alpha composition.
    /// </summary>
    /// <param name="data">The complete image-grid descriptor payload.</param>
    /// <returns>The validated row, column, and output dimensions.</returns>
    private static GridDescriptor ParseGridDescriptor(ReadOnlySpan<byte> data)
    {
        if (data.Length < ShortGridDescriptorLength)
        {
            throw new InvalidImageContentException("The HEIF image grid descriptor is truncated.");
        }

        int offset = 0;
        byte version = data[offset++];
        if (version != GridDescriptorVersion)
        {
            throw new InvalidImageContentException($"The HEIF image grid descriptor has unsupported version {version}.");
        }

        byte flags = data[offset++];
        bool usesLargeDimensions = (flags & LargeDimensionsFlag) != 0;
        int descriptorLength = usesLargeDimensions ? LongGridDescriptorLength : ShortGridDescriptorLength;
        if (data.Length != descriptorLength)
        {
            throw new InvalidImageContentException("The HEIF image grid descriptor has an invalid length.");
        }

        int rows = data[offset++] + 1;
        int columns = data[offset++] + 1;
        uint outputWidth = usesLargeDimensions
            ? BinaryPrimitives.ReadUInt32BigEndian(data[offset..])
            : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

        offset += usesLargeDimensions ? sizeof(uint) : sizeof(ushort);
        uint outputHeight = usesLargeDimensions
            ? BinaryPrimitives.ReadUInt32BigEndian(data[offset..])
            : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

        if (outputWidth is 0 or > int.MaxValue || outputHeight is 0 or > int.MaxValue)
        {
            throw new InvalidImageContentException("The HEIF image grid descriptor has invalid output dimensions.");
        }

        return new GridDescriptor(rows, columns, new Size((int)outputWidth, (int)outputHeight));
    }

    /// <summary>
    /// Resolves and validates the row-major tile identifiers for a grid descriptor.
    /// </summary>
    /// <param name="gridItem">The grid item whose derived-image references are being resolved.</param>
    /// <param name="descriptor">The validated grid dimensions.</param>
    /// <returns>The exact row-major tile identifiers required by the descriptor.</returns>
    private IReadOnlyList<uint> GetLinkedTileIds(HeifItem gridItem, in GridDescriptor descriptor)
    {
        IReadOnlyList<uint> linked;
        if (this.tileItemIds is not null)
        {
            // Auxiliary grids already own an immutable row-major identifier list; no defensive list copy is needed.
            linked = this.tileItemIds;
        }
        else
        {
            List<uint> resolved = [];
            foreach (HeifItemLink link in this.itemLinks)
            {
                if (link.Type == Heif4CharCode.Dimg && link.SourceId == gridItem.Id)
                {
                    // The order of dimg destinations is the normative row-major order of the grid cells.
                    resolved.AddRange(link.DestinationIds);
                }
            }

            linked = resolved;
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
