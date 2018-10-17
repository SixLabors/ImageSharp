// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using SixLabors.ImageSharp.Common.Tuples;
using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    /// <summary>
    /// Encapsulates the conversion of Jpeg channels to RGBA values packed in <see cref="Vector4"/> buffer.
    /// </summary>
    internal abstract partial class JpegColorConverter
    {
        /// <summary>
        /// The avalilable converters
        /// </summary>
        private static readonly JpegColorConverter[] Converters =
            {
                GetYCbCrConverter(), new FromYccK(), new FromCmyk(), new FromGrayscale(), new FromRgb()
            };

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

            if (converter is null)
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
        public abstract void ConvertToRgba(in ComponentValues values, Span<Vector4> result);

        /// <summary>
        /// Returns the <see cref="JpegColorConverter"/> for the YCbCr colorspace that matches the current CPU architecture.
        /// </summary>
        private static JpegColorConverter GetYCbCrConverter() =>
            FromYCbCrSimdAvx2.IsAvailable ? (JpegColorConverter)new FromYCbCrSimdAvx2() : new FromYCbCrSimd();

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
            public ComponentValues(IReadOnlyList<Buffer2D<float>> componentBuffers, int row)
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

        internal struct Vector4Octet
        {
#pragma warning disable SA1132 // Do not combine fields
            public Vector4 V0, V1, V2, V3, V4, V5, V6, V7;

            /// <summary>
            /// Collect (r0,r1...r8) (g0,g1...g8) (b0,b1...b8) vector values in the expected (r0,g0,g1,1), (r1,g1,g2,1) ... order.
            /// </summary>
            public void Collect(ref Vector4Pair r, ref Vector4Pair g, ref Vector4Pair b)
            {
                this.V0.X = r.A.X;
                this.V0.Y = g.A.X;
                this.V0.Z = b.A.X;
                this.V0.W = 1f;

                this.V1.X = r.A.Y;
                this.V1.Y = g.A.Y;
                this.V1.Z = b.A.Y;
                this.V1.W = 1f;

                this.V2.X = r.A.Z;
                this.V2.Y = g.A.Z;
                this.V2.Z = b.A.Z;
                this.V2.W = 1f;

                this.V3.X = r.A.W;
                this.V3.Y = g.A.W;
                this.V3.Z = b.A.W;
                this.V3.W = 1f;

                this.V4.X = r.B.X;
                this.V4.Y = g.B.X;
                this.V4.Z = b.B.X;
                this.V4.W = 1f;

                this.V5.X = r.B.Y;
                this.V5.Y = g.B.Y;
                this.V5.Z = b.B.Y;
                this.V5.W = 1f;

                this.V6.X = r.B.Z;
                this.V6.Y = g.B.Z;
                this.V6.Z = b.B.Z;
                this.V6.W = 1f;

                this.V7.X = r.B.W;
                this.V7.Y = g.B.W;
                this.V7.Z = b.B.W;
                this.V7.W = 1f;
            }
        }
    }
}