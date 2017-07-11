// <copyright file="RotateFlip.cs" company="James Jackson-South">
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
        /// Mutates the image by applying the operations to it.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="operations">The operations to perform on the source.</param>
        public static void Mutate<TPixel>(this Image<TPixel> source, Action<IImageOperations<TPixel>> operations)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(operations, nameof(operations));

            // TODO: add parameter to Configuration to configure how this is created, create an IImageOperationsFactory that cna be used to switch this out with a fake for testing
            IImageOperations<TPixel> operationsRunner = source.Configuration.ImageOperationsProvider.CreateMutator(source);
            operations(operationsRunner);
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

            // TODO: add parameter to Configuration to configure how this is created, create an IImageOperationsFactory that cna be used to switch this out with a fake for testing
            IImageOperations<TPixel> operationsRunner = source.Configuration.ImageOperationsProvider.CreateMutator(source);
            operationsRunner.ApplyProcessors(operations);
        }

        /// <summary>
        /// Clones the current image mutating the clone by applying the operations to it.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="operations">The operations to perform on the source.</param>
        /// <returns>Anew Image which has teh data from the <paramref name="source"/> but with the <paramref name="operations"/> applied.</returns>
        public static Image<TPixel> Clone<TPixel>(this Image<TPixel> source, Action<IImageOperations<TPixel>> operations)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(operations, nameof(operations));
            var generated = new Image<TPixel>(source);

            // TODO: add parameter to Configuration to configure how this is created, create an IImageOperationsFactory that cna be used to switch this out with a fake for testing
            IImageOperations<TPixel> operationsRunner = source.Configuration.ImageOperationsProvider.CreateMutator(generated);
            operations(operationsRunner);
            return generated;
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
            var generated = new Image<TPixel>(source);

            // TODO: add parameter to Configuration to configure how this is created, create an IImageOperationsFactory that cna be used to switch this out with a fake for testing
            IImageOperations<TPixel> operationsRunner = source.Configuration.ImageOperationsProvider.CreateMutator(generated);
            operationsRunner.ApplyProcessors(operations);
            return generated;
        }

        /// <summary>
        /// Queues up a simple operation that provides access to the mutatable image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="operation">The operations to perform on the source.</param>
        /// <returns>returns the current optinoatins class to allow chaining of oprations.</returns>
        public static IImageOperations<TPixel> Run<TPixel>(this IImageOperations<TPixel> source, Action<Image<TPixel>> operation)
                where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new DelegateImageProcessor<TPixel>(operation));

        /// <summary>
        /// Queues up a simple operation that provides access to the mutatable image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="operations">The operations to perform on the source.</param>
        /// <returns>returns the current optinoatins class to allow chaining of oprations.</returns>
        internal static IImageOperations<TPixel> ApplyProcessors<TPixel>(this IImageOperations<TPixel> source, params IImageProcessor<TPixel>[] operations)
                where TPixel : struct, IPixel<TPixel>
        {
            foreach (IImageProcessor<TPixel> op in operations)
            {
                source = source.ApplyProcessor(op);
            }

            return source;
        }
    }
}
