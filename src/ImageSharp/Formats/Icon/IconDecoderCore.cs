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
    private IconDir fileHeader;
    private IconDirEntry[]? entries;

    /// <summary>
    /// Reusable storage for an icon directory entry and smaller fixed values.
    /// </summary>
    private InlineArray16<byte> buffer;

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
        // Stream may not at 0.
        long basePosition = stream.Position;
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
                int entryIndex = i;

                this.ExecuteImageDataSegmentAction(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ref IconDirEntry entry = ref this.entries[entryIndex];
                    this.SetFrameStreamBounds(frameStream, stream, basePosition, entry);
                    Span<byte> flag = this.buffer[..PngConstants.HeaderBytes.Length];
                    CheckEndOfStream(frameStream.Read(flag), flag.Length);
                    frameStream.Position = 0;

                    bool isPng = flag.SequenceEqual(PngConstants.HeaderBytes);
                    IconFrameCompression compression = isPng ? IconFrameCompression.Png : IconFrameCompression.Bmp;

                    // Frames remain alive until the largest decoded dimensions are known and the common canvas can be allocated.
                    Image<TPixel> decoded = this.GetDecoder(isPng).Decode<TPixel>(this.Options.Configuration, frameStream, cancellationToken);
                    decodedEntries[decodedCount++] = (entryIndex, decoded, compression);

                    // The embedded header is authoritative because a zero directory dimension can represent 256 pixels or a larger Vista-era PNG.
                    this.Dimensions = new Size(Math.Max(this.Dimensions.Width, decoded.Width), Math.Max(this.Dimensions.Height, decoded.Height));
                });
            }

            if (decodedCount is 0)
            {
                throw new InvalidImageContentException("The icon file does not contain any decodable image entries.");
            }

            // General profiles belong to the icon result even though the first successfully decoded child image is temporary.
            ImageMetadata metadata = decodedEntries[0].Image.Metadata.DeepClone();
            BmpMetadata? bmpMetadata = null;
            PngMetadata? pngMetadata = null;
            ImageFrame<TPixel>[] frames = new ImageFrame<TPixel>[decodedCount];
            int initializedFrameCount = 0;

            try
            {
                for (int i = 0; i < decodedCount; i++)
                {
                    BmpBitsPerPixel bitsPerPixel = BmpBitsPerPixel.Bit32;
                    ReadOnlyMemory<Color>? colorTable = null;
                    Image<TPixel> decoded = decodedEntries[i].Image;
                    ref IconDirEntry entry = ref this.entries[decodedEntries[i].EntryIndex];
                    ImageFrame<TPixel> source = decoded.Frames.RootFrameUnsafe;
                    ImageFrame<TPixel> target = new(this.Options.Configuration, this.Dimensions, source.Metadata.DeepClone());
                    frames[i] = target;
                    initializedFrameCount++;

                    for (int y = 0; y < source.Height; y++)
                    {
                        source.PixelBuffer.DangerousGetRowSpan(y).CopyTo(target.PixelBuffer.DangerousGetRowSpan(y));
                    }

                    // Preserve both the embedded format metadata and the ICO/CUR directory metadata on the output frame.
                    if (decodedEntries[i].Compression is IconFrameCompression.Png)
                    {
                        if (i == 0)
                        {
                            pngMetadata = decoded.Metadata.GetPngMetadata();
                        }
                    }
                    else
                    {
                        BmpMetadata currentBmpMetadata = decoded.Metadata.GetBmpMetadata();
                        bitsPerPixel = currentBmpMetadata.BitsPerPixel;
                        colorTable = currentBmpMetadata.ColorTable;

                        if (i == 0)
                        {
                            bmpMetadata = currentBmpMetadata;
                        }
                    }

                    this.SetFrameMetadata(metadata, target.Metadata, i, entry, decodedEntries[i].Compression, bitsPerPixel, colorTable);
                }

                // Embedded metadata belongs to the container even though the temporary decoded images are disposed below.
                if (bmpMetadata is not null)
                {
                    metadata.SetFormatMetadata(BmpFormat.Instance, bmpMetadata);
                }

                if (pngMetadata is not null)
                {
                    metadata.SetFormatMetadata(PngFormat.Instance, pngMetadata);
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
        // Stream may not at 0.
        long basePosition = stream.Position;
        this.ReadHeader(stream);

        ImageMetadata metadata = new();
        BmpMetadata? bmpMetadata = null;
        PngMetadata? pngMetadata = null;
        ImageFrameMetadata[] frames = new ImageFrameMetadata[this.entries.Length];
        int frameCount = 0;
        IconFrameStream frameStream = new(stream);
        this.Dimensions = default;

        for (int i = 0; i < frames.Length; i++)
        {
            int entryIndex = i;

            this.ExecuteImageDataSegmentAction(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                BmpBitsPerPixel bitsPerPixel = BmpBitsPerPixel.Bit32;
                ReadOnlyMemory<Color>? colorTable = null;
                ref IconDirEntry entry = ref this.entries[entryIndex];
                this.SetFrameStreamBounds(frameStream, stream, basePosition, entry);
                Span<byte> flag = this.buffer[..PngConstants.HeaderBytes.Length];
                CheckEndOfStream(frameStream.Read(flag), flag.Length);
                frameStream.Position = 0;

                bool isPng = flag.SequenceEqual(PngConstants.HeaderBytes);
                ImageInfo frameInfo = this.GetDecoder(isPng).Identify(this.Options.Configuration, frameStream, cancellationToken);
                ImageFrameMetadata frameMetadata = frameInfo.FrameMetadataCollection.Count is 0 ? new ImageFrameMetadata() : frameInfo.FrameMetadataCollection[0].DeepClone();

                if (frameCount is 0)
                {
                    // The container has one image-level metadata object, so the first valid entry supplies general profiles and resolution.
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

                if (isPng)
                {
                    if (frameCount is 0)
                    {
                        pngMetadata = frameInfo.Metadata.GetPngMetadata();
                    }
                }
                else
                {
                    BmpMetadata currentBmpMetadata = frameInfo.Metadata.GetBmpMetadata();
                    bitsPerPixel = currentBmpMetadata.BitsPerPixel;
                    colorTable = currentBmpMetadata.ColorTable;

                    if (frameCount is 0)
                    {
                        bmpMetadata = currentBmpMetadata;
                    }
                }

                IconFrameCompression compression = isPng ? IconFrameCompression.Png : IconFrameCompression.Bmp;
                this.SetFrameMetadata(metadata, frameMetadata, frameCount, entry, compression, bitsPerPixel, colorTable);
                frames[frameCount++] = frameMetadata;

                // Identification uses the same embedded-header dimensions as decoding, without allocating pixel buffers.
                this.Dimensions = new Size(Math.Max(this.Dimensions.Width, frameInfo.Width), Math.Max(this.Dimensions.Height, frameInfo.Height));
            });
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

        // Copy the format specific metadata to the image.
        if (bmpMetadata is not null)
        {
            metadata.SetFormatMetadata(BmpFormat.Instance, bmpMetadata);
        }

        if (pngMetadata is not null)
        {
            metadata.SetFormatMetadata(PngFormat.Instance, pngMetadata);
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
    /// <param name="colorTable">The embedded bitmap color table.</param>
    protected abstract void SetFrameMetadata(
        ImageMetadata imageMetadata,
        ImageFrameMetadata frameMetadata,
        int index,
        in IconDirEntry entry,
        IconFrameCompression compression,
        BmpBitsPerPixel bitsPerPixel,
        ReadOnlyMemory<Color>? colorTable);

    /// <summary>
    /// Reads the icon directory entries needed by the configured frame limit.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    [MemberNotNull(nameof(entries))]
    private void ReadHeader(Stream stream)
    {
        Span<byte> buffer = this.buffer;

        // ICONDIR
        CheckEndOfStream(stream.Read(buffer[..IconDir.Size]), IconDir.Size);
        this.fileHeader = IconDir.Parse(buffer);
        if (this.fileHeader.Reserved != 0 || this.fileHeader.Type != this.iconFileType || this.fileHeader.Count == 0)
        {
            throw new InvalidImageContentException("The icon directory header is invalid.");
        }

        // ICONDIRENTRY
        int entryCount = (int)Math.Min(this.fileHeader.Count, this.Options.MaxFrames);
        this.entries = new IconDirEntry[entryCount];
        for (int i = 0; i < this.entries.Length; i++)
        {
            CheckEndOfStream(stream.Read(buffer[..IconDirEntry.Size]), IconDirEntry.Size);
            this.entries[i] = IconDirEntry.Parse(buffer);
        }
    }

    /// <summary>
    /// Creates the decoder configured for an embedded PNG or headerless, double-height bitmap frame.
    /// </summary>
    /// <param name="isPng">Whether the embedded frame has a PNG signature.</param>
    /// <returns>The configured frame decoder.</returns>
    private ImageDecoderCore GetDecoder(bool isPng)
    {
        if (isPng)
        {
            return new PngDecoderCore(new PngDecoderOptions
            {
                GeneralOptions = this.Options
            });
        }

        return new BmpDecoderCore(new BmpDecoderOptions
        {
            GeneralOptions = this.Options,
            ProcessedAlphaMask = true,
            SkipFileHeader = true,
            UseDoubleHeight = true
        });
    }

    /// <summary>
    /// Creates a seekable view bounded to one directory entry's declared payload.
    /// </summary>
    /// <param name="frameStream">The reusable bounded payload stream.</param>
    /// <param name="stream">The containing icon stream.</param>
    /// <param name="basePosition">The absolute start of the icon resource.</param>
    /// <param name="entry">The directory entry describing the payload.</param>
    private void SetFrameStreamBounds(IconFrameStream frameStream, BufferedReadStream stream, long basePosition, in IconDirEntry entry)
    {
        long available = stream.Length - basePosition;
        uint directorySize = (uint)(IconDir.Size + (this.fileHeader.Count * IconDirEntry.Size));

        // Offsets are relative to the icon resource and must not point into its directory or at or beyond its containing stream.
        if (entry.Reserved is not 0
            || entry.BytesInRes is 0
            || entry.ImageOffset < directorySize
            || entry.ImageOffset >= available)
        {
            throw new InvalidImageContentException("The icon directory contains an invalid image resource range.");
        }

        long remaining = available - entry.ImageOffset;
        long length = entry.BytesInRes;
        if (length > remaining)
        {
            if (this.fileHeader.Count is not 1)
            {
                // Clamping a multi-entry resource could expose the next image payload to the current child decoder.
                throw new InvalidImageContentException("The icon directory contains an invalid image resource range.");
            }

            // Some established single-image ICO files overstate BytesInRes but contain a complete payload.
            // The containing stream is still a safe hard boundary because no sibling image can follow it.
            length = remaining;
        }

        frameStream.Reset(basePosition + entry.ImageOffset, length);
    }

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
