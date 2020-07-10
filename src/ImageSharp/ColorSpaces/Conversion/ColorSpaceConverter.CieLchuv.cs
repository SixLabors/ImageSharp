// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="CieLchuv"/>.
    /// </content>
    public partial class ColorSpaceConverter
    {
        /// <summary>
        /// The converter for converting between CieLab to CieLchuv.
        /// </summary>
        private static readonly CieLuvToCieLchuvConverter CieLuvToCieLchuvConverter = new CieLuvToCieLchuvConverter();

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLab"/> into <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLab> source, Span<CieLchuv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLchuv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLchuv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLchuv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLch"/> into <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLch> source, Span<CieLchuv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLch sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLchuv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLch sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLchuv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLchuv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in CieLuv color)
        {
            CieLuv adapted = this.Adapt(color);

            return CieLuvToCieLchuvConverter.Convert(adapted);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLuv"/> into <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLuv> source, Span<CieLchuv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLchuv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLchuv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLchuv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyy"/> into <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieXyy> source, Span<CieLchuv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieXyy sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLchuv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyy sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLchuv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLchuv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in CieXyz color)
        {
            var luvColor = this.ToCieLuv(color);

            return this.ToCieLchuv(luvColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyz"/> into <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieXyz> source, Span<CieLchuv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieXyz sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLchuv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyz sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLchuv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLchuv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in Cmyk color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Cmyk"/> into <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Cmyk> source, Span<CieLchuv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Cmyk sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLchuv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Cmyk sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLchuv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLchuv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in Hsl color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsl"/> into <see cref="CieLchuv"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Hsl> source, Span<CieLchuv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Hsl sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLchuv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsl sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLchuv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLchuv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in Hsv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsv"/> into <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Hsv> source, Span<CieLchuv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Hsv sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLchuv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsv sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLchuv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLchuv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="HunterLab"/> into <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<HunterLab> source, Span<CieLchuv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref HunterLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLchuv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref HunterLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLchuv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLchuv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in LinearRgb color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="LinearRgb"/> into <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<LinearRgb> source, Span<CieLchuv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref LinearRgb sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLchuv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref LinearRgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLchuv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLchuv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in Lms color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Lms"/> into <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Lms> source, Span<CieLchuv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Lms sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLchuv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Lms sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLchuv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLchuv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in Rgb color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Rgb"/> into <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Rgb> source, Span<CieLchuv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Rgb sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLchuv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Rgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLchuv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLchuv(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLchuv"/></returns>
        public CieLchuv ToCieLchuv(in YCbCr color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLchuv(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="YCbCr"/> into <see cref="CieLchuv"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<YCbCr> source, Span<CieLchuv> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref YCbCr sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLchuv destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref YCbCr sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLchuv dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLchuv(sp);
            }
        }
    }
}
