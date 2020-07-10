// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="CieLch"/>.
    /// </content>
    public partial class ColorSpaceConverter
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
        public CieLch ToCieLch(in CieLab color)
        {
            CieLab adapted = this.Adapt(color);

            return CieLabToCieLchConverter.Convert(adapted);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLab"/> into <see cref="CieLch"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLab> source, Span<CieLch> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLch destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLchuv"/> into a <see cref="CieLch"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in CieLchuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLchuv"/> into <see cref="CieLch"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLchuv> source, Span<CieLch> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLchuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLch destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLchuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="CieLch"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in CieLuv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLuv"/> into <see cref="CieLch"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLuv> source, Span<CieLch> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLch destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="CieLch"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in CieXyy color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyy"/> into <see cref="CieLch"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieXyy> source, Span<CieLch> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieXyy sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLch destRef = ref MemoryMarshal.GetReference(destination);

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
        public CieLch ToCieLch(in CieXyz color)
        {
            var labColor = this.ToCieLab(color);

            return this.ToCieLch(labColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyz"/> into <see cref="CieLch"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieXyz> source, Span<CieLch> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieXyz sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLch destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyz sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="CieLch"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in Cmyk color)
        {
            var xyzColor = this.ToCieXyz(color);
            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Cmyk"/> into <see cref="CieLch"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Cmyk> source, Span<CieLch> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Cmyk sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLch destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Cmyk sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="CieLch"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in Hsl color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsl"/> into <see cref="CieLch"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Hsl> source, Span<CieLch> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Hsl sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLch destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsl sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="CieLch"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in Hsv color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsv"/> into <see cref="CieLch"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Hsv> source, Span<CieLch> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Hsv sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLch destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsv sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="CieLch"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in HunterLab color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="HunterLab"/> into <see cref="CieLch"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<HunterLab> source, Span<CieLch> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref HunterLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLch destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref HunterLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="CieLch"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in LinearRgb color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="LinearRgb"/> into <see cref="CieLch"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<LinearRgb> source, Span<CieLch> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref LinearRgb sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLch destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref LinearRgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="CieLch"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in Lms color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Lms"/> into <see cref="CieLch"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Lms> source, Span<CieLch> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Lms sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLch destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Lms sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="CieLch"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in Rgb color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Rgb"/> into <see cref="CieLch"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Rgb> source, Span<CieLch> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Rgb sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLch destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Rgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="CieLch"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieLch"/></returns>
        public CieLch ToCieLch(in YCbCr color)
        {
            var xyzColor = this.ToCieXyz(color);

            return this.ToCieLch(xyzColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="YCbCr"/> into <see cref="CieLch"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<YCbCr> source, Span<CieLch> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref YCbCr sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieLch destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref YCbCr sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieLch dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieLch(sp);
            }
        }
    }
}
