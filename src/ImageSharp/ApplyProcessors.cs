// <copyright file="ApplyProcessors.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using ImageSharp.PixelFormats;

    using ImageSharp.Processing;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Mutates the image by applying the image operation to it.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="operation">The operations to perform on the source.</param>
        public static void Mutate<TPixel>(this Image<TPixel> source, Action<IImageProcessingContext<TPixel>> operation)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(operation, nameof(operation));
            Guard.NotNull(source, nameof(source));

            IInternalImageProcessingContext<TPixel> operationsRunner = source.Configuration.ImageOperationsProvider.CreateImageProcessingContext(source, true);
            operation(operationsRunner);
            operationsRunner.Apply();
        }

        /// <summary>
        /// Mutates the image by applying the operations to it.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="operations">The operations to perform on the source.</param>
        public static void Mutate<TPixel>(this Image<TPixel> source, params IImageProcessor<TPixel>[] operations)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(operations, nameof(operations));
            Guard.NotNull(source, nameof(source));

            IInternalImageProcessingContext<TPixel> operationsRunner = source.Configuration.ImageOperationsProvider.CreateImageProcessingContext(source, true);
            operationsRunner.ApplyProcessors(operations);
            operationsRunner.Apply();
        }

        /// <summary>
        /// Clones the current image mutating the clone by applying the operation to it.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="operation">The operations to perform on the source.</param>
        /// <returns>Anew Image which has teh data from the <paramref name="source"/> but with the <paramref name="operation"/> applied.</returns>
        public static Image<TPixel> Clone<TPixel>(this Image<TPixel> source, Action<IImageProcessingContext<TPixel>> operation)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(operation, nameof(operation));
            Guard.NotNull(source, nameof(source));

            IInternalImageProcessingContext<TPixel> operationsRunner = source.Configuration.ImageOperationsProvider.CreateImageProcessingContext(source, false);
            operation(operationsRunner);
            return operationsRunner.Apply();
        }

        /// <summary>
        /// Clones the current image mutating the clone by applying the operations to it.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="operations">The operations to perform on the source.</param>
        /// <returns>Anew Image which has teh data from the <paramref name="source"/> but with the <paramref name="operations"/> applied.</returns>
        public static Image<TPixel> Clone<TPixel>(this Image<TPixel> source, params IImageProcessor<TPixel>[] operations)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(operations, nameof(operations));
            Guard.NotNull(source, nameof(source));

            IInternalImageProcessingContext<TPixel> operationsRunner = source.Configuration.ImageOperationsProvider.CreateImageProcessingContext(source, false);
            operationsRunner.ApplyProcessors(operations);
            return operationsRunner.Apply();
        }

        /// <summary>
        /// Applies all the ImageProcessors agains the operation
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="operations">The operations to perform on the source.</param>
        /// <returns>returns the current operations class to allow chaining of operations.</returns>
        public static IImageProcessingContext<TPixel> ApplyProcessors<TPixel>(this IImageProcessingContext<TPixel> source, params IImageProcessor<TPixel>[] operations)
                where TPixel : struct, IPixel<TPixel>
        {
            foreach (IImageProcessor<TPixel> p in operations)
            {
                source = source.ApplyProcessor(p);
            }

            return source;
        }
    }
}