// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

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
        this.HalfValue = MathF.Ceiling(this.MaximumValue * 0.5F);   // /2
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
        => Array.Find(Converters, c => c.ColorSpace == colorSpace && c.Precision == precision)
        ?? throw new InvalidImageContentException($"Could not find any converter for JpegColorSpace {colorSpace}!");

    /// <summary>
    /// Converts planar jpeg component values in <paramref name="values"/> to RGB color space in-place.
    /// </summary>
    /// <param name="values">The input/output as a stack-only <see cref="ComponentValues"/> struct</param>
    public abstract void ConvertToRgbInPlace(in ComponentValues values);

    /// <summary>
    /// Converts planar jpeg component values in <paramref name="values"/> to RGB color space in-place using the given ICC profile.
    /// </summary>
    /// <param name="configuration">The configuration instance to use for the conversion.</param>
    /// <param name="values">The input/output as a stack-only <see cref="ComponentValues"/> struct.</param>
    /// <param name="profile">The ICC profile to use for the conversion.</param>
    public abstract void ConvertToRgbInPlaceWithIcc(Configuration configuration, in ComponentValues values, IccProfile profile);

    /// <summary>
    /// Converts RGB lanes to jpeg component values.
    /// </summary>
    /// <param name="values">Jpeg component values.</param>
    /// <param name="rLane">Red colors lane.</param>
    /// <param name="gLane">Green colors lane.</param>
    /// <param name="bLane">Blue colors lane.</param>
    public abstract void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane);

    public static void PackedNormalizeInterleave3(
        ReadOnlySpan<float> xLane,
        ReadOnlySpan<float> yLane,
        ReadOnlySpan<float> zLane,
        Span<float> packed,
        float scale)
    {
        DebugGuard.IsTrue(packed.Length % 3 == 0, "Packed length must be divisible by 3.");
        DebugGuard.IsTrue(yLane.Length == xLane.Length, nameof(yLane), "Channels must be of same size!");
        DebugGuard.IsTrue(zLane.Length == xLane.Length, nameof(zLane), "Channels must be of same size!");
        DebugGuard.MustBeLessThanOrEqualTo(packed.Length / 3, xLane.Length, nameof(packed));

        // TODO: Investigate SIMD version of this.
        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);
        ref float packedRef = ref MemoryMarshal.GetReference(packed);

        for (nuint i = 0; i < (nuint)xLane.Length; i++)
        {
            nuint baseIdx = i * 3;
            Unsafe.Add(ref packedRef, baseIdx) = Unsafe.Add(ref xLaneRef, i) * scale;
            Unsafe.Add(ref packedRef, baseIdx + 1) = Unsafe.Add(ref yLaneRef, i) * scale;
            Unsafe.Add(ref packedRef, baseIdx + 2) = Unsafe.Add(ref zLaneRef, i) * scale;
        }
    }

    public static void UnpackDeinterleave3(
        ReadOnlySpan<Vector3> packed,
        Span<float> xLane,
        Span<float> yLane,
        Span<float> zLane)
    {
        DebugGuard.IsTrue(packed.Length == xLane.Length, nameof(packed), "Channels must be of same size!");
        DebugGuard.IsTrue(yLane.Length == xLane.Length, nameof(yLane), "Channels must be of same size!");
        DebugGuard.IsTrue(zLane.Length == xLane.Length, nameof(zLane), "Channels must be of same size!");

        // TODO: Investigate SIMD version of this.
        ref float packedRef = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(packed));
        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);

        for (nuint i = 0; i < (nuint)packed.Length; i++)
        {
            nuint baseIdx = i * 3;
            Unsafe.Add(ref xLaneRef, i) = Unsafe.Add(ref packedRef, baseIdx);
            Unsafe.Add(ref yLaneRef, i) = Unsafe.Add(ref packedRef, baseIdx + 1);
            Unsafe.Add(ref zLaneRef, i) = Unsafe.Add(ref packedRef, baseIdx + 2);
        }
    }

    public static void PackedNormalizeInterleave4(
        ReadOnlySpan<float> xLane,
        ReadOnlySpan<float> yLane,
        ReadOnlySpan<float> zLane,
        ReadOnlySpan<float> wLane,
        Span<float> packed,
        float maxValue)
    {
        DebugGuard.IsTrue(packed.Length % 4 == 0, "Packed length must be divisible by 4.");
        DebugGuard.IsTrue(yLane.Length == xLane.Length, nameof(yLane), "Channels must be of same size!");
        DebugGuard.IsTrue(zLane.Length == xLane.Length, nameof(zLane), "Channels must be of same size!");
        DebugGuard.IsTrue(wLane.Length == xLane.Length, nameof(wLane), "Channels must be of same size!");
        DebugGuard.MustBeLessThanOrEqualTo(packed.Length / 4, xLane.Length, nameof(packed));

        float scale = 1F / maxValue;

        // TODO: Investigate SIMD version of this.
        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);
        ref float wLaneRef = ref MemoryMarshal.GetReference(wLane);
        ref float packedRef = ref MemoryMarshal.GetReference(packed);

        for (nuint i = 0; i < (nuint)xLane.Length; i++)
        {
            nuint baseIdx = i * 4;
            Unsafe.Add(ref packedRef, baseIdx) = Unsafe.Add(ref xLaneRef, i) * scale;
            Unsafe.Add(ref packedRef, baseIdx + 1) = Unsafe.Add(ref yLaneRef, i) * scale;
            Unsafe.Add(ref packedRef, baseIdx + 2) = Unsafe.Add(ref zLaneRef, i) * scale;
            Unsafe.Add(ref packedRef, baseIdx + 3) = Unsafe.Add(ref wLaneRef, i) * scale;
        }
    }

    public static void PackedInvertNormalizeInterleave4(
        ReadOnlySpan<float> xLane,
        ReadOnlySpan<float> yLane,
        ReadOnlySpan<float> zLane,
        ReadOnlySpan<float> wLane,
        Span<float> packed,
        float maxValue)
    {
        DebugGuard.IsTrue(packed.Length % 4 == 0, "Packed length must be divisible by 4.");
        DebugGuard.IsTrue(yLane.Length == xLane.Length, nameof(yLane), "Channels must be of same size!");
        DebugGuard.IsTrue(zLane.Length == xLane.Length, nameof(zLane), "Channels must be of same size!");
        DebugGuard.IsTrue(wLane.Length == xLane.Length, nameof(wLane), "Channels must be of same size!");
        DebugGuard.MustBeLessThanOrEqualTo(packed.Length / 4, xLane.Length, nameof(packed));

        float scale = 1F / maxValue;

        // TODO: Investigate SIMD version of this.
        ref float xLaneRef = ref MemoryMarshal.GetReference(xLane);
        ref float yLaneRef = ref MemoryMarshal.GetReference(yLane);
        ref float zLaneRef = ref MemoryMarshal.GetReference(zLane);
        ref float wLaneRef = ref MemoryMarshal.GetReference(wLane);
        ref float packedRef = ref MemoryMarshal.GetReference(packed);

        for (nuint i = 0; i < (nuint)xLane.Length; i++)
        {
            nuint baseIdx = i * 4;
            Unsafe.Add(ref packedRef, baseIdx) = (maxValue - Unsafe.Add(ref xLaneRef, i)) * scale;
            Unsafe.Add(ref packedRef, baseIdx + 1) = (maxValue - Unsafe.Add(ref yLaneRef, i)) * scale;
            Unsafe.Add(ref packedRef, baseIdx + 2) = (maxValue - Unsafe.Add(ref zLaneRef, i)) * scale;
            Unsafe.Add(ref packedRef, baseIdx + 3) = (maxValue - Unsafe.Add(ref wLaneRef, i)) * scale;
        }
    }

    /// <summary>
    /// Returns the <see cref="JpegColorConverterBase"/>s for all supported color spaces and precisions.
    /// </summary>
    private static JpegColorConverterBase[] CreateConverters()
        => [

            // 8-bit converters
            GetYCbCrConverter(8),
            GetYccKConverter(8),
            GetCmykConverter(8),
            GetGrayScaleConverter(8),
            GetRgbConverter(8),
            GetTiffCmykConverter(8),
            GetTiffYccKConverter(8),

            // 12-bit converters
            GetYCbCrConverter(12),
            GetYccKConverter(12),
            GetCmykConverter(12),
            GetGrayScaleConverter(12),
            GetRgbConverter(12),
            GetTiffCmykConverter(12),
            GetTiffYccKConverter(12),
        ];

    /// <summary>
    /// Returns the <see cref="JpegColorConverterBase"/>s for the YCbCr colorspace.
    /// </summary>
    /// <param name="precision">The precision in bits.</param>
    private static JpegColorConverterBase GetYCbCrConverter(int precision)
    {
        if (JpegColorConverterVector512.IsSupported)
        {
            return new YCbCrVector512(precision);
        }

        if (JpegColorConverterVector256.IsSupported)
        {
            return new YCbCrVector256(precision);
        }

        if (JpegColorConverterVector128.IsSupported)
        {
            return new YCbCrVector128(precision);
        }

        return new YCbCrScalar(precision);
    }

    /// <summary>
    /// Returns the <see cref="JpegColorConverterBase"/>s for the YccK colorspace.
    /// </summary>
    /// <param name="precision">The precision in bits.</param>
    private static JpegColorConverterBase GetYccKConverter(int precision)
    {
        if (JpegColorConverterVector512.IsSupported)
        {
            return new YccKVector512(precision);
        }

        if (JpegColorConverterVector256.IsSupported)
        {
            return new YccKVector256(precision);
        }

        if (JpegColorConverterVector128.IsSupported)
        {
            return new YccKVector128(precision);
        }

        return new YccKScalar(precision);
    }

    /// <summary>
    /// Returns the <see cref="JpegColorConverterBase"/>s for the CMYK colorspace.
    /// </summary>
    /// <param name="precision">The precision in bits.</param>
    private static JpegColorConverterBase GetCmykConverter(int precision)
    {
        if (JpegColorConverterVector512.IsSupported)
        {
            return new CmykVector512(precision);
        }

        if (JpegColorConverterVector256.IsSupported)
        {
            return new CmykVector256(precision);
        }

        if (JpegColorConverterVector128.IsSupported)
        {
            return new CmykVector128(precision);
        }

        return new CmykScalar(precision);
    }

    /// <summary>
    /// Returns the <see cref="JpegColorConverterBase"/>s for the gray scale colorspace.
    /// </summary>
    /// <param name="precision">The precision in bits.</param>
    private static JpegColorConverterBase GetGrayScaleConverter(int precision)
    {
        if (JpegColorConverterVector512.IsSupported)
        {
            return new GrayScaleVector512(precision);
        }

        if (JpegColorConverterVector256.IsSupported)
        {
            return new GrayScaleVector256(precision);
        }

        if (JpegColorConverterVector128.IsSupported)
        {
            return new GrayScaleVector128(precision);
        }

        return new GrayScaleScalar(precision);
    }

    /// <summary>
    /// Returns the <see cref="JpegColorConverterBase"/>s for the RGB colorspace.
    /// </summary>
    /// <param name="precision">The precision in bits.</param>
    private static JpegColorConverterBase GetRgbConverter(int precision)
    {
        if (JpegColorConverterVector512.IsSupported)
        {
            return new RgbVector512(precision);
        }

        if (JpegColorConverterVector256.IsSupported)
        {
            return new RgbVector256(precision);
        }

        if (JpegColorConverterVector128.IsSupported)
        {
            return new RgbVector128(precision);
        }

        return new RgbScalar(precision);
    }

    private static JpegColorConverterBase GetTiffCmykConverter(int precision)
    {
        if (JpegColorConverterVector512.IsSupported)
        {
            return new TiffCmykVector512(precision);
        }

        if (JpegColorConverterVector256.IsSupported)
        {
            return new TiffCmykVector256(precision);
        }

        if (JpegColorConverterVector128.IsSupported)
        {
            return new TiffCmykVector128(precision);
        }

        return new TiffCmykScalar(precision);
    }

    private static JpegColorConverterBase GetTiffYccKConverter(int precision)
    {
        if (JpegColorConverterVector512.IsSupported)
        {
            return new TiffYccKVector512(precision);
        }

        if (JpegColorConverterVector256.IsSupported)
        {
            return new TiffYccKVector256(precision);
        }

        if (JpegColorConverterVector128.IsSupported)
        {
            return new TiffYccKVector128(precision);
        }

        return new TiffYccKScalar(precision);
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
            this.Component3 = this.ComponentCount > 3 ? componentBuffers[3].DangerousGetRowSpan(row) : [];
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
            this.Component3 = this.ComponentCount > 3 ? processors[3].GetColorBufferRowSpan(row) : [];
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
            this.Component3 = this.ComponentCount > 3 ? processors[3].GetColorBufferRowSpan(row) : [];
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
            Span<float> c1 = this.Component1.Length > 0 ? this.Component1.Slice(start, length) : [];
            Span<float> c2 = this.Component2.Length > 0 ? this.Component2.Slice(start, length) : [];
            Span<float> c3 = this.Component3.Length > 0 ? this.Component3.Slice(start, length) : [];

            return new ComponentValues(this.ComponentCount, c0, c1, c2, c3);
        }
    }
}
