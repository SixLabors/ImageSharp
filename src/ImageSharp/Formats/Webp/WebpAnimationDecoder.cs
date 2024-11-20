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
    /// <param name="backgroundColorHandling">The flag to decide how to handle the background color in the Animation Chunk.</param>
    public WebpAnimationDecoder(MemoryAllocator memoryAllocator, Configuration configuration, uint maxFrames, BackgroundColorHandling backgroundColorHandling)
    {
        this.memoryAllocator = memoryAllocator;
        this.configuration = configuration;
        this.maxFrames = maxFrames;
        this.backgroundColorHandling = backgroundColorHandling;
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
    public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, WebpFeatures features, uint width, uint height, uint completeDataSize)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Image<TPixel>? image = null;
        ImageFrame<TPixel>? previousFrame = null;

        this.metadata = new ImageMetadata();
        this.webpMetadata = this.metadata.GetWebpMetadata();
        this.webpMetadata.RepeatCount = features.AnimationLoopCount;

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
                    Color backgroundColor = this.backgroundColorHandling == BackgroundColorHandling.Ignore
                        ? Color.FromPixel(new Bgra32(0, 0, 0, 0))
                        : features.AnimationBackgroundColor!.Value;
                    uint dataSize = this.ReadFrame(stream, ref image, ref previousFrame, width, height, backgroundColor);
                    remainingBytes -= (int)dataSize;
                    break;
                case WebpChunkType.Xmp:
                case WebpChunkType.Exif:
                    WebpChunkParsingUtils.ParseOptionalChunks(stream, chunkType, image!.Metadata, false, buffer);
                    break;
                default:
                    WebpThrowHelper.ThrowImageFormatException("Read unexpected webp chunk data");
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
    /// Reads an individual webp frame.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The stream, where the image should be decoded from. Cannot be null.</param>
    /// <param name="image">The image to decode the information to.</param>
    /// <param name="previousFrame">The previous frame.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="backgroundColor">The default background color of the canvas in.</param>
    private uint ReadFrame<TPixel>(BufferedReadStream stream, ref Image<TPixel>? image, ref ImageFrame<TPixel>? previousFrame, uint width, uint height, Color backgroundColor)
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

        ImageFrame<TPixel>? currentFrame = null;
        ImageFrame<TPixel> imageFrame;
        if (previousFrame is null)
        {
            image = new Image<TPixel>(this.configuration, (int)width, (int)height, backgroundColor.ToPixel<TPixel>(), this.metadata);

            SetFrameMetadata(image.Frames.RootFrame.Metadata, frameData);

            imageFrame = image.Frames.RootFrame;
        }
        else
        {
            currentFrame = image!.Frames.AddFrame(previousFrame); // This clones the frame and adds it the collection.

            SetFrameMetadata(currentFrame.Metadata, frameData);

            imageFrame = currentFrame;
        }

        Rectangle regionRectangle = frameData.Bounds;

        if (frameData.DisposalMethod is FrameDisposalMode.RestoreToBackground)
        {
            this.RestoreToBackground(imageFrame, backgroundColor);
        }

        using Buffer2D<TPixel> decodedImageFrame = this.DecodeImageFrameData<TPixel>(frameData, webpInfo);

        bool blend = previousFrame != null && frameData.BlendingMethod == FrameBlendMode.Over;
        DrawDecodedImageFrameOnCanvas(decodedImageFrame, imageFrame, regionRectangle, blend);

        previousFrame = currentFrame ?? image.Frames.RootFrame;
        this.restoreArea = regionRectangle;

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
        frameMetadata.BlendMode = frameData.BlendingMethod;
        frameMetadata.DisposalMode = frameData.DisposalMethod;
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
            Buffer2D<TPixel> pixelBufferDecoded = decodedFrame.PixelBuffer;
            if (webpInfo.IsLossless)
            {
                WebpLosslessDecoder losslessDecoder =
                    new(webpInfo.Vp8LBitReader, this.memoryAllocator, this.configuration);
                losslessDecoder.Decode(pixelBufferDecoded, (int)webpInfo.Width, (int)webpInfo.Height);
            }
            else
            {
                WebpLossyDecoder lossyDecoder =
                    new(webpInfo.Vp8BitReader, this.memoryAllocator, this.configuration);
                lossyDecoder.Decode(pixelBufferDecoded, (int)webpInfo.Width, (int)webpInfo.Height, webpInfo, this.alphaData);
            }

            return pixelBufferDecoded;
        }
        catch
        {
            decodedFrame?.Dispose();
            throw;
        }
        finally
        {
            webpInfo.Dispose();
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
            PixelBlender<TPixel> blender =
                PixelOperations<TPixel>.Instance.GetPixelBlender(PixelColorBlendingMode.Normal, PixelAlphaCompositionMode.SrcOver);

            for (int y = 0; y < restoreArea.Height; y++)
            {
                Span<TPixel> framePixelRow = imageFramePixels.DangerousGetRowSpan(y);
                Span<TPixel> decodedPixelRow = decodedImageFrame.DangerousGetRowSpan(y)[..restoreArea.Width];

                blender.Blend<TPixel>(imageFrame.Configuration, framePixelRow, framePixelRow, decodedPixelRow, 1f);
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
    private void RestoreToBackground<TPixel>(ImageFrame<TPixel> imageFrame, Color backgroundColor)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!this.restoreArea.HasValue)
        {
            return;
        }

        Rectangle interest = Rectangle.Intersect(imageFrame.Bounds(), this.restoreArea.Value);
        Buffer2DRegion<TPixel> pixelRegion = imageFrame.PixelBuffer.GetRegion(interest);
        TPixel backgroundPixel = backgroundColor.ToPixel<TPixel>();
        pixelRegion.Fill(backgroundPixel);
    }

    /// <inheritdoc/>
    public void Dispose() => this.alphaData?.Dispose();
}
