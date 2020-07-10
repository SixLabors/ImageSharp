// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="Hsv"/>.
    /// </content>
    public partial class ColorSpaceConverter
    {
        private static readonly HsvAndRgbConverter HsvAndRgbConverter = new HsvAndRgbConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLab"/> into <see cref="Hsv"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLab> source, Span<Hsv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref Hsv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref Hsv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToHsv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLch"/> into <see cref="Hsv"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLch> source, Span<Hsv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLch sourceRef = ref MemoryMarshal.GetReference(source);
            ref Hsv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLch sp = ref Unsafe.Add(ref sourceRef, i);
                ref Hsv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToHsv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLchuv"/> into <see cref="Hsv"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLchuv> source, Span<Hsv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLchuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref Hsv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLchuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref Hsv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToHsv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLuv"/> into <see cref="Hsv"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLuv> source, Span<Hsv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref Hsv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref Hsv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToHsv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyy"/> into <see cref="Hsv"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieXyy> source, Span<Hsv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieXyy sourceRef = ref MemoryMarshal.GetReference(source);
            ref Hsv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyy sp = ref Unsafe.Add(ref sourceRef, i);
                ref Hsv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToHsv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in CieXyz color)
        {
            var rgb = this.ToRgb(color);

            return HsvAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyz"/> into <see cref="Hsv"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieXyz> source, Span<Hsv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieXyz sourceRef = ref MemoryMarshal.GetReference(source);
            ref Hsv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyz sp = ref Unsafe.Add(ref sourceRef, i);
                ref Hsv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToHsv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in Cmyk color)
        {
            var rgb = this.ToRgb(color);

            return HsvAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Cmyk"/> into <see cref="Hsv"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Cmyk> source, Span<Hsv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Cmyk sourceRef = ref MemoryMarshal.GetReference(source);
            ref Hsv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Cmyk sp = ref Unsafe.Add(ref sourceRef, i);
                ref Hsv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToHsv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in Hsl color)
        {
            var rgb = this.ToRgb(color);

            return HsvAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsl"/> into <see cref="Hsv"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors.</param>
        public void Convert(ReadOnlySpan<Hsl> source, Span<Hsv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Hsl sourceRef = ref MemoryMarshal.GetReference(source);
            ref Hsv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsl sp = ref Unsafe.Add(ref sourceRef, i);
                ref Hsv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToHsv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="HunterLab"/> into <see cref="Hsv"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<HunterLab> source, Span<Hsv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref HunterLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref Hsv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref HunterLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref Hsv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToHsv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in LinearRgb color)
        {
            var rgb = this.ToRgb(color);

            return HsvAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="LinearRgb"/> into <see cref="Hsv"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<LinearRgb> source, Span<Hsv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref LinearRgb sourceRef = ref MemoryMarshal.GetReference(source);
            ref Hsv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref LinearRgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref Hsv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToHsv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(Lms color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToHsv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Lms"/> into <see cref="Hsv"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Lms> source, Span<Hsv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Lms sourceRef = ref MemoryMarshal.GetReference(source);
            ref Hsv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Lms sp = ref Unsafe.Add(ref sourceRef, i);
                ref Hsv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToHsv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in Rgb color) => HsvAndRgbConverter.Convert(color);

        /// <summary>
        /// Performs the bulk conversion from <see cref="Rgb"/> into <see cref="Hsv"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Rgb> source, Span<Hsv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Rgb sourceRef = ref MemoryMarshal.GetReference(source);
            ref Hsv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Rgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref Hsv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToHsv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="Hsv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Hsv"/></returns>
        public Hsv ToHsv(in YCbCr color)
        {
            var rgb = this.ToRgb(color);

            return HsvAndRgbConverter.Convert(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="YCbCr"/> into <see cref="Hsv"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<YCbCr> source, Span<Hsv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref YCbCr sourceRef = ref MemoryMarshal.GetReference(source);
            ref Hsv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref YCbCr sp = ref Unsafe.Add(ref sourceRef, i);
                ref Hsv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToHsv(sp);
            }
        }
    }
}
