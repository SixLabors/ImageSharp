// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Convolution.Parameters;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution;

/// <summary>
/// Applies bokeh blur processing to the image.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
/// <remarks>
/// This processor is based on the code from Mike Pound, see <a href="https://github.com/mikepound/convolve">github.com/mikepound/convolve</a>.
/// </remarks>
internal class BokehBlurProcessor<TPixel> : ImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// The gamma highlight factor to use when applying the effect
    /// </summary>
    private readonly float gamma;

    /// <summary>
    /// The normalized source color range used by the shared gamma compander.
    /// </summary>
    private readonly PixelColorRange colorRange;

    /// <summary>
    /// The source component precision used to select the shared gamma conversion.
    /// </summary>
    private readonly int componentBitDepth;

    /// <summary>
    /// The size of each complex convolution kernel.
    /// </summary>
    private readonly int kernelSize;

    /// <summary>
    /// The kernel parameters to use for the current instance (a: X, b: Y, A: Z, B: W)
    /// </summary>
    private readonly Vector4[] kernelParameters;

    /// <summary>
    /// The kernel components for the current instance
    /// </summary>
    private readonly Complex64[][] kernels;

    /// <summary>
    /// Initializes a new instance of the <see cref="BokehBlurProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="definition">The <see cref="BokehBlurProcessor"/> defining the processor parameters.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    public BokehBlurProcessor(Configuration configuration, BokehBlurProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
    {
        this.gamma = definition.Gamma;
        this.kernelSize = (definition.Radius * 2) + 1;

        PixelTypeInfo pixelInfo = TPixel.GetPixelTypeInfo();
        this.componentBitDepth = pixelInfo.ComponentInfo?.GetMaximumComponentPrecision() ?? 32;
        this.colorRange = pixelInfo.ColorRange;

        // Get the bokeh blur data
        BokehBlurKernelData data = BokehBlurKernelDataProvider.GetBokehBlurKernelData(
            definition.Radius,
            this.kernelSize,
            definition.Components);

        this.kernelParameters = data.Parameters;
        this.kernels = data.Kernels;
    }

    /// <summary>
    /// Gets the complex kernels to use to apply the blur for the current instance
    /// </summary>
    public IReadOnlyList<Complex64[]> Kernels => this.kernels;

    /// <summary>
    /// Gets the kernel parameters used to compute the pixel values from each complex pixel
    /// </summary>
    public IReadOnlyList<Vector4> KernelParameters => this.kernelParameters;

    /// <inheritdoc/>
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        Rectangle sourceRectangle = Rectangle.Intersect(this.SourceRectangle, source.Bounds);

        // Each row holds two image-width planes: [gamma-expanded input | accumulated output].
        // The input stays unchanged until all kernel components finish. The output accumulates their contributions.
        // GetRegion creates non-owning views into colorBuffer.
        // Only the processing rectangle is initialized; the sampling map keeps every convolution read inside it.
        using Buffer2D<Vector4> colorBuffer = this.Configuration.MemoryAllocator.Allocate2D<Vector4>(source.Width * 2, source.Height);
        Buffer2DRegion<Vector4> exposedValues = colorBuffer.GetRegion(0, 0, source.Width, source.Height);
        Buffer2DRegion<Vector4> outputValues = colorBuffer.GetRegion(source.Width, 0, source.Width, source.Height);

        ApplyGammaExposureRowOperation exposure = new(
            sourceRectangle,
            source.PixelBuffer,
            exposedValues,
            outputValues,
            this.Configuration,
            this.gamma,
            this.componentBitDepth,
            this.colorRange);

        ParallelRowIterator.IterateRows(this.Configuration, sourceRectangle, in exposure);
        this.OnFrameApplyCore(exposedValues, sourceRectangle, this.Configuration, outputValues);

        ApplyInverseGammaExposureRowOperation inverse = new(
            sourceRectangle,
            source.PixelBuffer,
            outputValues,
            this.Configuration,
            this.gamma,
            this.componentBitDepth,
            this.colorRange);

        ParallelRowIterator.IterateRows(this.Configuration, sourceRectangle, in inverse);
    }

    /// <summary>
    /// Evaluates each complex kernel as two real separable convolutions and accumulates their contributions.
    /// </summary>
    /// <param name="exposedValues">The associated, gamma-expanded float colors shared by every kernel component.</param>
    /// <param name="sourceRectangle">The processing rectangle in image coordinates.</param>
    /// <param name="configuration">The memory allocation and parallel processing configuration.</param>
    /// <param name="outputValues">The initially zeroed float output plane, separate from the input plane in the same allocation.</param>
    private void OnFrameApplyCore(
        Buffer2DRegion<Vector4> exposedValues,
        Rectangle sourceRectangle,
        Configuration configuration,
        Buffer2DRegion<Vector4> outputValues)
    {
        // Standard two-pass convolution stores its horizontal results in TPixel.
        // Bokeh must retain signed RGB and alpha intermediates until all weighted kernel contributions are accumulated.
        // Keep those values as floats to avoid pixel-format rounding or clamping between passes.

        // This scratch buffer holds either horizontal sum, Ha or Hb. Every pass overwrites its processed pixels.
        // Together with the input and output planes, image storage uses 48 bytes per pixel.
        using Buffer2D<Vector4> horizontalValues = configuration.MemoryAllocator.Allocate2D<Vector4>(exposedValues.Size);

        // Both convolution directions use the same kernel size and processing rectangle.
        // Build their clamped sampling offsets once and share them across all components.
        using KernelSamplingMap mapXY = new(configuration.MemoryAllocator);

        mapXY.BuildSamplingOffsetMap(this.kernelSize, this.kernelSize, sourceRectangle);

        ref Complex64[] baseRef = ref MemoryMarshal.GetReference(this.kernels.AsSpan());
        ref Vector4 paramsRef = ref MemoryMarshal.GetReference(this.kernelParameters.AsSpan());

        for (int i = 0; i < this.kernels.Length; i++)
        {
            Complex64[] kernel = Unsafe.Add(ref baseRef, (uint)i);
            Vector4 parameters = Unsafe.Add(ref paramsRef, (uint)i);

            // Let a and b be the real and imaginary kernel coefficients, and Ha and Hb their horizontal sums.
            // The weighted complex result is z*Re + w*Im:
            // z*(a*Ha - b*Hb) + w*(a*Hb + b*Ha) = (z*a + w*b)*Ha + (w*a - z*b)*Hb.
            // Evaluate the two terms separately so one float image can hold either Ha or Hb.
            for (int kernelOffset = 0; kernelOffset < 2; kernelOffset++)
            {
                BokehBlurProcessor.FirstPassConvolutionRowOperation horizontalOperation = new(
                    sourceRectangle,
                    horizontalValues,
                    exposedValues,
                    mapXY,
                    kernel,
                    kernelOffset);

                ParallelRowIterator.IterateRows(configuration, sourceRectangle, in horizontalOperation);

                float realWeight = kernelOffset == 0 ? parameters.Z : parameters.W;
                float imaginaryWeight = kernelOffset == 0 ? parameters.W : -parameters.Z;

                BokehBlurProcessor.SecondPassConvolutionRowOperation verticalOperation = new(
                    sourceRectangle,
                    outputValues,
                    horizontalValues,
                    mapXY,
                    kernel,
                    realWeight,
                    imaginaryWeight);

                // Each iterator completes before returning. Finish every vertical read before reusing horizontalValues.
                ParallelRowIterator.IterateRows(configuration, sourceRectangle, in verticalOperation);
            }
        }
    }

    /// <summary>
    /// Converts source pixels to associated float colors, applies gamma once, and clears the output plane.
    /// </summary>
    private readonly struct ApplyGammaExposureRowOperation : IRowOperation
    {
        private readonly Rectangle bounds;
        private readonly Buffer2D<TPixel> sourcePixels;
        private readonly Buffer2DRegion<Vector4> targetValues;
        private readonly Buffer2DRegion<Vector4> outputValues;
        private readonly Configuration configuration;
        private readonly float gamma;
        private readonly int componentBitDepth;
        private readonly PixelColorRange colorRange;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplyGammaExposureRowOperation"/> struct.
        /// </summary>
        /// <param name="bounds">The processed rectangle.</param>
        /// <param name="sourcePixels">The original pixels, which remain unchanged until the final output conversion.</param>
        /// <param name="targetValues">The input plane for associated, gamma-expanded colors.</param>
        /// <param name="outputValues">The separate output plane to clear before accumulating convolution results.</param>
        /// <param name="configuration">The pixel conversion configuration.</param>
        /// <param name="gamma">The expansion exponent.</param>
        /// <param name="componentBitDepth">The source component precision.</param>
        /// <param name="colorRange">The source and result color range.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public ApplyGammaExposureRowOperation(
            Rectangle bounds,
            Buffer2D<TPixel> sourcePixels,
            Buffer2DRegion<Vector4> targetValues,
            Buffer2DRegion<Vector4> outputValues,
            Configuration configuration,
            float gamma,
            int componentBitDepth,
            PixelColorRange colorRange)
        {
            this.bounds = bounds;
            this.sourcePixels = sourcePixels;
            this.targetValues = targetValues;
            this.outputValues = outputValues;
            this.configuration = configuration;
            this.gamma = gamma;
            this.componentBitDepth = componentBitDepth;
            this.colorRange = colorRange;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int y)
        {
            ReadOnlySpan<TPixel> sourceRow = this.sourcePixels.DangerousGetRowSpan(y).Slice(this.bounds.X, this.bounds.Width);
            Span<Vector4> targetRow = this.targetValues.DangerousGetRowSpan(y).Slice(this.bounds.X, this.bounds.Width);

            // Associate before gamma, then retain the float result for every kernel component.
            // The shared compander selects the curve path and bounds RGB while retaining alpha.
            PixelOperations<TPixel>.Instance.ToAssociatedScaledVector4(this.configuration, sourceRow, targetRow);
            GammaCompanding.Expand(targetRow, this.gamma, this.componentBitDepth, this.colorRange);

            // Clear only output pixels that the vertical passes will accumulate. The input plane remains untouched.
            this.outputValues.DangerousGetRowSpan(y).Slice(this.bounds.X, this.bounds.Width).Clear();
        }
    }

    /// <summary>
    /// Applies inverse gamma to the accumulated float output and converts it to the destination pixel format.
    /// </summary>
    private readonly struct ApplyInverseGammaExposureRowOperation : IRowOperation
    {
        private readonly Rectangle bounds;
        private readonly Buffer2D<TPixel> targetPixels;
        private readonly Buffer2DRegion<Vector4> sourceValues;
        private readonly Configuration configuration;
        private readonly float gamma;
        private readonly int componentBitDepth;
        private readonly PixelColorRange colorRange;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplyInverseGammaExposureRowOperation"/> struct.
        /// </summary>
        /// <param name="bounds">The processed rectangle.</param>
        /// <param name="targetPixels">The destination pixels.</param>
        /// <param name="sourceValues">The accumulated float output plane.</param>
        /// <param name="configuration">The pixel conversion configuration.</param>
        /// <param name="gamma">The gamma value whose inverse compresses the samples.</param>
        /// <param name="componentBitDepth">The source component precision.</param>
        /// <param name="colorRange">The source and result color range.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public ApplyInverseGammaExposureRowOperation(
            Rectangle bounds,
            Buffer2D<TPixel> targetPixels,
            Buffer2DRegion<Vector4> sourceValues,
            Configuration configuration,
            float gamma,
            int componentBitDepth,
            PixelColorRange colorRange)
        {
            this.bounds = bounds;
            this.targetPixels = targetPixels;
            this.sourceValues = sourceValues;
            this.configuration = configuration;
            this.gamma = gamma;
            this.componentBitDepth = componentBitDepth;
            this.colorRange = colorRange;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int y)
        {
            Span<TPixel> targetRow = this.targetPixels.DangerousGetRowSpan(y).Slice(this.bounds.X, this.bounds.Width);
            Span<Vector4> sourceRow = this.sourceValues.DangerousGetRowSpan(y).Slice(this.bounds.X, this.bounds.Width);

            GammaCompanding.Compress(sourceRow, this.gamma, this.componentBitDepth, this.colorRange);
            PixelOperations<TPixel>.Instance.FromAssociatedScaledVector4Destructive(this.configuration, sourceRow, targetRow);
        }
    }
}
