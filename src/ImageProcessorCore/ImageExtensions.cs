// <copyright file="ImageExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.IO;

    using Formats;
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Saves the image to the given stream with the bmp format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsBmp(this ImageBase source, Stream stream) => new BmpEncoder().Encode(source, stream);

        /// <summary>
        /// Saves the image to the given stream with the png format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="quality">The quality to save the image to representing the number of colors. 
        /// Anything equal to 256 and below will cause the encoder to save the image in an indexed format.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsPng(this ImageBase source, Stream stream, int quality = Int32.MaxValue) => new PngEncoder { Quality = quality }.Encode(source, stream);

        /// <summary>
        /// Saves the image to the given stream with the jpeg format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="quality">The quality to save the image to. Between 1 and 100.</param>
        /// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsJpeg(this ImageBase source, Stream stream, int quality = 75) => new JpegEncoder { Quality = quality }.Encode(source, stream);

        /// <summary>
        /// Saves the image to the given stream with the gif format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="quality">The quality to save the image to representing the number of colors. Between 1 and 256.</param>
        /// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsGif(this ImageBase source, Stream stream, int quality = 256) => new GifEncoder { Quality = quality }.Encode(source, stream);

        /// <summary>
        /// Applies the collection of processors to the image.
        /// <remarks>This method does not resize the target image.</remarks>
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="processor">The processor to apply to the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Process(this Image source, IImageProcessor processor)
        {
            return Process(source, source.Bounds, processor);
        }

        /// <summary>
        /// Applies the collection of processors to the image.
        /// <remarks>This method does not resize the target image.</remarks>
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="processor">The processors to apply to the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Process(this Image source, Rectangle sourceRectangle, IImageProcessor processor)
        {
            return PerformAction(source, true, (sourceImage, targetImage) => processor.Apply(targetImage, sourceImage, sourceRectangle));
        }

        /// <summary>
        /// Applies the collection of processors to the image.
        /// <remarks>
        /// This method is not chainable.
        /// </remarks>
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The processor to apply to the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Process(this Image source, int width, int height, IImageSampler sampler)
        {
            return Process(source, width, height, source.Bounds, default(Rectangle), sampler);
        }

        /// <summary>
        /// Applies the collection of processors to the image.
        /// <remarks>
        /// This method does will resize the target image if the source and target rectangles are different.
        /// </remarks>
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="targetRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the location and size of the drawn image.
        /// The image is scaled to fit the rectangle.
        /// </param>
        /// <param name="sampler">The processor to apply to the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Process(this Image source, int width, int height, Rectangle sourceRectangle, Rectangle targetRectangle, IImageSampler sampler)
        {
           return PerformAction(source, false, (sourceImage, targetImage) => sampler.Apply(targetImage, sourceImage, width, height, targetRectangle, sourceRectangle));
        }

        /// <summary>
        /// Performs the given action on the source image.
        /// </summary>
        /// <param name="source">The image to perform the action against.</param>
        /// <param name="clone">Whether to clone the image.</param>
        /// <param name="action">The <see cref="Action"/> to perform against the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the <paramref name="source"/> has been disposed.</exception>
        private static Image PerformAction(Image source, bool clone, Action<ImageBase, ImageBase> action)
        {
            Image transformedImage = clone
                ? new Image(source)
                : new Image
                {
                    // Several properties require copying
                    // TODO: Check why we need to set these?
                    HorizontalResolution = source.HorizontalResolution,
                    VerticalResolution = source.VerticalResolution,
                    CurrentImageFormat = source.CurrentImageFormat,
                    RepeatCount = source.RepeatCount
                };

            action(source, transformedImage);

            for (int i = 0; i < source.Frames.Count; i++)
            {
                ImageFrame sourceFrame = source.Frames[i];
                ImageFrame tranformedFrame = clone ? new ImageFrame(sourceFrame) : new ImageFrame { FrameDelay = sourceFrame.FrameDelay };
                action(sourceFrame, tranformedFrame);

                if (!clone)
                {
                    transformedImage.Frames.Add(tranformedFrame);
                }
                else
                {
                    transformedImage.Frames[i] = tranformedFrame;
                }
            }

            source = transformedImage;
            return source;
        }
    }
}
