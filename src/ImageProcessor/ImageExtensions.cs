// <copyright file="ImageFilterExtensions.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;
    using System.IO;

    using Formats;

    /// <summary>
    /// Exstension methods for the <see cref="Image"/> type.
    /// </summary>
    public static class ImageExtensions
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
        /// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsPng(this ImageBase source, Stream stream) => new PngEncoder().Encode(source, stream);

        /// <summary>
        /// Saves the image to the given stream with the jpeg format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="quality">The quality to save the image to. Between 1 and 100.</param>
        /// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsJpeg(this ImageBase source, Stream stream, int quality = 80) => new JpegEncoder { Quality = quality }.Encode(source, stream);

        /// <summary>
        /// Saves the image to the given stream with the gif format.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="quality">The quality to save the image to representing the number of colors. Between 1 and 100.</param>
        /// <exception cref="ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsGif(this ImageBase source, Stream stream, int quality = 256) => new GifEncoder() { Quality = quality }.Encode(source, stream);

        /// <summary>
        /// Applies the collection of processors to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="processors">Any processors to apply to the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Process(this Image source, params IImageProcessor[] processors) => Process(source, source.Bounds, processors);

        /// <summary>
        /// Applies the collection of processors to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The rectangle defining the bounds of the pixels the image filter with adjust.</param>
        /// <param name="processors">Any processors to apply to the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Process(this Image source, Rectangle rectangle, params IImageProcessor[] processors)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (IImageProcessor filter in processors)
            {
                source = PerformAction(source, true, (sourceImage, targetImage) => filter.Apply(targetImage, sourceImage, rectangle));
            }

            return source;
        }

        /// <summary>
        /// Applies the collection of processors to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The rectangle defining the bounds of the pixels the image filter with adjust.</param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="sourceRectangle"></param>
        /// <param name="targetRectangle"></param>
        /// <param name="processors">Any processors to apply to the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Process(this Image source, int width, int height, Rectangle sourceRectangle, Rectangle targetRectangle, params IImageProcessor[] processors)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (IImageProcessor filter in processors)
            {
                source = PerformAction(source, false, (sourceImage, targetImage) => filter.Apply(targetImage, sourceImage, width, height, targetRectangle, sourceRectangle));
            }

            return source;
        }

        /// <summary>
        /// Performs the given action on the source image.
        /// </summary>
        /// <param name="source">The image to perform the action against.</param>
        /// <param name="clone">Whether to clone the image.</param>
        /// <param name="action">The <see cref="Action"/> to perform against the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        private static Image PerformAction(Image source, bool clone, Action<ImageBase, ImageBase> action)
        {
            Image transformedImage = clone ? new Image(source) : new Image();

            // Only on clone?
            transformedImage.CurrentImageFormat = source.CurrentImageFormat;
            transformedImage.RepeatCount = source.RepeatCount;

            action(source, transformedImage);

            for (int i = 0; i < source.Frames.Count; i++)
            {
                ImageFrame sourceFrame = source.Frames[i];
                ImageFrame tranformedFrame = clone ? new ImageFrame(sourceFrame) : new ImageFrame();
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

            return transformedImage;
        }
    }
}
