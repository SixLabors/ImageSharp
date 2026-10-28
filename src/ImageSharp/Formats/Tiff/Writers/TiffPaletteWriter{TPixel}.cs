// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Quantizes sRGB pixels to indices and converts the final palette for TIFF's 16-bit RGB ColorMap entry.
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
internal sealed class TiffPaletteWriter<TPixel> : TiffBaseColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly int maxColors;
    private readonly int colorPaletteSize;
    private readonly int colorPaletteBytes;
    private readonly IndexedImageFrame<TPixel> quantizedFrame;
    private IMemoryOwner<byte> indexedPixelsBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffPaletteWriter{TPixel}"/> class.
    /// </summary>
    /// <param name="frame">The source frame.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="quantizer">The palette quantizer.</param>
    /// <param name="pixelSamplingStrategy">The palette sampling strategy.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="entriesCollector">The directory entries.</param>
    /// <param name="bitsPerPixel">The index precision.</param>
    /// <param name="converter">The converter from sRGB to the retained RGB profile, or null when no ICC conversion is required.</param>
    /// <param name="clearTransparentPixels">Whether the quantizer sets RGB to zero for fully transparent input pixels.</param>
    public TiffPaletteWriter(
        ImageFrame<TPixel> frame,
        Size encodingSize,
        IQuantizer quantizer,
        IPixelSamplingStrategy pixelSamplingStrategy,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector,
        int bitsPerPixel,
        ColorProfileConverter converter,
        bool clearTransparentPixels)
         : base(frame, encodingSize, memoryAllocator, configuration, entriesCollector)
    {
        DebugGuard.NotNull(quantizer, nameof(quantizer));
        DebugGuard.NotNull(quantizer, nameof(pixelSamplingStrategy));
        DebugGuard.NotNull(configuration, nameof(configuration));
        DebugGuard.NotNull(entriesCollector, nameof(entriesCollector));
        DebugGuard.MustBeBetweenOrEqualTo(bitsPerPixel, 4, 8, nameof(bitsPerPixel));

        this.BitsPerPixel = bitsPerPixel;
        this.maxColors = this.BitsPerPixel == 4 ? 16 : 256;
        this.colorPaletteSize = this.maxColors * 3;
        this.colorPaletteBytes = this.colorPaletteSize * 2;
        using IQuantizer<TPixel> frameQuantizer = quantizer.CreatePixelSpecificQuantizer<TPixel>(
            this.Configuration,
            new QuantizerOptions
            {
                MaxColors = this.maxColors,
                TransparentColorMode = clearTransparentPixels ? TransparentColorMode.Clear : TransparentColorMode.Preserve
            });

        // Choose indices against the sRGB pixels and palette. Only the final ColorMap entries are returned to the retained ICC profile.
        frameQuantizer.BuildPalette(pixelSamplingStrategy, frame);
        this.quantizedFrame = frameQuantizer.QuantizeFrame(frame, new Rectangle(Point.Empty, encodingSize));

        this.AddColorMapTag(converter);
    }

    /// <inheritdoc />
    public override int BitsPerPixel { get; }

    /// <inheritdoc />
    protected override void EncodeStrip(int y, int height, TiffBaseCompressor compressor)
    {
        int width = this.quantizedFrame.Width;

        if (this.BitsPerPixel == 4)
        {
            int halfWidth = width >> 1;
            int excess = (width & 1) * height; // (width % 2) * height
            int rows4BitBufferLength = (halfWidth * height) + excess;
            this.indexedPixelsBuffer ??= this.MemoryAllocator.Allocate<byte>(rows4BitBufferLength);
            Span<byte> rows4bit = this.indexedPixelsBuffer.GetSpan();
            int idx4bitRows = 0;
            int lastRow = y + height;
            for (int row = y; row < lastRow; row++)
            {
                ReadOnlySpan<byte> indexedPixelRow = this.quantizedFrame.DangerousGetRowSpan(row);
                int idxPixels = 0;

                // Each byte stores two palette indices: the left pixel in its high nibble, then the right pixel in its low nibble.
                for (int x = 0; x < halfWidth; x++)
                {
                    rows4bit[idx4bitRows] = (byte)((indexedPixelRow[idxPixels] << 4) | (indexedPixelRow[idxPixels + 1] & 0xF));
                    idxPixels += 2;
                    idx4bitRows++;
                }

                // An odd-width row ends with one high-nibble index and a zero low nibble. The next row starts in a new byte.
                if (width % 2 != 0)
                {
                    rows4bit[idx4bitRows++] = (byte)(indexedPixelRow[idxPixels] << 4);
                }
            }

            compressor.CompressStrip(rows4bit[..idx4bitRows], height);
        }
        else
        {
            int stripPixels = width * height;
            this.indexedPixelsBuffer ??= this.MemoryAllocator.Allocate<byte>(stripPixels);
            Span<byte> indexedPixels = this.indexedPixelsBuffer.GetSpan();
            int lastRow = y + height;
            int indexedPixelsRowIdx = 0;
            for (int row = y; row < lastRow; row++)
            {
                ReadOnlySpan<byte> indexedPixelRow = this.quantizedFrame.DangerousGetRowSpan(row);
                indexedPixelRow.CopyTo(indexedPixels.Slice(indexedPixelsRowIdx * width, width));
                indexedPixelsRowIdx++;
            }

            compressor.CompressStrip(indexedPixels[..stripPixels], height);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        this.quantizedFrame?.Dispose();
        this.indexedPixelsBuffer?.Dispose();
    }

    /// <summary>
    /// Adds the final RGB palette to the TIFF ColorMap entry after any ICC conversion.
    /// </summary>
    /// <param name="converter">The converter from sRGB to the retained RGB profile, or null when no ICC conversion is required.</param>
    private void AddColorMapTag(ColorProfileConverter converter)
    {
        using IMemoryOwner<byte> colorPaletteBuffer = this.MemoryAllocator.Allocate<byte>(this.colorPaletteBytes);
        Span<byte> colorPalette = colorPaletteBuffer.GetSpan();

        ReadOnlySpan<TPixel> quantizedColors = this.quantizedFrame.Palette.Span;
        int quantizedColorBytes = quantizedColors.Length * 3 * 2;

        // In the ColorMap, black is represented by 0, 0, 0 and white is represented by 65535, 65535, 65535.
        Span<Rgb48> quantizedColorRgb48 = MemoryMarshal.Cast<byte, Rgb48>(colorPalette[..quantizedColorBytes]);
        if (converter is null)
        {
            PixelOperations<TPixel>.Instance.ToRgb48(this.Configuration, quantizedColors, quantizedColorRgb48);
        }
        else
        {
            // Quantization selects indices against the sRGB palette. Convert the palette colors after those indices are fixed.
            // Keep ICC results as floats until the 16-bit RGB packer runs. Neither the indices nor the quantizer's palette change.
            using IMemoryOwner<float> owner = this.MemoryAllocator.Allocate<float>(quantizedColors.Length * 7);
            Span<float> storage = owner.GetSpan();
            Span<Vector4> vectors = MemoryMarshal.Cast<float, Vector4>(storage[..(quantizedColors.Length * 4)]);
            Span<Rgb> rgb = MemoryMarshal.Cast<float, Rgb>(storage[(quantizedColors.Length * 4)..]);
            converter.Convert(this.Configuration, quantizedColors, vectors, rgb);
            PixelOperations<Rgb48>.Instance.FromUnassociatedScaledVector4Destructive(this.Configuration, vectors, quantizedColorRgb48);
        }

        // ColorMap reserves the full 16 or 256 entries per channel. Leave unused entries zero in the new array.
        int diffToMaxColors = this.maxColors - quantizedColors.Length;

        // In a TIFF ColorMap, all the Red values come first, followed by the Green values,
        // then the Blue values. Convert the quantized palette to this format.
        ushort[] palette = new ushort[this.colorPaletteSize];
        int paletteIdx = 0;
        for (int i = 0; i < quantizedColors.Length; i++)
        {
            palette[paletteIdx++] = quantizedColorRgb48[i].R;
        }

        paletteIdx += diffToMaxColors;

        for (int i = 0; i < quantizedColors.Length; i++)
        {
            palette[paletteIdx++] = quantizedColorRgb48[i].G;
        }

        paletteIdx += diffToMaxColors;

        for (int i = 0; i < quantizedColors.Length; i++)
        {
            palette[paletteIdx++] = quantizedColorRgb48[i].B;
        }

        ExifShortArray colorMap = new(ExifTagValue.ColorMap)
        {
            Value = palette
        };

        this.EntriesCollector.AddOrReplace(colorMap);
    }
}
