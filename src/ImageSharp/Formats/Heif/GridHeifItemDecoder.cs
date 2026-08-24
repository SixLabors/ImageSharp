// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
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
    /// The configuration used to decode each compressed grid tile.
    /// </summary>
    private readonly Configuration configuration;

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
    /// Initializes a new instance of the <see cref="GridHeifItemDecoder{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration used to decode compressed grid tiles.</param>
    /// <param name="items">The item definitions in the containing HEIF file.</param>
    /// <param name="itemLinks">The item-reference relationships in the containing HEIF file.</param>
    /// <param name="buffers">The assembled encoded payload for each image item.</param>
    public GridHeifItemDecoder(Configuration configuration, IList<HeifItem> items, IList<HeifItemLink> itemLinks, IDictionary<uint, IMemoryOwner<byte>> buffers)
    {
        this.configuration = configuration;
        this.items = items;
        this.itemLinks = itemLinks;
        this.buffers = buffers;
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
    /// <param name="configuration">The configuration associated with the containing HEIF decode.</param>
    /// <param name="gridItem">The grid derived-image item.</param>
    /// <param name="data">The grid descriptor payload.</param>
    /// <returns>The image reconstructed from the referenced grid tiles.</returns>
    public Image<TPixel> DecodeItemData(Configuration configuration, HeifItem gridItem, Span<byte> data)
    {
        List<uint> linked = this.itemLinks.First(
            link => link.Type == Heif4CharCode.Dimg && link.SourceId == gridItem.Id).DestinationIds;

        // Each compressed tile decoder returns an owned Image. Keep every tile alive until
        // the final grid has copied its pixels, then dispose all intermediates together.
        using DisposableList<Image<TPixel>> gridTiles = new(linked.Count);
        foreach (uint id in linked)
        {
            HeifItem? item = this.items.FirstOrDefault(item => item.Id == id);
            if (item is not null)
            {
                IHeifItemDecoder<TPixel>? decoder = HeifCompressionFactory.GetDecoder<TPixel>(item.Type);
                if (decoder is not null)
                {
                    this.CompressionMethod = decoder.CompressionMethod;
                    IMemoryOwner<byte> itemMemory = this.buffers[item.Id];
                    gridTiles.Add(decoder.DecodeItemData(this.configuration, item, itemMemory.GetSpan()));
                }
            }
        }

        if (gridTiles.Count == 0)
        {
            return new Image<TPixel>(1, 1);
        }

        // TODO: Combine grid tiles into a single image.
        return new Image<TPixel>(1, 1);
    }
}
