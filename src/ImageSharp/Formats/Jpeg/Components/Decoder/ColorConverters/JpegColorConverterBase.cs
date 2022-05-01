// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
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
        public static JpegColorConverterBase GetConverter(JpegColorSpace colorSpace, int precision)
        {
            JpegColorConverterBase converter = Array.Find(
                Converters,
                c => c.ColorSpace == colorSpace
                && c.Precision == precision);

            if (converter is null)
            {
                throw new Exception($"Could not find any converter for JpegColorSpace {colorSpace}!");
            }

            return converter;
        }

        /// <summary>
        /// Converts planar jpeg component values in <paramref name="values"/> to RGB color space inplace.
        /// </summary>
        /// <param name="values">The input/ouptut as a stack-only <see cref="ComponentValues"/> struct</param>
        public abstract void ConvertToRgbInplace(in ComponentValues values);

        /// <summary>
        /// Returns the <see cref="JpegColorConverterBase"/>s for all supported colorspaces and precisions.
        /// </summary>
        private static JpegColorConverterBase[] CreateConverters()
        {
            var converters = new List<JpegColorConverterBase>();

            // 8-bit converters
            converters.AddRange(GetYCbCrConverters(8));
            converters.AddRange(GetYccKConverters(8));
            converters.AddRange(GetCmykConverters(8));
            converters.AddRange(GetGrayScaleConverters(8));
            converters.AddRange(GetRgbConverters(8));

            // 12-bit converters
            converters.AddRange(GetYCbCrConverters(12));
            converters.AddRange(GetYccKConverters(12));
            converters.AddRange(GetCmykConverters(12));
            converters.AddRange(GetGrayScaleConverters(12));
            converters.AddRange(GetRgbConverters(12));

            return converters.Where(x => x.IsAvailable).ToArray();
        }

        /// <summary>
        /// Returns the <see cref="JpegColorConverterBase"/>s for the YCbCr colorspace.
        /// </summary>
        private static IEnumerable<JpegColorConverterBase> GetYCbCrConverters(int precision)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            yield return new FromYCbCrAvx(precision);
#endif
            yield return new FromYCbCrVector(precision);
            yield return new FromYCbCrScalar(precision);
        }

        /// <summary>
        /// Returns the <see cref="JpegColorConverterBase"/>s for the YccK colorspace.
        /// </summary>
        private static IEnumerable<JpegColorConverterBase> GetYccKConverters(int precision)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            yield return new FromYccKAvx(precision);
#endif
            yield return new FromYccKVector(precision);
            yield return new FromYccKScalar(precision);
        }

        /// <summary>
        /// Returns the <see cref="JpegColorConverterBase"/>s for the CMYK colorspace.
        /// </summary>
        private static IEnumerable<JpegColorConverterBase> GetCmykConverters(int precision)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            yield return new FromCmykAvx(precision);
#endif
            yield return new FromCmykVector(precision);
            yield return new FromCmykScalar(precision);
        }

        /// <summary>
        /// Returns the <see cref="JpegColorConverterBase"/>s for the gray scale colorspace.
        /// </summary>
        private static IEnumerable<JpegColorConverterBase> GetGrayScaleConverters(int precision)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            yield return new FromGrayscaleAvx(precision);
#endif
            yield return new FromGrayScaleVector(precision);
            yield return new FromGrayscaleScalar(precision);
        }

        /// <summary>
        /// Returns the <see cref="JpegColorConverterBase"/>s for the RGB colorspace.
        /// </summary>
        private static IEnumerable<JpegColorConverterBase> GetRgbConverters(int precision)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            yield return new FromRgbAvx(precision);
#endif
            yield return new FromRgbVector(precision);
            yield return new FromRgbScalar(precision);
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
            public ComponentValues(IReadOnlyList<ComponentProcessor> processors, int row)
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
}
