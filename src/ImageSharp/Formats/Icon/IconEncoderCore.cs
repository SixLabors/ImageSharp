// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Icon;

internal abstract class IconEncoderCore : IImageEncoderInternals
{
    protected IconDir FileHeader { get; set; }

    protected IconDirEntry[]? Entries { get; set; }

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
        this.GetHeader(image);

        int dataOffset = IconDir.Size + (IconDirEntry.Size * this.Entries.Length);
        _ = stream.Seek(dataOffset, SeekOrigin.Current);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            ImageFrame<TPixel> frame = image.Frames[i];
            this.Entries[i].ImageOffset = (uint)stream.Position;
            Image<TPixel> img = new(Configuration.Default, frame.PixelBuffer, new());

            // Note: this encoder are not supported PNG Data.
            BmpEncoder encoder = new()
            {
                ProcessedAlphaMask = true,
                UseDoubleHeight = true,
                SkipFileHeader = true,
                SupportTransparency = false,
                BitsPerPixel = this.Entries[i].BitCount is 0
                    ? BmpBitsPerPixel.Pixel8
                    : (BmpBitsPerPixel?)this.Entries[i].BitCount
            };

            encoder.Encode(img, stream);
            this.Entries[i].BytesInRes = this.Entries[i].ImageOffset - (uint)stream.Position;
        }

        long endPosition = stream.Position;
        _ = stream.Seek(basePosition, SeekOrigin.Begin);
        this.FileHeader.WriteTo(stream);
        foreach (IconDirEntry entry in this.Entries)
        {
            entry.WriteTo(stream);
        }

        _ = stream.Seek(endPosition, SeekOrigin.Begin);
    }

    [MemberNotNull(nameof(Entries))]
    protected abstract void GetHeader(in Image image);
}
