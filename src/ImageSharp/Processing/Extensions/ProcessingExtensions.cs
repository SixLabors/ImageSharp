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
        /// Mutates the source image by applying the image operation to it.
        /// </summary>
        /// <param name="source">The image to mutate.</param>
        /// <param name="operation">The operation to perform on the source.</param>
        public static void Mutate(this Image source, Action<IImageProcessingContext> operation)
        {
            Guard.NotNull(source, nameof(source));
            Guard.NotNull(operation, nameof(operation));
            source.EnsureNotDisposed();

            var visitor = new ProcessingVisitor(operation, true);
            source.AcceptVisitor(visitor);
        }

        /// <summary>
        /// Mutates the source image by applying the image operation to it.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to mutate.</param>
        /// <param name="operation">The operation to perform on the source.</param>
        public static void Mutate<TPixel>(this Image<TPixel> source, Action<IImageProcessingContext> operation)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(source, nameof(source));
            Guard.NotNull(operation, nameof(operation));
            source.EnsureNotDisposed();

            IInternalImageProcessingContext<TPixel> operationsRunner = source.GetConfiguration().ImageOperationsProvider
                .CreateImageProcessingContext(source, true);
            operation(operationsRunner);
        }

        /// <summary>
        /// Mutates the source image by applying the operations to it.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to mutate.</param>
        /// <param name="operations">The operations to perform on the source.</param>
        public static void Mutate<TPixel>(this Image<TPixel> source, params IImageProcessor[] operations)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(source, nameof(source));
            Guard.NotNull(operations, nameof(operations));
            source.EnsureNotDisposed();

            IInternalImageProcessingContext<TPixel> operationsRunner = source.GetConfiguration().ImageOperationsProvider
                .CreateImageProcessingContext(source, true);
            operationsRunner.ApplyProcessors(operations);
        }

        /// <summary>
        /// Creates a deep clone of the current image. The clone is then mutated by the given operation.
        /// </summary>
        /// <param name="source">The image to clone.</param>
        /// <param name="operation">The operation to perform on the clone.</param>
        /// <returns>The new <see cref="SixLabors.ImageSharp.Image"/>.</returns>
        public static Image Clone(this Image source, Action<IImageProcessingContext> operation)
        {
            Guard.NotNull(source, nameof(source));
            Guard.NotNull(operation, nameof(operation));
            source.EnsureNotDisposed();

            var visitor = new ProcessingVisitor(operation, false);
            source.AcceptVisitor(visitor);
            return visitor.ResultImage;
        }

        /// <summary>
        /// Creates a deep clone of the current image. The clone is then mutated by the given operation.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to clone.</param>
        /// <param name="operation">The operation to perform on the clone.</param>
        /// <returns>The new <see cref="SixLabors.ImageSharp.Image{TPixel}"/></returns>
        public static Image<TPixel> Clone<TPixel>(this Image<TPixel> source, Action<IImageProcessingContext> operation)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(source, nameof(source));
            Guard.NotNull(operation, nameof(operation));
            source.EnsureNotDisposed();

            IInternalImageProcessingContext<TPixel> operationsRunner = source.GetConfiguration().ImageOperationsProvider
                .CreateImageProcessingContext(source, false);
            operation(operationsRunner);
            return operationsRunner.GetResultImage();
        }

        /// <summary>
        /// Creates a deep clone of the current image. The clone is then mutated by the given operations.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to clone.</param>
        /// <param name="operations">The operations to perform on the clone.</param>
        /// <returns>The new <see cref="SixLabors.ImageSharp.Image{TPixel}"/></returns>
        public static Image<TPixel> Clone<TPixel>(this Image<TPixel> source, params IImageProcessor[] operations)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(source, nameof(source));
            Guard.NotNull(operations, nameof(operations));
            source.EnsureNotDisposed();

            IInternalImageProcessingContext<TPixel> operationsRunner = source.GetConfiguration().ImageOperationsProvider
                .CreateImageProcessingContext(source, false);
            operationsRunner.ApplyProcessors(operations);
            return operationsRunner.GetResultImage();
        }

        /// <summary>
        /// Applies the given <see cref="IImageProcessor{TPixel}"/> collection against the context
        /// </summary>
        /// <param name="source">The image processing context.</param>
        /// <param name="operations">The operations to perform on the source.</param>
        /// <returns>The <see cref="IImageProcessor{TPixel}"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext ApplyProcessors(
            this IImageProcessingContext source,
            params IImageProcessor[] operations)
        {
            foreach (IImageProcessor p in operations)
            {
                source = source.ApplyProcessor(p);
            }

            return source;
        }

        private class ProcessingVisitor : IImageVisitor
        {
            private readonly Action<IImageProcessingContext> operation;

            private readonly bool mutate;

            public ProcessingVisitor(Action<IImageProcessingContext> operation, bool mutate)
            {
                this.operation = operation;
                this.mutate = mutate;
            }

            public Image ResultImage { get; private set; }

            public void Visit<TPixel>(Image<TPixel> image)
                where TPixel : struct, IPixel<TPixel>
            {
                IInternalImageProcessingContext<TPixel> operationsRunner = image.GetConfiguration()
                    .ImageOperationsProvider.CreateImageProcessingContext(image, this.mutate);
                this.operation(operationsRunner);
                this.ResultImage = operationsRunner.GetResultImage();
            }
        }
    }
}
