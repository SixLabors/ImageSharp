// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Formats.Webp.Lossy;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Decoder for animated webp images.
    /// </summary>
    internal class WebpAnimationDecoder : IDisposable
    {
        /// <summary>
        /// Reusable buffer.
        /// </summary>
        private readonly byte[] buffer = new byte[4];

        /// <summary>
        /// Used for allocating memory during the decoding operations.
        /// </summary>
        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// The global configuration.
        /// </summary>
        private readonly Configuration configuration;

        /// <summary>
        /// The area to restore.
        /// </summary>
        private Rectangle? restoreArea;

        /// <summary>
        /// The abstract metadata.
        /// </summary>
        private ImageMetadata metadata;

        /// <summary>
        /// The gif specific metadata.
        /// </summary>
        private WebpMetadata webpMetadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebpAnimationDecoder"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="configuration">The global configuration.</param>
        /// <param name="decodingMode">The frame decoding mode.</param>
        public WebpAnimationDecoder(MemoryAllocator memoryAllocator, Configuration configuration, FrameDecodingMode decodingMode)
        {
            this.memoryAllocator = memoryAllocator;
            this.configuration = configuration;
            this.DecodingMode = decodingMode;
        }

        /// <summary>
        /// Gets or sets the alpha data, if an ALPH chunk is present.
        /// </summary>
        public IMemoryOwner<byte> AlphaData { get; set; }

        /// <summary>
        /// Gets the decoding mode for multi-frame images.
        /// </summary>
        public FrameDecodingMode DecodingMode { get; }

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
            Image<TPixel> image = null;
            ImageFrame<TPixel> previousFrame = null;

            this.metadata = new ImageMetadata();
            this.webpMetadata = this.metadata.GetWebpMetadata();
            this.webpMetadata.AnimationLoopCount = features.AnimationLoopCount;

            int remainingBytes = (int)completeDataSize;
            while (remainingBytes > 0)
            {
                WebpChunkType chunkType = WebpChunkParsingUtils.ReadChunkType(stream, this.buffer);
                remainingBytes -= 4;
                switch (chunkType)
                {
                    case WebpChunkType.Animation:
                        uint dataSize = this.ReadFrame(stream, ref image, ref previousFrame, width, height, features.AnimationBackgroundColor.Value);
                        remainingBytes -= (int)dataSize;
                        break;
                    case WebpChunkType.Xmp:
                    case WebpChunkType.Exif:
                        WebpChunkParsingUtils.ParseOptionalChunks(stream, chunkType, image.Metadata, false, this.buffer);
                        break;
                    default:
                        WebpThrowHelper.ThrowImageFormatException("Read unexpected webp chunk data");
                        break;
                }

                if (stream.Position == stream.Length || this.DecodingMode is FrameDecodingMode.First)
                {
                    break;
                }
            }

            return image;
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
        private uint ReadFrame<TPixel>(BufferedReadStream stream, ref Image<TPixel> image, ref ImageFrame<TPixel> previousFrame, uint width, uint height, Color backgroundColor)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            AnimationFrameData frameData = this.ReadFrameHeader(stream);
            long streamStartPosition = stream.Position;

            WebpChunkType chunkType = WebpChunkParsingUtils.ReadChunkType(stream, this.buffer);
            bool hasAlpha = false;
            byte alphaChunkHeader = 0;
            if (chunkType is WebpChunkType.Alpha)
            {
                alphaChunkHeader = this.ReadAlphaData(stream);
                hasAlpha = true;
                chunkType = WebpChunkParsingUtils.ReadChunkType(stream, this.buffer);
            }

            WebpImageInfo webpInfo = null;
            var features = new WebpFeatures();
            switch (chunkType)
            {
                case WebpChunkType.Vp8:
                    webpInfo = WebpChunkParsingUtils.ReadVp8Header(this.memoryAllocator, stream, this.buffer, features);
                    features.Alpha = hasAlpha;
                    features.AlphaChunkHeader = alphaChunkHeader;
                    break;
                case WebpChunkType.Vp8L:
                    webpInfo = WebpChunkParsingUtils.ReadVp8LHeader(this.memoryAllocator, stream, this.buffer, features);
                    break;
                default:
                    WebpThrowHelper.ThrowImageFormatException("Read unexpected chunk type, should be VP8 or VP8L");
                    break;
            }

            ImageFrame<TPixel> currentFrame = null;
            ImageFrame<TPixel> imageFrame;
            if (previousFrame is null)
            {
                image = new Image<TPixel>(this.configuration, (int)width, (int)height, backgroundColor.ToPixel<TPixel>(), this.metadata);

                this.SetFrameMetadata(image.Frames.RootFrame.Metadata, frameData.Duration);

                imageFrame = image.Frames.RootFrame;
            }
            else
            {
                currentFrame = image.Frames.AddFrame(previousFrame); // This clones the frame and adds it the collection.

                this.SetFrameMetadata(currentFrame.Metadata, frameData.Duration);

                imageFrame = currentFrame;
            }

            int frameX = (int)(frameData.X * 2);
            int frameY = (int)(frameData.Y * 2);
            int frameWidth = (int)frameData.Width;
            int frameHeight = (int)frameData.Height;
            var regionRectangle = Rectangle.FromLTRB(frameX, frameY, frameX + frameWidth, frameY + frameHeight);

            if (frameData.DisposalMethod is AnimationDisposalMethod.Dispose)
            {
                this.RestoreToBackground(imageFrame, backgroundColor);
            }

            using Buffer2D<TPixel> decodedImage = this.DecodeImageData<TPixel>(frameData, webpInfo);
            this.DrawDecodedImageOnCanvas(decodedImage, imageFrame, frameX, frameY, frameWidth, frameHeight);

            if (previousFrame != null && frameData.BlendingMethod is AnimationBlendingMethod.AlphaBlending)
            {
                this.AlphaBlend(previousFrame, imageFrame, frameX, frameY, frameWidth, frameHeight);
            }

            previousFrame = currentFrame ?? image.Frames.RootFrame;
            this.restoreArea = regionRectangle;

            return (uint)(stream.Position - streamStartPosition);
        }

        /// <summary>
        /// Sets the frames metadata.
        /// </summary>
        /// <param name="meta">The metadata.</param>
        /// <param name="duration">The frame duration.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetFrameMetadata(ImageFrameMetadata meta, uint duration)
        {
            WebpFrameMetadata frameMetadata = meta.GetWebpMetadata();
            frameMetadata.FrameDuration = duration;
        }

        /// <summary>
        /// Reads the ALPH chunk data.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        private byte ReadAlphaData(BufferedReadStream stream)
        {
            this.AlphaData?.Dispose();

            uint alphaChunkSize = WebpChunkParsingUtils.ReadChunkSize(stream, this.buffer);
            int alphaDataSize = (int)(alphaChunkSize - 1);
            this.AlphaData = this.memoryAllocator.Allocate<byte>(alphaDataSize);

            byte alphaChunkHeader = (byte)stream.ReadByte();
            Span<byte> alphaData = this.AlphaData.GetSpan();
            stream.Read(alphaData, 0, alphaDataSize);

            return alphaChunkHeader;
        }

        /// <summary>
        /// Decodes the either lossy or lossless webp image data.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="frameData">The frame data.</param>
        /// <param name="webpInfo">The webp information.</param>
        /// <returns>A decoded image.</returns>
        private Buffer2D<TPixel> DecodeImageData<TPixel>(AnimationFrameData frameData, WebpImageInfo webpInfo)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var decodedImage = new Image<TPixel>((int)frameData.Width, (int)frameData.Height);

            try
            {
                Buffer2D<TPixel> pixelBufferDecoded = decodedImage.Frames.RootFrame.PixelBuffer;
                if (webpInfo.IsLossless)
                {
                    var losslessDecoder = new WebpLosslessDecoder(webpInfo.Vp8LBitReader, this.memoryAllocator, this.configuration);
                    losslessDecoder.Decode(pixelBufferDecoded, (int)webpInfo.Width, (int)webpInfo.Height);
                }
                else
                {
                    var lossyDecoder = new WebpLossyDecoder(webpInfo.Vp8BitReader, this.memoryAllocator, this.configuration);
                    lossyDecoder.Decode(pixelBufferDecoded, (int)webpInfo.Width, (int)webpInfo.Height, webpInfo, this.AlphaData);
                }

                return pixelBufferDecoded;
            }
            catch
            {
                decodedImage?.Dispose();
                throw;
            }
            finally
            {
                webpInfo.Dispose();
            }
        }

        /// <summary>
        /// Draws the decoded image on canvas. The decoded image can be smaller the the canvas.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="decodedImage">The decoded image.</param>
        /// <param name="imageFrame">The image frame to draw into.</param>
        /// <param name="frameX">The frame x coordinate.</param>
        /// <param name="frameY">The frame y coordinate.</param>
        /// <param name="frameWidth">The width of the frame.</param>
        /// <param name="frameHeight">The height of the frame.</param>
        private void DrawDecodedImageOnCanvas<TPixel>(Buffer2D<TPixel> decodedImage, ImageFrame<TPixel> imageFrame, int frameX, int frameY, int frameWidth, int frameHeight)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Buffer2D<TPixel> imageFramePixels = imageFrame.PixelBuffer;
            int decodedRowIdx = 0;
            for (int y = frameY; y < frameY + frameHeight; y++)
            {
                Span<TPixel> framePixelRow = imageFramePixels.DangerousGetRowSpan(y);
                Span<TPixel> decodedPixelRow = decodedImage.DangerousGetRowSpan(decodedRowIdx++).Slice(0, frameWidth);
                decodedPixelRow.TryCopyTo(framePixelRow.Slice(frameX));
            }
        }

        /// <summary>
        /// After disposing of the previous frame, render the current frame on the canvas using alpha-blending.
        /// If the current frame does not have an alpha channel, assume alpha value of 255, effectively replacing the rectangle.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="src">The source image.</param>
        /// <param name="dst">The destination image.</param>
        /// <param name="frameX">The frame x coordinate.</param>
        /// <param name="frameY">The frame y coordinate.</param>
        /// <param name="frameWidth">The width of the frame.</param>
        /// <param name="frameHeight">The height of the frame.</param>
        private void AlphaBlend<TPixel>(ImageFrame<TPixel> src, ImageFrame<TPixel> dst, int frameX, int frameY, int frameWidth, int frameHeight)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Buffer2D<TPixel> srcPixels = src.PixelBuffer;
            Buffer2D<TPixel> dstPixels = dst.PixelBuffer;
            PixelBlender<TPixel> blender = PixelOperations<TPixel>.Instance.GetPixelBlender(PixelColorBlendingMode.Normal, PixelAlphaCompositionMode.SrcOver);
            for (int y = frameY; y < frameY + frameHeight; y++)
            {
                Span<TPixel> srcPixelRow = srcPixels.DangerousGetRowSpan(y).Slice(frameX, frameWidth);
                Span<TPixel> dstPixelRow = dstPixels.DangerousGetRowSpan(y).Slice(frameX, frameWidth);

                blender.Blend<TPixel>(this.configuration, dstPixelRow, srcPixelRow, dstPixelRow, 1.0f);
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

            var interest = Rectangle.Intersect(imageFrame.Bounds(), this.restoreArea.Value);
            Buffer2DRegion<TPixel> pixelRegion = imageFrame.PixelBuffer.GetRegion(interest);
            TPixel backgroundPixel = backgroundColor.ToPixel<TPixel>();
            pixelRegion.Fill(backgroundPixel);
        }

        /// <summary>
        /// Reads the animation frame header.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <returns>Animation frame data.</returns>
        private AnimationFrameData ReadFrameHeader(BufferedReadStream stream)
        {
            var data = new AnimationFrameData
            {
                DataSize = WebpChunkParsingUtils.ReadChunkSize(stream, this.buffer)
            };

            // 3 bytes for the X coordinate of the upper left corner of the frame.
            data.X = WebpChunkParsingUtils.ReadUnsignedInt24Bit(stream, this.buffer);

            // 3 bytes for the Y coordinate of the upper left corner of the frame.
            data.Y = WebpChunkParsingUtils.ReadUnsignedInt24Bit(stream, this.buffer);

            // Frame width Minus One.
            data.Width = WebpChunkParsingUtils.ReadUnsignedInt24Bit(stream, this.buffer) + 1;

            // Frame height Minus One.
            data.Height = WebpChunkParsingUtils.ReadUnsignedInt24Bit(stream, this.buffer) + 1;

            // Frame duration.
            data.Duration = WebpChunkParsingUtils.ReadUnsignedInt24Bit(stream, this.buffer);

            byte flags = (byte)stream.ReadByte();
            data.DisposalMethod = (flags & 1) == 1 ? AnimationDisposalMethod.Dispose : AnimationDisposalMethod.DoNotDispose;
            data.BlendingMethod = (flags & (1 << 1)) != 0 ? AnimationBlendingMethod.DoNotBlend : AnimationBlendingMethod.AlphaBlending;

            return data;
        }

        /// <inheritdoc/>
        public void Dispose() => this.AlphaData?.Dispose();
    }
}
