// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="Cmyk"/>.
    /// </content>
    public partial class ColorSpaceConverter
    {
        private static readonly CmykAndRgbConverter CmykAndRgbConverter = new CmykAndRgbConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLab"/> into <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLab> source, Span<Cmyk> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref Cmyk destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref Cmyk dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCmyk(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLch"/> into <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLch> source, Span<Cmyk> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLch sourceRef = ref MemoryMarshal.GetReference(source);
            ref Cmyk destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLch sp = ref Unsafe.Add(ref sourceRef, i);
                ref Cmyk dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCmyk(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLchuv"/> into <see cref="Cmyk"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLchuv> source, Span<Cmyk> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLchuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref Cmyk destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLchuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref Cmyk dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCmyk(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="Cmyk"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLuv"/> into <see cref="Cmyk"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLuv> source, Span<Cmyk> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref Cmyk destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref Cmyk dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCmyk(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyy"/> into <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieXyy> source, Span<Cmyk> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieXyy sourceRef = ref MemoryMarshal.GetReference(source);
            ref Cmyk destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyy sp = ref Unsafe.Add(ref sourceRef, i);
                ref Cmyk dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCmyk(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in CieXyz color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyz"/> into <see cref="Cmyk"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieXyz> source, Span<Cmyk> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieXyz sourceRef = ref MemoryMarshal.GetReference(source);
            ref Cmyk destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyz sp = ref Unsafe.Add(ref sourceRef, i);
                ref Cmyk dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCmyk(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in Hsl color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsl"/> into <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Hsl> source, Span<Cmyk> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Hsl sourceRef = ref MemoryMarshal.GetReference(source);
            ref Cmyk destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsl sp = ref Unsafe.Add(ref sourceRef, i);
                ref Cmyk dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCmyk(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in Hsv color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsv"/> into <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Hsv> source, Span<Cmyk> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Hsv sourceRef = ref MemoryMarshal.GetReference(source);
            ref Cmyk destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsv sp = ref Unsafe.Add(ref sourceRef, i);
                ref Cmyk dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCmyk(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="HunterLab"/> into <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<HunterLab> source, Span<Cmyk> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref HunterLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref Cmyk destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref HunterLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref Cmyk dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCmyk(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in LinearRgb color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="LinearRgb"/> into <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<LinearRgb> source, Span<Cmyk> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref LinearRgb sourceRef = ref MemoryMarshal.GetReference(source);
            ref Cmyk destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref LinearRgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref Cmyk dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCmyk(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in Lms color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCmyk(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Lms"/> into <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="source">The span to the source colors,</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Lms> source, Span<Cmyk> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Lms sourceRef = ref MemoryMarshal.GetReference(source);
            ref Cmyk destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Lms sp = ref Unsafe.Add(ref sourceRef, i);
                ref Cmyk dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCmyk(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in Rgb color) => CmykAndRgbConverter.Convert(color);

        /// <summary>
        /// Performs the bulk conversion from <see cref="Rgb"/> into <see cref="Cmyk"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Rgb> source, Span<Cmyk> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Rgb sourceRef = ref MemoryMarshal.GetReference(source);
            ref Cmyk destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Rgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref Cmyk dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCmyk(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Cmyk"/></returns>
        public Cmyk ToCmyk(in YCbCr color)
        {
            var rgb = this.ToRgb(color);

            return CmykAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="YCbCr"/> into <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<YCbCr> source, Span<Cmyk> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref YCbCr sourceRef = ref MemoryMarshal.GetReference(source);
            ref Cmyk destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref YCbCr sp = ref Unsafe.Add(ref sourceRef, i);
                ref Cmyk dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCmyk(sp);
            }
        }
    }
}
