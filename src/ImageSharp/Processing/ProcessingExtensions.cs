// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the processing of images to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class ProcessingExtensions
    {
        /// <summary>
        /// Applies the given operation to the mutable image.
        /// Useful when we need to extract information like Width/Height to parametrize the next operation working on the <see cref="IImageProcessingContext{TPixel}"/> chain.
        /// To achieve this the method actually implements an "inline" <see cref="IImageProcessor{TPixel}"/> with <paramref name="operation"/> as it's processing logic.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to mutate.</param>
        /// <param name="operation">The operation to perform on the source.</param>
        /// <returns>The <see cref="IImageProcessingContext{TPixel}"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext<TPixel> Apply<TPixel>(this IImageProcessingContext<TPixel> source, Action<Image<TPixel>> operation)
            where TPixel : struct, IPixel<TPixel> => source.ApplyProcessor(new DelegateProcessor<TPixel>(operation));

        /// <summary>
        /// Mutates the source image by applying the image operation to it.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to mutate.</param>
        /// <param name="operation">The operation to perform on the source.</param>
        public static void Mutate<TPixel>(this Image<TPixel> source, Action<IImageProcessingContext<TPixel>> operation)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(operation, nameof(operation));
            Guard.NotNull(source, nameof(source));

            IInternalImageProcessingContext<TPixel> operationsRunner = source.GetConfiguration().ImageOperationsProvider.CreateImageProcessingContext(source, true);
            operation(operationsRunner);
            operationsRunner.Apply();
        }

        /// <summary>
        /// Mutates the source image by applying the operations to it.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to mutate.</param>
        /// <param name="operations">The operations to perform on the source.</param>
        public static void Mutate<TPixel>(this Image<TPixel> source, params IImageProcessor<TPixel>[] operations)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(operations, nameof(operations));
            Guard.NotNull(source, nameof(source));

            IInternalImageProcessingContext<TPixel> operationsRunner = source.GetConfiguration().ImageOperationsProvider.CreateImageProcessingContext(source, true);
            operationsRunner.ApplyProcessors(operations);
            operationsRunner.Apply();
        }

        /// <summary>
        /// Creates a deep clone of the current image. The clone is then mutated by the given operation.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to clone.</param>
        /// <param name="operation">The operation to perform on the clone.</param>
        /// <returns>The new <see cref="SixLabors.ImageSharp.Image{TPixel}"/></returns>
        public static Image<TPixel> Clone<TPixel>(this Image<TPixel> source, Action<IImageProcessingContext<TPixel>> operation)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(operation, nameof(operation));
            Guard.NotNull(source, nameof(source));

            IInternalImageProcessingContext<TPixel> operationsRunner = source.GetConfiguration().ImageOperationsProvider.CreateImageProcessingContext(source, false);
            operation(operationsRunner);
            return operationsRunner.Apply();
        }

        /// <summary>
        /// Creates a deep clone of the current image. The clone is then mutated by the given operations.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to clone.</param>
        /// <param name="operations">The operations to perform on the clone.</param>
        /// <returns>The new <see cref="SixLabors.ImageSharp.Image{TPixel}"/></returns>
        public static Image<TPixel> Clone<TPixel>(this Image<TPixel> source, params IImageProcessor<TPixel>[] operations)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(operations, nameof(operations));
            Guard.NotNull(source, nameof(source));

            IInternalImageProcessingContext<TPixel> operationsRunner = source.GetConfiguration().ImageOperationsProvider.CreateImageProcessingContext(source, false);
            operationsRunner.ApplyProcessors(operations);
            return operationsRunner.Apply();
        }

        /// <summary>
        /// Applies the given <see cref="IImageProcessor{TPixel}"/> collection against the context
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image processing context.</param>
        /// <param name="operations">The operations to perform on the source.</param>
        /// <returns>The <see cref="IImageProcessor{TPixel}"/> to allow chaining of operations.</returns>
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