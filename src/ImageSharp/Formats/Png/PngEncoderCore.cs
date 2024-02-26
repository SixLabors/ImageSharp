// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.IO.Hashing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.Formats.Png.Filters;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Performs the png encoding operation.
/// </summary>
internal sealed class PngEncoderCore : IImageEncoderInternals, IDisposable
{
    /// <summary>
    /// The maximum block size, defaults at 64k for uncompressed blocks.
    /// </summary>
    private const int MaxBlockSize = 65535;

    /// <summary>
    /// Used the manage memory allocations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// The configuration instance for the encoding operation.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Reusable buffer for writing chunk data.
    /// </summary>
    private ScratchBuffer chunkDataBuffer;  // mutable struct, don't make readonly

    /// <summary>
    /// The encoder with options
    /// </summary>
    private readonly PngEncoder encoder;

    /// <summary>
    /// The gamma value
    /// </summary>
    private float? gamma;

    /// <summary>
    /// The color type.
    /// </summary>
    private PngColorType colorType;

    /// <summary>
    /// The number of bits per sample or per palette index (not per pixel).
    /// </summary>
    private byte bitDepth;

    /// <summary>
    /// The filter method used to prefilter the encoded pixels before compression.
    /// </summary>
    private PngFilterMethod filterMethod;

    /// <summary>
    /// Gets the interlace mode.
    /// </summary>
    private PngInterlaceMode interlaceMode;

    /// <summary>
    /// The chunk filter method. This allows to filter ancillary chunks.
    /// </summary>
    private PngChunkFilter chunkFilter;

    /// <summary>
    /// A value indicating whether to use 16 bit encoding for supported color types.
    /// </summary>
    private bool use16Bit;

    /// <summary>
    /// The number of bytes per pixel.
    /// </summary>
    private int bytesPerPixel;

    /// <summary>
    /// The image width.
    /// </summary>
    private int width;

    /// <summary>
    /// The image height.
    /// </summary>
    private int height;

    /// <summary>
    /// The raw data of previous scanline.
    /// </summary>
    private IMemoryOwner<byte> previousScanline = null!;

    /// <summary>
    /// The raw data of current scanline.
    /// </summary>
    private IMemoryOwner<byte> currentScanline = null!;

    /// <summary>
    /// The color profile name.
    /// </summary>
    private const string ColorProfileName = "ICC Profile";

    /// <summary>
    /// The encoder quantizer, if present.
    /// </summary>
    private IQuantizer? quantizer;

    /// <summary>
    /// Any explicit quantized transparent index provided by the background color.
    /// </summary>
    private int derivedTransparencyIndex = -1;

    /// <summary>
    /// A reusable Crc32 hashing instance.
    /// </summary>
    private readonly Crc32 crc32 = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PngEncoderCore" /> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="encoder">The encoder with options.</param>
    public PngEncoderCore(Configuration configuration, PngEncoder encoder)
    {
        this.configuration = configuration;
        this.memoryAllocator = configuration.MemoryAllocator;
        this.encoder = encoder;
        this.quantizer = encoder.Quantizer;
    }

    /// <summary>
    /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
    /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        this.width = image.Width;
        this.height = image.Height;

        ImageMetadata metadata = image.Metadata;
        PngMetadata pngMetadata = GetPngMetadata(image);
        this.SanitizeAndSetEncoderOptions<TPixel>(this.encoder, pngMetadata, out this.use16Bit, out this.bytesPerPixel);

        stream.Write(PngConstants.HeaderBytes);

        ImageFrame<TPixel>? clonedFrame = null;
        ImageFrame<TPixel> currentFrame = image.Frames.RootFrame;

        bool clearTransparency = this.encoder.TransparentColorMode is PngTransparentColorMode.Clear;
        if (clearTransparency)
        {
            currentFrame = clonedFrame = currentFrame.Clone();
            ClearTransparentPixels(currentFrame);
        }

        // Do not move this. We require an accurate bit depth for the header chunk.
        IndexedImageFrame<TPixel>? quantized = this.CreateQuantizedImageAndUpdateBitDepth(
            pngMetadata,
            currentFrame,
            currentFrame.Bounds(),
            null);

        this.WriteHeaderChunk(stream);
        this.WriteGammaChunk(stream);
        this.WriteCicpChunk(stream, metadata);
        this.WriteColorProfileChunk(stream, metadata);
        this.WritePaletteChunk(stream, quantized);
        this.WriteTransparencyChunk(stream, pngMetadata);
        this.WritePhysicalChunk(stream, metadata);
        this.WriteExifChunk(stream, metadata);
        this.WriteXmpChunk(stream, metadata);
        this.WriteTextChunks(stream, pngMetadata);

        if (image.Frames.Count > 1)
        {
            this.WriteAnimationControlChunk(stream, (uint)image.Frames.Count, pngMetadata.RepeatCount);

            // Write the first frame.
            PngFrameMetadata frameMetadata = GetPngFrameMetadata(currentFrame);
            PngDisposalMethod previousDisposal = frameMetadata.DisposalMethod;
            FrameControl frameControl = this.WriteFrameControlChunk(stream, frameMetadata, currentFrame.Bounds(), 0);
            this.WriteDataChunks(frameControl, currentFrame.PixelBuffer.GetRegion(), quantized, stream, false);

            // Capture the global palette for reuse on subsequent frames.
            ReadOnlyMemory<TPixel>? previousPalette = quantized?.Palette.ToArray();

            // Write following frames.
            uint increment = 0;
            ImageFrame<TPixel> previousFrame = image.Frames.RootFrame;

            // This frame is reused to store de-duplicated pixel buffers.
            using ImageFrame<TPixel> encodingFrame = new(image.Configuration, previousFrame.Size());

            for (int i = 1; i < image.Frames.Count; i++)
            {
                ImageFrame<TPixel>? prev = previousDisposal == PngDisposalMethod.RestoreToBackground ? null : previousFrame;
                currentFrame = image.Frames[i];
                ImageFrame<TPixel>? nextFrame = i < image.Frames.Count - 1 ? image.Frames[i + 1] : null;

                frameMetadata = GetPngFrameMetadata(currentFrame);
                bool blend = frameMetadata.BlendMethod == PngBlendMethod.Over;

                (bool difference, Rectangle bounds) =
                    AnimationUtilities.DeDuplicatePixels(
                        image.Configuration,
                        prev,
                        currentFrame,
                        nextFrame,
                        encodingFrame,
                        Color.Transparent,
                        blend);

                if (clearTransparency)
                {
                    ClearTransparentPixels(encodingFrame);
                }

                // Each frame control sequence number must be incremented by the number of frame data chunks that follow.
                frameControl = this.WriteFrameControlChunk(stream, frameMetadata, bounds, (uint)i + increment);

                // Dispose of previous quantized frame and reassign.
                quantized?.Dispose();
                quantized = this.CreateQuantizedImageAndUpdateBitDepth(pngMetadata, encodingFrame, bounds, previousPalette);
                increment += this.WriteDataChunks(frameControl, encodingFrame.PixelBuffer.GetRegion(bounds), quantized, stream, true);

                previousFrame = currentFrame;
                previousDisposal = frameMetadata.DisposalMethod;
            }
        }
        else
        {
            FrameControl frameControl = new((uint)this.width, (uint)this.height);
            this.WriteDataChunks(frameControl, currentFrame.PixelBuffer.GetRegion(), quantized, stream, false);
        }

        this.WriteEndChunk(stream);

        stream.Flush();

        // Dispose of allocations from final frame.
        clonedFrame?.Dispose();
        quantized?.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.previousScanline?.Dispose();
        this.currentScanline?.Dispose();
    }

    private static PngMetadata GetPngMetadata<TPixel>(Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (image.Metadata.TryGetPngMetadata(out PngMetadata? png))
        {
            return (PngMetadata)png.DeepClone();
        }

        if (image.Metadata.TryGetGifMetadata(out GifMetadata? gif))
        {
            AnimatedImageMetadata ani = gif.ToAnimatedImageMetadata();
            return PngMetadata.FromAnimatedMetadata(ani);
        }

        if (image.Metadata.TryGetWebpMetadata(out WebpMetadata? webp))
        {
            AnimatedImageMetadata ani = webp.ToAnimatedImageMetadata();
            return PngMetadata.FromAnimatedMetadata(ani);
        }

        // Return explicit new instance so we do not mutate the original metadata.
        return new();
    }

    private static PngFrameMetadata GetPngFrameMetadata<TPixel>(ImageFrame<TPixel> frame)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (frame.Metadata.TryGetPngMetadata(out PngFrameMetadata? png))
        {
            return (PngFrameMetadata)png.DeepClone();
        }

        if (frame.Metadata.TryGetGifMetadata(out GifFrameMetadata? gif))
        {
            AnimatedImageFrameMetadata ani = gif.ToAnimatedImageFrameMetadata();
            return PngFrameMetadata.FromAnimatedMetadata(ani);
        }

        if (frame.Metadata.TryGetWebpFrameMetadata(out WebpFrameMetadata? webp))
        {
            AnimatedImageFrameMetadata ani = webp.ToAnimatedImageFrameMetadata();
            return PngFrameMetadata.FromAnimatedMetadata(ani);
        }

        // Return explicit new instance so we do not mutate the original metadata.
        return new();
    }

    /// <summary>
    /// Convert transparent pixels, to transparent black pixels, which can yield to better compression in some cases.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="clone">The cloned image frame where the transparent pixels will be changed.</param>
    private static void ClearTransparentPixels<TPixel>(ImageFrame<TPixel> clone)
        where TPixel : unmanaged, IPixel<TPixel>
        => clone.ProcessPixelRows(accessor =>
        {
            // TODO: We should be able to speed this up with SIMD and masking.
            Rgba32 transparent = Color.Transparent.ToPixel<Rgba32>();
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<TPixel> span = accessor.GetRowSpan(y);
                for (int x = 0; x < accessor.Width; x++)
                {
                    ref TPixel pixel = ref span[x];
                    Rgba32 rgba = pixel.ToRgba32();
                    if (rgba.A is 0)
                    {
                        pixel = TPixel.FromRgba32(transparent);
                    }
                }
            }
        });

    /// <summary>
    /// Creates the quantized image and calculates and sets the bit depth.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="frame">The frame to quantize.</param>
    /// <param name="bounds">The area of interest within the frame.</param>
    /// <param name="previousPalette">Any previously derived palette.</param>
    /// <returns>The quantized image.</returns>
    private IndexedImageFrame<TPixel>? CreateQuantizedImageAndUpdateBitDepth<TPixel>(
        PngMetadata metadata,
        ImageFrame<TPixel> frame,
        Rectangle bounds,
        ReadOnlyMemory<TPixel>? previousPalette)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        IndexedImageFrame<TPixel>? quantized = this.CreateQuantizedFrame(this.encoder, this.colorType, this.bitDepth, metadata, frame, bounds, previousPalette);
        this.bitDepth = CalculateBitDepth(this.colorType, this.bitDepth, quantized);
        return quantized;
    }

    /// <summary>Collects a row of grayscale pixels.</summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="rowSpan">The image row span.</param>
    private void CollectGrayscaleBytes<TPixel>(ReadOnlySpan<TPixel> rowSpan)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Span<byte> rawScanlineSpan = this.currentScanline.GetSpan();

        if (this.colorType == PngColorType.Grayscale)
        {
            if (this.use16Bit)
            {
                // 16 bit grayscale
                using IMemoryOwner<L16> luminanceBuffer = this.memoryAllocator.Allocate<L16>(rowSpan.Length);
                Span<L16> luminanceSpan = luminanceBuffer.GetSpan();
                ref L16 luminanceRef = ref MemoryMarshal.GetReference(luminanceSpan);
                PixelOperations<TPixel>.Instance.ToL16(this.configuration, rowSpan, luminanceSpan);

                // Can't map directly to byte array as it's big-endian.
                for (int x = 0, o = 0; x < luminanceSpan.Length; x++, o += 2)
                {
                    L16 luminance = Unsafe.Add(ref luminanceRef, (uint)x);
                    BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o, 2), luminance.PackedValue);
                }
            }
            else if (this.bitDepth == 8)
            {
                // 8 bit grayscale
                PixelOperations<TPixel>.Instance.ToL8Bytes(
                    this.configuration,
                    rowSpan,
                    rawScanlineSpan,
                    rowSpan.Length);
            }
            else
            {
                // 1, 2, and 4 bit grayscale
                using IMemoryOwner<byte> temp = this.memoryAllocator.Allocate<byte>(rowSpan.Length, AllocationOptions.Clean);
                int scaleFactor = 255 / (ColorNumerics.GetColorCountForBitDepth(this.bitDepth) - 1);
                Span<byte> tempSpan = temp.GetSpan();

                // We need to first create an array of luminance bytes then scale them down to the correct bit depth.
                PixelOperations<TPixel>.Instance.ToL8Bytes(
                    this.configuration,
                    rowSpan,
                    tempSpan,
                    rowSpan.Length);
                PngEncoderHelpers.ScaleDownFrom8BitArray(tempSpan, rawScanlineSpan, this.bitDepth, scaleFactor);
            }
        }
        else if (this.use16Bit)
        {
            // 16 bit grayscale + alpha
            using IMemoryOwner<La32> laBuffer = this.memoryAllocator.Allocate<La32>(rowSpan.Length);
            Span<La32> laSpan = laBuffer.GetSpan();
            ref La32 laRef = ref MemoryMarshal.GetReference(laSpan);
            PixelOperations<TPixel>.Instance.ToLa32(this.configuration, rowSpan, laSpan);

            // Can't map directly to byte array as it's big endian.
            for (int x = 0, o = 0; x < laSpan.Length; x++, o += 4)
            {
                La32 la = Unsafe.Add(ref laRef, (uint)x);
                BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o, 2), la.L);
                BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 2, 2), la.A);
            }
        }
        else
        {
            // 8 bit grayscale + alpha
            PixelOperations<TPixel>.Instance.ToLa16Bytes(
                this.configuration,
                rowSpan,
                rawScanlineSpan,
                rowSpan.Length);
        }
    }

    /// <summary>
    /// Collects a row of true color pixel data.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="rowSpan">The row span.</param>
    private void CollectTPixelBytes<TPixel>(ReadOnlySpan<TPixel> rowSpan)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Span<byte> rawScanlineSpan = this.currentScanline.GetSpan();

        switch (this.bytesPerPixel)
        {
            case 4:

                // 8 bit Rgba
                PixelOperations<TPixel>.Instance.ToRgba32Bytes(
                    this.configuration,
                    rowSpan,
                    rawScanlineSpan,
                    rowSpan.Length);
                break;

            case 3:

                // 8 bit Rgb
                PixelOperations<TPixel>.Instance.ToRgb24Bytes(
                    this.configuration,
                    rowSpan,
                    rawScanlineSpan,
                    rowSpan.Length);
                break;

            case 8:

                // 16 bit Rgba
                using (IMemoryOwner<Rgba64> rgbaBuffer = this.memoryAllocator.Allocate<Rgba64>(rowSpan.Length))
                {
                    Span<Rgba64> rgbaSpan = rgbaBuffer.GetSpan();
                    ref Rgba64 rgbaRef = ref MemoryMarshal.GetReference(rgbaSpan);
                    PixelOperations<TPixel>.Instance.ToRgba64(this.configuration, rowSpan, rgbaSpan);

                    // Can't map directly to byte array as it's big endian.
                    for (int x = 0, o = 0; x < rowSpan.Length; x++, o += 8)
                    {
                        Rgba64 rgba = Unsafe.Add(ref rgbaRef, (uint)x);
                        BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o, 2), rgba.R);
                        BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 2, 2), rgba.G);
                        BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 4, 2), rgba.B);
                        BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 6, 2), rgba.A);
                    }
                }

                break;

            default:

                // 16 bit Rgb
                using (IMemoryOwner<Rgb48> rgbBuffer = this.memoryAllocator.Allocate<Rgb48>(rowSpan.Length))
                {
                    Span<Rgb48> rgbSpan = rgbBuffer.GetSpan();
                    ref Rgb48 rgbRef = ref MemoryMarshal.GetReference(rgbSpan);
                    PixelOperations<TPixel>.Instance.ToRgb48(this.configuration, rowSpan, rgbSpan);

                    // Can't map directly to byte array as it's big endian.
                    for (int x = 0, o = 0; x < rowSpan.Length; x++, o += 6)
                    {
                        Rgb48 rgb = Unsafe.Add(ref rgbRef, (uint)x);
                        BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o, 2), rgb.R);
                        BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 2, 2), rgb.G);
                        BinaryPrimitives.WriteUInt16BigEndian(rawScanlineSpan.Slice(o + 4, 2), rgb.B);
                    }
                }

                break;
        }
    }

    /// <summary>
    /// Encodes the pixel data line by line.
    /// Each scanline is encoded in the most optimal manner to improve compression.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="rowSpan">The row span.</param>
    /// <param name="quantized">The quantized pixels. Can be null.</param>
    /// <param name="row">The row.</param>
    private void CollectPixelBytes<TPixel>(ReadOnlySpan<TPixel> rowSpan, IndexedImageFrame<TPixel>? quantized, int row)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        switch (this.colorType)
        {
            case PngColorType.Palette:
                if (this.bitDepth < 8)
                {
                    PngEncoderHelpers.ScaleDownFrom8BitArray(quantized!.DangerousGetRowSpan(row), this.currentScanline.GetSpan(), this.bitDepth);
                }
                else
                {
                    quantized?.DangerousGetRowSpan(row).CopyTo(this.currentScanline.GetSpan());
                }

                break;
            case PngColorType.Grayscale:
            case PngColorType.GrayscaleWithAlpha:
                this.CollectGrayscaleBytes(rowSpan);
                break;
            default:
                this.CollectTPixelBytes(rowSpan);
                break;
        }
    }

    /// <summary>
    /// Apply the line filter for the raw scanline to enable better compression.
    /// </summary>
    /// <param name="filter">The filtered buffer.</param>
    /// <param name="attempt">Used for attempting optimized filtering.</param>
    private void FilterPixelBytes(ref Span<byte> filter, ref Span<byte> attempt)
    {
        switch (this.filterMethod)
        {
            case PngFilterMethod.None:
                NoneFilter.Encode(this.currentScanline.GetSpan(), filter);
                break;
            case PngFilterMethod.Sub:
                SubFilter.Encode(this.currentScanline.GetSpan(), filter, this.bytesPerPixel, out int _);
                break;

            case PngFilterMethod.Up:
                UpFilter.Encode(this.currentScanline.GetSpan(), this.previousScanline.GetSpan(), filter, out int _);
                break;

            case PngFilterMethod.Average:
                AverageFilter.Encode(this.currentScanline.GetSpan(), this.previousScanline.GetSpan(), filter, (uint)this.bytesPerPixel, out int _);
                break;

            case PngFilterMethod.Paeth:
                PaethFilter.Encode(this.currentScanline.GetSpan(), this.previousScanline.GetSpan(), filter, this.bytesPerPixel, out int _);
                break;
            default:
                this.ApplyOptimalFilteredScanline(ref filter, ref attempt);
                break;
        }
    }

    /// <summary>
    /// Collects the pixel data line by line for compressing.
    /// Each scanline is filtered in the most optimal manner to improve compression.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="rowSpan">The row span.</param>
    /// <param name="filter">The filtered buffer.</param>
    /// <param name="attempt">Used for attempting optimized filtering.</param>
    /// <param name="quantized">The quantized pixels. Can be <see langword="null"/>.</param>
    /// <param name="row">The row number.</param>
    private void CollectAndFilterPixelRow<TPixel>(
        ReadOnlySpan<TPixel> rowSpan,
        ref Span<byte> filter,
        ref Span<byte> attempt,
        IndexedImageFrame<TPixel>? quantized,
        int row)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        this.CollectPixelBytes(rowSpan, quantized, row);
        this.FilterPixelBytes(ref filter, ref attempt);
    }

    /// <summary>
    /// Encodes the indexed pixel data (with palette) for Adam7 interlaced mode.
    /// </summary>
    /// <param name="row">The row span.</param>
    /// <param name="filter">The filtered buffer.</param>
    /// <param name="attempt">Used for attempting optimized filtering.</param>
    private void EncodeAdam7IndexedPixelRow(
        ReadOnlySpan<byte> row,
        ref Span<byte> filter,
        ref Span<byte> attempt)
    {
        // CollectPixelBytes
        if (this.bitDepth < 8)
        {
            PngEncoderHelpers.ScaleDownFrom8BitArray(row, this.currentScanline.GetSpan(), this.bitDepth);
        }
        else
        {
            row.CopyTo(this.currentScanline.GetSpan());
        }

        this.FilterPixelBytes(ref filter, ref attempt);
    }

    /// <summary>
    /// Applies all PNG filters to the given scanline and returns the filtered scanline that is deemed
    /// to be most compressible, using lowest total variation as proxy for compressibility.
    /// </summary>
    /// <param name="filter">The filtered buffer.</param>
    /// <param name="attempt">Used for attempting optimized filtering.</param>
    private void ApplyOptimalFilteredScanline(ref Span<byte> filter, ref Span<byte> attempt)
    {
        // Palette images don't compress well with adaptive filtering.
        // Nor do images comprising a single row.
        if (this.colorType == PngColorType.Palette || this.height == 1 || this.bitDepth < 8)
        {
            NoneFilter.Encode(this.currentScanline.GetSpan(), filter);
            return;
        }

        Span<byte> current = this.currentScanline.GetSpan();
        Span<byte> previous = this.previousScanline.GetSpan();

        int min = int.MaxValue;
        SubFilter.Encode(current, attempt, this.bytesPerPixel, out int sum);
        if (sum < min)
        {
            min = sum;
            RuntimeUtility.Swap(ref filter, ref attempt);
        }

        UpFilter.Encode(current, previous, attempt, out sum);
        if (sum < min)
        {
            min = sum;
            RuntimeUtility.Swap(ref filter, ref attempt);
        }

        AverageFilter.Encode(current, previous, attempt, (uint)this.bytesPerPixel, out sum);
        if (sum < min)
        {
            min = sum;
            RuntimeUtility.Swap(ref filter, ref attempt);
        }

        PaethFilter.Encode(current, previous, attempt, this.bytesPerPixel, out sum);
        if (sum < min)
        {
            RuntimeUtility.Swap(ref filter, ref attempt);
        }
    }

    /// <summary>
    /// Writes the header chunk to the stream.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    private void WriteHeaderChunk(Stream stream)
    {
        PngHeader header = new(
            width: this.width,
            height: this.height,
            bitDepth: this.bitDepth,
            colorType: this.colorType,
            compressionMethod: 0, // None
            filterMethod: 0,
            interlaceMethod: this.interlaceMode);

        header.WriteTo(this.chunkDataBuffer.Span);

        this.WriteChunk(stream, PngChunkType.Header, this.chunkDataBuffer.Span, 0, PngHeader.Size);
    }

    /// <summary>
    /// Writes the animation control chunk to the stream.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="framesCount">The number of frames.</param>
    /// <param name="playsCount">The number of times to loop this APNG.</param>
    private void WriteAnimationControlChunk(Stream stream, uint framesCount, uint playsCount)
    {
        AnimationControl acTL = new(framesCount, playsCount);

        acTL.WriteTo(this.chunkDataBuffer.Span);

        this.WriteChunk(stream, PngChunkType.AnimationControl, this.chunkDataBuffer.Span, 0, AnimationControl.Size);
    }

    /// <summary>
    /// Writes the palette chunk to the stream.
    /// Should be written before the first IDAT chunk.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="quantized">The quantized frame.</param>
    private void WritePaletteChunk<TPixel>(Stream stream, IndexedImageFrame<TPixel>? quantized)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (quantized is null)
        {
            return;
        }

        // Grab the palette and write it to the stream.
        ReadOnlySpan<TPixel> palette = quantized.Palette.Span;
        int paletteLength = palette.Length;
        int colorTableLength = paletteLength * Unsafe.SizeOf<Rgb24>();
        bool hasAlpha = false;

        using IMemoryOwner<byte> colorTable = this.memoryAllocator.Allocate<byte>(colorTableLength);
        using IMemoryOwner<byte> alphaTable = this.memoryAllocator.Allocate<byte>(paletteLength);

        ref Rgb24 colorTableRef = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, Rgb24>(colorTable.GetSpan()));
        ref byte alphaTableRef = ref MemoryMarshal.GetReference(alphaTable.GetSpan());

        // Bulk convert our palette to RGBA to allow assignment to tables.
        using IMemoryOwner<Rgba32> rgbaOwner = quantized.Configuration.MemoryAllocator.Allocate<Rgba32>(paletteLength);
        Span<Rgba32> rgbaPaletteSpan = rgbaOwner.GetSpan();
        PixelOperations<TPixel>.Instance.ToRgba32(quantized.Configuration, quantized.Palette.Span, rgbaPaletteSpan);
        ref Rgba32 rgbaPaletteRef = ref MemoryMarshal.GetReference(rgbaPaletteSpan);

        // Loop, assign, and extract alpha values from the palette.
        for (int i = 0; i < paletteLength; i++)
        {
            Rgba32 rgba = Unsafe.Add(ref rgbaPaletteRef, (uint)i);
            byte alpha = rgba.A;

            Unsafe.Add(ref colorTableRef, (uint)i) = rgba.Rgb;
            if (alpha > this.encoder.Threshold)
            {
                alpha = byte.MaxValue;
            }

            hasAlpha = hasAlpha || alpha < byte.MaxValue;
            Unsafe.Add(ref alphaTableRef, (uint)i) = alpha;
        }

        this.WriteChunk(stream, PngChunkType.Palette, colorTable.GetSpan(), 0, colorTableLength);

        // Write the transparency data
        if (hasAlpha)
        {
            this.WriteChunk(stream, PngChunkType.Transparency, alphaTable.GetSpan(), 0, paletteLength);
        }
    }

    /// <summary>
    /// Writes the physical dimension information to the stream.
    /// Should be written before IDAT chunk.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="meta">The image metadata.</param>
    private void WritePhysicalChunk(Stream stream, ImageMetadata meta)
    {
        if (this.chunkFilter.HasFlag(PngChunkFilter.ExcludePhysicalChunk))
        {
            return;
        }

        PngPhysical.FromMetadata(meta).WriteTo(this.chunkDataBuffer.Span);

        this.WriteChunk(stream, PngChunkType.Physical, this.chunkDataBuffer.Span, 0, PngPhysical.Size);
    }

    /// <summary>
    /// Writes the eXIf chunk to the stream, if any EXIF Profile values are present in the metadata.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="meta">The image metadata.</param>
    private void WriteExifChunk(Stream stream, ImageMetadata meta)
    {
        if ((this.chunkFilter & PngChunkFilter.ExcludeExifChunk) == PngChunkFilter.ExcludeExifChunk)
        {
            return;
        }

        if (meta.ExifProfile is null || meta.ExifProfile.Values.Count == 0)
        {
            return;
        }

        meta.SyncProfiles();
        this.WriteChunk(stream, PngChunkType.Exif, meta.ExifProfile.ToByteArray());
    }

    /// <summary>
    /// Writes an iTXT chunk, containing the XMP metadata to the stream, if such profile is present in the metadata.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="meta">The image metadata.</param>
    private void WriteXmpChunk(Stream stream, ImageMetadata meta)
    {
        const int iTxtHeaderSize = 5;
        if ((this.chunkFilter & PngChunkFilter.ExcludeTextChunks) == PngChunkFilter.ExcludeTextChunks)
        {
            return;
        }

        if (meta.XmpProfile is null)
        {
            return;
        }

        byte[]? xmpData = meta.XmpProfile.Data;

        if (xmpData?.Length is 0 or null)
        {
            return;
        }

        int payloadLength = xmpData.Length + PngConstants.XmpKeyword.Length + iTxtHeaderSize;

        using IMemoryOwner<byte> owner = this.memoryAllocator.Allocate<byte>(payloadLength);
        Span<byte> payload = owner.GetSpan();
        PngConstants.XmpKeyword.CopyTo(payload);
        int bytesWritten = PngConstants.XmpKeyword.Length;

        // Write the iTxt header (all zeros in this case).
        Span<byte> iTxtHeader = payload[bytesWritten..];
        iTxtHeader[4] = 0;
        iTxtHeader[3] = 0;
        iTxtHeader[2] = 0;
        iTxtHeader[1] = 0;
        iTxtHeader[0] = 0;
        bytesWritten += 5;

        // And the XMP data itself.
        xmpData.CopyTo(payload[bytesWritten..]);
        this.WriteChunk(stream, PngChunkType.InternationalText, payload);
    }

    /// <summary>
    /// Writes the CICP profile chunk
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="metaData">The image meta data.</param>
    private void WriteCicpChunk(Stream stream, ImageMetadata metaData)
    {
        if (metaData.CicpProfile is null)
        {
            return;
        }

        // by spec, the matrix coefficients must be set to Identity
        if (metaData.CicpProfile.MatrixCoefficients != Metadata.Profiles.Cicp.CicpMatrixCoefficients.Identity)
        {
            throw new NotSupportedException("CICP matrix coefficients other than Identity are not supported in PNG");
        }

        Span<byte> outputBytes = this.chunkDataBuffer.Span[..4];
        outputBytes[0] = (byte)metaData.CicpProfile.ColorPrimaries;
        outputBytes[1] = (byte)metaData.CicpProfile.TransferCharacteristics;
        outputBytes[2] = (byte)metaData.CicpProfile.MatrixCoefficients;
        outputBytes[3] = (byte)(metaData.CicpProfile.FullRange ? 1 : 0);
        this.WriteChunk(stream, PngChunkType.Cicp, outputBytes);
    }

    /// <summary>
    /// Writes the color profile chunk.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="metaData">The image meta data.</param>
    private void WriteColorProfileChunk(Stream stream, ImageMetadata metaData)
    {
        if (metaData.IccProfile is null)
        {
            return;
        }

        byte[] iccProfileBytes = metaData.IccProfile.ToByteArray();

        byte[] compressedData = this.GetZlibCompressedBytes(iccProfileBytes);
        int payloadLength = ColorProfileName.Length + compressedData.Length + 2;

        using IMemoryOwner<byte> owner = this.memoryAllocator.Allocate<byte>(payloadLength);
        Span<byte> outputBytes = owner.GetSpan();
        PngConstants.Encoding.GetBytes(ColorProfileName).CopyTo(outputBytes);
        int bytesWritten = ColorProfileName.Length;
        outputBytes[bytesWritten++] = 0; // Null separator.
        outputBytes[bytesWritten++] = 0; // Compression.
        compressedData.CopyTo(outputBytes[bytesWritten..]);
        this.WriteChunk(stream, PngChunkType.EmbeddedColorProfile, outputBytes);
    }

    /// <summary>
    /// Writes a text chunk to the stream. Can be either a tTXt, iTXt or zTXt chunk,
    /// depending whether the text contains any latin characters or should be compressed.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="meta">The image metadata.</param>
    private void WriteTextChunks(Stream stream, PngMetadata meta)
    {
        if ((this.chunkFilter & PngChunkFilter.ExcludeTextChunks) == PngChunkFilter.ExcludeTextChunks)
        {
            return;
        }

        const int maxLatinCode = 255;
        foreach (PngTextData textData in meta.TextData)
        {
            bool hasUnicodeCharacters = textData.Value.Any(c => c > maxLatinCode);

            if (hasUnicodeCharacters || !string.IsNullOrWhiteSpace(textData.LanguageTag) || !string.IsNullOrWhiteSpace(textData.TranslatedKeyword))
            {
                // Write iTXt chunk.
                byte[] keywordBytes = PngConstants.Encoding.GetBytes(textData.Keyword);
                byte[] textBytes = textData.Value.Length > this.encoder.TextCompressionThreshold
                    ? this.GetZlibCompressedBytes(PngConstants.TranslatedEncoding.GetBytes(textData.Value))
                    : PngConstants.TranslatedEncoding.GetBytes(textData.Value);

                byte[] translatedKeyword = PngConstants.TranslatedEncoding.GetBytes(textData.TranslatedKeyword);
                byte[] languageTag = PngConstants.LanguageEncoding.GetBytes(textData.LanguageTag);

                int payloadLength = keywordBytes.Length + textBytes.Length + translatedKeyword.Length + languageTag.Length + 5;

                using IMemoryOwner<byte> owner = this.memoryAllocator.Allocate<byte>(payloadLength);
                Span<byte> outputBytes = owner.GetSpan();
                keywordBytes.CopyTo(outputBytes);
                int bytesWritten = keywordBytes.Length;
                outputBytes[bytesWritten++] = 0;
                if (textData.Value.Length > this.encoder.TextCompressionThreshold)
                {
                    // Indicate that the text is compressed.
                    outputBytes[bytesWritten++] = 1;
                }
                else
                {
                    outputBytes[bytesWritten++] = 0;
                }

                outputBytes[bytesWritten++] = 0;
                languageTag.CopyTo(outputBytes[bytesWritten..]);
                bytesWritten += languageTag.Length;
                outputBytes[bytesWritten++] = 0;
                translatedKeyword.CopyTo(outputBytes[bytesWritten..]);
                bytesWritten += translatedKeyword.Length;
                outputBytes[bytesWritten++] = 0;
                textBytes.CopyTo(outputBytes[bytesWritten..]);
                this.WriteChunk(stream, PngChunkType.InternationalText, outputBytes);
            }
            else if (textData.Value.Length > this.encoder.TextCompressionThreshold)
            {
                // Write zTXt chunk.
                byte[] compressedData = this.GetZlibCompressedBytes(PngConstants.Encoding.GetBytes(textData.Value));
                int payloadLength = textData.Keyword.Length + compressedData.Length + 2;

                using IMemoryOwner<byte> owner = this.memoryAllocator.Allocate<byte>(payloadLength);
                Span<byte> outputBytes = owner.GetSpan();
                PngConstants.Encoding.GetBytes(textData.Keyword).CopyTo(outputBytes);
                int bytesWritten = textData.Keyword.Length;
                outputBytes[bytesWritten++] = 0; // Null separator.
                outputBytes[bytesWritten++] = 0; // Compression.
                compressedData.CopyTo(outputBytes[bytesWritten..]);
                this.WriteChunk(stream, PngChunkType.CompressedText, outputBytes);
            }
            else
            {
                // Write tEXt chunk.
                int payloadLength = textData.Keyword.Length + textData.Value.Length + 1;

                using IMemoryOwner<byte> owner = this.memoryAllocator.Allocate<byte>(payloadLength);
                Span<byte> outputBytes = owner.GetSpan();
                PngConstants.Encoding.GetBytes(textData.Keyword).CopyTo(outputBytes);
                int bytesWritten = textData.Keyword.Length;
                outputBytes[bytesWritten++] = 0;
                PngConstants.Encoding.GetBytes(textData.Value).CopyTo(outputBytes[bytesWritten..]);
                this.WriteChunk(stream, PngChunkType.Text, outputBytes);
            }
        }
    }

    /// <summary>
    /// Compresses a given text using Zlib compression.
    /// </summary>
    /// <param name="dataBytes">The bytes to compress.</param>
    /// <returns>The compressed byte array.</returns>
    private byte[] GetZlibCompressedBytes(byte[] dataBytes)
    {
        using MemoryStream memoryStream = new();
        using (ZlibDeflateStream deflateStream = new(this.memoryAllocator, memoryStream, this.encoder.CompressionLevel))
        {
            deflateStream.Write(dataBytes);
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Writes the gamma information to the stream.
    /// Should be written before PLTE and IDAT chunk.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    private void WriteGammaChunk(Stream stream)
    {
        if ((this.chunkFilter & PngChunkFilter.ExcludeGammaChunk) == PngChunkFilter.ExcludeGammaChunk)
        {
            return;
        }

        if (this.gamma > 0)
        {
            // 4-byte unsigned integer of gamma * 100,000.
            uint gammaValue = (uint)(this.gamma * 100_000F);

            BinaryPrimitives.WriteUInt32BigEndian(this.chunkDataBuffer.Span[..4], gammaValue);

            this.WriteChunk(stream, PngChunkType.Gamma, this.chunkDataBuffer.Span, 0, 4);
        }
    }

    /// <summary>
    /// Writes the transparency chunk to the stream.
    /// Should be written after PLTE and before IDAT.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="pngMetadata">The image metadata.</param>
    private void WriteTransparencyChunk(Stream stream, PngMetadata pngMetadata)
    {
        if (pngMetadata.TransparentColor is null)
        {
            return;
        }

        Span<byte> alpha = this.chunkDataBuffer.Span;
        if (pngMetadata.ColorType == PngColorType.Rgb)
        {
            if (this.use16Bit)
            {
                Rgb48 rgb = pngMetadata.TransparentColor.Value.ToPixel<Rgb48>();
                BinaryPrimitives.WriteUInt16LittleEndian(alpha, rgb.R);
                BinaryPrimitives.WriteUInt16LittleEndian(alpha.Slice(2, 2), rgb.G);
                BinaryPrimitives.WriteUInt16LittleEndian(alpha.Slice(4, 2), rgb.B);

                this.WriteChunk(stream, PngChunkType.Transparency, this.chunkDataBuffer.Span, 0, 6);
            }
            else
            {
                alpha.Clear();
                Rgb24 rgb = pngMetadata.TransparentColor.Value.ToPixel<Rgb24>();
                alpha[1] = rgb.R;
                alpha[3] = rgb.G;
                alpha[5] = rgb.B;
                this.WriteChunk(stream, PngChunkType.Transparency, this.chunkDataBuffer.Span, 0, 6);
            }
        }
        else if (pngMetadata.ColorType == PngColorType.Grayscale)
        {
            if (this.use16Bit)
            {
                L16 l16 = pngMetadata.TransparentColor.Value.ToPixel<L16>();
                BinaryPrimitives.WriteUInt16LittleEndian(alpha, l16.PackedValue);
                this.WriteChunk(stream, PngChunkType.Transparency, this.chunkDataBuffer.Span, 0, 2);
            }
            else
            {
                L8 l8 = pngMetadata.TransparentColor.Value.ToPixel<L8>();
                alpha.Clear();
                alpha[1] = l8.PackedValue;
                this.WriteChunk(stream, PngChunkType.Transparency, this.chunkDataBuffer.Span, 0, 2);
            }
        }
    }

    /// <summary>
    /// Writes the animation control chunk to the stream.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="frameMetadata">The frame metadata.</param>
    /// <param name="bounds">The frame area of interest.</param>
    /// <param name="sequenceNumber">The frame sequence number.</param>
    private FrameControl WriteFrameControlChunk(Stream stream, PngFrameMetadata frameMetadata, Rectangle bounds, uint sequenceNumber)
    {
        FrameControl fcTL = new(
            sequenceNumber: sequenceNumber,
            width: (uint)bounds.Width,
            height: (uint)bounds.Height,
            xOffset: (uint)bounds.Left,
            yOffset: (uint)bounds.Top,
            delayNumerator: (ushort)frameMetadata.FrameDelay.Numerator,
            delayDenominator: (ushort)frameMetadata.FrameDelay.Denominator,
            disposeOperation: frameMetadata.DisposalMethod,
            blendOperation: frameMetadata.BlendMethod);

        fcTL.WriteTo(this.chunkDataBuffer.Span);

        this.WriteChunk(stream, PngChunkType.FrameControl, this.chunkDataBuffer.Span, 0, FrameControl.Size);

        return fcTL;
    }

    /// <summary>
    /// Writes the pixel information to the stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frameControl">The frame control</param>
    /// <param name="frame">The image frame.</param>
    /// <param name="quantized">The quantized pixel data. Can be null.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="isFrame">Is writing fdAT or IDAT.</param>
    private uint WriteDataChunks<TPixel>(FrameControl frameControl, Buffer2DRegion<TPixel> frame, IndexedImageFrame<TPixel>? quantized, Stream stream, bool isFrame)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        byte[] buffer;
        int bufferLength;

        using (MemoryStream memoryStream = new())
        {
            using (ZlibDeflateStream deflateStream = new(this.memoryAllocator, memoryStream, this.encoder.CompressionLevel))
            {
                if (this.interlaceMode is PngInterlaceMode.Adam7)
                {
                    if (quantized is not null)
                    {
                        this.EncodeAdam7IndexedPixels(quantized, deflateStream);
                    }
                    else
                    {
                        this.EncodeAdam7Pixels(frame, deflateStream);
                    }
                }
                else
                {
                    this.EncodePixels(frame, quantized, deflateStream);
                }
            }

            buffer = memoryStream.ToArray();
            bufferLength = buffer.Length;
        }

        // Store the chunks in repeated 64k blocks.
        // This reduces the memory load for decoding the image for many decoders.
        int maxBlockSize = MaxBlockSize;
        if (isFrame)
        {
            maxBlockSize -= 4;
        }

        int numChunks = bufferLength / maxBlockSize;

        if (bufferLength % maxBlockSize != 0)
        {
            numChunks++;
        }

        for (int i = 0; i < numChunks; i++)
        {
            int length = bufferLength - (i * maxBlockSize);

            if (length > maxBlockSize)
            {
                length = maxBlockSize;
            }

            if (isFrame)
            {
                // We increment the sequence number for each frame chunk.
                // '1' is added to the sequence number to account for the preceding frame control chunk.
                uint sequenceNumber = (uint)(frameControl.SequenceNumber + 1 + i);
                this.WriteFrameDataChunk(stream, sequenceNumber, buffer, i * maxBlockSize, length);
            }
            else
            {
                this.WriteChunk(stream, PngChunkType.Data, buffer, i * maxBlockSize, length);
            }
        }

        return (uint)numChunks;
    }

    /// <summary>
    /// Allocates the buffers for each scanline.
    /// </summary>
    /// <param name="bytesPerScanline">The bytes per scanline.</param>
    private void AllocateScanlineBuffers(int bytesPerScanline)
    {
        // Clean up from any potential previous runs.
        this.previousScanline?.Dispose();
        this.currentScanline?.Dispose();
        this.previousScanline = this.memoryAllocator.Allocate<byte>(bytesPerScanline, AllocationOptions.Clean);
        this.currentScanline = this.memoryAllocator.Allocate<byte>(bytesPerScanline, AllocationOptions.Clean);
    }

    /// <summary>
    /// Encodes the pixels.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="pixels">The image frame pixel buffer.</param>
    /// <param name="quantized">The quantized pixels.</param>
    /// <param name="deflateStream">The deflate stream.</param>
    private void EncodePixels<TPixel>(Buffer2DRegion<TPixel> pixels, IndexedImageFrame<TPixel>? quantized, ZlibDeflateStream deflateStream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int bytesPerScanline = this.CalculateScanlineLength(pixels.Width);
        int filterLength = bytesPerScanline + 1;
        this.AllocateScanlineBuffers(bytesPerScanline);

        using IMemoryOwner<byte> filterBuffer = this.memoryAllocator.Allocate<byte>(filterLength, AllocationOptions.Clean);
        using IMemoryOwner<byte> attemptBuffer = this.memoryAllocator.Allocate<byte>(filterLength, AllocationOptions.Clean);

        Span<byte> filter = filterBuffer.GetSpan();
        Span<byte> attempt = attemptBuffer.GetSpan();
        for (int y = 0; y < pixels.Height; y++)
        {
            this.CollectAndFilterPixelRow(pixels.DangerousGetRowSpan(y), ref filter, ref attempt, quantized, y);
            deflateStream.Write(filter);
            this.SwapScanlineBuffers();
        }
    }

    /// <summary>
    /// Interlaced encoding the pixels.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="pixels">The image frame pixel buffer.</param>
    /// <param name="deflateStream">The deflate stream.</param>
    private void EncodeAdam7Pixels<TPixel>(Buffer2DRegion<TPixel> pixels, ZlibDeflateStream deflateStream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        for (int pass = 0; pass < 7; pass++)
        {
            int startRow = Adam7.FirstRow[pass];
            int startCol = Adam7.FirstColumn[pass];
            int blockWidth = Adam7.ComputeBlockWidth(pixels.Width, pass);

            int bytesPerScanline = this.bytesPerPixel <= 1
                ? ((blockWidth * this.bitDepth) + 7) / 8
                : blockWidth * this.bytesPerPixel;

            int filterLength = bytesPerScanline + 1;
            this.AllocateScanlineBuffers(bytesPerScanline);

            using IMemoryOwner<TPixel> blockBuffer = this.memoryAllocator.Allocate<TPixel>(blockWidth);
            using IMemoryOwner<byte> filterBuffer = this.memoryAllocator.Allocate<byte>(filterLength, AllocationOptions.Clean);
            using IMemoryOwner<byte> attemptBuffer = this.memoryAllocator.Allocate<byte>(filterLength, AllocationOptions.Clean);

            Span<TPixel> block = blockBuffer.GetSpan();
            Span<byte> filter = filterBuffer.GetSpan();
            Span<byte> attempt = attemptBuffer.GetSpan();

            for (int row = startRow; row < pixels.Height; row += Adam7.RowIncrement[pass])
            {
                // Collect pixel data
                Span<TPixel> srcRow = pixels.DangerousGetRowSpan(row);
                for (int col = startCol, i = 0; col < pixels.Width; col += Adam7.ColumnIncrement[pass], i++)
                {
                    block[i] = srcRow[col];
                }

                // Encode data
                // Note: quantized parameter not used
                // Note: row parameter not used
                this.CollectAndFilterPixelRow<TPixel>(block, ref filter, ref attempt, null, -1);
                deflateStream.Write(filter);

                this.SwapScanlineBuffers();
            }
        }
    }

    /// <summary>
    /// Interlaced encoding the quantized (indexed, with palette) pixels.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="quantized">The quantized.</param>
    /// <param name="deflateStream">The deflate stream.</param>
    private void EncodeAdam7IndexedPixels<TPixel>(IndexedImageFrame<TPixel> quantized, ZlibDeflateStream deflateStream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        for (int pass = 0; pass < 7; pass++)
        {
            int startRow = Adam7.FirstRow[pass];
            int startCol = Adam7.FirstColumn[pass];
            int blockWidth = Adam7.ComputeBlockWidth(quantized.Width, pass);

            int bytesPerScanline = this.bytesPerPixel <= 1
                ? ((blockWidth * this.bitDepth) + 7) / 8
                : blockWidth * this.bytesPerPixel;

            int filterLength = bytesPerScanline + 1;

            this.AllocateScanlineBuffers(bytesPerScanline);

            using IMemoryOwner<byte> blockBuffer = this.memoryAllocator.Allocate<byte>(blockWidth);
            using IMemoryOwner<byte> filterBuffer = this.memoryAllocator.Allocate<byte>(filterLength, AllocationOptions.Clean);
            using IMemoryOwner<byte> attemptBuffer = this.memoryAllocator.Allocate<byte>(filterLength, AllocationOptions.Clean);

            Span<byte> block = blockBuffer.GetSpan();
            Span<byte> filter = filterBuffer.GetSpan();
            Span<byte> attempt = attemptBuffer.GetSpan();

            for (int row = startRow; row < quantized.Height; row += Adam7.RowIncrement[pass])
            {
                // Collect data
                ReadOnlySpan<byte> srcRow = quantized.DangerousGetRowSpan(row);
                for (int col = startCol, i = 0; col < quantized.Width; col += Adam7.ColumnIncrement[pass], i++)
                {
                    block[i] = srcRow[col];
                }

                // Encode data
                this.EncodeAdam7IndexedPixelRow(block, ref filter, ref attempt);
                deflateStream.Write(filter);

                this.SwapScanlineBuffers();
            }
        }
    }

    /// <summary>
    /// Writes the chunk end to the stream.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    private void WriteEndChunk(Stream stream) => this.WriteChunk(stream, PngChunkType.End, null);

    /// <summary>
    /// Writes a chunk to the stream.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="type">The type of chunk to write.</param>
    /// <param name="data">The <see cref="T:byte[]"/> containing data.</param>
    private void WriteChunk(Stream stream, PngChunkType type, Span<byte> data)
        => this.WriteChunk(stream, type, data, 0, data.Length);

    /// <summary>
    /// Writes a chunk of a specified length to the stream at the given offset.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="type">The type of chunk to write.</param>
    /// <param name="data">The <see cref="Span{Byte}"/> containing data.</param>
    /// <param name="offset">The position to offset the data at.</param>
    /// <param name="length">The of the data to write.</param>
    private void WriteChunk(Stream stream, PngChunkType type, Span<byte> data, int offset, int length)
    {
        Span<byte> buffer = stackalloc byte[8];

        BinaryPrimitives.WriteInt32BigEndian(buffer, length);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(4, 4), (uint)type);

        stream.Write(buffer);

        this.crc32.Reset();
        this.crc32.Append(buffer[4..]); // Write the type buffer

        if (data.Length > 0 && length > 0)
        {
            stream.Write(data, offset, length);

            this.crc32.Append(data.Slice(offset, length));
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer, this.crc32.GetCurrentHashAsUInt32());

        stream.Write(buffer, 0, 4); // write the crc
    }

    /// <summary>
    /// Writes a frame data chunk of a specified length to the stream at the given offset.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> to write to.</param>
    /// <param name="sequenceNumber">The frame sequence number.</param>
    /// <param name="data">The <see cref="Span{Byte}"/> containing data.</param>
    /// <param name="offset">The position to offset the data at.</param>
    /// <param name="length">The of the data to write.</param>
    private void WriteFrameDataChunk(Stream stream, uint sequenceNumber, Span<byte> data, int offset, int length)
    {
        Span<byte> buffer = stackalloc byte[12];

        BinaryPrimitives.WriteInt32BigEndian(buffer, length + 4);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(4, 4), (uint)PngChunkType.FrameData);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(8, 4), sequenceNumber);

        stream.Write(buffer);

        this.crc32.Reset();
        this.crc32.Append(buffer[4..]); // Write the type buffer

        if (data.Length > 0 && length > 0)
        {
            stream.Write(data, offset, length);

            this.crc32.Append(data.Slice(offset, length));
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer, this.crc32.GetCurrentHashAsUInt32());

        stream.Write(buffer, 0, 4); // write the crc
    }

    /// <summary>
    /// Calculates the scanline length.
    /// </summary>
    /// <param name="width">The width of the row.</param>
    /// <returns>
    /// The <see cref="int"/> representing the length.
    /// </returns>
    private int CalculateScanlineLength(int width)
    {
        int mod = this.bitDepth is 16 ? 16 : 8;
        int scanlineLength = width * this.bitDepth * this.bytesPerPixel;

        int amount = scanlineLength % mod;
        if (amount != 0)
        {
            scanlineLength += mod - amount;
        }

        return scanlineLength / mod;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SwapScanlineBuffers()
    {
        ref IMemoryOwner<byte> prev = ref this.previousScanline;
        ref IMemoryOwner<byte> current = ref this.currentScanline;
        RuntimeUtility.Swap(ref prev, ref current);
    }

    /// <summary>
    /// Adjusts the options based upon the given metadata.
    /// </summary>
    /// <typeparam name="TPixel">The type of pixel format.</typeparam>
    /// <param name="encoder">The encoder with options.</param>
    /// <param name="pngMetadata">The PNG metadata.</param>
    /// <param name="use16Bit">if set to <c>true</c> [use16 bit].</param>
    /// <param name="bytesPerPixel">The bytes per pixel.</param>
    private void SanitizeAndSetEncoderOptions<TPixel>(
        PngEncoder encoder,
        PngMetadata pngMetadata,
        out bool use16Bit,
        out int bytesPerPixel)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Always take the encoder options over the metadata values.
        this.gamma = encoder.Gamma ?? pngMetadata.Gamma;

        // Use options, then check metadata, if nothing set there then we suggest
        // a sensible default based upon the pixel format.
        PngColorType? colorType = encoder.ColorType ?? pngMetadata.ColorType;
        byte? bits = (byte?)(encoder.BitDepth ?? pngMetadata.BitDepth);

        if (colorType is null || bits is null)
        {
            PixelTypeInfo info = TPixel.GetPixelTypeInfo();
            PixelComponentInfo? componentInfo = info.ComponentInfo;

            colorType ??= SuggestColorType<TPixel>(in info);

            if (bits is null)
            {
                // TODO: Update once we stop abusing PixelTypeInfo in decoders.
                if (componentInfo.HasValue)
                {
                    PixelComponentInfo c = componentInfo.Value;
                    bits = (byte)SuggestBitDepth<TPixel>(in c);
                }
                else
                {
                    bits = (byte)PngBitDepth.Bit8;
                }
            }
        }

        // Ensure bit depth and color type are a supported combination.
        // Bit8 is the only bit depth supported by all color types.
        byte[] validBitDepths = PngConstants.ColorTypes[colorType.Value];
        if (Array.IndexOf(validBitDepths, bits) == -1)
        {
            bits = (byte)PngBitDepth.Bit8;
        }

        this.colorType = colorType.Value;
        this.bitDepth = bits.Value;

        if (!encoder.FilterMethod.HasValue)
        {
            // Specification recommends default filter method None for paletted images and Paeth for others.
            this.filterMethod = this.colorType is PngColorType.Palette ? PngFilterMethod.None : PngFilterMethod.Paeth;
        }

        use16Bit = bits == (byte)PngBitDepth.Bit16;
        bytesPerPixel = CalculateBytesPerPixel(this.colorType, use16Bit);

        this.interlaceMode = (encoder.InterlaceMethod ?? pngMetadata.InterlaceMethod)!.Value;
        this.chunkFilter = encoder.SkipMetadata ? PngChunkFilter.ExcludeAll : encoder.ChunkFilter ?? PngChunkFilter.None;
    }

    /// <summary>
    /// Creates the quantized frame.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="encoder">The png encoder.</param>
    /// <param name="colorType">The color type.</param>
    /// <param name="bitDepth">The bits per component.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="frame">The frame to quantize.</param>
    /// <param name="bounds">The frame area of interest.</param>
    /// <param name="previousPalette">Any previously derived palette.</param>
    private IndexedImageFrame<TPixel>? CreateQuantizedFrame<TPixel>(
        QuantizingImageEncoder encoder,
        PngColorType colorType,
        byte bitDepth,
        PngMetadata metadata,
        ImageFrame<TPixel> frame,
        Rectangle bounds,
        ReadOnlyMemory<TPixel>? previousPalette)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (colorType is not PngColorType.Palette)
        {
            return null;
        }

        if (previousPalette is not null)
        {
            // Use the previously derived palette created by quantizing the root frame to quantize the current frame.
            using PaletteQuantizer<TPixel> paletteQuantizer = new(
                this.configuration,
                this.quantizer!.Options,
                previousPalette.Value,
                this.derivedTransparencyIndex);
            paletteQuantizer.BuildPalette(encoder.PixelSamplingStrategy, frame);
            return paletteQuantizer.QuantizeFrame(frame, bounds);
        }

        // Use the metadata to determine what quantization depth to use if no quantizer has been set.
        if (this.quantizer is null)
        {
            if (metadata.ColorTable is not null)
            {
                // We can use the color data from the decoded metadata here.
                // We avoid dithering by default to preserve the original colors.
                ReadOnlySpan<Color> palette = metadata.ColorTable.Value.Span;

                // Certain operations perform alpha premultiplication, which can cause the color to change so we
                // must search for the transparency index in the palette.
                // Transparent pixels are much more likely to be found at the end of a palette.
                int index = -1;
                for (int i = palette.Length - 1; i >= 0; i--)
                {
                    Vector4 instance = palette[i].ToScaledVector4();
                    if (instance.W == 0f)
                    {
                        index = i;
                        break;
                    }
                }

                this.derivedTransparencyIndex = index;

                this.quantizer = new PaletteQuantizer(metadata.ColorTable.Value, new() { Dither = null }, this.derivedTransparencyIndex);
            }
            else
            {
                this.quantizer = new WuQuantizer(new QuantizerOptions { MaxColors = ColorNumerics.GetColorCountForBitDepth(bitDepth) });
            }
        }

        // Create quantized frame returning the palette and set the bit depth.
        using IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(frame.Configuration);

        frameQuantizer.BuildPalette(encoder.PixelSamplingStrategy, frame);
        return frameQuantizer.QuantizeFrame(frame, bounds);
    }

    /// <summary>
    /// Calculates the bit depth value.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="colorType">The color type.</param>
    /// <param name="bitDepth">The bits per component.</param>
    /// <param name="quantizedFrame">The quantized frame.</param>
    /// <exception cref="NotSupportedException">Bit depth is not supported or not valid.</exception>
    private static byte CalculateBitDepth<TPixel>(
        PngColorType colorType,
        byte bitDepth,
        IndexedImageFrame<TPixel>? quantizedFrame)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (colorType is PngColorType.Palette)
        {
            byte quantizedBits = (byte)Numerics.Clamp(ColorNumerics.GetBitsNeededForColorDepth(quantizedFrame!.Palette.Length), 1, 8);
            byte bits = Math.Max(bitDepth, quantizedBits);

            // Png only supports in four pixel depths: 1, 2, 4, and 8 bits when using the PLTE chunk
            // We check again for the bit depth as the bit depth of the color palette from a given quantizer might not
            // be within the acceptable range.
            bits = bits switch
            {
                3 => 4,
                >= 5 and <= 7 => 8,
                _ => bits
            };

            bitDepth = bits;
        }

        if (Array.IndexOf(PngConstants.ColorTypes[colorType], bitDepth) < 0)
        {
            throw new NotSupportedException("Bit depth is not supported or not valid.");
        }

        return bitDepth;
    }

    /// <summary>
    /// Calculates the correct number of bytes per pixel for the given color type.
    /// </summary>
    /// <param name="pngColorType">The color type.</param>
    /// <param name="use16Bit">Whether to use 16 bits per component.</param>
    /// <returns>Bytes per pixel.</returns>
    private static int CalculateBytesPerPixel(PngColorType? pngColorType, bool use16Bit)
        => pngColorType switch
        {
            PngColorType.Grayscale => use16Bit ? 2 : 1,
            PngColorType.GrayscaleWithAlpha => use16Bit ? 4 : 2,
            PngColorType.Palette => 1,
            PngColorType.Rgb => use16Bit ? 6 : 3,

            // PngColorType.RgbWithAlpha
            _ => use16Bit ? 8 : 4,
        };

    /// <summary>
    /// Returns a suggested <see cref="PngColorType"/> for the given <typeparamref name="TPixel"/>
    /// </summary>
    /// <param name="info">The pixel type info.</param>
    /// <typeparam name="TPixel">The type of pixel format.</typeparam>
    private static PngColorType SuggestColorType<TPixel>(in PixelTypeInfo info)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (info.AlphaRepresentation == PixelAlphaRepresentation.None)
        {
            return info.ColorType switch
            {
                PixelColorType.Grayscale => PngColorType.Grayscale,
                _ => PngColorType.Rgb,
            };
        }

        return info.ColorType switch
        {
            PixelColorType.Grayscale | PixelColorType.Alpha or PixelColorType.Alpha => PngColorType.GrayscaleWithAlpha,
            _ => PngColorType.RgbWithAlpha,
        };
    }

    /// <summary>
    /// Returns a suggested <see cref="PngBitDepth"/> for the given <typeparamref name="TPixel"/>
    /// </summary>
    /// <param name="info">The pixel type info.</param>
    /// <typeparam name="TPixel">The type of pixel format.</typeparam>
    private static PngBitDepth SuggestBitDepth<TPixel>(in PixelComponentInfo info)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int bits = info.GetMaximumComponentPrecision();
        if (bits > (int)PixelComponentBitDepth.Bit8)
        {
            return PngBitDepth.Bit16;
        }

        return PngBitDepth.Bit8;
    }

    private unsafe struct ScratchBuffer
    {
        private const int Size = 26;
        private fixed byte scratch[Size];

        public Span<byte> Span => MemoryMarshal.CreateSpan(ref this.scratch[0], Size);
    }
}
