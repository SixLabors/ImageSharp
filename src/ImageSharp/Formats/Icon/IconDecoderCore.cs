// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
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

        Span<byte> flag = stackalloc byte[Png.PngConstants.HeaderBytes.Length];

        List<(Image<TPixel> Image, bool IsPng, int Index)> frames = new(this.Entries.Length);
        for (int i = 0; i < this.Entries.Length; i++)
        {
            _ = IconAssert.EndOfStream(stream.Seek(basePosition + this.Entries[i].ImageOffset, SeekOrigin.Begin), basePosition + this.Entries[i].ImageOffset);
            _ = IconAssert.EndOfStream(stream.Read(flag), Png.PngConstants.HeaderBytes.Length);
            _ = stream.Seek(-Png.PngConstants.HeaderBytes.Length, SeekOrigin.Current);

            bool isPng = flag.SequenceEqual(Png.PngConstants.HeaderBytes);

            Image<TPixel> img = this.GetDecoder(isPng).Decode<TPixel>(stream, cancellationToken);
            IconAssert.NotSquare(img.Size);
            frames.Add((img, isPng, i));
            if (isPng && img.Size.Width > this.Dimensions.Width)
            {
                this.Dimensions = img.Size;
            }
        }

        ImageMetadata metadata = new();
        return new(this.Options.Configuration, metadata, frames.Select(i =>
        {
            ImageFrame<TPixel> target = new(this.Options.Configuration, this.Dimensions);
            ImageFrame<TPixel> source = i.Image.Frames.RootFrameUnsafe;
            for (int h = 0; h < source.Height; h++)
            {
                source.PixelBuffer.DangerousGetRowSpan(h).CopyTo(target.PixelBuffer.DangerousGetRowSpan(h));
            }

            if (i.IsPng)
            {
                target.Metadata.UnsafeSetFormatMetadata(Png.PngFormat.Instance, i.Image.Metadata.GetPngMetadata());
            }
            else
            {
                target.Metadata.UnsafeSetFormatMetadata(Bmp.BmpFormat.Instance, i.Image.Metadata.GetBmpMetadata());
            }

            this.GetFrameMetadata(target.Metadata).FromIconDirEntry(this.Entries[i.Index]);

            i.Image.Dispose();
            return target;
        }).ToArray());
    }

    public ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.ReadHeader(stream);

        ImageMetadata metadata = new();
        ImageFrameMetadata[] frames = new ImageFrameMetadata[this.FileHeader.Count];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = new();
            IconFrameMetadata icoFrameMetadata = this.GetFrameMetadata(frames[i]);
            icoFrameMetadata.FromIconDirEntry(this.Entries[i]);
        }

        return new(new(32), new(0), metadata, frames);
    }

    protected abstract IconFrameMetadata GetFrameMetadata(ImageFrameMetadata metadata);

    protected void ReadHeader(Stream stream)
    {
        _ = Read(stream, out this.fileHeader, IconDir.Size);
        this.Entries = new IconDirEntry[this.FileHeader.Count];
        for (int i = 0; i < this.Entries.Length; i++)
        {
            _ = Read(stream, out this.Entries[i], IconDirEntry.Size);
        }

        this.Dimensions = new(
             this.Entries.Max(i => i.Width),
             this.Entries.Max(i => i.Height));
    }

    private static int Read<T>(Stream stream, out T data, int size)
        where T : unmanaged
    {
        Span<byte> buffer = stackalloc byte[size];
        _ = IconAssert.EndOfStream(stream.Read(buffer), size);
        data = MemoryMarshal.Cast<byte, T>(buffer)[0];
        return size;
    }

    private IImageDecoderInternals GetDecoder(bool isPng)
    {
        if (isPng)
        {
            return new Png.PngDecoderCore(this.Options);
        }
        else
        {
            return new Bmp.BmpDecoderCore(new()
            {
                ProcessedAlphaMask = true,
                SkipFileHeader = true,
                IsDoubleHeight = true,
            });
        }
    }
}
