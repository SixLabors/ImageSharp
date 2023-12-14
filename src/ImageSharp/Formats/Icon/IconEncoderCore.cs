// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Cur;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Icon;

internal abstract class IconEncoderCore(IconFileType iconFileType)
    : IImageEncoderInternals
{
    private IconDir fileHeader;

    private IconFrameMetadata[]? entries;

    public void Encode<TPixel>(
        Image<TPixel> image,
        Stream stream,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        IconAssert.CanSeek(stream);

        // Stream may not at 0.
        long basePosition = stream.Position;
        this.InitHeader(image);

        int dataOffset = IconDir.Size + (IconDirEntry.Size * this.entries.Length);
        _ = stream.Seek(dataOffset, SeekOrigin.Current);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            ImageFrame<TPixel> frame = image.Frames[i];
            int width = this.entries[i].Entry.Width;
            if (width is 0)
            {
                width = frame.Width;
            }

            int height = this.entries[i].Entry.Height;
            if (height is 0)
            {
                height = frame.Height;
            }

            this.entries[i].Entry.ImageOffset = (uint)stream.Position;

            Image<TPixel> img = new(width, height);
            for (int y = 0; y < height; y++)
            {
                frame.PixelBuffer.DangerousGetRowSpan(y)[..width].CopyTo(img.GetRootFramePixelBuffer().DangerousGetRowSpan(y));
            }

            QuantizingImageEncoder encoder = this.entries[i].Compression switch
            {
                IconFrameCompression.Bmp => new Bmp.BmpEncoder()
                {
                    ProcessedAlphaMask = true,
                    UseDoubleHeight = true,
                    SkipFileHeader = true,
                    SupportTransparency = false,
                    BitsPerPixel = iconFileType is IconFileType.ICO
                        ? (Bmp.BmpBitsPerPixel?)this.entries[i].Entry.BitCount
                        : Bmp.BmpBitsPerPixel.Pixel24 // TODO: Here you need to switch to selecting the corresponding value according to the size of the image
                },
                IconFrameCompression.Png => new Png.PngEncoder(),
                _ => throw new NotSupportedException(),
            };

            encoder.Encode(img, stream);
            this.entries[i].Entry.BytesInRes = (uint)stream.Position - this.entries[i].Entry.ImageOffset;
        }

        long endPosition = stream.Position;
        _ = stream.Seek(basePosition, SeekOrigin.Begin);
        this.fileHeader.WriteTo(stream);
        foreach (IconFrameMetadata frame in this.entries)
        {
            frame.Entry.WriteTo(stream);
        }

        _ = stream.Seek(endPosition, SeekOrigin.Begin);
    }

    [MemberNotNull(nameof(entries))]
    private void InitHeader(in Image image)
    {
        this.fileHeader = new(iconFileType, (ushort)image.Frames.Count);
        this.entries = iconFileType switch
        {
            IconFileType.ICO =>
            image.Frames.Select(i =>
            {
                IcoFrameMetadata metadata = i.Metadata.GetIcoMetadata();
                return new IconFrameMetadata(metadata.Compression, metadata.ToIconDirEntry());
            }).ToArray(),
            IconFileType.CUR =>
            image.Frames.Select(i =>
            {
                CurFrameMetadata metadata = i.Metadata.GetCurMetadata();
                return new IconFrameMetadata(metadata.Compression, metadata.ToIconDirEntry());
            }).ToArray(),
            _ => throw new NotSupportedException(),
        };
    }

    internal sealed class IconFrameMetadata(IconFrameCompression compression, IconDirEntry iconDirEntry)
    {
        private IconDirEntry iconDirEntry = iconDirEntry;

        public IconFrameCompression Compression { get; set; } = compression;

        public ref IconDirEntry Entry => ref this.iconDirEntry;
    }
}
