// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution;

/// <summary>
/// Applies bokeh blur processing to the image.
/// </summary>
public sealed class BokehBlurProcessor : IImageProcessor
{
    /// <summary>
    /// The default radius used by the parameterless constructor.
    /// </summary>
    public const int DefaultRadius = 32;

    /// <summary>
    /// The default component count used by the parameterless constructor.
    /// </summary>
    public const int DefaultComponents = 2;

    /// <summary>
    /// The default gamma used by the parameterless constructor.
    /// </summary>
    public const float DefaultGamma = 3F;

    /// <summary>
    /// Initializes a new instance of the <see cref="BokehBlurProcessor"/> class.
    /// </summary>
    public BokehBlurProcessor()
    {
        this.Radius = DefaultRadius;
        this.Components = DefaultComponents;
        this.Gamma = DefaultGamma;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BokehBlurProcessor"/> class.
    /// </summary>
    /// <param name="radius">
    /// The 'radius' value representing the size of the area to sample.
    /// </param>
    /// <param name="components">
    /// The number of components to use to approximate the original 2D bokeh blur convolution kernel.
    /// </param>
    /// <param name="gamma">
    /// The gamma highlight factor. Must be at least 1.
    /// </param>
    public BokehBlurProcessor(int radius, int components, float gamma)
    {
        Guard.MustBeGreaterThan(radius, 0, nameof(radius));
        Guard.MustBeBetweenOrEqualTo(components, 1, 6, nameof(components));
        Guard.MustBeGreaterThanOrEqualTo(gamma, 1, nameof(gamma));

        this.Radius = radius;
        this.Components = components;

        this.Gamma = gamma;
    }

    /// <summary>
    /// Gets the radius.
    /// </summary>
    public int Radius { get; }

    /// <summary>
    /// Gets the number of components.
    /// </summary>
    public int Components { get; }

    /// <summary>
    /// Gets the gamma highlight factor.
    /// </summary>
    public float Gamma { get; }

    /// <inheritdoc />
    public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
        where TPixel : unmanaged, IPixel<TPixel>
        => new BokehBlurProcessor<TPixel>(configuration, this, source, sourceRectangle);

    /// <summary>
    /// Applies one real horizontal convolution to the associated, gamma-expanded float colors.
    /// </summary>
    /// <remarks>
    /// Both convolution operations use float storage and do not depend on TPixel.
    /// Keep them in this non-generic type so JIT and AOT compilation can share them across pixel formats.
    /// </remarks>
    internal readonly struct FirstPassConvolutionRowOperation : IRowOperation
    {
        private readonly Rectangle bounds;
        private readonly Buffer2D<Vector4> targetValues;
        private readonly Buffer2DRegion<Vector4> sourceValues;
        private readonly KernelSamplingMap map;
        private readonly Complex64[] kernel;
        private readonly int kernelOffset;

        /// <summary>
        /// Initializes a new instance of the <see cref="FirstPassConvolutionRowOperation"/> struct.
        /// </summary>
        /// <param name="bounds">The processing rectangle.</param>
        /// <param name="targetValues">The float buffer overwritten by each horizontal pass.</param>
        /// <param name="sourceValues">The associated, gamma-expanded input plane shared by all kernel components.</param>
        /// <param name="map">The clamped sampling offsets for the processing rectangle.</param>
        /// <param name="kernel">The current component's complex weights.</param>
        /// <param name="kernelOffset">Zero selects real coefficients; one selects imaginary coefficients.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public FirstPassConvolutionRowOperation(
            Rectangle bounds,
            Buffer2D<Vector4> targetValues,
            Buffer2DRegion<Vector4> sourceValues,
            KernelSamplingMap map,
            Complex64[] kernel,
            int kernelOffset)
        {
            this.bounds = bounds;
            this.targetValues = targetValues;
            this.sourceValues = sourceValues;
            this.map = map;
            this.kernel = kernel;
            this.kernelOffset = kernelOffset;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int y)
        {
            int boundsX = this.bounds.X;
            int boundsWidth = this.bounds.Width;
            int kernelSize = this.kernel.Length;

            // Store horizontal results from column zero. The vertical pass uses this compact row layout
            // even when the processing rectangle starts at a nonzero image X coordinate.
            Span<Vector4> targetRow = this.targetValues.DangerousGetRowSpan(y)[..boundsWidth];
            ReadOnlySpan<Vector4> sourceRow = this.sourceValues.DangerousGetRowSpan(y).Slice(boundsX, boundsWidth);

            // Complex64 stores consecutive real and imaginary floats: [a0,b0,a1,b1,...].
            // Select the first coefficient once, then advance by two floats per tap. No kernel copy or per-pixel branch is needed.
            ReadOnlySpan<float> coefficients = MemoryMarshal.Cast<Complex64, float>(this.kernel.AsSpan());
            ref float coefficientBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(coefficients), (uint)this.kernelOffset);
            ref Vector4 sourceBase = ref MemoryMarshal.GetReference(sourceRow);
            ref Vector4 targetStart = ref MemoryMarshal.GetReference(targetRow);
            ref Vector4 targetEnd = ref Unsafe.Add(ref targetStart, (uint)boundsWidth);
            ref int sampleColumnBase = ref MemoryMarshal.GetReference(this.map.GetColumnOffsetSpan());

            while (Unsafe.IsAddressLessThan(ref targetStart, ref targetEnd))
            {
                Vector4 sum = Vector4.Zero;
                ref int sampleColumnStart = ref sampleColumnBase;

                for (int tap = 0; tap < kernelSize; tap++)
                {
                    // The clamped map stores image coordinates. Subtract boundsX to address the sliced source row.
                    // Vector4 multiplies the same real coefficient into all four color and alpha lanes.
                    Vector4 sample = Unsafe.Add(ref sourceBase, (uint)(sampleColumnStart - boundsX));
                    float coefficient = Unsafe.Add(ref coefficientBase, (uint)(tap * 2));
                    sum += sample * coefficient;

                    sampleColumnStart = ref Unsafe.Add(ref sampleColumnStart, 1);
                }

                // Overwrite every processed pixel, so the scratch buffer needs no separate clearing pass.
                targetStart = sum;
                sampleColumnBase = ref Unsafe.Add(ref sampleColumnBase, (uint)kernelSize);
                targetStart = ref Unsafe.Add(ref targetStart, 1);
            }
        }
    }

    /// <summary>
    /// Applies real vertical convolution and adds its contribution to the float output plane.
    /// </summary>
    internal readonly struct SecondPassConvolutionRowOperation : IRowOperation
    {
        private readonly Rectangle bounds;
        private readonly Buffer2DRegion<Vector4> targetValues;
        private readonly Buffer2D<Vector4> sourceValues;
        private readonly KernelSamplingMap map;
        private readonly Complex64[] kernel;
        private readonly float realWeight;
        private readonly float imaginaryWeight;

        /// <summary>
        /// Initializes a new instance of the <see cref="SecondPassConvolutionRowOperation"/> struct.
        /// </summary>
        /// <param name="bounds">The processing rectangle.</param>
        /// <param name="targetValues">The output plane shared by all kernel contributions.</param>
        /// <param name="sourceValues">The current real horizontal convolution results.</param>
        /// <param name="map">The clamped sampling offsets for the processing rectangle.</param>
        /// <param name="kernel">The current component's complex weights.</param>
        /// <param name="realWeight">The multiplier of each real kernel coefficient: z for Ha, or w for Hb.</param>
        /// <param name="imaginaryWeight">The multiplier of each imaginary kernel coefficient: w for Ha, or -z for Hb.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public SecondPassConvolutionRowOperation(
            Rectangle bounds,
            Buffer2DRegion<Vector4> targetValues,
            Buffer2D<Vector4> sourceValues,
            KernelSamplingMap map,
            Complex64[] kernel,
            float realWeight,
            float imaginaryWeight)
        {
            this.bounds = bounds;
            this.targetValues = targetValues;
            this.sourceValues = sourceValues;
            this.map = map;
            this.kernel = kernel;
            this.realWeight = realWeight;
            this.imaginaryWeight = imaginaryWeight;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int y)
        {
            int boundsWidth = this.bounds.Width;
            int kernelSize = this.kernel.Length;

            ReadOnlySpan<int> sampleRows = this.map.GetRowOffsetSpan().Slice((y - this.bounds.Y) * kernelSize, kernelSize);
            Span<Vector4> targetRow = this.targetValues.DangerousGetRowSpan(y).Slice(this.bounds.X, boundsWidth);
            ref Vector4 targetBase = ref MemoryMarshal.GetReference(targetRow);

            for (int tap = 0; tap < kernelSize; tap++)
            {
                // Ha uses z*a + w*b; Hb uses w*a - z*b. Combine the scalar coefficients once per sampled row,
                // outside the pixel loop. The exposure pass cleared the output plane before any contribution was added.
                Complex64 coefficient = this.kernel[tap];
                float factor = (this.realWeight * coefficient.Real) + (this.imaginaryWeight * coefficient.Imaginary);

                ReadOnlySpan<Vector4> sourceRow = this.sourceValues.DangerousGetRowSpan(sampleRows[tap])[..boundsWidth];
                ref Vector4 sourceBase = ref MemoryMarshal.GetReference(sourceRow);
                ref Vector4 sourceEnd = ref Unsafe.Add(ref sourceBase, (uint)boundsWidth);
                ref Vector4 targetStart = ref targetBase;

                // All four lanes retain float precision. Packing and alpha unassociation happen only after all components finish.
                while (Unsafe.IsAddressLessThan(ref sourceBase, ref sourceEnd))
                {
                    targetStart += sourceBase * factor;

                    sourceBase = ref Unsafe.Add(ref sourceBase, 1);
                    targetStart = ref Unsafe.Add(ref targetStart, 1);
                }
            }
        }
    }
}
