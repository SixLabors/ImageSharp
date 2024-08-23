// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution;

/// <summary>
/// Defines a processor that detects edges within an image using a eight two dimensional matrices.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal class EdgeDetectorCompassProcessor<TPixel> : ImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly DenseMatrix<float>[] kernels;
    private readonly bool grayscale;

    /// <summary>
    /// Initializes a new instance of the <see cref="EdgeDetectorCompassProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="definition">The <see cref="EdgeDetectorCompassProcessor"/> defining the processor parameters.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    internal EdgeDetectorCompassProcessor(
        Configuration configuration,
        EdgeDetectorCompassProcessor definition,
        Image<TPixel> source,
        Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
    {
        this.grayscale = definition.Grayscale;
        this.kernels = definition.Kernel.Flatten();
    }

    /// <inheritdoc/>
    protected override void BeforeImageApply()
    {
        using (IImageProcessor<TPixel> opaque = new OpaqueProcessor<TPixel>(this.Configuration, this.Source, this.SourceRectangle))
        {
            opaque.Execute();
        }

        if (this.grayscale)
        {
            new GrayscaleBt709Processor(1F).Execute(this.Configuration, this.Source, this.SourceRectangle);
        }

        base.BeforeImageApply();
    }

    /// <inheritdoc />
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        Rectangle interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

        // We need a clean copy for each pass to start from
        using ImageFrame<TPixel> cleanCopy = source.Clone();

        using (ConvolutionProcessor<TPixel> processor = new(this.Configuration, in this.kernels[0], true, this.Source, interest))
        {
            processor.Apply(source);
        }

        if (this.kernels.Length == 1)
        {
            return;
        }

        // Additional runs
        for (int i = 1; i < this.kernels.Length; i++)
        {
            using ImageFrame<TPixel> pass = cleanCopy.Clone();

            using (ConvolutionProcessor<TPixel> processor = new(this.Configuration, in this.kernels[i], true, this.Source, interest))
            {
                processor.Apply(pass);
            }

            RowOperation operation = new(source.PixelBuffer, pass.PixelBuffer, interest);
            ParallelRowIterator.IterateRows(
                this.Configuration,
                interest,
                in operation);
        }
    }

    /// <summary>
    /// A <see langword="struct"/> implementing the convolution logic for <see cref="EdgeDetectorCompassProcessor{T}"/>.
    /// </summary>
    private readonly struct RowOperation : IRowOperation
    {
        private readonly Buffer2D<TPixel> targetPixels;
        private readonly Buffer2D<TPixel> passPixels;
        private readonly uint minX;
        private readonly uint maxX;

        [MethodImpl(InliningOptions.ShortMethod)]
        public RowOperation(
            Buffer2D<TPixel> targetPixels,
            Buffer2D<TPixel> passPixels,
            Rectangle bounds)
        {
            this.targetPixels = targetPixels;
            this.passPixels = passPixels;
            this.minX = (uint)bounds.X;
            this.maxX = (uint)bounds.Right;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int y)
        {
            ref TPixel passPixelsBase = ref MemoryMarshal.GetReference(this.passPixels.DangerousGetRowSpan(y));
            ref TPixel targetPixelsBase = ref MemoryMarshal.GetReference(this.targetPixels.DangerousGetRowSpan(y));

            for (nuint x = this.minX; x < this.maxX; x++)
            {
                // Grab the max components of the two pixels
                ref TPixel currentPassPixel = ref Unsafe.Add(ref passPixelsBase, x);
                ref TPixel currentTargetPixel = ref Unsafe.Add(ref targetPixelsBase, x);
                currentTargetPixel = TPixel.FromVector4(Vector4.Max(currentPassPixel.ToVector4(), currentTargetPixel.ToVector4()));
            }
        }
    }
}
