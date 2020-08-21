// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="Rgb"/>.
    /// </content>
    public partial class ColorSpaceConverter
    {
        private static readonly LinearRgbToRgbConverter LinearRgbToRgbConverter = new LinearRgbToRgbConverter();

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLab"/> into <see cref="Rgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLab> source, Span<Rgb> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref Rgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref Rgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToRgb(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLch"/> into <see cref="Rgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLch> source, Span<Rgb> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLch sourceRef = ref MemoryMarshal.GetReference(source);
            ref Rgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLch sp = ref Unsafe.Add(ref sourceRef, i);
                ref Rgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToRgb(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLchuv"/> into <see cref="Rgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLchuv> source, Span<Rgb> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLchuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref Rgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLchuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref Rgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToRgb(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLuv"/> into <see cref="Rgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLuv> source, Span<Rgb> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref Rgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref Rgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToRgb(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyy"/> into <see cref="Rgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieXyy> source, Span<Rgb> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieXyy sourceRef = ref MemoryMarshal.GetReference(source);
            ref Rgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyy sp = ref Unsafe.Add(ref sourceRef, i);
                ref Rgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToRgb(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyz"/> into <see cref="Rgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieXyz> source, Span<Rgb> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieXyz sourceRef = ref MemoryMarshal.GetReference(source);
            ref Rgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyz sp = ref Unsafe.Add(ref sourceRef, i);
                ref Rgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToRgb(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Cmyk"/> into <see cref="Rgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Cmyk> source, Span<Rgb> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Cmyk sourceRef = ref MemoryMarshal.GetReference(source);
            ref Rgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Cmyk sp = ref Unsafe.Add(ref sourceRef, i);
                ref Rgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToRgb(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsv"/> into <see cref="Rgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Hsv> source, Span<Rgb> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Hsv sourceRef = ref MemoryMarshal.GetReference(source);
            ref Rgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsv sp = ref Unsafe.Add(ref sourceRef, i);
                ref Rgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToRgb(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsl"/> into <see cref="Rgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Hsl> source, Span<Rgb> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Hsl sourceRef = ref MemoryMarshal.GetReference(source);
            ref Rgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsl sp = ref Unsafe.Add(ref sourceRef, i);
                ref Rgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToRgb(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="HunterLab"/> into <see cref="Rgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<HunterLab> source, Span<Rgb> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref HunterLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref Rgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref HunterLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref Rgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToRgb(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="LinearRgb"/> into <see cref="Rgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<LinearRgb> source, Span<Rgb> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref LinearRgb sourceRef = ref MemoryMarshal.GetReference(source);
            ref Rgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref LinearRgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref Rgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToRgb(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Lms"/> into <see cref="Rgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Lms> source, Span<Rgb> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Lms sourceRef = ref MemoryMarshal.GetReference(source);
            ref Rgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Lms sp = ref Unsafe.Add(ref sourceRef, i);
                ref Rgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToRgb(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="YCbCr"/> into <see cref="Rgb"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<YCbCr> source, Span<Rgb> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref YCbCr sourceRef = ref MemoryMarshal.GetReference(source);
            ref Rgb destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref YCbCr sp = ref Unsafe.Add(ref sourceRef, i);
                ref Rgb dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToRgb(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in CieXyz color)
        {
            // Conversion
            var linear = this.ToLinearRgb(color);

            // Compand
            return this.ToRgb(linear);
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in Cmyk color)
        {
            // Conversion
            return CmykAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in Hsv color)
        {
            // Conversion
            return HsvAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in Hsl color)
        {
            // Conversion
            return HslAndRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in LinearRgb color)
        {
            // Conversion
            return LinearRgbToRgbConverter.Convert(color);
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in Lms color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToRgb(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="Rgb"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Rgb"/></returns>
        public Rgb ToRgb(in YCbCr color)
        {
            // Conversion
            Rgb rgb = YCbCrAndRgbConverter.Convert(color);

            // Adaptation
            return this.Adapt(rgb);
        }
    }
}
