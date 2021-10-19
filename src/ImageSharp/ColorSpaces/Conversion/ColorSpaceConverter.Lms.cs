﻿// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="Lms"/>.
    /// </content>
    public partial class ColorSpaceConverter
    {
        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLab"/> into <see cref="Lms"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLab> source, Span<Lms> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref Lms destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref Lms dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLms(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLch"/> into <see cref="Lms"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLch> source, Span<Lms> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLch sourceRef = ref MemoryMarshal.GetReference(source);
            ref Lms destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLch sp = ref Unsafe.Add(ref sourceRef, i);
                ref Lms dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLms(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLchuv"/> into <see cref="Lms"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLchuv> source, Span<Lms> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLchuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref Lms destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLchuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref Lms dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLms(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLuv"/> into <see cref="Lms"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLuv> source, Span<Lms> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref Lms destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref Lms dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLms(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyy"/> into <see cref="Lms"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieXyy> source, Span<Lms> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieXyy sourceRef = ref MemoryMarshal.GetReference(source);
            ref Lms destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyy sp = ref Unsafe.Add(ref sourceRef, i);
                ref Lms dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLms(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyz"/> into <see cref="Lms"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieXyz> source, Span<Lms> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieXyz sourceRef = ref MemoryMarshal.GetReference(source);
            ref Lms destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyz sp = ref Unsafe.Add(ref sourceRef, i);
                ref Lms dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLms(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Cmyk"/> into <see cref="Lms"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Cmyk> source, Span<Lms> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Cmyk sourceRef = ref MemoryMarshal.GetReference(source);
            ref Lms destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Cmyk sp = ref Unsafe.Add(ref sourceRef, i);
                ref Lms dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLms(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsl"/> into <see cref="Lms"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Hsl> source, Span<Lms> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Hsl sourceRef = ref MemoryMarshal.GetReference(source);
            ref Lms destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsl sp = ref Unsafe.Add(ref sourceRef, i);
                ref Lms dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLms(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsv"/> into <see cref="Lms"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Hsv> source, Span<Lms> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Hsv sourceRef = ref MemoryMarshal.GetReference(source);
            ref Lms destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsv sp = ref Unsafe.Add(ref sourceRef, i);
                ref Lms dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLms(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="HunterLab"/> into <see cref="Lms"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<HunterLab> source, Span<Lms> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref HunterLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref Lms destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref HunterLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref Lms dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLms(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="LinearRgb"/> into <see cref="Lms"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<LinearRgb> source, Span<Lms> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref LinearRgb sourceRef = ref MemoryMarshal.GetReference(source);
            ref Lms destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref LinearRgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref Lms dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLms(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Rgb"/> into <see cref="Lms"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Rgb> source, Span<Lms> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Rgb sourceRef = ref MemoryMarshal.GetReference(source);
            ref Lms destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Rgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref Lms dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLms(sp);
            }
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="YCbCr"/> into <see cref="Lms"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<YCbCr> source, Span<Lms> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref YCbCr sourceRef = ref MemoryMarshal.GetReference(source);
            ref Lms destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref YCbCr sp = ref Unsafe.Add(ref sourceRef, i);
                ref Lms dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToLms(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(in CieLab color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(in CieLch color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(in CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(in CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(in CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="CieXyz"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(in CieXyz color) => this.cieXyzAndLmsConverter.Convert(color);

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(in Cmyk color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(in Hsl color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(in Hsv color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(in HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(in LinearRgb color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(in Rgb color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="Lms"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="Lms"/></returns>
        public Lms ToLms(in YCbCr color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToLms(xyzColor);
        }
    }
}
