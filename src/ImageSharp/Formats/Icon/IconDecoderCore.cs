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
        Image<TPixel> result = new(this.Dimensions.Width, this.Dimensions.Height);

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
            using Image<TPixel> temp = this.GetDecoder(isPng).Decode<TPixel>(stream, cancellationToken);

            ImageFrame<TPixel> source = temp.Frames.RootFrameUnsafe;
            ImageFrame<TPixel> target = i == 0 ? result.Frames.RootFrameUnsafe : result.Frames.CreateFrame();

            // Draw the new frame at position 0,0. We capture the dimensions for cropping during encoding
            // via the icon entry.
            for (int h = 0; h < source.Height; h++)
            {
                source.PixelBuffer.DangerousGetRowSpan(h).CopyTo(target.PixelBuffer.DangerousGetRowSpan(h));
            }

            // Copy the format specific metadata to the image.
            if (isPng)
            {
                if (i == 0)
                {
                    result.Metadata.SetFormatMetadata(PngFormat.Instance, temp.Metadata.GetPngMetadata());
                }

                target.Metadata.SetFormatMetadata(PngFormat.Instance, target.Metadata.GetPngFrameMetadata());
            }
            else if (i == 0)
            {
                // Bmp does not contain frame specific metadata.
                result.Metadata.SetFormatMetadata(BmpFormat.Instance, temp.Metadata.GetBmpMetadata());
            }

            // TODO: The inheriting decoder should be responsible for setting the actual data (FromIconDirEntry)
            // so we can avoid the protected Field1 and Field2 properties and use strong typing.
            this.GetFrameMetadata(target.Metadata).FromIconDirEntry(entry);
        }

        return result;
    }

    public ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.ReadHeader(stream);

        ImageMetadata metadata = new();
        ImageFrameMetadata[] frames = new ImageFrameMetadata[this.FileHeader.Count];
        for (int i = 0; i < frames.Length; i++)
        {
            // TODO: Use the Identify methods in each decoder to return the
            // format specific metadata for the image and frame.
            frames[i] = new();
            IconFrameMetadata icoFrameMetadata = this.GetFrameMetadata(frames[i]);
            icoFrameMetadata.FromIconDirEntry(this.Entries[i]);
        }

        // TODO: Use real values from the metadata.
        return new(new(32), new(0), metadata, frames);
    }

    protected abstract IconFrameMetadata GetFrameMetadata(ImageFrameMetadata metadata);

    protected void ReadHeader(Stream stream)
    {
        // TODO: Check length and throw if the header cannot be read.
        _ = Read(stream, out this.fileHeader, IconDir.Size);
        this.Entries = new IconDirEntry[this.FileHeader.Count];
        for (int i = 0; i < this.Entries.Length; i++)
        {
            _ = Read(stream, out this.Entries[i], IconDirEntry.Size);
        }

        int width = 0;
        int height = 0;
        foreach (IconDirEntry entry in this.Entries)
        {
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

    private static int Read<T>(Stream stream, out T data, int size)
        where T : unmanaged
    {
        // TODO: Use explicit parsing methods for each T type.
        // See PngHeader.Parse() for example.
        Span<byte> buffer = stackalloc byte[size];

        _ = IconAssert.EndOfStream(stream.Read(buffer), size);
        data = MemoryMarshal.Cast<byte, T>(buffer)[0];
        return size;
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
