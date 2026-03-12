// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

internal abstract class TiffBaseColorWriter<TPixel> : IDisposable
    where TPixel : unmanaged, IPixel<TPixel>
{
    private bool isDisposed;

    protected TiffBaseColorWriter(
        ImageFrame<TPixel> image,
        Size encodingSize,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector)
    {
        this.Width = encodingSize.Width;
        this.Height = encodingSize.Height;
        this.Image = image;
        this.MemoryAllocator = memoryAllocator;
        this.Configuration = configuration;
        this.EntriesCollector = entriesCollector;
    }

    /// <summary>
    /// Gets the bits per pixel.
    /// </summary>
    public abstract int BitsPerPixel { get; }

    /// <summary>
    /// Gets the width of the portion of the image to be encoded.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the portion of the image to be encoded.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the bytes per row.
    /// </summary>
    public int BytesPerRow => (int)(((uint)(this.Width * this.BitsPerPixel) + 7) / 8);

    protected ImageFrame<TPixel> Image { get; }

    protected MemoryAllocator MemoryAllocator { get; }

    protected Configuration Configuration { get; }

    protected TiffEncoderEntriesCollector EntriesCollector { get; }

    public virtual void Write(TiffBaseCompressor compressor, int rowsPerStrip)
    {
        DebugGuard.IsTrue(this.BytesPerRow == compressor.BytesPerRow, "bytes per row of the compressor does not match tiff color writer");
        int stripsCount = (this.Height + rowsPerStrip - 1) / rowsPerStrip;

        uint[] stripOffsets = new uint[stripsCount];
        uint[] stripByteCounts = new uint[stripsCount];

        int stripIndex = 0;
        compressor.Initialize(rowsPerStrip);
        for (int y = 0; y < this.Height; y += rowsPerStrip)
        {
            long offset = compressor.Output.Position;

            int height = Math.Min(rowsPerStrip, this.Height - y);
            this.EncodeStrip(y, height, compressor);

            long endOffset = compressor.Output.Position;
            stripOffsets[stripIndex] = (uint)offset;
            stripByteCounts[stripIndex] = (uint)(endOffset - offset);
            stripIndex++;
        }

        DebugGuard.IsTrue(stripIndex == stripsCount, "stripIndex and stripsCount should match");
        this.AddStripTags(rowsPerStrip, stripOffsets, stripByteCounts);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.isDisposed = true;
        this.Dispose(true);
    }

    protected abstract void EncodeStrip(int y, int height, TiffBaseCompressor compressor);

    /// <summary>
    /// Adds image format information to the specified IFD.
    /// </summary>
    /// <param name="rowsPerStrip">The rows per strip.</param>
    /// <param name="stripOffsets">The strip offsets.</param>
    /// <param name="stripByteCounts">The strip byte counts.</param>
    private void AddStripTags(int rowsPerStrip, uint[] stripOffsets, uint[] stripByteCounts)
    {
        this.EntriesCollector.AddOrReplace(new ExifLong(ExifTagValue.RowsPerStrip)
        {
            Value = (uint)rowsPerStrip
        });

        this.EntriesCollector.AddOrReplace(new ExifLongArray(ExifTagValue.StripOffsets)
        {
            Value = stripOffsets
        });

        this.EntriesCollector.AddOrReplace(new ExifLongArray(ExifTagValue.StripByteCounts)
        {
            Value = stripByteCounts
        });
    }

    protected abstract void Dispose(bool disposing);
}
