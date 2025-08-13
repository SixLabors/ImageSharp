// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors;

/// <summary>
/// The base class for all pixel specific image processors.
/// Allows the application of processing algorithms to the image.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
public abstract class ImageProcessor<TPixel> : IImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    protected ImageProcessor(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
    {
        this.Configuration = configuration;
        this.Source = source;
        this.SourceRectangle = sourceRectangle;
    }

    /// <summary>
    /// Gets The source <see cref="Image{TPixel}"/> for the current processor instance.
    /// </summary>
    protected Image<TPixel> Source { get; }

    /// <summary>
    /// Gets The source area to process for the current processor instance.
    /// </summary>
    protected Rectangle SourceRectangle { get; }

    /// <summary>
    /// Gets the <see cref="ImageSharp.Configuration"/> instance to use when performing operations.
    /// </summary>
    protected Configuration Configuration { get; }

    /// <inheritdoc/>
    void IImageProcessor<TPixel>.Execute()
    {
        this.BeforeImageApply();

        foreach (ImageFrame<TPixel> sourceFrame in this.Source.Frames)
        {
            this.Apply(sourceFrame);
        }

        this.AfterImageApply();
    }

    /// <summary>
    /// Applies the processor to a single image frame.
    /// </summary>
    /// <param name="source">the source image.</param>
    public void Apply(ImageFrame<TPixel> source)
    {
        this.BeforeFrameApply(source);
        this.OnFrameApply(source);
        this.AfterFrameApply(source);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// This method is called before the process is applied to prepare the processor.
    /// </summary>
    protected virtual void BeforeImageApply()
    {
    }

    /// <summary>
    /// This method is called before the process is applied to prepare the processor.
    /// </summary>
    /// <param name="source">The source image. Cannot be null.</param>
    protected virtual void BeforeFrameApply(ImageFrame<TPixel> source)
    {
    }

    /// <summary>
    /// Applies the process to the specified portion of the specified <see cref="ImageFrame{TPixel}" /> at the specified location
    /// and with the specified size.
    /// </summary>
    /// <param name="source">The source image. Cannot be null.</param>
    protected abstract void OnFrameApply(ImageFrame<TPixel> source);

    /// <summary>
    /// This method is called after the process is applied to each frame.
    /// </summary>
    /// <param name="source">The source image. Cannot be null.</param>
    protected virtual void AfterFrameApply(ImageFrame<TPixel> source)
        => source.Metadata.AfterFrameApply(source, source, Matrix4x4.Identity);

    /// <summary>
    /// This method is called after the process is applied to the complete image.
    /// </summary>
    protected virtual void AfterImageApply()
        => this.Source.Metadata.AfterImageApply(this.Source, Matrix4x4.Identity);

    /// <summary>
    /// Disposes the object and frees resources for the Garbage Collector.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed and unmanaged objects.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
