// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decoder for a grid of several <see cref="HeifItem"/> into a single image.
/// </summary>
internal class GridHeifItemDecoder<TPixel> : IHeifItemDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly IList<HeifItem> items;
    private readonly IList<HeifItemLink> itemLinks;
    private readonly IDictionary<uint, IMemoryOwner<byte>> buffers;

    public GridHeifItemDecoder(Configuration configuration, IList<HeifItem> items, IList<HeifItemLink> itemLinks, IDictionary<uint, IMemoryOwner<byte>> buffers)
    {
        this.configuration = configuration;
        this.items = items;
        this.itemLinks = itemLinks;
        this.buffers = buffers;
    }

    /// <summary>
    /// Gets the item type this decoder decodes, which is <see cref="Heif4CharCode.Grid"/>.
    /// </summary>
    public Heif4CharCode Type => Heif4CharCode.Grid;

    /// <summary>
    /// Gets the compression method this doceder uses.
    /// </summary>
    public HeifCompressionMethod CompressionMethod { get; private set; }

    /// <summary>
    /// Decode the specified item as single image.
    /// </summary>
    public Image<TPixel> DecodeItemData(Configuration configuration, HeifItem gridItem, Span<byte> data)
    {
        List<uint> linked = this.itemLinks.First(l => l.SourceId == gridItem.Id).DestinationIds;
        using DisposableList<Image<TPixel>> gridTiles = new(linked.Count);
        foreach (uint id in linked)
        {
            HeifItem? item = this.items.FirstOrDefault(item => item.Id == id);
            if (item != null)
            {
                IHeifItemDecoder<TPixel>? decoder = HeifCompressionFactory.GetDecoder<TPixel>(item.Type);
                if (decoder != null)
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
