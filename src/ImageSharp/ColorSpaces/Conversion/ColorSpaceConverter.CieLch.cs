// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="CieLch"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        /// <summary>
        /// The converter for converting between CieLab to CieLch.
        /// </summary>
        private static readonly CieLabToCieLchConverter CieLabToCieLchConverter = new CieLabToCieLchConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(CieLab color)
        {
            Guard.NotNull(color, nameof(color));

            // Adaptation
            CieLab adapted = this.IsChromaticAdaptationPerformed ? this.Adapt(color) : color;

            // Conversion
            return CieLabToCieLchConverter.Convert(adapted);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLab"/> into <see cref="CieLch"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<CieLab> source, Span<CieLch> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref CieLab sourceRef = ref source.DangerousGetPinnableReference();
            ref CieLch destRef = ref destination.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref CieLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(CieLchuv color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLchuv"/> into <see cref="CieLch"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<CieLchuv> source, Span<CieLch> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref CieLchuv sourceRef = ref source.DangerousGetPinnableReference();
            ref CieLch destRef = ref destination.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref CieLchuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(CieLuv color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLuv"/> into <see cref="CieLch"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<CieLuv> source, Span<CieLch> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref CieLuv sourceRef = ref source.DangerousGetPinnableReference();
            ref CieLch destRef = ref destination.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref CieLuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(CieXyy color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyy"/> into <see cref="CieLch"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<CieXyy> source, Span<CieLch> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref CieXyy sourceRef = ref source.DangerousGetPinnableReference();
            ref CieLch destRef = ref destination.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref CieXyy sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(CieXyz color)
        {
            Guard.NotNull(color, nameof(color));

            var labColor = this.ToCieLab(color);
            return this.ToCieLch(labColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyz"/> into <see cref="CieLch"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<CieXyz> source, Span<CieLch> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref CieXyz sourceRef = ref source.DangerousGetPinnableReference();
            ref CieLch destRef = ref destination.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref CieXyz sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(Cmyk color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Cmyk"/> into <see cref="CieLch"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<Cmyk> source, Span<CieLch> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref Cmyk sourceRef = ref source.DangerousGetPinnableReference();
            ref CieLch destRef = ref destination.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref Cmyk sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(Hsl color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsl"/> into <see cref="CieLch"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<Hsl> source, Span<CieLch> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref Hsl sourceRef = ref source.DangerousGetPinnableReference();
            ref CieLch destRef = ref destination.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref Hsl sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(Hsv color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsv"/> into <see cref="CieLch"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<Hsv> source, Span<CieLch> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref Hsv sourceRef = ref source.DangerousGetPinnableReference();
            ref CieLch destRef = ref destination.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref Hsv sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(HunterLab color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="HunterLab"/> into <see cref="CieLch"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<HunterLab> source, Span<CieLch> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref HunterLab sourceRef = ref source.DangerousGetPinnableReference();
            ref CieLch destRef = ref destination.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref HunterLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(LinearRgb color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="LinearRgb"/> into <see cref="CieLch"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<LinearRgb> source, Span<CieLch> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref LinearRgb sourceRef = ref source.DangerousGetPinnableReference();
            ref CieLch destRef = ref destination.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref LinearRgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(Lms color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Lms"/> into <see cref="CieLch"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<Lms> source, Span<CieLch> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref Lms sourceRef = ref source.DangerousGetPinnableReference();
            ref CieLch destRef = ref destination.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref Lms sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(Rgb color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Rgb"/> into <see cref="CieLch"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<Rgb> source, Span<CieLch> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref Rgb sourceRef = ref source.DangerousGetPinnableReference();
            ref CieLch destRef = ref destination.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref Rgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="CieLch"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(YCbCr color)
        {
            Guard.NotNull(color, nameof(color));

            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="YCbCr"/> into <see cref="CieLch"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<YCbCr> source, Span<CieLch> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref YCbCr sourceRef = ref source.DangerousGetPinnableReference();
            ref CieLch destRef = ref destination.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref YCbCr sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }
    }
}