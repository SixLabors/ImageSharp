using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    /// <summary>
    /// Encapsulates the conversion of Jpeg channels to RGBA values packed in <see cref="Vector4"/> buffer.
    /// </summary>
    internal abstract partial class JpegColorConverter
    {
        /// <summary>
        /// The avalilable converters
        /// </summary>
        private static readonly JpegColorConverter[] Converters = { new FromYCbCrSimd(), new FromYccK(), new FromCmyk(), new FromGrayScale(), new FromRgb() };

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegColorConverter"/> class.
        /// </summary>
        protected JpegColorConverter(JpegColorSpace colorSpace)
        {
            this.ColorSpace = colorSpace;
        }

        /// <summary>
        /// Gets the <see cref="JpegColorSpace"/> of this converter.
        /// </summary>
        public JpegColorSpace ColorSpace { get; }

        /// <summary>
        /// Returns the <see cref="JpegColorConverter"/> corresponding to the given <see cref="JpegColorSpace"/>
        /// </summary>
        public static JpegColorConverter GetConverter(JpegColorSpace colorSpace)
        {
            JpegColorConverter converter = Converters.FirstOrDefault(c => c.ColorSpace == colorSpace);
            if (converter == null)
            {
                throw new Exception($"Could not find any converter for JpegColorSpace {colorSpace}!");
            }

            return converter;
        }

        /// <summary>
        /// He implementation of the conversion.
        /// </summary>
        /// <param name="values">The input as a stack-only <see cref="ComponentValues"/> struct</param>
        /// <param name="result">The destination buffer of <see cref="Vector4"/> values</param>
        public abstract void ConvertToRGBA(ComponentValues values, Span<Vector4> result);

        /// <summary>
        /// A stack-only struct to reference the input buffers using <see cref="ReadOnlySpan{T}"/>-s.
        /// </summary>
        public struct ComponentValues
        {
            /// <summary>
            /// The component count
            /// </summary>
            public readonly int ComponentCount;

            /// <summary>
            /// The component 0 (eg. Y)
            /// </summary>
            public readonly ReadOnlySpan<float> Component0;

            /// <summary>
            /// The component 1 (eg. Cb)
            /// </summary>
            public readonly ReadOnlySpan<float> Component1;

            /// <summary>
            /// The component 2 (eg. Cr)
            /// </summary>
            public readonly ReadOnlySpan<float> Component2;

            /// <summary>
            /// The component 4
            /// </summary>
            public readonly ReadOnlySpan<float> Component3;

            /// <summary>
            /// Initializes a new instance of the <see cref="ComponentValues"/> struct.
            /// </summary>
            /// <param name="componentBuffers">The 1-4 sized list of component buffers.</param>
            /// <param name="row">The row to convert</param>
            public ComponentValues(IReadOnlyList<IBuffer2D<float>> componentBuffers, int row)
            {
                this.ComponentCount = componentBuffers.Count;

                this.Component0 = componentBuffers[0].GetRowSpan(row);
                this.Component1 = Span<float>.Empty;
                this.Component2 = Span<float>.Empty;
                this.Component3 = Span<float>.Empty;

                if (this.ComponentCount > 1)
                {
                    this.Component1 = componentBuffers[1].GetRowSpan(row);
                    if (this.ComponentCount > 2)
                    {
                        this.Component2 = componentBuffers[2].GetRowSpan(row);
                        if (this.ComponentCount > 3)
                        {
                            this.Component3 = componentBuffers[3].GetRowSpan(row);
                        }
                    }
                }
            }

            private ComponentValues(
                int componentCount,
                ReadOnlySpan<float> c0,
                ReadOnlySpan<float> c1,
                ReadOnlySpan<float> c2,
                ReadOnlySpan<float> c3)
            {
                this.ComponentCount = componentCount;
                this.Component0 = c0;
                this.Component1 = c1;
                this.Component2 = c2;
                this.Component3 = c3;
            }

            public ComponentValues Slice(int start, int length)
            {
                ReadOnlySpan<float> c0 = this.Component0.Slice(start, length);
                ReadOnlySpan<float> c1 = this.ComponentCount > 1 ? this.Component1.Slice(start, length) : ReadOnlySpan<float>.Empty;
                ReadOnlySpan<float> c2 = this.ComponentCount > 2 ? this.Component2.Slice(start, length) : ReadOnlySpan<float>.Empty;
                ReadOnlySpan<float> c3 = this.ComponentCount > 3 ? this.Component3.Slice(start, length) : ReadOnlySpan<float>.Empty;

                return new ComponentValues(this.ComponentCount, c0, c1, c2, c3);
            }
        }
    }
}