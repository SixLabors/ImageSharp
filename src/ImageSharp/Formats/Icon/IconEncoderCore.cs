// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
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
        /// <returns>The encoding metadata.</returns>
        public EncodingFrameMetadata GetEncodingFrameMetadata(ImageFrame frame);

        /// <summary>
        /// Gets the bitmap color table for a source frame.
        /// </summary>
        /// <param name="frame">The source frame.</param>
        /// <returns>The bitmap color table, or <see langword="null"/>.</returns>
        public ReadOnlyMemory<Color>? GetColorTable(ImageFrame frame);
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

        // The encoder checks the metadata of each frame before the first write. Thus, if a frame has invalid metadata, the encoder writes no bytes.
        for (int i = 0; i < image.Frames.Count; i++)
        {
            ImageFrame<TPixel> frame = image.Frames[i];
            ValidateEntry(provider.GetEncodingFrameMetadata(frame), frame.Size);
        }

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
    internal void Encode<TPixel, TProvider>(
        Image<TPixel> image,
        Stream stream,
        int frameIndex,
        Span<EncodingFrameMetadata> entries,
        TProvider provider,
        CancellationToken cancellationToken)
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

        // The struct provider uses static dispatch. Thus the encoder does not box the provider or allocate intermediate ICO or CUR metadata. The caller checks
        // the metadata of each frame before the first write.
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i] = provider.GetEncodingFrameMetadata(image.Frames[frameIndex + i]);
        }

        // Reserve the directory first because BytesInRes and ImageOffset are known only after each payload is encoded.
        int dataOffset = IconDir.Size + (IconDirEntry.Size * entries.Length);
        _ = stream.Seek(dataOffset, SeekOrigin.Current);

        for (int i = 0; i < entries.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ImageFrame<TPixel> frame = image.Frames[frameIndex + i];
            (int width, int height) = entries[i].GetEncodingSize(frame.Size);

            long imageStart = stream.Position;

            // The directory is less than 1 MiB and the end check below bounds each earlier payload, so this offset fits in uint.
            entries[i].Entry.ImageOffset = (uint)(imageStart - basePosition);
            ref EncodingFrameMetadata encodingMetadata = ref entries[i];
            Image<TPixel>? encodingImage = null;
            ImageMetadata? copiedMetadata = null;

            try
            {
                bool requiresCrop = width != frame.Width || height != frame.Height;
                bool requiresIsolatedImage = encodingMetadata.Compression is IconFrameCompression.Png && image.Frames.Count > 1;

                if (requiresCrop || requiresIsolatedImage)
                {
                    // PNG accepts Image rather than ImageFrame, and ANI variants may occupy only part of their common canvas.
                    // Allocate only for those cases; full-sized BMP frames can be encoded directly from their existing storage.
                    copiedMetadata = this.encoder.SkipMetadata || encodingMetadata.Compression is not IconFrameCompression.Png
                        ? null
                        : image.Metadata.DeepClone();

                    encodingImage = new Image<TPixel>(image.Configuration, width, height, copiedMetadata);

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
                IccProfile? frameProfile = frame.Metadata.IccProfile;
                IccProfile? imageProfile = image.Metadata.IccProfile;
                if (copiedMetadata != null)
                {
                    // The metadata clone already checked the source image profile. Use its copy for this resource.
                    if (ReferenceEquals(frameProfile, imageProfile))
                    {
                        frameProfile = copiedMetadata.IccProfile;
                    }

                    imageProfile = copiedMetadata.IccProfile;
                }

                IccProfile? sourceProfile = IccProfile.SelectValidProfile(frameProfile, imageProfile);

                // Compression and bitmap depth are per-entry, so the concrete encoder configuration must be selected per frame.
                switch (encodingMetadata.Compression)
                {
                    case IconFrameCompression.Bmp:
                    {
                        BmpEncoder bmpEncoder = new()
                        {
                            Quantizer = this.GetQuantizer(encodingMetadata, provider.GetColorTable(frame)),
                            ProcessedAlphaMask = true,
                            UseDoubleHeight = true,
                            SkipFileHeader = true,
                            TransparentColorMode = this.encoder.TransparentColorMode,
                            PixelSamplingStrategy = this.encoder.PixelSamplingStrategy,
                            BitsPerPixel = encodingMetadata.BmpBitsPerPixel,

                            // ICONIMAGE uses only six BITMAPINFOHEADER members, and "All other members must be 0", so
                            // biCompression is 0 (BI_RGB): https://learn.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10)
                            // BI_RGB stores a palette, or BGR without masks. A 32-bit icon keeps alpha in the fourth byte
                            // of each pixel, which BI_RGB does not define. The encoder sets the compression, so the BMP
                            // metadata of the image cannot select RLE.
                            ColorType = encodingMetadata.BmpBitsPerPixel <= BmpBitsPerPixel.Bit8 ? BmpColorType.Palette : BmpColorType.Bgr,
                            Compression = BmpCompression.None,
                            SkipMetadata = this.encoder.SkipMetadata
                        };

                        BmpEncoderCore bmpEncoderCore = new(bmpEncoder, image.Configuration.MemoryAllocator);
                        bmpEncoderCore.Encode(sourceFrame, image.Metadata, sourceProfile, stream, cancellationToken);
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
                        pngEncoderCore.Encode(encodingImage ?? image, sourceProfile, stream, cancellationToken);
                        break;
                    }
                }
            }
            finally
            {
                encodingImage?.Dispose();
            }

            // ImageOffset and BytesInRes are 32-bit fields. If the payload ends inside that range, its length and the next offset fit too.
            if (stream.Position - basePosition > uint.MaxValue)
            {
                throw new ImageFormatException("ICO and CUR resources cannot be larger than 4294967295 bytes.");
            }

            encodingMetadata.Entry.BytesInRes = (uint)(stream.Position - imageStart);
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
    /// Checks the encoding metadata of one frame before the first write, because the values come from public metadata.
    /// </summary>
    /// <param name="metadata">The encoding metadata of the frame.</param>
    /// <param name="frameSize">The size of the source frame.</param>
    /// <exception cref="ImageFormatException">
    /// The compression is not BMP or PNG, or the encoding dimensions are larger than the source frame dimensions.
    /// </exception>
    public static void ValidateEntry(EncodingFrameMetadata metadata, Size frameSize)
    {
        if (metadata.Compression is not (IconFrameCompression.Bmp or IconFrameCompression.Png))
        {
            throw new ImageFormatException("ICO and CUR frames must use BMP or PNG compression.");
        }

        // EncodingWidth and EncodingHeight select a crop of the source frame. A crop cannot be larger than the source frame.
        Size size = metadata.GetEncodingSize(frameSize);
        if (size.Width > frameSize.Width || size.Height > frameSize.Height)
        {
            throw new ImageFormatException("The icon encoding dimensions exceed the source frame dimensions.");
        }
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

        // Keep the colors of the palette.
        return EncodingUtilities.CreateDecodedPaletteQuantizer(colorTable.Value, this.encoder.TransparentColorMode);
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

        /// <summary>
        /// Gets the size of the encoded image. A zero directory dimension means 256 pixels or more, so the image keeps the frame dimension.
        /// </summary>
        /// <remarks>
        /// Since Windows Vista, the BITMAPINFOHEADER structure or the PNG image data gives the size of an image. Thus an icon image can be larger than 256
        /// pixels, but Microsoft does not recommend these sizes.
        /// </remarks>
        /// <param name="frameSize">The size of the source frame.</param>
        /// <returns>The size of the encoded image.</returns>
        public readonly Size GetEncodingSize(Size frameSize)
            => new(this.Entry.Width is 0 ? frameSize.Width : this.Entry.Width, this.Entry.Height is 0 ? frameSize.Height : this.Entry.Height);
    }
}
