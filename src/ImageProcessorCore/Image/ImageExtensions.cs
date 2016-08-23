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
    /// Extension methods for the <see cref="Image{TColor, TPacked}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Saves the image to the given stream with the bmp format.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam> 
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsBmp<TColor, TPacked>(this Image<TColor, TPacked> source, Stream stream)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
            => new BmpEncoder().Encode(source, stream);


        /// <summary>
        /// Saves the image to the given stream with the png format.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam> 
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="quality">The quality to save the image to representing the number of colors. 
        /// Anything equal to 256 and below will cause the encoder to save the image in an indexed format.
        /// </param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsPng<TColor, TPacked>(this Image<TColor, TPacked> source, Stream stream, int quality = int.MaxValue)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
            => new PngEncoder { Quality = quality }.Encode(source, stream);

        /// <summary>
        /// Saves the image to the given stream with the jpeg format.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam> 
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="quality">The quality to save the image to. Between 1 and 100.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        public static void SaveAsJpeg<TColor, TPacked>(this Image<TColor, TPacked> source, Stream stream, int quality = 75)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
            => new JpegEncoder { Quality = quality }.Encode(source, stream);

        /// <summary>
        /// Saves the image to the given stream with the gif format.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam> 
        /// <param name="source">The image this method extends.</param>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="quality">The quality to save the image to representing the number of colors. Between 1 and 256.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream is null.</exception>
        internal static void SaveAsGif<TColor, TPacked>(this Image<TColor, TPacked> source, Stream stream, int quality = 256)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
            => new GifEncoder { Quality = quality }.Encode(source, stream);

        /// <summary>
        /// Applies the collection of processors to the image.
        /// <remarks>This method does not resize the target image.</remarks>
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="processor">The processor to apply to the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        internal static Image<TColor, TPacked> Process<TColor, TPacked>(this Image<TColor, TPacked> source, IImageProcessor<TColor, TPacked> processor)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return Process(source, source.Bounds, processor);
        }

        /// <summary>
        /// Applies the collection of processors to the image.
        /// <remarks>This method does not resize the target image.</remarks>
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="processor">The processors to apply to the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        internal static Image<TColor, TPacked> Process<TColor, TPacked>(this Image<TColor, TPacked> source, Rectangle sourceRectangle, IImageProcessor<TColor, TPacked> processor)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return PerformAction(source, true, (sourceImage, targetImage) => processor.Apply(targetImage, sourceImage, sourceRectangle));
        }

        /// <summary>
        /// Applies the collection of processors to the image.
        /// <remarks>
        /// This method is not chainable.
        /// </remarks>
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The processor to apply to the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        internal static Image<TColor, TPacked> Process<TColor, TPacked>(this Image<TColor, TPacked> source, int width, int height, IImageSampler<TColor, TPacked> sampler)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return Process(source, width, height, source.Bounds, default(Rectangle), sampler);
        }

        /// <summary>
        /// Applies the collection of processors to the image.
        /// <remarks>
        /// This method does will resize the target image if the source and target rectangles are different.
        /// </remarks>
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
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
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        internal static Image<TColor, TPacked> Process<TColor, TPacked>(this Image<TColor, TPacked> source, int width, int height, Rectangle sourceRectangle, Rectangle targetRectangle, IImageSampler<TColor, TPacked> sampler)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return PerformAction(source, false, (sourceImage, targetImage) => sampler.Apply(targetImage, sourceImage, width, height, targetRectangle, sourceRectangle));
        }

        /// <summary>
        /// Performs the given action on the source image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to perform the action against.</param>
        /// <param name="clone">Whether to clone the image.</param>
        /// <param name="action">The <see cref="Action"/> to perform against the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        private static Image<TColor, TPacked> PerformAction<TColor, TPacked>(Image<TColor, TPacked> source, bool clone, Action<ImageBase<TColor, TPacked>, ImageBase<TColor, TPacked>> action)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            Image<TColor, TPacked> transformedImage = clone
                ? new Image<TColor, TPacked>(source)
                : new Image<TColor, TPacked>();

            // Several properties still require copying
            if (!clone)
                transformedImage.CopyProperties(source);

            action(source, transformedImage);

            for (int i = 0; i < source.Frames.Count; i++)
            {
                ImageFrame<TColor, TPacked> sourceFrame = source.Frames[i];
                ImageFrame<TColor, TPacked> tranformedFrame = clone
                    ? new ImageFrame<TColor, TPacked>(sourceFrame)
                    : new ImageFrame<TColor, TPacked> { FrameDelay = sourceFrame.FrameDelay };

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
