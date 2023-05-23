// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;

namespace SixLabors.ImageSharp.Processing;

/// <summary>
/// Adds extensions that allow the processing of images to the <see cref="Image{TPixel}"/> type.
/// </summary>
public static partial class ProcessingExtensions
{
    /// <summary>
    /// Mutates the source image by applying the image operation to it.
    /// </summary>
    /// <param name="source">The image to mutate.</param>
    /// <param name="operation">The operation to perform on the source.</param>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operation is null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static Task MutateAsync(this Image source, Func<IImageProcessingContext, Task> operation)
        => MutateAsync(source, source.GetConfiguration(), operation);

    /// <summary>
    /// Mutates the source image by applying the image operation to it.
    /// </summary>
    /// <param name="source">The image to mutate.</param>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="operation">The operation to perform on the source.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operation is null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static Task MutateAsync(this Image source, Configuration configuration, Func<IImageProcessingContext, Task> operation)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(source, nameof(source));
        Guard.NotNull(operation, nameof(operation));
        source.EnsureNotDisposed();

        return source.AcceptVisitorAsync(new AsyncProcessingVisitor(configuration, operation, true));
    }

    /// <summary>
    /// Mutates the source image by applying the image operation to it.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="source">The image to mutate.</param>
    /// <param name="operation">The operation to perform on the source.</param>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operation is null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static Task MutateAsync<TPixel>(this Image<TPixel> source, Func<IImageProcessingContext, Task> operation)
        where TPixel : unmanaged, IPixel<TPixel>
        => MutateAsync(source, source.GetConfiguration(), operation);

    /// <summary>
    /// Mutates the source image by applying the image operation to it.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="source">The image to mutate.</param>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="operation">The operation to perform on the source.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operation is null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static Task MutateAsync<TPixel>(this Image<TPixel> source, Configuration configuration, Func<IImageProcessingContext, Task> operation)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(source, nameof(source));
        Guard.NotNull(operation, nameof(operation));
        source.EnsureNotDisposed();

        IInternalImageProcessingContext<TPixel> operationsRunner
            = configuration.ImageOperationsProvider.CreateImageProcessingContext(configuration, source, true);

        return operation(operationsRunner);
    }

    /// <summary>
    /// Mutates the source image by applying the image operation to it.
    /// </summary>
    /// <param name="source">The image to mutate.</param>
    /// <param name="operation">The operation to perform on the source.</param>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operation is null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    public static void Mutate(this Image source, Action<IImageProcessingContext> operation)
        => Mutate(source, source.GetConfiguration(), operation);

    /// <summary>
    /// Mutates the source image by applying the image operation to it.
    /// </summary>
    /// <param name="source">The image to mutate.</param>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="operation">The operation to perform on the source.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operation is null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    public static void Mutate(this Image source, Configuration configuration, Action<IImageProcessingContext> operation)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(source, nameof(source));
        Guard.NotNull(operation, nameof(operation));
        source.EnsureNotDisposed();

        source.AcceptVisitor(new ProcessingVisitor(configuration, operation, true));
    }

    /// <summary>
    /// Mutates the source image by applying the image operation to it.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="source">The image to mutate.</param>
    /// <param name="operation">The operation to perform on the source.</param>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operation is null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    public static void Mutate<TPixel>(this Image<TPixel> source, Action<IImageProcessingContext> operation)
        where TPixel : unmanaged, IPixel<TPixel>
        => Mutate(source, source.GetConfiguration(), operation);

    /// <summary>
    /// Mutates the source image by applying the image operation to it.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="source">The image to mutate.</param>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="operation">The operation to perform on the source.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operation is null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    public static void Mutate<TPixel>(this Image<TPixel> source, Configuration configuration, Action<IImageProcessingContext> operation)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(source, nameof(source));
        Guard.NotNull(operation, nameof(operation));
        source.EnsureNotDisposed();

        IInternalImageProcessingContext<TPixel> operationsRunner
            = configuration.ImageOperationsProvider.CreateImageProcessingContext(configuration, source, true);

        operation(operationsRunner);
    }

    /// <summary>
    /// Mutates the source image by applying the operations to it.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="source">The image to mutate.</param>
    /// <param name="operations">The operations to perform on the source.</param>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operations are null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    public static void Mutate<TPixel>(this Image<TPixel> source, params IImageProcessor[] operations)
        where TPixel : unmanaged, IPixel<TPixel>
        => Mutate(source, source.GetConfiguration(), operations);

    /// <summary>
    /// Mutates the source image by applying the operations to it.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="source">The image to mutate.</param>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="operations">The operations to perform on the source.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operations are null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    public static void Mutate<TPixel>(this Image<TPixel> source, Configuration configuration, params IImageProcessor[] operations)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(source, nameof(source));
        Guard.NotNull(operations, nameof(operations));
        source.EnsureNotDisposed();

        IInternalImageProcessingContext<TPixel> operationsRunner
            = configuration.ImageOperationsProvider.CreateImageProcessingContext(configuration, source, true);

        operationsRunner.ApplyProcessors(operations);
    }

    /// <summary>
    /// Creates a deep clone of the current image. The clone is then mutated by the given operation.
    /// </summary>
    /// <param name="source">The image to clone.</param>
    /// <param name="operation">The operation to perform on the clone.</param>
    /// <returns>The new <see cref="Image"/>.</returns>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operation is null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    public static Image Clone(this Image source, Action<IImageProcessingContext> operation)
        => Clone(source, source.GetConfiguration(), operation);

    /// <summary>
    /// Creates a deep clone of the current image. The clone is then mutated by the given operation.
    /// </summary>
    /// <param name="source">The image to clone.</param>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="operation">The operation to perform on the clone.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operation is null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    /// <returns>The new <see cref="Image"/>.</returns>
    public static Image Clone(this Image source, Configuration configuration, Action<IImageProcessingContext> operation)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(source, nameof(source));
        Guard.NotNull(operation, nameof(operation));
        source.EnsureNotDisposed();

        ProcessingVisitor visitor = new(configuration, operation, false);
        source.AcceptVisitor(visitor);
        return visitor.GetResultImage();
    }

    /// <summary>
    /// Creates a deep clone of the current image. The clone is then mutated by the given operation.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="source">The image to clone.</param>
    /// <param name="operation">The operation to perform on the clone.</param>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operation is null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    /// <returns>The new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> Clone<TPixel>(this Image<TPixel> source, Action<IImageProcessingContext> operation)
        where TPixel : unmanaged, IPixel<TPixel>
        => Clone(source, source.GetConfiguration(), operation);

    /// <summary>
    /// Creates a deep clone of the current image. The clone is then mutated by the given operation.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="source">The image to clone.</param>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="operation">The operation to perform on the clone.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operation is null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    /// <returns>The new <see cref="Image{TPixel}"/></returns>
    public static Image<TPixel> Clone<TPixel>(this Image<TPixel> source, Configuration configuration, Action<IImageProcessingContext> operation)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(source, nameof(source));
        Guard.NotNull(operation, nameof(operation));
        source.EnsureNotDisposed();

        IInternalImageProcessingContext<TPixel> operationsRunner
            = configuration.ImageOperationsProvider.CreateImageProcessingContext(configuration, source, false);

        operation(operationsRunner);
        return operationsRunner.GetResultImage();
    }

    /// <summary>
    /// Creates a deep clone of the current image. The clone is then mutated by the given operations.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="source">The image to clone.</param>
    /// <param name="operations">The operations to perform on the clone.</param>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operations are null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    /// <returns>The new <see cref="Image{TPixel}"/></returns>
    public static Image<TPixel> Clone<TPixel>(this Image<TPixel> source, params IImageProcessor[] operations)
        where TPixel : unmanaged, IPixel<TPixel>
        => Clone(source, source.GetConfiguration(), operations);

    /// <summary>
    /// Creates a deep clone of the current image. The clone is then mutated by the given operations.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="source">The image to clone.</param>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="operations">The operations to perform on the clone.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentNullException">The operations are null.</exception>
    /// <exception cref="ObjectDisposedException">The source has been disposed.</exception>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
    /// <returns>The new <see cref="Image{TPixel}"/></returns>
    public static Image<TPixel> Clone<TPixel>(this Image<TPixel> source, Configuration configuration, params IImageProcessor[] operations)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(source, nameof(source));
        Guard.NotNull(operations, nameof(operations));
        source.EnsureNotDisposed();

        IInternalImageProcessingContext<TPixel> operationsRunner
            = configuration.ImageOperationsProvider.CreateImageProcessingContext(configuration, source, false);

        operationsRunner.ApplyProcessors(operations);
        return operationsRunner.GetResultImage();
    }

    /// <summary>
    /// Applies the given <see cref="IImageProcessor{TPixel}"/> collection against the context
    /// </summary>
    /// <param name="source">The image processing context.</param>
    /// <param name="operations">The operations to perform on the source.</param>
    /// <exception cref="ImageProcessingException">The processing operation failed.</exception>
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

    private class AsyncProcessingVisitor : IImageVisitorAsync
    {
        private readonly Configuration configuration;

        private readonly Func<IImageProcessingContext, Task> operation;

        private readonly bool mutate;

        private Image? resultImage;

        public AsyncProcessingVisitor(Configuration configuration, Func<IImageProcessingContext, Task> operation, bool mutate)
        {
            this.configuration = configuration;
            this.operation = operation;
            this.mutate = mutate;
        }

        public Image GetResultImage() => this.resultImage!;

        public async Task VisitAsync<TPixel>(Image<TPixel> image, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            IInternalImageProcessingContext<TPixel> operationsRunner =
                this.configuration.ImageOperationsProvider.CreateImageProcessingContext(this.configuration, image, this.mutate);

            await this.operation(operationsRunner).ConfigureAwait(false);
            this.resultImage = operationsRunner.GetResultImage();
        }
    }

    private class ProcessingVisitor : IImageVisitor
    {
        private readonly Configuration configuration;

        private readonly Action<IImageProcessingContext> operation;

        private readonly bool mutate;

        private Image? resultImage;

        public ProcessingVisitor(Configuration configuration, Action<IImageProcessingContext> operation, bool mutate)
        {
            this.configuration = configuration;
            this.operation = operation;
            this.mutate = mutate;
        }

        public Image GetResultImage() => this.resultImage!;

        public void Visit<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            IInternalImageProcessingContext<TPixel> operationsRunner =
                this.configuration.ImageOperationsProvider.CreateImageProcessingContext(this.configuration, image, this.mutate);

            this.operation(operationsRunner);
            this.resultImage = operationsRunner.GetResultImage();
        }
    }
}
