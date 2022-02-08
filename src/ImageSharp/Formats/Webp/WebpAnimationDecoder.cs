// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
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
    internal class WebpAnimationDecoder
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
        /// Initializes a new instance of the <see cref="WebpAnimationDecoder"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator.</param>
        /// <param name="configuration">The global configuration.</param>
        public WebpAnimationDecoder(MemoryAllocator memoryAllocator, Configuration configuration)
        {
            this.memoryAllocator = memoryAllocator;
            this.configuration = configuration;
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
            Image<TPixel> image = null;
            ImageFrame<TPixel> previousFrame = null;

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

                if (stream.Position == stream.Length)
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
            if (chunkType is WebpChunkType.Alpha)
            {
                // TODO: ignore alpha for now.
                stream.Skip(4);
                uint alphaChunkSize = WebpChunkParsingUtils.ReadChunkSize(stream, this.buffer);
                stream.Skip((int)alphaChunkSize);
                chunkType = WebpChunkParsingUtils.ReadChunkType(stream, this.buffer);
            }

            WebpImageInfo webpInfo = null;
            var features = new WebpFeatures();
            switch (chunkType)
            {
                case WebpChunkType.Vp8:
                    webpInfo = WebpChunkParsingUtils.ReadVp8Header(this.memoryAllocator, stream, this.buffer, features);
                    break;
                case WebpChunkType.Vp8L:
                    webpInfo = WebpChunkParsingUtils.ReadVp8LHeader(this.memoryAllocator, stream, this.buffer, features);
                    break;
                default:
                    WebpThrowHelper.ThrowImageFormatException("Read unexpected chunk type, should be VP8 or VP8L");
                    break;
            }

            var metaData = new ImageMetadata();
            ImageFrame<TPixel> currentFrame = null;
            ImageFrame<TPixel> imageFrame;
            if (previousFrame is null)
            {
                image = new Image<TPixel>(this.configuration, (int)width, (int)height, backgroundColor.ToPixel<TPixel>(), metaData);
                imageFrame = image.Frames.RootFrame;
            }
            else
            {
                currentFrame = image.Frames.AddFrame(previousFrame); // This clones the frame and adds it the collection.
                imageFrame = currentFrame;
            }

            if (frameData.DisposalMethod is AnimationDisposalMethod.Dispose)
            {
                this.RestoreToBackground(imageFrame, backgroundColor);
            }

            uint frameX = frameData.X * 2;
            uint frameY = frameData.Y * 2;
            uint frameWidth = frameData.Width;
            uint frameHeight = frameData.Height;
            var regionRectangle = Rectangle.FromLTRB((int)frameX, (int)frameY, (int)(frameX + frameWidth), (int)(frameY + frameHeight));

            using Image<TPixel> decodedImage = this.DecodeImageData<TPixel>(frameData, webpInfo);
            this.DrawDecodedImageOnCanvas(decodedImage, imageFrame, frameX, frameY, frameWidth, frameHeight);

            if (previousFrame != null && frameData.BlendingMethod is AnimationBlendingMethod.AlphaBlending)
            {
                this.AlphaBlend(previousFrame, imageFrame);
            }

            previousFrame = currentFrame ?? image.Frames.RootFrame;
            this.restoreArea = regionRectangle;

            return (uint)(stream.Position - streamStartPosition);
        }

        /// <summary>
        /// Decodes the either lossy or lossless webp image data.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="frameData">The frame data.</param>
        /// <param name="webpInfo">The webp information.</param>
        /// <returns>A decoded image.</returns>
        private Image<TPixel> DecodeImageData<TPixel>(AnimationFrameData frameData, WebpImageInfo webpInfo)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var decodedImage = new Image<TPixel>((int)frameData.Width, (int)frameData.Height);
            Buffer2D<TPixel> pixelBufferDecoded = decodedImage.Frames.RootFrame.PixelBuffer;
            if (webpInfo.IsLossless)
            {
                var losslessDecoder = new WebpLosslessDecoder(webpInfo.Vp8LBitReader, this.memoryAllocator, this.configuration);
                losslessDecoder.Decode(pixelBufferDecoded, (int)webpInfo.Width, (int)webpInfo.Height);
            }
            else
            {
                var lossyDecoder = new WebpLossyDecoder(webpInfo.Vp8BitReader, this.memoryAllocator, this.configuration);
                lossyDecoder.Decode(pixelBufferDecoded, (int)webpInfo.Width, (int)webpInfo.Height, webpInfo);
            }

            return decodedImage;
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
        private void DrawDecodedImageOnCanvas<TPixel>(Image<TPixel> decodedImage, ImageFrame<TPixel> imageFrame, uint frameX, uint frameY, uint frameWidth, uint frameHeight)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Buffer2D<TPixel> decodedImagePixels = decodedImage.Frames.RootFrame.PixelBuffer;
            Buffer2D<TPixel> imageFramePixels = imageFrame.PixelBuffer;
            int decodedRowIdx = 0;
            for (uint y = frameY; y < frameHeight; y++)
            {
                Span<TPixel> framePixelRow = imageFramePixels.DangerousGetRowSpan((int)y);
                Span<TPixel> decodedPixelRow = decodedImagePixels.DangerousGetRowSpan(decodedRowIdx++).Slice(0, (int)frameWidth);
                decodedPixelRow.TryCopyTo(framePixelRow.Slice((int)frameX));
            }
        }

        /// <summary>
        /// After disposing of the previous frame, render the current frame on the canvas using alpha-blending.
        /// If the current frame does not have an alpha channel, assume alpha value of 255, effectively replacing the rectangle.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="src">The source image.</param>
        /// <param name="dst">The destination image.</param>
        private void AlphaBlend<TPixel>(ImageFrame<TPixel> src, ImageFrame<TPixel> dst)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int width = src.Width;
            int height = src.Height;

            PixelBlender<Rgba32> blender = PixelOperations<Rgba32>.Instance.GetPixelBlender(PixelColorBlendingMode.Normal, PixelAlphaCompositionMode.SrcOver);
            Buffer2D<TPixel> srcPixels = src.PixelBuffer;
            Buffer2D<TPixel> dstPixels = dst.PixelBuffer;
            Rgba32 srcRgba = default;
            Rgba32 dstRgba = default;
            for (int y = 0; y < height; y++)
            {
                Span<TPixel> srcPixelRow = srcPixels.DangerousGetRowSpan(y);
                Span<TPixel> dstPixelRow = dstPixels.DangerousGetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    ref TPixel srcPixel = ref srcPixelRow[x];
                    ref TPixel dstPixel = ref dstPixelRow[x];
                    srcPixel.ToRgba32(ref srcRgba);
                    dstPixel.ToRgba32(ref dstRgba);
                    if (dstRgba.A == 0)
                    {
                        Rgba32 blendResult = blender.Blend(srcRgba, dstRgba, 1.0f);
                        dstPixel.FromRgba32(blendResult);
                    }
                }
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
            for (int y = 0; y < pixelRegion.Height; y++)
            {
                Span<TPixel> pixelRow = pixelRegion.DangerousGetRowSpan(y);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    ref TPixel pixel = ref pixelRow[x];
                    pixel.FromRgba32(backgroundColor);
                }
            }
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
    }
}
