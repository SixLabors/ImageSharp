// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Icon;

internal abstract class IconDecoderCore : ImageDecoderCore
{
    private IconDir fileHeader;
    private IconDirEntry[]? entries;

    protected IconDecoderCore(DecoderOptions options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        // Stream may not at 0.
        long basePosition = stream.Position;
        this.ReadHeader(stream);

        Span<byte> flag = stackalloc byte[PngConstants.HeaderBytes.Length];

        List<(Image<TPixel> Image, IconFrameCompression Compression, int Index)> decodedEntries
            = new((int)Math.Min(this.entries.Length, this.Options.MaxFrames));

        for (int i = 0; i < this.entries.Length; i++)
        {
            if (i == this.Options.MaxFrames)
            {
                break;
            }

            ref IconDirEntry entry = ref this.entries[i];

            // If we hit the end of the stream we should break.
            if (stream.Seek(basePosition + entry.ImageOffset, SeekOrigin.Begin) >= stream.Length)
            {
                break;
            }

            // There should always be enough bytes for this regardless of the entry type.
            if (stream.Read(flag) != PngConstants.HeaderBytes.Length)
            {
                break;
            }

            // Reset the stream position.
            _ = stream.Seek(-PngConstants.HeaderBytes.Length, SeekOrigin.Current);

            bool isPng = flag.SequenceEqual(PngConstants.HeaderBytes);

            // Decode the frame into a temp image buffer. This is disposed after the frame is copied to the result.
            Image<TPixel> temp = this.GetDecoder(isPng).Decode<TPixel>(this.Options.Configuration, stream, cancellationToken);
            decodedEntries.Add((temp, isPng ? IconFrameCompression.Png : IconFrameCompression.Bmp, i));

            // Since Windows Vista, the size of an image is determined from the BITMAPINFOHEADER structure or PNG image data
            // which technically allows storing icons with larger than 256 pixels, but such larger sizes are not recommended by Microsoft.
            this.Dimensions = new Size(Math.Max(this.Dimensions.Width, temp.Size.Width), Math.Max(this.Dimensions.Height, temp.Size.Height));
        }

        ImageMetadata metadata = new();
        BmpMetadata? bmpMetadata = null;
        PngMetadata? pngMetadata = null;
        Image<TPixel> result = new(this.Options.Configuration, metadata, decodedEntries.Select(x =>
        {
            BmpBitsPerPixel bitsPerPixel = BmpBitsPerPixel.Bit32;
            ReadOnlyMemory<Color>? colorTable = null;
            ImageFrame<TPixel> target = new(this.Options.Configuration, this.Dimensions);
            ImageFrame<TPixel> source = x.Image.Frames.RootFrameUnsafe;
            for (int y = 0; y < source.Height; y++)
            {
                source.PixelBuffer.DangerousGetRowSpan(y).CopyTo(target.PixelBuffer.DangerousGetRowSpan(y));
            }

            // Copy the format specific frame metadata to the image.
            if (x.Compression is IconFrameCompression.Png)
            {
                if (x.Index == 0)
                {
                    pngMetadata = x.Image.Metadata.GetPngMetadata();
                }

                target.Metadata.SetFormatMetadata(PngFormat.Instance, target.Metadata.GetPngMetadata());
            }
            else
            {
                BmpMetadata meta = x.Image.Metadata.GetBmpMetadata();
                bitsPerPixel = meta.BitsPerPixel;
                colorTable = meta.ColorTable;

                if (x.Index == 0)
                {
                    bmpMetadata = meta;
                }
            }

            this.SetFrameMetadata(
                metadata,
                target.Metadata,
                x.Index,
                this.entries[x.Index],
                x.Compression,
                bitsPerPixel,
                colorTable);

            x.Image.Dispose();

            return target;
        }).ToArray());

        // Copy the format specific metadata to the image.
        if (bmpMetadata != null)
        {
            result.Metadata.SetFormatMetadata(BmpFormat.Instance, bmpMetadata);
        }

        if (pngMetadata != null)
        {
            result.Metadata.SetFormatMetadata(PngFormat.Instance, pngMetadata);
        }

        return result;
    }

    /// <inheritdoc />
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        // Stream may not at 0.
        long basePosition = stream.Position;
        this.ReadHeader(stream);

        Span<byte> flag = stackalloc byte[PngConstants.HeaderBytes.Length];

        ImageMetadata metadata = new();
        BmpMetadata? bmpMetadata = null;
        PngMetadata? pngMetadata = null;
        ImageFrameMetadata[] frames = new ImageFrameMetadata[Math.Min(this.fileHeader.Count, this.Options.MaxFrames)];
        int bpp = 0;
        for (int i = 0; i < frames.Length; i++)
        {
            BmpBitsPerPixel bitsPerPixel = BmpBitsPerPixel.Bit32;
            ReadOnlyMemory<Color>? colorTable = null;
            ref IconDirEntry entry = ref this.entries[i];

            // If we hit the end of the stream we should break.
            if (stream.Seek(basePosition + entry.ImageOffset, SeekOrigin.Begin) >= stream.Length)
            {
                break;
            }

            // There should always be enough bytes for this regardless of the entry type.
            if (stream.Read(flag) != PngConstants.HeaderBytes.Length)
            {
                break;
            }

            // Reset the stream position.
            _ = stream.Seek(-PngConstants.HeaderBytes.Length, SeekOrigin.Current);

            bool isPng = flag.SequenceEqual(PngConstants.HeaderBytes);

            // Decode the frame into a temp image buffer. This is disposed after the frame is copied to the result.
            ImageInfo frameInfo = this.GetDecoder(isPng).Identify(this.Options.Configuration, stream, cancellationToken);

            ImageFrameMetadata frameMetadata = new();

            if (isPng)
            {
                if (i == 0)
                {
                    pngMetadata = frameInfo.Metadata.GetPngMetadata();
                }

                frameMetadata.SetFormatMetadata(PngFormat.Instance, frameInfo.FrameMetadataCollection[0].GetPngMetadata());
            }
            else
            {
                BmpMetadata meta = frameInfo.Metadata.GetBmpMetadata();
                bitsPerPixel = meta.BitsPerPixel;
                colorTable = meta.ColorTable;

                if (i == 0)
                {
                    bmpMetadata = meta;
                }
            }

            bpp = Math.Max(bpp, (int)bitsPerPixel);

            frames[i] = frameMetadata;

            this.SetFrameMetadata(
                metadata,
                frames[i],
                i,
                this.entries[i],
                isPng ? IconFrameCompression.Png : IconFrameCompression.Bmp,
                bitsPerPixel,
                colorTable);

            // Since Windows Vista, the size of an image is determined from the BITMAPINFOHEADER structure or PNG image data
            // which technically allows storing icons with larger than 256 pixels, but such larger sizes are not recommended by Microsoft.
            this.Dimensions = new Size(Math.Max(this.Dimensions.Width, frameInfo.Size.Width), Math.Max(this.Dimensions.Height, frameInfo.Size.Height));
        }

        // Copy the format specific metadata to the image.
        if (bmpMetadata != null)
        {
            metadata.SetFormatMetadata(BmpFormat.Instance, bmpMetadata);
        }

        if (pngMetadata != null)
        {
            metadata.SetFormatMetadata(PngFormat.Instance, pngMetadata);
        }

        return new ImageInfo(this.Dimensions, metadata, frames);
    }

    protected abstract void SetFrameMetadata(
        ImageMetadata imageMetadata,
        ImageFrameMetadata frameMetadata,
        int index,
        in IconDirEntry entry,
        IconFrameCompression compression,
        BmpBitsPerPixel bitsPerPixel,
        ReadOnlyMemory<Color>? colorTable);

    [MemberNotNull(nameof(entries))]
    protected void ReadHeader(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[IconDirEntry.Size];

        // ICONDIR
        _ = CheckEndOfStream(stream.Read(buffer[..IconDir.Size]), IconDir.Size);
        this.fileHeader = IconDir.Parse(buffer);

        // ICONDIRENTRY
        this.entries = new IconDirEntry[this.fileHeader.Count];
        for (int i = 0; i < this.entries.Length; i++)
        {
            _ = CheckEndOfStream(stream.Read(buffer[..IconDirEntry.Size]), IconDirEntry.Size);
            this.entries[i] = IconDirEntry.Parse(buffer);
        }

        int width = 0;
        int height = 0;
        foreach (IconDirEntry entry in this.entries)
        {
            // Since Windows 95 size of an image in the ICONDIRENTRY structure might
            // be set to zero, which means 256 pixels.
            if (entry.Width == 0)
            {
                width = 256;
            }

            if (entry.Height == 0)
            {
                height = 256;
            }

            if (width == 256 && height == 256)
            {
                break;
            }

            width = Math.Max(width, entry.Width);
            height = Math.Max(height, entry.Height);
        }

        this.Dimensions = new Size(width, height);
    }

    private ImageDecoderCore GetDecoder(bool isPng)
    {
        if (isPng)
        {
            return new PngDecoderCore(new PngDecoderOptions
            {
                GeneralOptions = this.Options,
            });
        }

        return new BmpDecoderCore(new BmpDecoderOptions
        {
            GeneralOptions = this.Options,
            ProcessedAlphaMask = true,
            SkipFileHeader = true,
            UseDoubleHeight = true,
        });
    }

    private static int CheckEndOfStream(int v, int length)
    {
        if (v != length)
        {
            throw new InvalidImageContentException("Not enough bytes to read icon header.");
        }

        return v;
    }
}
