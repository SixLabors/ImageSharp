// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="LinearRgb"/>.
    /// </content>
    internal partial class ColorSpaceConverter
    {
        private static readonly RgbToLinearRgbConverter RgbToLinearRgbConverter = new RgbToLinearRgbConverter();

        private CieXyzToLinearRgbConverter cieXyzToLinearRgbConverter;

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(in CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLab"/> into <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<CieLab> source, Span<LinearRgb> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref CieLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref LinearRgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLinearRgb(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(in CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLch"/> into <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<CieLch> source, Span<LinearRgb> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref CieLch sourceRef = ref MemoryMarshal.GetReference(source);
            ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLch sp = ref Unsafe.Add(ref sourceRef, i);
                ref LinearRgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLinearRgb(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(in CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLchuv"/> into <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<CieLchuv> source, Span<LinearRgb> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref CieLchuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLchuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref LinearRgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLinearRgb(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(in CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLuv"/> into <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<CieLuv> source, Span<LinearRgb> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref CieLuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref LinearRgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLinearRgb(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(in CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyy"/> into <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<CieXyy> source, Span<LinearRgb> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref CieXyy sourceRef = ref MemoryMarshal.GetReference(source);
            ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyy sp = ref Unsafe.Add(ref sourceRef, i);
                ref LinearRgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLinearRgb(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(in CieXyz color)
        {
            // Adaptation
            CieXyz adapted = this.TargetRgbWorkingSpace.WhitePoint.Equals(this.WhitePoint) || !this.IsChromaticAdaptationPerformed
                ? color
                : this.ChromaticAdaptation.Transform(color, this.WhitePoint, this.TargetRgbWorkingSpace.WhitePoint);

            // Conversion
            CieXyzToLinearRgbConverter xyzConverter = this.GetCieXyxToLinearRgbConverter(this.TargetRgbWorkingSpace);
            return xyzConverter.Convert(adapted);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyz"/> into <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<CieXyz> source, Span<LinearRgb> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref CieXyz sourceRef = ref MemoryMarshal.GetReference(source);
            ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyz sp = ref Unsafe.Add(ref sourceRef, i);
                ref LinearRgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLinearRgb(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(in Cmyk color)
        {
            var rgb = this.ToRgb(color);
            return this.ToLinearRgb(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Cmyk"/> into <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<Cmyk> source, Span<LinearRgb> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref Cmyk sourceRef = ref MemoryMarshal.GetReference(source);
            ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Cmyk sp = ref Unsafe.Add(ref sourceRef, i);
                ref LinearRgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLinearRgb(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(in Hsl color)
        {
            var rgb = this.ToRgb(color);
            return this.ToLinearRgb(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsl"/> into <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<Hsl> source, Span<LinearRgb> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref Hsl sourceRef = ref MemoryMarshal.GetReference(source);
            ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsl sp = ref Unsafe.Add(ref sourceRef, i);
                ref LinearRgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLinearRgb(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(in Hsv color)
        {
            var rgb = this.ToRgb(color);
            return this.ToLinearRgb(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsv"/> into <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<Hsv> source, Span<LinearRgb> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref Hsv sourceRef = ref MemoryMarshal.GetReference(source);
            ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsv sp = ref Unsafe.Add(ref sourceRef, i);
                ref LinearRgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLinearRgb(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(in HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="HunterLab"/> into <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<HunterLab> source, Span<LinearRgb> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref HunterLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref HunterLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref LinearRgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLinearRgb(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(in Lms color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLinearRgb(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Lms"/> into <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<Lms> source, Span<LinearRgb> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref Lms sourceRef = ref MemoryMarshal.GetReference(source);
            ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Lms sp = ref Unsafe.Add(ref sourceRef, i);
                ref LinearRgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLinearRgb(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(in Rgb color)
        {
            // Conversion
            return RgbToLinearRgbConverter.Convert(color);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Lms"/> into <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<Rgb> source, Span<LinearRgb> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref Rgb sourceRef = ref MemoryMarshal.GetReference(source);
            ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Rgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref LinearRgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLinearRgb(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="LinearRgb"/></returns>
        public LinearRgb ToLinearRgb(in YCbCr color)
        {
            var rgb = this.ToRgb(color);
            return this.ToLinearRgb(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="YCbCr"/> into <see cref="LinearRgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        /// <param name="count">The number of colors to convert.</param>
        public void Convert(Span<YCbCr> source, Span<LinearRgb> destination, int count)
        {
            Guard.SpansMustBeSizedAtLeast(source, nameof(source), destination, nameof(destination), count);

            ref YCbCr sourceRef = ref MemoryMarshal.GetReference(source);
            ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref YCbCr sp = ref Unsafe.Add(ref sourceRef, i);
                ref LinearRgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLinearRgb(sp);
            }
        }

        /// <summary>
        /// Gets the correct converter for the given rgb working space.
        /// </summary>
        /// <param name="workingSpace">The target working space</param>
        /// <returns>The <see cref="CieXyzToLinearRgbConverter"/></returns>
        private CieXyzToLinearRgbConverter GetCieXyxToLinearRgbConverter(RgbWorkingSpace workingSpace)
        {
            if (this.cieXyzToLinearRgbConverter != null && this.cieXyzToLinearRgbConverter.TargetWorkingSpace.Equals(workingSpace))
            {
                return this.cieXyzToLinearRgbConverter;
            }

            return this.cieXyzToLinearRgbConverter = new CieXyzToLinearRgbConverter(workingSpace);
        }
    }
}