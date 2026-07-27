// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Icon;

/// <summary>
/// Encodes ICO and CUR containers.
/// </summary>
internal abstract class IconEncoderCore
{
    private readonly QuantizingImageEncoder encoder;
    private readonly IconFileType iconFileType;

    /// <summary>
    /// Initializes a new instance of the <see cref="IconEncoderCore"/> class.
    /// </summary>
    /// <param name="encoder">The encoder options.</param>
    /// <param name="iconFileType">The icon container type.</param>
    protected IconEncoderCore(QuantizingImageEncoder encoder, IconFileType iconFileType)
    {
        this.encoder = encoder;
        this.iconFileType = iconFileType;
    }

    /// <summary>
    /// Supplies icon directory and color-table metadata without allocating intermediary metadata objects.
    /// </summary>
    internal interface IEncodingFrameMetadataProvider
    {
        /// <summary>
        /// Gets the encoding metadata for a source frame.
        /// </summary>
        /// <param name="frame">The source frame.</param>
        /// <param name="colorTable">The optional bitmap color table.</param>
        /// <returns>The encoding metadata.</returns>
        public EncodingFrameMetadata GetEncodingFrameMetadata(ImageFrame frame, out ReadOnlyMemory<Color>? colorTable);
    }

    /// <summary>
    /// Encodes all source frames using the metadata provider owned by the concrete icon format.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TProvider">The metadata provider type.</typeparam>
    /// <param name="image">The source image.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="provider">The frame metadata provider.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    protected void Encode<TPixel, TProvider>(Image<TPixel> image, Stream stream, TProvider provider, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
        where TProvider : struct, IEncodingFrameMetadataProvider
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        // Directory metadata is unmanaged and short-lived, so allocator-owned storage avoids an array plus one object per frame.
        using IMemoryOwner<EncodingFrameMetadata> owner = image.Configuration.MemoryAllocator.Allocate<EncodingFrameMetadata>(image.Frames.Count);
        Span<EncodingFrameMetadata> entries = owner.GetSpan()[..image.Frames.Count];
        this.Encode(image, stream, 0, entries, provider, cancellationToken);
    }

    /// <summary>
    /// Encodes a contiguous source-frame range using a stack-only metadata provider.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel type.</typeparam>
    /// <typeparam name="TProvider">The metadata provider type.</typeparam>
    /// <param name="image">The source image.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="frameIndex">The first source-frame index.</param>
    /// <param name="entries">The directory metadata for the source frames.</param>
    /// <param name="provider">The frame metadata provider.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    internal void Encode<TPixel, TProvider>(Image<TPixel> image, Stream stream, int frameIndex, Span<EncodingFrameMetadata> entries, TProvider provider, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
        where TProvider : struct, IEncodingFrameMetadataProvider
    {
        if ((uint)entries.Length > ushort.MaxValue)
        {
            throw new ImageFormatException("ICO and CUR resources cannot contain more than 65535 directory entries.");
        }

        // Offsets stored in ICO/CUR entries are relative to the start of this child resource, not the containing ANI stream.
        long basePosition = stream.Position;
        IconDir fileHeader = new(this.iconFileType, (ushort)entries.Length);

        // Reserve the directory first because BytesInRes and ImageOffset are known only after each payload is encoded.
        int dataOffset = IconDir.Size + (IconDirEntry.Size * entries.Length);
        _ = stream.Seek(dataOffset, SeekOrigin.Current);

        for (int i = 0; i < entries.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Since Windows Vista, the size of an image is determined from the BITMAPINFOHEADER structure or PNG image data
            // which technically allows storing icons with larger than 256 pixels, but such larger sizes are not recommended by Microsoft.
            ImageFrame<TPixel> frame = image.Frames[frameIndex + i];

            // The struct provider is statically dispatched, avoiding boxing and intermediary ICO/CUR metadata allocations.
            // Only unmanaged directory data survives until backpatching; the managed color table is consumed for this frame.
            entries[i] = provider.GetEncodingFrameMetadata(frame, out ReadOnlyMemory<Color>? colorTable);
            int width = entries[i].Entry.Width;
            if (width is 0)
            {
                width = frame.Width;
            }

            int height = entries[i].Entry.Height;
            if (height is 0)
            {
                height = frame.Height;
            }

            if (width > frame.Width || height > frame.Height)
            {
                // EncodingWidth and EncodingHeight are public metadata, so reject a crop that exceeds the source frame here.
                throw new ImageFormatException("The icon encoding dimensions exceed the source frame dimensions.");
            }

            long imageStart = stream.Position;
            entries[i].Entry.ImageOffset = checked((uint)(imageStart - basePosition));
            ref EncodingFrameMetadata encodingMetadata = ref entries[i];
            Image<TPixel>? encodingImage = null;

            try
            {
                bool requiresCrop = width != frame.Width || height != frame.Height;
                bool requiresIsolatedImage = encodingMetadata.Compression is IconFrameCompression.Png && image.Frames.Count > 1;

                if (requiresCrop || requiresIsolatedImage)
                {
                    // PNG accepts Image rather than ImageFrame, and ANI variants may occupy only part of their common canvas.
                    // Allocate only for those cases; full-sized BMP frames can be encoded directly from their existing storage.
                    ImageMetadata? metadata = this.encoder.SkipMetadata || encodingMetadata.Compression is not IconFrameCompression.Png ? null : image.Metadata.DeepClone();
                    encodingImage = new Image<TPixel>(image.Configuration, width, height, metadata);

                    for (int y = 0; y < height; y++)
                    {
                        frame.PixelBuffer.DangerousGetRowSpan(y)[..width].CopyTo(encodingImage.GetRootFramePixelBuffer().DangerousGetRowSpan(y));
                    }

                    if (!this.encoder.SkipMetadata && encodingMetadata.Compression is IconFrameCompression.Png)
                    {
                        encodingImage.Frames.RootFrame.Metadata.SetFormatMetadata(PngFormat.Instance, frame.Metadata.GetPngMetadata().DeepClone());
                    }
                }

                ImageFrame<TPixel> sourceFrame = encodingImage?.Frames.RootFrame ?? frame;

                // Compression and bitmap depth are per-entry, so the concrete encoder configuration must be selected per frame.
                switch (encodingMetadata.Compression)
                {
                    case IconFrameCompression.Bmp:
                    {
                        BmpEncoder bmpEncoder = new()
                        {
                            Quantizer = this.GetQuantizer(encodingMetadata, colorTable),
                            ProcessedAlphaMask = true,
                            UseDoubleHeight = true,
                            SkipFileHeader = true,
                            SupportTransparency = false,
                            TransparentColorMode = this.encoder.TransparentColorMode,
                            PixelSamplingStrategy = this.encoder.PixelSamplingStrategy,
                            BitsPerPixel = encodingMetadata.BmpBitsPerPixel,
                            SkipMetadata = this.encoder.SkipMetadata
                        };

                        BmpEncoderCore bmpEncoderCore = new(bmpEncoder, image.Configuration.MemoryAllocator);
                        bmpEncoderCore.Encode(sourceFrame, image.Metadata, stream, cancellationToken);
                        break;
                    }

                    case IconFrameCompression.Png:
                    {
                        PngEncoder pngEncoder = new()
                        {
                            // Only 32bit Png supported.
                            // https://devblogs.microsoft.com/oldnewthing/20101022-00/?p=12473
                            BitDepth = PngBitDepth.Bit8,
                            ColorType = PngColorType.RgbWithAlpha,
                            TransparentColorMode = this.encoder.TransparentColorMode,
                            CompressionLevel = PngCompressionLevel.BestCompression,
                            SkipMetadata = this.encoder.SkipMetadata
                        };

                        using PngEncoderCore pngEncoderCore = new(image.Configuration, pngEncoder);
                        pngEncoderCore.Encode(encodingImage ?? image, stream, cancellationToken);
                        break;
                    }

                    default:
                        throw new NotSupportedException();
                }
            }
            finally
            {
                encodingImage?.Dispose();
            }

            encodingMetadata.Entry.BytesInRes = checked((uint)(stream.Position - imageStart));
        }

        // Backpatch the reserved directory after every relative offset and payload length has been measured.
        long endPosition = stream.Position;
        _ = stream.Seek(basePosition, SeekOrigin.Begin);
        fileHeader.WriteTo(stream);
        foreach (EncodingFrameMetadata frame in entries)
        {
            frame.Entry.WriteTo(stream);
        }

        _ = stream.Seek(endPosition, SeekOrigin.Begin);
    }

    /// <summary>
    /// Gets the quantizer for an embedded bitmap frame.
    /// </summary>
    /// <param name="metadata">The frame encoding metadata.</param>
    /// <param name="colorTable">The optional bitmap color table.</param>
    /// <returns>The configured quantizer, or <see langword="null"/> when quantization is not required.</returns>
    private IQuantizer? GetQuantizer(EncodingFrameMetadata metadata, ReadOnlyMemory<Color>? colorTable)
    {
        // CUR stores its vertical hotspot in Entry.BitCount, so quantization must use the independent bitmap depth.
        if (metadata.BmpBitsPerPixel > BmpBitsPerPixel.Bit8)
        {
            return null;
        }

        if (this.encoder.Quantizer is not null)
        {
            return this.encoder.Quantizer;
        }

        if (colorTable is null)
        {
            int count = metadata.Entry.ColorCount;
            if (count == 0)
            {
                count = 256;
            }

            return new WuQuantizer(new QuantizerOptions
            {
                MaxColors = count
            });
        }

        // Don't dither if we have a palette. We want to preserve as much information as possible.
        return new PaletteQuantizer(colorTable.Value, new QuantizerOptions { Dither = null });
    }

    /// <summary>
    /// Stores the unmanaged per-frame state required while an icon directory is backpatched.
    /// </summary>
    internal struct EncodingFrameMetadata
    {
        /// <summary>
        /// The icon directory entry.
        /// </summary>
        public IconDirEntry Entry;

        /// <summary>
        /// Initializes a new instance of the <see cref="EncodingFrameMetadata"/> struct.
        /// </summary>
        /// <param name="compression">The embedded image compression.</param>
        /// <param name="bmpBitsPerPixel">The bitmap bit depth.</param>
        /// <param name="iconDirEntry">The icon directory entry.</param>
        public EncodingFrameMetadata(IconFrameCompression compression, BmpBitsPerPixel bmpBitsPerPixel, IconDirEntry iconDirEntry)
        {
            this.Compression = compression;
            this.BmpBitsPerPixel = compression == IconFrameCompression.Png
                ? BmpBitsPerPixel.Bit32
                : bmpBitsPerPixel;
            this.Entry = iconDirEntry;
        }

        /// <summary>
        /// Gets the embedded image compression.
        /// </summary>
        public IconFrameCompression Compression { get; }

        /// <summary>
        /// Gets the bitmap bit depth.
        /// </summary>
        public BmpBitsPerPixel BmpBitsPerPixel { get; }
    }
}
