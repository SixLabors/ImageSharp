// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Icon;

internal abstract class IconDecoderCore : IImageDecoderInternals
{
    private IconDir fileHeader;

    public IconDecoderCore(DecoderOptions options) => this.Options = options;

    public DecoderOptions Options { get; }

    public Size Dimensions { get; private set; }

    protected IconDir FileHeader { get => this.fileHeader; private set => this.fileHeader = value; }

    protected IconDirEntry[] Entries { get; private set; } = Array.Empty<IconDirEntry>();

    public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Stream may not at 0.
        long basePosition = stream.Position;
        this.ReadHeader(stream);

        Span<byte> flag = stackalloc byte[PngConstants.HeaderBytes.Length];

        List<(Image<TPixel> Image, IconFrameCompression Compression, int Index)> decodedEntries = new(this.Entries.Length);

        for (int i = 0; i < this.Entries.Length; i++)
        {
            ref IconDirEntry entry = ref this.Entries[i];

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
            stream.Seek(-PngConstants.HeaderBytes.Length, SeekOrigin.Current);

            bool isPng = flag.SequenceEqual(PngConstants.HeaderBytes);

            // Decode the frame into a temp image buffer. This is disposed after the frame is copied to the result.
            Image<TPixel> temp = this.GetDecoder(isPng).Decode<TPixel>(stream, cancellationToken);
            decodedEntries.Add((temp, isPng ? IconFrameCompression.Png : IconFrameCompression.Bmp, i));

            // Since Windows Vista, the size of an image is determined from the BITMAPINFOHEADER structure or PNG image data
            // which technically allows storing icons with larger than 256 pixels, but such larger sizes are not recommended by Microsoft.
            this.Dimensions = new(Math.Max(this.Dimensions.Width, temp.Size.Width), Math.Max(this.Dimensions.Height, temp.Size.Height));
        }

        ImageMetadata metadata = new();
        PngMetadata? pngMetadata = null;
        Image<TPixel> result = new(this.Options.Configuration, metadata, decodedEntries.Select(x =>
        {
            BmpBitsPerPixel bitsPerPixel = default;
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

                // Bmp does not contain frame specific metadata.
                target.Metadata.SetFormatMetadata(PngFormat.Instance, target.Metadata.GetPngFrameMetadata());
            }
            else
            {
                bitsPerPixel = x.Image.Metadata.GetBmpMetadata().BitsPerPixel;
            }

            this.SetFrameMetadata(target.Metadata, this.Entries[x.Index], x.Compression, bitsPerPixel);

            x.Image.Dispose();

            return target;
        }).ToArray());

        // Copy the format specific metadata to the image.
        if (pngMetadata != null)
        {
            result.Metadata.SetFormatMetadata(PngFormat.Instance, pngMetadata);
        }

        return result;
    }

    public ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        // Stream may not at 0.
        long basePosition = stream.Position;
        this.ReadHeader(stream);

        Span<byte> flag = stackalloc byte[PngConstants.HeaderBytes.Length];

        ImageMetadata metadata = new();
        ImageFrameMetadata[] frames = new ImageFrameMetadata[this.FileHeader.Count];
        for (int i = 0; i < frames.Length; i++)
        {
            BmpBitsPerPixel bitsPerPixel = default;
            ref IconDirEntry entry = ref this.Entries[i];

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
            stream.Seek(-PngConstants.HeaderBytes.Length, SeekOrigin.Current);

            bool isPng = flag.SequenceEqual(PngConstants.HeaderBytes);

            // Decode the frame into a temp image buffer. This is disposed after the frame is copied to the result.
            ImageInfo temp = this.GetDecoder(isPng).Identify(stream, cancellationToken);

            frames[i] = new();
            if (isPng)
            {
                bitsPerPixel = temp.Metadata.GetBmpMetadata().BitsPerPixel;
            }

            this.SetFrameMetadata(frames[i], this.Entries[i], isPng ? IconFrameCompression.Png : IconFrameCompression.Bmp, bitsPerPixel);

            // Since Windows Vista, the size of an image is determined from the BITMAPINFOHEADER structure or PNG image data
            // which technically allows storing icons with larger than 256 pixels, but such larger sizes are not recommended by Microsoft.
            this.Dimensions = new(Math.Max(this.Dimensions.Width, temp.Size.Width), Math.Max(this.Dimensions.Height, temp.Size.Height));
        }

        return new(new(32), this.Dimensions, metadata, frames);
    }

    protected abstract void SetFrameMetadata(ImageFrameMetadata metadata, in IconDirEntry entry, IconFrameCompression compression, BmpBitsPerPixel bitsPerPixel);

    protected void ReadHeader(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[IconDirEntry.Size];

        // ICONDIR
        _ = IconAssert.EndOfStream(stream.Read(buffer[..IconDir.Size]), IconDir.Size);
        this.fileHeader = IconDir.Parse(buffer);

        // ICONDIRENTRY
        this.Entries = new IconDirEntry[this.FileHeader.Count];
        for (int i = 0; i < this.Entries.Length; i++)
        {
            _ = IconAssert.EndOfStream(stream.Read(buffer[..IconDirEntry.Size]), IconDirEntry.Size);
            this.Entries[i] = IconDirEntry.Parse(buffer);
        }

        int width = 0;
        int height = 0;
        foreach (IconDirEntry entry in this.Entries)
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

        this.Dimensions = new(width, height);
    }

    private IImageDecoderInternals GetDecoder(bool isPng)
    {
        if (isPng)
        {
            return new PngDecoderCore(this.Options);
        }
        else
        {
            return new BmpDecoderCore(new()
            {
                GeneralOptions = this.Options,
                ProcessedAlphaMask = true,
                SkipFileHeader = true,
                UseDoubleHeight = true,
            });
        }
    }
}
