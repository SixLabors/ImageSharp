// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Icon;

/// <summary>
/// Decodes the shared ICO/CUR directory and embedded BMP or PNG frame payloads.
/// </summary>
internal abstract class IconDecoderCore : ImageDecoderCore
{
    private readonly IconFileType iconFileType;
    private IconDirEntry[]? entries;

    /// <summary>
    /// Reusable storage for an icon directory entry and smaller fixed values.
    /// </summary>
    private InlineArray16<byte> buffer;

    /// <summary>
    /// The absolute start of the icon resource. <see cref="ReadHeader"/> sets it. The entry offsets are relative to it.
    /// </summary>
    private long basePosition;

    /// <summary>
    /// Initializes a new instance of the <see cref="IconDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    /// <param name="iconFileType">The expected icon container type.</param>
    protected IconDecoderCore(DecoderOptions options, IconFileType iconFileType)
        : base(options)
        => this.iconFileType = iconFileType;

    /// <inheritdoc />
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.ReadHeader(stream);

        int entryCount = this.entries.Length;
        (int EntryIndex, Image<TPixel> Image, IconFrameCompression Compression)[] decodedEntries = new (int, Image<TPixel>, IconFrameCompression)[entryCount];
        int decodedCount = 0;
        IconFrameStream frameStream = new(stream);
        this.Dimensions = default;

        try
        {
            for (int i = 0; i < entryCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!this.TryProbeEntry(frameStream, ref this.entries[i], out IconFrameCompression compression))
                {
                    continue;
                }

                // The BMP or PNG decoder of the entry gets the same options. Thus it applies the same integrity mode to the pixel data of the entry. The frames
                // stay alive until the decoder knows the largest decoded dimensions and can allocate the common canvas.
                Image<TPixel> decoded = this.GetDecoder(compression).Decode<TPixel>(this.Options.Configuration, frameStream, cancellationToken);
                decodedEntries[decodedCount++] = (i, decoded, compression);

                // The embedded header gives the dimensions. A zero directory dimension can mean 256 pixels, or a larger PNG image (Windows Vista and later).
                this.Dimensions = new Size(Math.Max(this.Dimensions.Width, decoded.Width), Math.Max(this.Dimensions.Height, decoded.Height));
            }

            if (decodedCount is 0)
            {
                throw new InvalidImageContentException("The icon file does not contain any decodable image entries.");
            }

            // General profiles belong to the icon result even though the first successfully decoded child image is temporary.
            ImageMetadata metadata = decodedEntries[0].Image.Metadata.DeepClone();
            ImageFrame<TPixel>[] frames = new ImageFrame<TPixel>[decodedCount];
            int initializedFrameCount = 0;

            try
            {
                for (int i = 0; i < decodedCount; i++)
                {
                    Image<TPixel> decoded = decodedEntries[i].Image;
                    ref IconDirEntry entry = ref this.entries[decodedEntries[i].EntryIndex];
                    ImageFrame<TPixel> source = decoded.Frames.RootFrameUnsafe;
                    ImageFrameMetadata frameMetadata = source.Metadata.DeepClone();

                    // PNG and BMP store ICC on the child image. Each icon entry needs its own retained profile on its output frame.
                    frameMetadata.IccProfile ??= decoded.Metadata.IccProfile?.DeepClone();
                    ImageFrame<TPixel> target = new(this.Options.Configuration, this.Dimensions, frameMetadata);
                    frames[i] = target;
                    initializedFrameCount++;

                    for (int y = 0; y < source.Height; y++)
                    {
                        source.PixelBuffer.DangerousGetRowSpan(y).CopyTo(target.PixelBuffer.DangerousGetRowSpan(y));
                    }

                    this.SetEntryMetadata(metadata, target.Metadata, i, ref entry, decodedEntries[i].Compression, decoded.Metadata);
                }

                Image<TPixel> result = new(this.Options.Configuration, metadata, frames);

                // Ownership of every output frame transfers to the result only after construction succeeds.
                initializedFrameCount = 0;
                return result;
            }
            finally
            {
                for (int i = 0; i < initializedFrameCount; i++)
                {
                    frames[i].Dispose();
                }
            }
        }
        finally
        {
            for (int i = 0; i < decodedCount; i++)
            {
                decodedEntries[i].Image.Dispose();
            }
        }
    }

    /// <inheritdoc />
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.ReadHeader(stream);

        ImageMetadata metadata = new();
        ImageFrameMetadata[] frames = new ImageFrameMetadata[this.entries.Length];
        int frameCount = 0;
        IconFrameStream frameStream = new(stream);
        this.Dimensions = default;

        for (int i = 0; i < frames.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ref IconDirEntry entry = ref this.entries[i];
            if (!this.TryProbeEntry(frameStream, ref entry, out IconFrameCompression compression))
            {
                continue;
            }

            ImageInfo frameInfo = this.GetDecoder(compression).Identify(this.Options.Configuration, frameStream, cancellationToken);
            ImageFrameMetadata frameMetadata = frameInfo.FrameMetadataCollection.Count is 0
                ? new ImageFrameMetadata()
                : frameInfo.FrameMetadataCollection[0].DeepClone();

            // Identify keeps the image-level profile of each resource on its frame, as Decode does.
            frameMetadata.IccProfile ??= frameInfo.Metadata.IccProfile?.DeepClone();

            if (frameCount is 0)
            {
                // The container has one image-level metadata object. Thus the first valid entry gives the general profiles and the resolution.
                ImageMetadata sourceMetadata = frameInfo.Metadata;
                metadata.HorizontalResolution = sourceMetadata.HorizontalResolution;
                metadata.VerticalResolution = sourceMetadata.VerticalResolution;
                metadata.ResolutionUnits = sourceMetadata.ResolutionUnits;
                metadata.ExifProfile = sourceMetadata.ExifProfile?.DeepClone();
                metadata.IccProfile = sourceMetadata.IccProfile?.DeepClone();
                metadata.IptcProfile = sourceMetadata.IptcProfile?.DeepClone();
                metadata.XmpProfile = sourceMetadata.XmpProfile?.DeepClone();
                metadata.CicpProfile = sourceMetadata.CicpProfile?.DeepClone();
            }

            this.SetEntryMetadata(metadata, frameMetadata, frameCount, ref entry, compression, frameInfo.Metadata);
            frames[frameCount++] = frameMetadata;

            // Identify uses the same embedded-header dimensions as Decode, but it does not allocate pixel buffers.
            this.Dimensions = new Size(Math.Max(this.Dimensions.Width, frameInfo.Width), Math.Max(this.Dimensions.Height, frameInfo.Height));
        }

        if (frameCount is 0)
        {
            throw new InvalidImageContentException("The icon file does not contain any identifiable image entries.");
        }

        if (frameCount != frames.Length)
        {
            // Preserve successfully identified frames when truncated image data ends the scan before the declared directory count.
            Array.Resize(ref frames, frameCount);
        }

        return new ImageInfo(this.Dimensions, metadata, frames);
    }

    /// <summary>
    /// Copies format-specific directory and embedded-frame metadata to an output frame.
    /// </summary>
    /// <param name="imageMetadata">The output image metadata.</param>
    /// <param name="frameMetadata">The output frame metadata.</param>
    /// <param name="index">The directory entry index.</param>
    /// <param name="entry">The directory entry.</param>
    /// <param name="compression">The embedded frame compression.</param>
    /// <param name="bitsPerPixel">The embedded bitmap bit depth.</param>
    /// <param name="colorType">The color type of the embedded bitmap.</param>
    /// <param name="colorTable">The embedded bitmap color table.</param>
    protected abstract void SetFrameMetadata(
        ImageMetadata imageMetadata,
        ImageFrameMetadata frameMetadata,
        int index,
        in IconDirEntry entry,
        IconFrameCompression compression,
        BmpBitsPerPixel bitsPerPixel,
        BmpColorType colorType,
        ReadOnlyMemory<Color>? colorTable);

    /// <summary>
    /// Reads the icon directory entries that the configured frame limit needs. Then it checks the payload range of each entry before an entry decodes.
    /// </summary>
    /// <param name="stream">The source stream. The icon resource starts at its current position.</param>
    [MemberNotNull(nameof(entries))]
    private void ReadHeader(BufferedReadStream stream)
    {
        this.basePosition = stream.Position;
        Span<byte> buffer = this.buffer;

        // ICONDIR
        CheckEndOfStream(stream.Read(buffer[..IconDir.Size]), IconDir.Size);
        IconDir fileHeader = IconDir.Parse(buffer);
        if (fileHeader.Reserved != 0 || fileHeader.Type != this.iconFileType || fileHeader.Count == 0)
        {
            throw new InvalidImageContentException("The icon directory header is invalid.");
        }

        // ICONDIRENTRY
        long available = stream.Length - this.basePosition;
        long lastStart = 0;
        int entryCount = (int)Math.Min(fileHeader.Count, this.Options.MaxFrames);
        this.entries = new IconDirEntry[entryCount];
        for (int i = 0; i < this.entries.Length; i++)
        {
            CheckEndOfStream(stream.Read(buffer[..IconDirEntry.Size]), IconDirEntry.Size);
            this.entries[i] = IconDirEntry.Parse(buffer);

            // The last payload start in the stream shows if a clamped payload exposes the payload of another entry.
            if (this.entries[i].ImageOffset < available)
            {
                lastStart = Math.Max(lastStart, this.entries[i].ImageOffset);
            }
        }

        // An offset must not point into the directory, or at or after the end of the stream. The decoder clamps a payload that continues after the end of the
        // stream. A clamped payload can expose the payload of the next entry to the child decoder. Thus the clamp is safe only if the decoder read each
        // directory entry, and no other payload starts after the start of this payload.
        uint directorySize = (uint)(IconDir.Size + (fileHeader.Count * IconDirEntry.Size));
        bool allEntriesRead = entryCount == fileHeader.Count;
        for (int i = 0; i < this.entries.Length; i++)
        {
            ref IconDirEntry entry = ref this.entries[i];
            long remaining = available - entry.ImageOffset;
            bool truncated = entry.BytesInRes > remaining;
            bool exposesSibling = truncated && (!allEntriesRead || lastStart > entry.ImageOffset);
            if (entry.Reserved is not 0 || entry.ImageOffset < directorySize || remaining <= 0 || exposesSibling)
            {
                throw new InvalidImageContentException("The icon directory contains an invalid image resource range.");
            }

            if (truncated)
            {
                if (fileHeader.Count is not 1)
                {
                    // The stream ends inside this payload, so the payload is truncated image data. If the mode ignores the error, the child decoder keeps the
                    // pixels that it can decode from the payload.
                    this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("The icon image resource is truncated.");
                }

                // Some established single-image ICO files overstate BytesInRes but contain a complete payload. The end of the stream is a safe hard boundary,
                // because no other payload follows this payload. The entry keeps the clamped length, so the probe and the child decoder read only the bytes
                // that the stream holds.
                entry.BytesInRes = (uint)remaining;
            }
        }
    }

    /// <summary>
    /// Bounds the frame stream to the payload of a directory entry. Then it reads the signature that selects the decoder of the entry.
    /// </summary>
    /// <param name="frameStream">The reusable bounded payload stream. If the method returns <see langword="true"/>, the stream is at the start of the
    /// payload.</param>
    /// <param name="entry">The directory entry. <see cref="ReadHeader"/> checked its payload range.</param>
    /// <param name="compression">The compression of the payload.</param>
    /// <returns>
    /// <see langword="true"/> if the payload holds the signature bytes. <see langword="false"/> if the payload is too short and the mode ignores image data
    /// errors.
    /// </returns>
    private bool TryProbeEntry(IconFrameStream frameStream, ref IconDirEntry entry, out IconFrameCompression compression)
    {
        compression = IconFrameCompression.Bmp;
        frameStream.Reset(this.basePosition + entry.ImageOffset, entry.BytesInRes);

        // This probe also owns an empty payload. No BMP or PNG image is shorter than the PNG signature.
        Span<byte> signature = this.buffer[..PngConstants.HeaderBytes.Length];
        if (frameStream.Read(signature) != signature.Length)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("Not enough bytes to read the icon frame header.");
            return false;
        }

        // The child decoder reads the payload from its start.
        frameStream.Position = 0;
        if (signature.SequenceEqual(PngConstants.HeaderBytes))
        {
            compression = IconFrameCompression.Png;
        }

        return true;
    }

    /// <summary>
    /// Copies the directory metadata and the metadata of the embedded BMP or PNG image of one entry to an output frame.
    /// </summary>
    /// <param name="imageMetadata">The output image metadata.</param>
    /// <param name="frameMetadata">The output frame metadata.</param>
    /// <param name="index">The output frame index. The first output frame also gives the BMP or PNG metadata of the image.</param>
    /// <param name="entry">The directory entry.</param>
    /// <param name="compression">The compression of the embedded frame.</param>
    /// <param name="entryMetadata">The image metadata from the BMP or PNG decoder of the entry.</param>
    private void SetEntryMetadata(
        ImageMetadata imageMetadata,
        ImageFrameMetadata frameMetadata,
        int index,
        ref IconDirEntry entry,
        IconFrameCompression compression,
        ImageMetadata entryMetadata)
    {
        BmpBitsPerPixel bitsPerPixel = BmpBitsPerPixel.Bit32;
        BmpColorType colorType = BmpColorType.Bgra;
        ReadOnlyMemory<Color>? colorTable = null;

        // The embedded format metadata belongs to the container, but the decoder disposes the temporary entry images.
        if (compression is IconFrameCompression.Png)
        {
            if (index == 0)
            {
                imageMetadata.SetFormatMetadata(PngFormat.Instance, entryMetadata.GetPngMetadata());
            }
        }
        else
        {
            BmpMetadata bmpMetadata = entryMetadata.GetBmpMetadata();
            (bitsPerPixel, colorType) = GetBitmapLayout(bmpMetadata);
            colorTable = GetBitmapColorTable(bmpMetadata);

            if (index == 0)
            {
                imageMetadata.SetFormatMetadata(BmpFormat.Instance, bmpMetadata);
            }
        }

        this.SetFrameMetadata(imageMetadata, frameMetadata, index, entry, compression, bitsPerPixel, colorType, colorTable);
    }

    /// <summary>
    /// Creates the decoder for an embedded PNG frame or a headerless, double-height bitmap frame.
    /// </summary>
    /// <param name="compression">The compression of the embedded frame.</param>
    /// <returns>The configured frame decoder.</returns>
    private ImageDecoderCore GetDecoder(IconFrameCompression compression)
    {
        if (compression is IconFrameCompression.Png)
        {
            return new PngDecoderCore(new PngDecoderOptions(this.Options));
        }

        return new BmpDecoderCore(CreateBitmapDecoderOptions(this.Options));
    }

    /// <summary>
    /// Gets the color table of an icon bitmap for the frame metadata.
    /// </summary>
    /// <remarks>
    /// ICONIMAGE uses only six BITMAPINFOHEADER members, and "All other members must be 0", biClrUsed too. With
    /// biClrUsed 0, a bitmap above 8 bits per pixel has no color table: https://learn.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10)
    /// A direct-color bitmap can store an optional table of any size, which the icon formats do not use.
    /// </remarks>
    /// <param name="metadata">The BMP metadata of the decoded bitmap.</param>
    /// <returns>The color table, or <see langword="null"/> for a direct-color bitmap.</returns>
    public static ReadOnlyMemory<Color>? GetBitmapColorTable(BmpMetadata metadata)
        => metadata.BitsPerPixel <= BmpBitsPerPixel.Bit8 ? metadata.ColorTable : null;

    /// <summary>
    /// Gets the bit depth and the color type of an icon bitmap for the frame metadata.
    /// </summary>
    /// <remarks>
    /// The bitmap of an icon image must set biCompression to 0 (BI_RGB), so the icon encoder writes BI_RGB only:
    /// https://learn.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10)
    /// A 16-bit BI_RGB bitmap has the 5-5-5 layout. A decoded 5-6-5 bitmap gets the 24-bit layout, so a re-encode keeps
    /// each bit.
    /// </remarks>
    /// <param name="metadata">The BMP metadata of the decoded bitmap.</param>
    /// <returns>The bit depth and the color type.</returns>
    public static (BmpBitsPerPixel BitsPerPixel, BmpColorType ColorType) GetBitmapLayout(BmpMetadata metadata)
        => metadata.ColorType is BmpColorType.Bgr565
            ? (BmpBitsPerPixel.Bit24, BmpColorType.Bgr)
            : (metadata.BitsPerPixel, metadata.ColorType);

    /// <summary>
    /// Creates the BMP decoder options for the bitmap of an icon image. The bitmap is a DIB with no file header. Its height
    /// covers the color data and the AND mask that follows it.
    /// </summary>
    /// <remarks>
    /// The ICO, CUR and ANI decoders use these options, so each icon bitmap decodes in the same way.
    /// </remarks>
    /// <param name="options">The general decoder options.</param>
    /// <returns>The BMP decoder options.</returns>
    public static BmpDecoderOptions CreateBitmapDecoderOptions(DecoderOptions options)
        => new(options)
        {
            ProcessedAlphaMask = true,
            SkipFileHeader = true,
            UseDoubleHeight = true
        };

    /// <summary>
    /// Ensures that a complete fixed-size directory structure was read.
    /// </summary>
    /// <param name="bytesRead">The number of bytes read.</param>
    /// <param name="expectedLength">The required structure length.</param>
    private static void CheckEndOfStream(int bytesRead, int expectedLength)
    {
        if (bytesRead != expectedLength)
        {
            throw new InvalidImageContentException("Not enough bytes to read icon header.");
        }
    }
}
