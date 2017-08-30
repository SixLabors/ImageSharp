using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    internal abstract partial class JpegColorConverter
    {
        private static readonly JpegColorConverter[] Converters = { new FromYCbCr(), new FromYccK(), new FromCmyk() };

        protected JpegColorConverter(JpegColorSpace colorSpace)
        {
            this.ColorSpace = colorSpace;
        }

        public JpegColorSpace ColorSpace { get; }

        public static JpegColorConverter GetConverter(JpegColorSpace colorSpace)
        {
            JpegColorConverter converter = Converters.FirstOrDefault(c => c.ColorSpace == colorSpace);
            if (converter == null)
            {
                throw new Exception($"Could not find any converter for JpegColorSpace {colorSpace}!");
            }

            return converter;
        }

        public abstract void ConvertToRGBA(ComponentValues values, Span<Vector4> result);

        public struct ComponentValues
        {
            public readonly int ComponentCount;

            public readonly ReadOnlySpan<float> Component0;

            public readonly ReadOnlySpan<float> Component1;

            public readonly ReadOnlySpan<float> Component2;

            public readonly ReadOnlySpan<float> Component3;

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
        }
    }
}