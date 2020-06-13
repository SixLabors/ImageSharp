// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <content>
    /// Allows conversion to <see cref="CieXyz"/>.
    /// </content>
    public partial class ColorSpaceConverter
    {
        private static readonly CieLabToCieXyzConverter CieLabToCieXyzConverter = new CieLabToCieXyzConverter();

        private static readonly CieLuvToCieXyzConverter CieLuvToCieXyzConverter = new CieLuvToCieXyzConverter();

        private static readonly HunterLabToCieXyzConverter
            HunterLabToCieXyzConverter = new HunterLabToCieXyzConverter();

        private LinearRgbToCieXyzConverter linearRgbToCieXyzConverter;

        /// <summary>
        /// Converts a <see cref="CieLab"/> into a <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in CieLab color)
        {
            // Conversion
            CieXyz unadapted = CieLabToCieXyzConverter.Convert(color);

            // Adaptation
            return this.Adapt(unadapted, color.WhitePoint);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLab"/> into <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLab> source, Span<CieXyz> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieXyz destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieXyz dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieXyz(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLch"/> into a <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in CieLch color)
        {
            // Conversion to Lab
            CieLab labColor = CieLchToCieLabConverter.Convert(color);

            // Conversion to XYZ (incl. adaptation)
            return this.ToCieXyz(labColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLch"/> into <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLch> source, Span<CieXyz> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLch sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieXyz destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLch sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieXyz dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieXyz(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in CieLchuv color)
        {
            // Conversion to Luv
            CieLuv luvColor = CieLchuvToCieLuvConverter.Convert(color);

            // Conversion to XYZ (incl. adaptation)
            return this.ToCieXyz(luvColor);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLchuv"/> into <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLchuv> source, Span<CieXyz> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLchuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieXyz destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLchuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieXyz dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieXyz(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieLuv"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in CieLuv color)
        {
            // Conversion
            CieXyz unadapted = CieLuvToCieXyzConverter.Convert(color);

            // Adaptation
            return this.Adapt(unadapted, color.WhitePoint);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieLuv"/> into <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieLuv> source, Span<CieXyz> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieLuv sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieXyz destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieLuv sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieXyz dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieXyz(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="CieXyy"/> into a <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in CieXyy color)
        {
            // Conversion
            return CieXyzAndCieXyyConverter.Convert(color);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="CieXyy"/> into <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<CieXyy> source, Span<CieXyz> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref CieXyy sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieXyz destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref CieXyy sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieXyz dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieXyz(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Cmyk"/> into a <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in Cmyk color)
        {
            var rgb = this.ToRgb(color);

            return this.ToCieXyz(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Cmyk"/> into <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Cmyk> source, Span<CieXyz> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Cmyk sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieXyz destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Cmyk sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieXyz dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieXyz(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Hsl"/> into a <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in Hsl color)
        {
            var rgb = this.ToRgb(color);

            return this.ToCieXyz(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsl"/> into <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="source">The span to the source colors.</param>
        /// <param name="destination">The span to the destination colors.</param>
        public void Convert(ReadOnlySpan<Hsl> source, Span<CieXyz> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Hsl sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieXyz destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsl sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieXyz dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieXyz(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Hsv"/> into a <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in Hsv color)
        {
            // Conversion
            var rgb = this.ToRgb(color);

            return this.ToCieXyz(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Hsv"/> into <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Hsv> source, Span<CieXyz> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Hsv sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieXyz destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Hsv sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieXyz dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieXyz(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="HunterLab"/> into a <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in HunterLab color)
        {
            CieXyz unadapted = HunterLabToCieXyzConverter.Convert(color);

            return this.Adapt(unadapted, color.WhitePoint);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="HunterLab"/> into <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<HunterLab> source, Span<CieXyz> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref HunterLab sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieXyz destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref HunterLab sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieXyz dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieXyz(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="LinearRgb"/> into a <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in LinearRgb color)
        {
            // Conversion
            LinearRgbToCieXyzConverter converter = this.GetLinearRgbToCieXyzConverter(color.WorkingSpace);
            CieXyz unadapted = converter.Convert(color);

            return this.Adapt(unadapted, color.WorkingSpace.WhitePoint);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="LinearRgb"/> into <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="source">The span to the source colors.</param>
        /// <param name="destination">The span to the destination colors.</param>
        public void Convert(ReadOnlySpan<LinearRgb> source, Span<CieXyz> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref LinearRgb sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieXyz destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref LinearRgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieXyz dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieXyz(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Lms"/> into a <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in Lms color)
        {
            return this.cieXyzAndLmsConverter.Convert(color);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Lms"/> into <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Lms> source, Span<CieXyz> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Lms sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieXyz destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Lms sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieXyz dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieXyz(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="Rgb"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in Rgb color)
        {
            // Conversion
            LinearRgb linear = RgbToLinearRgbConverter.Convert(color);
            return this.ToCieXyz(linear);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="Rgb"/> into <see cref="CieXyz"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<Rgb> source, Span<CieXyz> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref Rgb sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieXyz destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref Rgb sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieXyz dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieXyz(sp);
            }
        }

        /// <summary>
        /// Converts a <see cref="YCbCr"/> into a <see cref="CieXyz"/>
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>The <see cref="CieXyz"/></returns>
        public CieXyz ToCieXyz(in YCbCr color)
        {
            var rgb = this.ToRgb(color);

            return this.ToCieXyz(rgb);
        }

        /// <summary>
        /// Performs the bulk conversion from <see cref="YCbCr"/> into <see cref="CieXyz"/>
        /// </summary>
        /// <param name="source">The span to the source colors</param>
        /// <param name="destination">The span to the destination colors</param>
        public void Convert(ReadOnlySpan<YCbCr> source, Span<CieXyz> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
            int count = source.Length;

            ref YCbCr sourceRef = ref MemoryMarshal.GetReference(source);
            ref CieXyz destRef = ref MemoryMarshal.GetReference(destination);

            for (int i = 0; i < count; i++)
            {
                ref YCbCr sp = ref Unsafe.Add(ref sourceRef, i);
                ref CieXyz dp = ref Unsafe.Add(ref destRef, i);
                dp = this.ToCieXyz(sp);
            }
        }

        /// <summary>
        /// Gets the correct converter for the given rgb working space.
        /// </summary>
        /// <param name="workingSpace">The source working space</param>
        /// <returns>The <see cref="LinearRgbToCieXyzConverter"/></returns>
        private LinearRgbToCieXyzConverter GetLinearRgbToCieXyzConverter(RgbWorkingSpace workingSpace)
        {
            if (this.linearRgbToCieXyzConverter?.SourceWorkingSpace.Equals(workingSpace) == true)
            {
                return this.linearRgbToCieXyzConverter;
            }

            return this.linearRgbToCieXyzConverter = new LinearRgbToCieXyzConverter(workingSpace);
        }
    }
}
