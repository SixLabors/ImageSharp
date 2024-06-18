// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Cur;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Icon;

internal abstract class IconEncoderCore : IImageEncoderInternals
{
    private readonly IconFileType iconFileType;
    private IconDir fileHeader;
    private EncodingFrameMetadata[]? entries;

    protected IconEncoderCore(IconFileType iconFileType)
        => this.iconFileType = iconFileType;

    public void Encode<TPixel>(
        Image<TPixel> image,
        Stream stream,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        // Stream may not at 0.
        long basePosition = stream.Position;
        this.InitHeader(image);

        // We don't write the header and entries yet as we need to write the image data first.
        int dataOffset = IconDir.Size + (IconDirEntry.Size * this.entries.Length);
        _ = stream.Seek(dataOffset, SeekOrigin.Current);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            // Since Windows Vista, the size of an image is determined from the BITMAPINFOHEADER structure or PNG image data
            // which technically allows storing icons with larger than 256 pixels, but such larger sizes are not recommended by Microsoft.
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

            // We crop the frame to the size specified in the metadata.
            // TODO: we can optimize this by cropping the frame only if the new size is both required and different.
            using Image<TPixel> encodingFrame = new(width, height);
            for (int y = 0; y < height; y++)
            {
                frame.PixelBuffer.DangerousGetRowSpan(y)[..width]
                     .CopyTo(encodingFrame.GetRootFramePixelBuffer().DangerousGetRowSpan(y));
            }

            ref EncodingFrameMetadata encodingMetadata = ref this.entries[i];

            QuantizingImageEncoder encoder = encodingMetadata.Compression switch
            {
                IconFrameCompression.Bmp => new BmpEncoder()
                {
                    // We don't have access to the palette in the metadata so we need to quantize the image
                    // using a new one generated from the pixel data.
                    Quantizer = encodingMetadata.Entry.BitCount <= 8
                    ? new WuQuantizer(new()
                    {
                        MaxColors = encodingMetadata.Entry.ColorCount
                    })
                    : null,
                    ProcessedAlphaMask = true,
                    UseDoubleHeight = true,
                    SkipFileHeader = true,
                    SupportTransparency = false,
                    BitsPerPixel = encodingMetadata.BmpBitsPerPixel
                },
                IconFrameCompression.Png => new PngEncoder()
                {
                    // Only 32bit Png supported.
                    // https://devblogs.microsoft.com/oldnewthing/20101022-00/?p=12473
                    BitDepth = PngBitDepth.Bit8,
                    ColorType = PngColorType.RgbWithAlpha,
                    CompressionLevel = PngCompressionLevel.BestCompression
                },
                _ => throw new NotSupportedException(),
            };

            encoder.Encode(encodingFrame, stream);
            encodingMetadata.Entry.BytesInRes = (uint)stream.Position - encodingMetadata.Entry.ImageOffset;
        }

        // We now need to rewind the stream and write the header and the entries.
        long endPosition = stream.Position;
        _ = stream.Seek(basePosition, SeekOrigin.Begin);
        this.fileHeader.WriteTo(stream);
        foreach (EncodingFrameMetadata frame in this.entries)
        {
            frame.Entry.WriteTo(stream);
        }

        _ = stream.Seek(endPosition, SeekOrigin.Begin);
    }

    [MemberNotNull(nameof(entries))]
    private void InitHeader(Image image)
    {
        this.fileHeader = new(this.iconFileType, (ushort)image.Frames.Count);
        this.entries = this.iconFileType switch
        {
            IconFileType.ICO =>
            image.Frames.Select(i =>
            {
                IcoFrameMetadata metadata = i.Metadata.GetIcoMetadata();
                return new EncodingFrameMetadata(metadata.Compression, metadata.BmpBitsPerPixel, metadata.ToIconDirEntry());
            }).ToArray(),
            IconFileType.CUR =>
            image.Frames.Select(i =>
            {
                CurFrameMetadata metadata = i.Metadata.GetCurMetadata();
                return new EncodingFrameMetadata(metadata.Compression, metadata.BmpBitsPerPixel, metadata.ToIconDirEntry());
            }).ToArray(),
            _ => throw new NotSupportedException(),
        };
    }

    internal sealed class EncodingFrameMetadata(
        IconFrameCompression compression,
        BmpBitsPerPixel bmpBitsPerPixel,
        IconDirEntry iconDirEntry)
    {
        private IconDirEntry iconDirEntry = iconDirEntry;

        public IconFrameCompression Compression { get; set; } = compression;

        public BmpBitsPerPixel BmpBitsPerPixel { get; set; } = bmpBitsPerPixel;

        public ref IconDirEntry Entry => ref this.iconDirEntry;
    }
}
