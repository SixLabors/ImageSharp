// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Webp.Chunks;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Decoder for animated webp images.
/// </summary>
internal class WebpAnimationDecoder : IDisposable
{
    /// <summary>
    /// Used for allocating memory during the decoding operations.
    /// </summary>
    private readonly MemoryAllocator memoryAllocator;

    /// <summary>
    /// The global configuration.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The maximum number of frames to decode. Inclusive.
    /// </summary>
    private readonly uint maxFrames;

    /// <summary>
    /// Whether to skip metadata.
    /// </summary>
    private readonly bool skipMetadata;

    /// <summary>
    /// The area to restore.
    /// </summary>
    private Rectangle? restoreArea;

    /// <summary>
    /// The abstract metadata.
    /// </summary>
    private ImageMetadata? metadata;

    /// <summary>
    /// The gif specific metadata.
    /// </summary>
    private WebpMetadata? webpMetadata;

    /// <summary>
    /// The alpha data, if an ALPH chunk is present.
    /// </summary>
    private IMemoryOwner<byte>? alphaData;

    /// <summary>
    /// The flag to decide how to handle the background color in the Animation Chunk.
    /// </summary>
    private readonly BackgroundColorHandling backgroundColorHandling;

    /// <summary>
    /// The segment integrity mode. It controls incomplete ICC profiles and damaged or truncated frame image data.
    /// </summary>
    private readonly SegmentIntegrityHandling segmentIntegrityHandling;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebpAnimationDecoder"/> class.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="maxFrames">The maximum number of frames to decode. Inclusive.</param>
    /// <param name="skipMetadata">Whether to skip metadata.</param>
    /// <param name="backgroundColorHandling">The flag to decide how to handle the background color in the Animation Chunk.</param>
    /// <param name="segmentIntegrityHandling">The segment integrity mode of the decode.</param>
    public WebpAnimationDecoder(
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        uint maxFrames,
        bool skipMetadata,
        BackgroundColorHandling backgroundColorHandling,
        SegmentIntegrityHandling segmentIntegrityHandling)
    {
        this.memoryAllocator = memoryAllocator;
        this.configuration = configuration;
        this.maxFrames = maxFrames;
        this.skipMetadata = skipMetadata;
        this.backgroundColorHandling = backgroundColorHandling;
        this.segmentIntegrityHandling = segmentIntegrityHandling;
    }

    /// <summary>
    /// Reads the animated webp image information from the specified stream.
    /// </summary>
    /// <param name="stream">The stream, where the image should be decoded from. Cannot be null.</param>
    /// <param name="metadata">The image metadata populated before the animation chunk.</param>
    /// <param name="features">The webp features.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    public ImageInfo Identify(
        BufferedReadStream stream,
        ImageMetadata metadata,
        WebpFeatures features,
        uint width,
        uint height,
        long containerEnd)
    {
        List<ImageFrameMetadata> framesMetadata = [];

        // Keep ICCP and other chunks parsed before ANIM on the metadata returned by Identify.
        this.metadata = metadata;
        this.webpMetadata = this.metadata.GetWebpMetadata();
        this.webpMetadata.RepeatCount = features.AnimationLoopCount;

        this.webpMetadata.BackgroundColor = this.backgroundColorHandling == BackgroundColorHandling.Ignore
            ? Color.Transparent
            : features.AnimationBackgroundColor;

        Span<byte> buffer = stackalloc byte[4];
        uint frameCount = 0;
        while (frameCount < this.maxFrames && this.TryReadToNextFrame(stream, buffer, metadata, containerEnd))
        {
            ImageFrameMetadata frameMetadata = new();
            if (!TryReadFrameInfo(stream, frameMetadata, containerEnd, this.segmentIntegrityHandling, frameCount > 0))
            {
                // A later frame header is damaged, and the mode ignores the error. The position of the next chunk is not known, so Identify keeps the frames
                // before it, as Decode does.
                break;
            }

            framesMetadata.Add(frameMetadata);
            frameCount++;
        }

        return new ImageInfo(new Size((int)width, (int)height), this.metadata, framesMetadata);
    }

    /// <summary>
    /// Decodes the animated webp image from the specified stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The stream, where the image should be decoded from. Cannot be null.</param>
    /// <param name="metadata">The image metadata populated before the animation chunk.</param>
    /// <param name="features">The webp features.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    /// <param name="colorProfileConverter">The optional ICC converter applied to each decoded frame.</param>
    public Image<TPixel> Decode<TPixel>(
        BufferedReadStream stream,
        ImageMetadata metadata,
        WebpFeatures features,
        uint width,
        uint height,
        long containerEnd,
        ColorProfileConverter? colorProfileConverter)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Image<TPixel>? image = null;
        WebpFrameData? prevFrameData = null;

        // Frame creation and later optional chunks must share the pre-ANIM ICC metadata.
        this.metadata = metadata;
        this.webpMetadata = this.metadata.GetWebpMetadata();
        this.webpMetadata.RepeatCount = features.AnimationLoopCount;

        Color backgroundColor = this.backgroundColorHandling == BackgroundColorHandling.Ignore
            ? Color.Transparent
            : features.AnimationBackgroundColor;

        if (colorProfileConverter is not null && this.backgroundColorHandling != BackgroundColorHandling.Ignore)
        {
            backgroundColor = colorProfileConverter.Convert<TPixel>(backgroundColor);
        }

        this.webpMetadata.BackgroundColor = backgroundColor;
        TPixel backgroundPixel = backgroundColor.ToPixel<TPixel>();

        Span<byte> buffer = stackalloc byte[4];
        uint frameCount = 0;
        try
        {
            while (frameCount < this.maxFrames && this.TryReadToNextFrame(stream, buffer, metadata, containerEnd))
            {
                if (!this.TryReadFrame(stream, ref image, ref prevFrameData, width, height, backgroundPixel, colorProfileConverter, containerEnd))
                {
                    // A later frame header is damaged, and the mode ignores the error. The position of the next chunk is not known, so the decode keeps the
                    // frames before it and stops.
                    break;
                }

                frameCount++;
            }

            if (image is null)
            {
                WebpThrowHelper.ThrowInvalidImageContentException("Animated WebP contains no frames.");
            }

            return image;
        }
        catch
        {
            image?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Moves the stream to the size field of the next ANMF chunk. Decode and Identify use this one frame walker.
    /// </summary>
    /// <remarks>
    /// The walker reads the metadata chunks on the way and skips each other chunk, with its payload and padding.
    /// </remarks>
    /// <param name="stream">The stream positioned at the next chunk header.</param>
    /// <param name="buffer">The four-byte scratch buffer.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    /// <returns><see langword="true"/> if the stream is at the size field of an ANMF chunk. Otherwise <see langword="false"/>.</returns>
    private bool TryReadToNextFrame(BufferedReadStream stream, Span<byte> buffer, ImageMetadata metadata, long containerEnd)
    {
        SegmentIntegrityHandling handling = this.segmentIntegrityHandling;
        while (WebpChunkParsingUtils.TryReadNextChunkType(stream, buffer, metadata, this.skipMetadata, containerEnd, handling, out WebpChunkType chunkType))
        {
            if (chunkType == WebpChunkType.FrameData)
            {
                return true;
            }

            WebpChunkParsingUtils.SkipChunk(stream, buffer, containerEnd);
        }

        return false;
    }

    /// <summary>
    /// Reads frame information from the specified stream and updates the provided frame metadata.
    /// </summary>
    /// <param name="stream">The stream from which to read the frame information. The stream must support read and seek operations.</param>
    /// <param name="frameMetadata">The metadata updated from the frame header.</param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    /// <param name="segmentIntegrityHandling">The image data integrity mode. It controls a frame that ends at EOF.</param>
    /// <param name="imageExists">A value that tells if Identify read a frame before this frame.</param>
    /// <returns><see langword="true"/> if the frame header is valid. Otherwise <see langword="false"/>.</returns>
    private static bool TryReadFrameInfo(
        BufferedReadStream stream,
        ImageFrameMetadata frameMetadata,
        long containerEnd,
        SegmentIntegrityHandling segmentIntegrityHandling,
        bool imageExists)
    {
        if (!WebpFrameData.TryParse(stream, containerEnd, segmentIntegrityHandling, imageExists, out WebpFrameData frameData))
        {
            return false;
        }

        SetFrameMetadata(frameMetadata, frameData);

        // IgnoreImageData permits a frame that ends at EOF. Stop at EOF in that case.
        uint remaining = frameData.DataSize - WebpFrameData.HeaderSize;
        stream.Position = Math.Min(stream.Position + remaining + (frameData.DataSize & 1), stream.Length);
        return true;
    }

    /// <summary>
    /// Reads one webp frame.
    /// </summary>
    /// <remarks>
    /// The method checks each frame header and the headers of the image data before it adds a frame. Before the image exists, the method throws for damaged
    /// headers in each mode. After that, the integrity mode decides, and the method returns <see langword="false"/> when the mode ignores the error. The frame
    /// decoders apply the integrity mode to damage in the frame pixel data.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The stream that holds the image. It cannot be null.</param>
    /// <param name="image">The image to decode the information to.</param>
    /// <param name="prevFrameData">The previous frame data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="backgroundColor">The default background color of the canvas.</param>
    /// <param name="colorProfileConverter">The optional converter for the embedded ICC profile.</param>
    /// <param name="containerEnd">The end of the RIFF payload.</param>
    /// <returns><see langword="true"/> if the decoder added the frame. Otherwise <see langword="false"/>.</returns>
    private bool TryReadFrame<TPixel>(
        BufferedReadStream stream,
        ref Image<TPixel>? image,
        ref WebpFrameData? prevFrameData,
        uint width,
        uint height,
        TPixel backgroundColor,
        ColorProfileConverter? colorProfileConverter,
        long containerEnd)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        bool imageExists = image is not null;
        if (!WebpFrameData.TryParse(stream, containerEnd, this.segmentIntegrityHandling, imageExists, out WebpFrameData frameData))
        {
            return false;
        }

        if (!new Rectangle(0, 0, (int)width, (int)height).Contains(frameData.Bounds))
        {
            this.segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, "The WebP animation frame is outside the canvas.");
            return false;
        }

        // The ALPH data of the previous frame does not belong to this frame.
        this.alphaData?.Dispose();
        this.alphaData = null;

        // The frame payload holds an optional ALPH chunk and then a VP8 or VP8L chunk. Nested chunks must fit inside this ANMF payload. Bytes in the next
        // frame cannot complete this frame. A stream that ends before the image chunk header also misses the image chunk.
        long frameEnd = stream.Position + frameData.DataSize - WebpFrameData.HeaderSize;
        Span<byte> buffer = stackalloc byte[4];
        WebpChunkType chunkType;
        byte alphaChunkHeader = 0;
        while (true)
        {
            if (frameEnd - stream.Position < WebpConstants.ChunkHeaderSize || stream.RemainingBytes < WebpConstants.ChunkHeaderSize)
            {
                this.segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, "Missing WebP frame image chunk.");
                return false;
            }

            chunkType = WebpChunkParsingUtils.ReadChunkType(stream, buffer);
            if (chunkType != WebpChunkType.Alpha || this.alphaData is not null)
            {
                break;
            }

            if (!WebpChunkParsingUtils.TryReadAlphaChunk(
                stream,
                buffer,
                this.memoryAllocator,
                frameEnd,
                this.segmentIntegrityHandling,
                imageExists,
                out this.alphaData,
                out alphaChunkHeader))
            {
                return false;
            }
        }

        if (chunkType is not WebpChunkType.Vp8 and not WebpChunkType.Vp8L)
        {
            this.segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, "Read unexpected chunk type, should be VP8 or VP8L");
            return false;
        }

        if (chunkType == WebpChunkType.Vp8L && this.alphaData is not null)
        {
            // VP8L data holds its own alpha, so a frame with an ALPH chunk and a VP8L chunk is not valid. The libwebp demuxer also rejects this frame. See the
            // WebP Container Specification, section "Extended File Format" (https://developers.google.com/speed/webp/docs/riff_container).
            this.segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, "A WebP frame with a VP8L image chunk must not have an ALPH chunk.");
            return false;
        }

        WebpFeatures features = new() { Alpha = this.alphaData is not null, AlphaChunkHeader = alphaChunkHeader };
        if (!WebpChunkParsingUtils.TryReadImageChunk(
            chunkType,
            this.memoryAllocator,
            stream,
            buffer,
            features,
            frameEnd,
            this.segmentIntegrityHandling,
            imageExists,
            out WebpImageInfo? webpInfo))
        {
            return false;
        }

        using WebpImageInfo frameInfo = webpInfo;
        if (frameInfo.Width != frameData.Width || frameInfo.Height != frameData.Height)
        {
            this.segmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, "WebP frame dimensions do not match its image chunk.");
            return false;
        }

        // As for a still image, the decoder reads the headers of the image data before it creates the canvas or the frame.
        if (!frameInfo.TryReadImageHeaders(this.memoryAllocator, this.configuration, this.segmentIntegrityHandling, imageExists))
        {
            return false;
        }

        ImageFrame<TPixel> currentFrame;
        if (image is null)
        {
            image = new Image<TPixel>(this.configuration, (int)width, (int)height, backgroundColor, this.metadata);

            currentFrame = image.Frames.RootFrame;
            SetFrameMetadata(currentFrame.Metadata, frameData);
        }
        else
        {
            ImageFrame<TPixel> previousFrame = image.Frames[image.Frames.Count - 1];

            // If the frame is a key frame we do not need to clone the frame or clear it.
            bool isKeyFrame = prevFrameData?.DisposalMethod is FrameDisposalMode.RestoreToBackground
                 && this.restoreArea == image.Bounds;

            if (isKeyFrame)
            {
                currentFrame = image.Frames.CreateFrame(backgroundColor);
            }
            else
            {
                // This clones the frame and adds it the collection.
                currentFrame = image.Frames.AddFrame(previousFrame);
                if (prevFrameData?.DisposalMethod is FrameDisposalMode.RestoreToBackground)
                {
                    this.RestoreToBackground(currentFrame, backgroundColor);
                }
            }

            SetFrameMetadata(currentFrame.Metadata, frameData);
        }

        Rectangle interest = frameData.Bounds;
        bool blend = imageExists && frameData.BlendingMethod == FrameBlendMode.Over;
        using Buffer2D<TPixel> pixelData = this.DecodeImageFrameData<TPixel>(frameData, frameInfo, colorProfileConverter);
        DrawDecodedImageFrameOnCanvas(pixelData, currentFrame, interest, blend);

        prevFrameData = frameData;

        if (frameData.DisposalMethod is FrameDisposalMode.RestoreToBackground)
        {
            this.restoreArea = interest;
        }

        // IgnoreImageData permits a frame that ends at EOF. Stop at EOF in that case.
        stream.Position = Math.Min(frameEnd + (frameData.DataSize & 1), stream.Length);
        return true;
    }

    /// <summary>
    /// Sets the frames metadata.
    /// </summary>
    /// <param name="meta">The metadata.</param>
    /// <param name="frameData">The frame data.</param>
    private static void SetFrameMetadata(ImageFrameMetadata meta, WebpFrameData frameData)
    {
        WebpFrameMetadata frameMetadata = meta.GetWebpMetadata();
        frameMetadata.FrameDelay = frameData.Duration;
        frameMetadata.BlendMode = frameData.BlendingMethod;
        frameMetadata.DisposalMode = frameData.DisposalMethod;
    }

    /// <summary>
    /// Decodes the lossy or lossless webp image data.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frameData">The frame data.</param>
    /// <param name="webpInfo">The webp information. <see cref="WebpImageInfo.TryReadImageHeaders"/> read its headers.</param>
    /// <param name="colorProfileConverter">The optional converter for the embedded ICC profile.</param>
    /// <returns>A decoded image.</returns>
    private Buffer2D<TPixel> DecodeImageFrameData<TPixel>(WebpFrameData frameData, WebpImageInfo webpInfo, ColorProfileConverter? colorProfileConverter)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ImageFrame<TPixel> decodedFrame = new(this.configuration, (int)frameData.Width, (int)frameData.Height);

        try
        {
            Buffer2D<TPixel> decodeBuffer = decodedFrame.PixelBuffer;
            webpInfo.Decode(decodeBuffer, this.alphaData, colorProfileConverter, this.segmentIntegrityHandling);
            return decodeBuffer;
        }
        catch
        {
            decodedFrame?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Draws the decoded image on canvas. The decoded image can be smaller the canvas.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    /// <param name="decodedImageFrame">The decoded image.</param>
    /// <param name="imageFrame">The image frame to draw into.</param>
    /// <param name="restoreArea">The area of the frame.</param>
    /// <param name="blend">Whether to blend the decoded frame data onto the target frame.</param>
    private static void DrawDecodedImageFrameOnCanvas<TPixel>(
        Buffer2D<TPixel> decodedImageFrame,
        ImageFrame<TPixel> imageFrame,
        Rectangle restoreArea,
        bool blend)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Trim the destination frame to match the restore area. The source frame is already trimmed.
        Buffer2DRegion<TPixel> imageFramePixels = imageFrame.PixelBuffer.GetRegion(restoreArea);
        if (blend)
        {
            // The destination frame has already been prepopulated with the pixel data from the previous frame
            // so blending will leave the desired result which takes into consideration restoration to the
            // background color within the restore area.
            PixelBlender<TPixel> blender = PixelBlenders<TPixel>.NormalSrcOver;

            // By using a dedicated vector span we can avoid per-row pool allocations in PixelBlender.Blend
            // We need 3 Vector4 values per pixel to store the background, foreground, and result pixels for blending.
            using IMemoryOwner<Vector4> workingBufferOwner = imageFrame.Configuration.MemoryAllocator.Allocate<Vector4>(restoreArea.Width * 3);
            Span<Vector4> workingBuffer = workingBufferOwner.GetSpan();

            for (int y = 0; y < restoreArea.Height; y++)
            {
                Span<TPixel> framePixelRow = imageFramePixels.DangerousGetRowSpan(y);
                Span<TPixel> decodedPixelRow = decodedImageFrame.DangerousGetRowSpan(y)[..restoreArea.Width];

                blender.Blend<TPixel>(imageFrame.Configuration, framePixelRow, framePixelRow, decodedPixelRow, 1f, workingBuffer);
            }

            return;
        }

        for (int y = 0; y < restoreArea.Height; y++)
        {
            Span<TPixel> framePixelRow = imageFramePixels.DangerousGetRowSpan(y);
            Span<TPixel> decodedPixelRow = decodedImageFrame.DangerousGetRowSpan(y)[..restoreArea.Width];
            decodedPixelRow.CopyTo(framePixelRow);
        }
    }

    /// <summary>
    /// Dispose to background color. Fill the rectangle on the canvas covered by the current frame
    /// with background color specified in the ANIM chunk.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="imageFrame">The image frame.</param>
    /// <param name="backgroundColor">Color of the background.</param>
    private void RestoreToBackground<TPixel>(ImageFrame<TPixel> imageFrame, TPixel backgroundColor)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!this.restoreArea.HasValue)
        {
            return;
        }

        Rectangle interest = Rectangle.Intersect(imageFrame.Bounds, this.restoreArea.Value);
        Buffer2DRegion<TPixel> pixelRegion = imageFrame.PixelBuffer.GetRegion(interest);
        pixelRegion.Fill(backgroundColor);

        this.restoreArea = null;
    }

    /// <inheritdoc/>
    public void Dispose() => this.alphaData?.Dispose();
}
