// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Webp.Chunks;
using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
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
    /// Initializes a new instance of the <see cref="WebpAnimationDecoder"/> class.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The global configuration.</param>
    /// <param name="maxFrames">The maximum number of frames to decode. Inclusive.</param>
    /// <param name="skipMetadata">Whether to skip metadata.</param>
    /// <param name="backgroundColorHandling">The flag to decide how to handle the background color in the Animation Chunk.</param>
    public WebpAnimationDecoder(
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        uint maxFrames,
        bool skipMetadata,
        BackgroundColorHandling backgroundColorHandling)
    {
        this.memoryAllocator = memoryAllocator;
        this.configuration = configuration;
        this.maxFrames = maxFrames;
        this.skipMetadata = skipMetadata;
        this.backgroundColorHandling = backgroundColorHandling;
    }

    /// <summary>
    /// Reads the animated webp image information from the specified stream.
    /// </summary>
    /// <param name="stream">The stream, where the image should be decoded from. Cannot be null.</param>
    /// <param name="bitsPerPixel">The bits per pixel.</param>
    /// <param name="features">The webp features.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="completeDataSize">The size of the image data in bytes.</param>
    public ImageInfo Identify(
        BufferedReadStream stream,
        int bitsPerPixel,
        WebpFeatures features,
        uint width,
        uint height,
        uint completeDataSize)
    {
        List<ImageFrameMetadata> framesMetadata = new();
        this.metadata = new ImageMetadata();
        this.webpMetadata = this.metadata.GetWebpMetadata();
        this.webpMetadata.RepeatCount = features.AnimationLoopCount;

        this.webpMetadata.BackgroundColor = this.backgroundColorHandling == BackgroundColorHandling.Ignore
            ? Color.Transparent
            : features.AnimationBackgroundColor!.Value;

        Span<byte> buffer = stackalloc byte[4];
        uint frameCount = 0;
        int remainingBytes = (int)completeDataSize;
        while (remainingBytes > 0)
        {
            WebpChunkType chunkType = WebpChunkParsingUtils.ReadChunkType(stream, buffer);
            remainingBytes -= 4;
            switch (chunkType)
            {
                case WebpChunkType.FrameData:

                    ImageFrameMetadata frameMetadata = new();
                    uint dataSize = ReadFrameInfo(stream, ref frameMetadata);
                    framesMetadata.Add(frameMetadata);

                    remainingBytes -= (int)dataSize;
                    break;
                case WebpChunkType.Xmp:
                case WebpChunkType.Exif:
                    WebpChunkParsingUtils.ParseOptionalChunks(stream, chunkType, this.metadata, this.skipMetadata, buffer);
                    break;
                default:

                    // Specification explicitly states to ignore unknown chunks.
                    // We do not support writing these chunks at present.
                    break;
            }

            if (stream.Position == stream.Length || ++frameCount == this.maxFrames)
            {
                break;
            }
        }

        return new ImageInfo(new PixelTypeInfo(bitsPerPixel), new Size((int)width, (int)height), this.metadata, framesMetadata);
    }

    /// <summary>
    /// Decodes the animated webp image from the specified stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The stream, where the image should be decoded from. Cannot be null.</param>
    /// <param name="features">The webp features.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="completeDataSize">The size of the image data in bytes.</param>
    public Image<TPixel> Decode<TPixel>(
        BufferedReadStream stream,
        WebpFeatures features,
        uint width,
        uint height,
        uint completeDataSize)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Image<TPixel>? image = null;
        ImageFrame<TPixel>? previousFrame = null;
        WebpFrameData? prevFrameData = null;

        this.metadata = new ImageMetadata();
        this.webpMetadata = this.metadata.GetWebpMetadata();
        this.webpMetadata.RepeatCount = features.AnimationLoopCount;

        Color backgroundColor = this.backgroundColorHandling == BackgroundColorHandling.Ignore
            ? Color.Transparent
            : features.AnimationBackgroundColor!.Value;

        this.webpMetadata.BackgroundColor = backgroundColor;
        TPixel backgroundPixel = backgroundColor.ToPixel<TPixel>();

        Span<byte> buffer = stackalloc byte[4];
        uint frameCount = 0;
        int remainingBytes = (int)completeDataSize;
        while (remainingBytes > 0)
        {
            WebpChunkType chunkType = WebpChunkParsingUtils.ReadChunkType(stream, buffer);
            remainingBytes -= 4;
            switch (chunkType)
            {
                case WebpChunkType.FrameData:
                    uint dataSize = this.ReadFrame(
                        stream,
                        ref image,
                        ref previousFrame,
                        ref prevFrameData,
                        width,
                        height,
                        backgroundPixel);

                    remainingBytes -= (int)dataSize;
                    break;
                case WebpChunkType.Xmp:
                case WebpChunkType.Exif:
                    WebpChunkParsingUtils.ParseOptionalChunks(stream, chunkType, image!.Metadata, this.skipMetadata, buffer);
                    break;
                default:

                    // Specification explicitly states to ignore unknown chunks.
                    // We do not support writing these chunks at present.
                    break;
            }

            if (stream.Position == stream.Length || ++frameCount == this.maxFrames)
            {
                break;
            }
        }

        return image!;
    }

    /// <summary>
    /// Reads frame information from the specified stream and updates the provided frame metadata.
    /// </summary>
    /// <param name="stream">The stream from which to read the frame information. Must support reading and seeking.</param>
    /// <param name="frameMetadata">A reference to the structure that will be updated with the parsed frame metadata.</param>
    /// <returns>The number of bytes read from the stream while parsing the frame information.</returns>
    private static uint ReadFrameInfo(BufferedReadStream stream, ref ImageFrameMetadata frameMetadata)
    {
        WebpFrameData frameData = WebpFrameData.Parse(stream);
        SetFrameMetadata(frameMetadata, frameData);

        // Size of the frame header chunk.
        const int chunkHeaderSize = 16;

        uint remaining = frameData.DataSize - chunkHeaderSize;
        stream.Skip((int)remaining);

        return remaining;
    }

    /// <summary>
    /// Reads an individual webp frame.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The stream, where the image should be decoded from. Cannot be null.</param>
    /// <param name="image">The image to decode the information to.</param>
    /// <param name="previousFrame">The previous frame.</param>
    /// <param name="prevFrameData">The previous frame data.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="backgroundColor">The default background color of the canvas in.</param>
    private uint ReadFrame<TPixel>(
        BufferedReadStream stream,
        ref Image<TPixel>? image,
        ref ImageFrame<TPixel>? previousFrame,
        ref WebpFrameData? prevFrameData,
        uint width,
        uint height,
        TPixel backgroundColor)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpFrameData frameData = WebpFrameData.Parse(stream);
        long streamStartPosition = stream.Position;
        Span<byte> buffer = stackalloc byte[4];

        WebpChunkType chunkType = WebpChunkParsingUtils.ReadChunkType(stream, buffer);
        bool hasAlpha = false;
        byte alphaChunkHeader = 0;
        if (chunkType is WebpChunkType.Alpha)
        {
            alphaChunkHeader = this.ReadAlphaData(stream);
            hasAlpha = true;
            chunkType = WebpChunkParsingUtils.ReadChunkType(stream, buffer);
        }

        WebpImageInfo? webpInfo = null;
        WebpFeatures features = new();
        switch (chunkType)
        {
            case WebpChunkType.Vp8:
                webpInfo = WebpChunkParsingUtils.ReadVp8Header(this.memoryAllocator, stream, buffer, features);
                features.Alpha = hasAlpha;
                features.AlphaChunkHeader = alphaChunkHeader;
                break;
            case WebpChunkType.Vp8L:
                if (hasAlpha)
                {
                    WebpThrowHelper.ThrowNotSupportedException("Alpha channel is not supported for lossless webp images.");
                }

                webpInfo = WebpChunkParsingUtils.ReadVp8LHeader(this.memoryAllocator, stream, buffer, features);
                break;
            default:
                WebpThrowHelper.ThrowImageFormatException("Read unexpected chunk type, should be VP8 or VP8L");
                break;
        }

        ImageFrame<TPixel> currentFrame;
        if (previousFrame is null)
        {
            image = new Image<TPixel>(this.configuration, (int)width, (int)height, backgroundColor, this.metadata);

            currentFrame = image.Frames.RootFrame;
            SetFrameMetadata(currentFrame.Metadata, frameData);
        }
        else
        {
            // If the frame is a key frame we do not need to clone the frame or clear it.
            bool isKeyFrame = prevFrameData?.DisposalMethod is WebpDisposalMethod.RestoreToBackground
                 && this.restoreArea == image!.Bounds;

            if (isKeyFrame)
            {
                currentFrame = image!.Frames.CreateFrame(backgroundColor);
            }
            else
            {
                // This clones the frame and adds it the collection.
                currentFrame = image!.Frames.AddFrame(previousFrame);
                if (prevFrameData?.DisposalMethod is WebpDisposalMethod.RestoreToBackground)
                {
                    this.RestoreToBackground(currentFrame, backgroundColor);
                }
            }

            SetFrameMetadata(currentFrame.Metadata, frameData);
        }

        Rectangle interest = frameData.Bounds;
        bool blend = previousFrame != null && frameData.BlendingMethod == WebpBlendMethod.Over;
        using Buffer2D<TPixel> pixelData = this.DecodeImageFrameData<TPixel>(frameData, webpInfo);
        DrawDecodedImageFrameOnCanvas(pixelData, currentFrame, interest, blend);

        webpInfo?.Dispose();
        previousFrame = currentFrame;
        prevFrameData = frameData;

        if (frameData.DisposalMethod is WebpDisposalMethod.RestoreToBackground)
        {
            this.restoreArea = interest;
        }

        return (uint)(stream.Position - streamStartPosition);
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
        frameMetadata.BlendMethod = frameData.BlendingMethod;
        frameMetadata.DisposalMethod = frameData.DisposalMethod;
    }

    /// <summary>
    /// Reads the ALPH chunk data.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    private byte ReadAlphaData(BufferedReadStream stream)
    {
        this.alphaData?.Dispose();

        uint alphaChunkSize = WebpChunkParsingUtils.ReadChunkSize(stream, stackalloc byte[4]);
        int alphaDataSize = (int)(alphaChunkSize - 1);
        this.alphaData = this.memoryAllocator.Allocate<byte>(alphaDataSize);

        byte alphaChunkHeader = (byte)stream.ReadByte();
        Span<byte> alphaData = this.alphaData.GetSpan();
        _ = stream.Read(alphaData, 0, alphaDataSize);

        return alphaChunkHeader;
    }

    /// <summary>
    /// Decodes the either lossy or lossless webp image data.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="frameData">The frame data.</param>
    /// <param name="webpInfo">The webp information.</param>
    /// <returns>A decoded image.</returns>
    private Buffer2D<TPixel> DecodeImageFrameData<TPixel>(WebpFrameData frameData, WebpImageInfo webpInfo)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ImageFrame<TPixel> decodedFrame = new(this.configuration, (int)frameData.Width, (int)frameData.Height);

        try
        {
            Buffer2D<TPixel> decodeBuffer = decodedFrame.PixelBuffer;
            if (webpInfo.IsLossless)
            {
                WebpLosslessDecoder losslessDecoder = new(webpInfo.Vp8LBitReader, this.memoryAllocator, this.configuration);
                losslessDecoder.Decode(decodeBuffer, (int)frameData.Width, (int)frameData.Height);
            }
            else
            {
                WebpLossyDecoder lossyDecoder =
                    new(webpInfo.Vp8BitReader, this.memoryAllocator, this.configuration);
                lossyDecoder.Decode(decodeBuffer, (int)frameData.Width, (int)frameData.Height, webpInfo, this.alphaData);
            }

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
    /// <param name="interest">The area of the frame to draw to.</param>
    /// <param name="blend">Whether to blend the decoded frame data onto the target frame.</param>
    private static void DrawDecodedImageFrameOnCanvas<TPixel>(
        Buffer2D<TPixel> decodedImageFrame,
        ImageFrame<TPixel> imageFrame,
        Rectangle interest,
        bool blend)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Trim the destination frame to match the restore area. The source frame is already trimmed.
        Buffer2DRegion<TPixel> imageFramePixels = imageFrame.PixelBuffer.GetRegion(interest);
        if (blend)
        {
            // The destination frame has already been prepopulated with the pixel data from the previous frame
            // so blending will leave the desired result which takes into consideration restoration to the
            // background color within the restore area.
            PixelBlender<TPixel> blender =
                PixelOperations<TPixel>.Instance.GetPixelBlender(PixelColorBlendingMode.Normal, PixelAlphaCompositionMode.SrcOver);

            for (int y = 0; y < interest.Height; y++)
            {
                Span<TPixel> framePixelRow = imageFramePixels.DangerousGetRowSpan(y);
                Span<TPixel> decodedPixelRow = decodedImageFrame.DangerousGetRowSpan(y);

                blender.Blend<TPixel>(imageFrame.Configuration, framePixelRow, framePixelRow, decodedPixelRow, 1f);
            }

            return;
        }

        for (int y = 0; y < interest.Height; y++)
        {
            Span<TPixel> framePixelRow = imageFramePixels.DangerousGetRowSpan(y);
            Span<TPixel> decodedPixelRow = decodedImageFrame.DangerousGetRowSpan(y);
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

        Rectangle interest = Rectangle.Intersect(imageFrame.Bounds(), this.restoreArea.Value);
        Buffer2DRegion<TPixel> pixelRegion = imageFrame.PixelBuffer.GetRegion(interest);
        pixelRegion.Fill(backgroundColor);

        this.restoreArea = null;
    }

    /// <inheritdoc/>
    public void Dispose() => this.alphaData?.Dispose();
}
