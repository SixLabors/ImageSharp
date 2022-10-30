// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <summary>
/// Encapsulates the conversion of color channels from jpeg image to RGB channels.
/// </summary>
internal abstract partial class JpegColorConverterBase
{
    /// <summary>
    /// The available converters
    /// </summary>
    private static readonly JpegColorConverterBase[] Converters = CreateConverters();

    /// <summary>
    /// Initializes a new instance of the <see cref="JpegColorConverterBase"/> class.
    /// </summary>
    /// <param name="colorSpace">The color space.</param>
    /// <param name="precision">The precision in bits.</param>
    protected JpegColorConverterBase(JpegColorSpace colorSpace, int precision)
    {
        this.ColorSpace = colorSpace;
        this.Precision = precision;
        this.MaximumValue = MathF.Pow(2, precision) - 1;
        this.HalfValue = MathF.Ceiling(this.MaximumValue / 2);
    }

    /// <summary>
    /// Gets a value indicating whether this <see cref="JpegColorConverterBase"/> is available
    /// on the current runtime and CPU architecture.
    /// </summary>
    public abstract bool IsAvailable { get; }

    /// <summary>
    /// Gets a value indicating how many pixels are processed in a single batch.
    /// </summary>
    /// <remarks>
    /// This generally should be equal to register size,
    /// e.g. 1 for scalar implementation, 8 for AVX implementation and so on.
    /// </remarks>
    public abstract int ElementsPerBatch { get; }

    /// <summary>
    /// Gets the <see cref="JpegColorSpace"/> of this converter.
    /// </summary>
    public JpegColorSpace ColorSpace { get; }

    /// <summary>
    /// Gets the Precision of this converter in bits.
    /// </summary>
    public int Precision { get; }

    /// <summary>
    /// Gets the maximum value of a sample
    /// </summary>
    private float MaximumValue { get; }

    /// <summary>
    /// Gets the half of the maximum value of a sample
    /// </summary>
    private float HalfValue { get; }

    /// <summary>
    /// Returns the <see cref="JpegColorConverterBase"/> corresponding to the given <see cref="JpegColorSpace"/>
    /// </summary>
    /// <param name="colorSpace">The color space.</param>
    /// <param name="precision">The precision in bits.</param>
    /// <exception cref="InvalidImageContentException">Invalid colorspace.</exception>
    public static JpegColorConverterBase GetConverter(JpegColorSpace colorSpace, int precision)
    {
        JpegColorConverterBase converter = Array.Find(
            Converters,
            c => c.ColorSpace == colorSpace
            && c.Precision == precision);

        if (converter is null)
        {
            throw new InvalidImageContentException($"Could not find any converter for JpegColorSpace {colorSpace}!");
        }

        return converter;
    }

    /// <summary>
    /// Converts planar jpeg component values in <paramref name="values"/> to RGB color space inplace.
    /// </summary>
    /// <param name="values">The input/ouptut as a stack-only <see cref="ComponentValues"/> struct</param>
    public abstract void ConvertToRgbInplace(in ComponentValues values);

    /// <summary>
    /// Converts RGB lanes to jpeg component values.
    /// </summary>
    /// <param name="values">Jpeg component values.</param>
    /// <param name="rLane">Red colors lane.</param>
    /// <param name="gLane">Green colors lane.</param>
    /// <param name="bLane">Blue colors lane.</param>
    public abstract void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane);

    /// <summary>
    /// Returns the <see cref="JpegColorConverterBase"/>s for all supported colorspaces and precisions.
    /// </summary>
    private static JpegColorConverterBase[] CreateConverters()
    {
        // 5 color types with 2 supported precisions: 8 bit & 12 bit
        const int colorConvertersCount = 5 * 2;

        JpegColorConverterBase[] converters = new JpegColorConverterBase[colorConvertersCount];

        // 8-bit converters
        converters[0] = GetYCbCrConverter(8);
        converters[1] = GetYccKConverter(8);
        converters[2] = GetCmykConverter(8);
        converters[3] = GetGrayScaleConverter(8);
        converters[4] = GetRgbConverter(8);

        // 12-bit converters
        converters[5] = GetYCbCrConverter(12);
        converters[6] = GetYccKConverter(12);
        converters[7] = GetCmykConverter(12);
        converters[8] = GetGrayScaleConverter(12);
        converters[9] = GetRgbConverter(12);

        return converters;
    }

    /// <summary>
    /// Returns the <see cref="JpegColorConverterBase"/>s for the YCbCr colorspace.
    /// </summary>
    /// <param name="precision">The precision in bits.</param>
    private static JpegColorConverterBase GetYCbCrConverter(int precision)
    {
        if (JpegColorConverterAvx.IsSupported)
        {
            return new YCbCrAvx(precision);
        }

        if (JpegColorConverterVector.IsSupported)
        {
            return new YCbCrVector(precision);
        }

        return new YCbCrScalar(precision);
    }

    /// <summary>
    /// Returns the <see cref="JpegColorConverterBase"/>s for the YccK colorspace.
    /// </summary>
    /// <param name="precision">The precision in bits.</param>
    private static JpegColorConverterBase GetYccKConverter(int precision)
    {
        if (JpegColorConverterAvx.IsSupported)
        {
            return new YccKAvx(precision);
        }

        if (JpegColorConverterVector.IsSupported)
        {
            return new YccKVector(precision);
        }

        return new YccKScalar(precision);
    }

    /// <summary>
    /// Returns the <see cref="JpegColorConverterBase"/>s for the CMYK colorspace.
    /// </summary>
    /// <param name="precision">The precision in bits.</param>
    private static JpegColorConverterBase GetCmykConverter(int precision)
    {
        if (JpegColorConverterAvx.IsSupported)
        {
            return new CmykAvx(precision);
        }

        if (JpegColorConverterVector.IsSupported)
        {
            return new CmykVector(precision);
        }

        return new CmykScalar(precision);
    }

    /// <summary>
    /// Returns the <see cref="JpegColorConverterBase"/>s for the gray scale colorspace.
    /// </summary>
    /// <param name="precision">The precision in bits.</param>
    private static JpegColorConverterBase GetGrayScaleConverter(int precision)
    {
        if (JpegColorConverterAvx.IsSupported)
        {
            return new GrayscaleAvx(precision);
        }

        if (JpegColorConverterVector.IsSupported)
        {
            return new GrayScaleVector(precision);
        }

        return new GrayscaleScalar(precision);
    }

    /// <summary>
    /// Returns the <see cref="JpegColorConverterBase"/>s for the RGB colorspace.
    /// </summary>
    /// <param name="precision">The precision in bits.</param>
    private static JpegColorConverterBase GetRgbConverter(int precision)
    {
        if (JpegColorConverterAvx.IsSupported)
        {
            return new RgbAvx(precision);
        }

        if (JpegColorConverterVector.IsSupported)
        {
            return new RgbScalar(precision);
        }

        return new GrayscaleScalar(precision);
    }

    /// <summary>
    /// A stack-only struct to reference the input buffers using <see cref="ReadOnlySpan{T}"/>-s.
    /// </summary>
#pragma warning disable SA1206 // Declaration keywords should follow order
    public readonly ref struct ComponentValues
#pragma warning restore SA1206 // Declaration keywords should follow order
    {
        /// <summary>
        /// The component count
        /// </summary>
        public readonly int ComponentCount;

        /// <summary>
        /// The component 0 (eg. Y)
        /// </summary>
        public readonly Span<float> Component0;

        /// <summary>
        /// The component 1 (eg. Cb). In case of grayscale, it points to <see cref="Component0"/>.
        /// </summary>
        public readonly Span<float> Component1;

        /// <summary>
        /// The component 2 (eg. Cr). In case of grayscale, it points to <see cref="Component0"/>.
        /// </summary>
        public readonly Span<float> Component2;

        /// <summary>
        /// The component 4
        /// </summary>
        public readonly Span<float> Component3;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentValues"/> struct.
        /// </summary>
        /// <param name="componentBuffers">List of component buffers.</param>
        /// <param name="row">Row to convert</param>
        public ComponentValues(IReadOnlyList<Buffer2D<float>> componentBuffers, int row)
        {
            DebugGuard.MustBeGreaterThan(componentBuffers.Count, 0, nameof(componentBuffers));

            this.ComponentCount = componentBuffers.Count;

            this.Component0 = componentBuffers[0].DangerousGetRowSpan(row);

            // In case of grayscale, Component1 and Component2 point to Component0 memory area
            this.Component1 = this.ComponentCount > 1 ? componentBuffers[1].DangerousGetRowSpan(row) : this.Component0;
            this.Component2 = this.ComponentCount > 2 ? componentBuffers[2].DangerousGetRowSpan(row) : this.Component0;
            this.Component3 = this.ComponentCount > 3 ? componentBuffers[3].DangerousGetRowSpan(row) : Span<float>.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentValues"/> struct.
        /// </summary>
        /// <param name="processors">List of component color processors.</param>
        /// <param name="row">Row to convert</param>
        public ComponentValues(IReadOnlyList<Decoder.ComponentProcessor> processors, int row)
        {
            DebugGuard.MustBeGreaterThan(processors.Count, 0, nameof(processors));

            this.ComponentCount = processors.Count;

            this.Component0 = processors[0].GetColorBufferRowSpan(row);

            // In case of grayscale, Component1 and Component2 point to Component0 memory area
            this.Component1 = this.ComponentCount > 1 ? processors[1].GetColorBufferRowSpan(row) : this.Component0;
            this.Component2 = this.ComponentCount > 2 ? processors[2].GetColorBufferRowSpan(row) : this.Component0;
            this.Component3 = this.ComponentCount > 3 ? processors[3].GetColorBufferRowSpan(row) : Span<float>.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentValues"/> struct.
        /// </summary>
        /// <param name="processors">List of component color processors.</param>
        /// <param name="row">Row to convert</param>
        public ComponentValues(IReadOnlyList<Encoder.ComponentProcessor> processors, int row)
        {
            DebugGuard.MustBeGreaterThan(processors.Count, 0, nameof(processors));

            this.ComponentCount = processors.Count;

            this.Component0 = processors[0].GetColorBufferRowSpan(row);

            // In case of grayscale, Component1 and Component2 point to Component0 memory area
            this.Component1 = this.ComponentCount > 1 ? processors[1].GetColorBufferRowSpan(row) : this.Component0;
            this.Component2 = this.ComponentCount > 2 ? processors[2].GetColorBufferRowSpan(row) : this.Component0;
            this.Component3 = this.ComponentCount > 3 ? processors[3].GetColorBufferRowSpan(row) : Span<float>.Empty;
        }

        internal ComponentValues(
            int componentCount,
            Span<float> c0,
            Span<float> c1,
            Span<float> c2,
            Span<float> c3)
        {
            this.ComponentCount = componentCount;
            this.Component0 = c0;
            this.Component1 = c1;
            this.Component2 = c2;
            this.Component3 = c3;
        }

        public ComponentValues Slice(int start, int length)
        {
            Span<float> c0 = this.Component0.Slice(start, length);
            Span<float> c1 = this.Component1.Length > 0 ? this.Component1.Slice(start, length) : Span<float>.Empty;
            Span<float> c2 = this.Component2.Length > 0 ? this.Component2.Slice(start, length) : Span<float>.Empty;
            Span<float> c3 = this.Component3.Length > 0 ? this.Component3.Slice(start, length) : Span<float>.Empty;

            return new ComponentValues(this.ComponentCount, c0, c1, c2, c3);
        }
    }
}
