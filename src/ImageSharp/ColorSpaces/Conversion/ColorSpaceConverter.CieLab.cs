// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion;

/// <content>
/// Allows conversion to <see cref="CieLab"/>.
/// </content>
public partial class ColorSpaceConverter
{
    /// <summary>
    /// Converts a <see cref="CieLch"/> into a <see cref="CieLab"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieLab"/></returns>
    public CieLab ToCieLab(in CieLch color)
    {
        // Conversion (preserving white point)
        CieLab unadapted = CieLchToCieLabConverter.Convert(color);

        return this.Adapt(unadapted);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieLch"/> into <see cref="CieLab"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<CieLch> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieLch sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieLab destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieLch sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieLab dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieLab(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="CieLchuv"/> into a <see cref="CieLab"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieLab"/></returns>
    public CieLab ToCieLab(in CieLchuv color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return this.ToCieLab(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieLchuv"/> into <see cref="CieLab"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<CieLchuv> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieLchuv sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieLab destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieLchuv sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieLab dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieLab(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="CieLuv"/> into a <see cref="CieLab"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieLab"/></returns>
    public CieLab ToCieLab(in CieLuv color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return this.ToCieLab(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieLuv"/> into <see cref="CieLab"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<CieLuv> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieLuv sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieLab destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieLuv sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieLab dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieLab(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="CieXyy"/> into a <see cref="CieLab"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieLab"/></returns>
    public CieLab ToCieLab(in CieXyy color)
    {
        CieXyz xyzColor = ToCieXyz(color);

        return this.ToCieLab(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieXyy"/> into <see cref="CieLab"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<CieXyy> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieXyy sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieLab destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieXyy sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieLab dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieLab(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="CieXyz"/> into a <see cref="CieLab"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieLab"/></returns>
    public CieLab ToCieLab(in CieXyz color)
    {
        CieXyz adapted = this.Adapt(color, this.whitePoint, this.targetLabWhitePoint);

        return this.cieXyzToCieLabConverter.Convert(adapted);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieXyz"/> into <see cref="CieLab"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<CieXyz> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieXyz sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieLab destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieXyz sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieLab dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieLab(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="Cmyk"/> into a <see cref="CieLab"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieLab"/></returns>
    public CieLab ToCieLab(in Cmyk color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);
        return this.ToCieLab(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Cmyk"/> into <see cref="CieLab"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<Cmyk> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Cmyk sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieLab destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Cmyk sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieLab dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieLab(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="Hsl"/> into a <see cref="CieLab"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieLab"/></returns>
    public CieLab ToCieLab(in Hsl color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return this.ToCieLab(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Hsl"/> into <see cref="CieLab"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<Hsl> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Hsl sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieLab destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Hsl sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieLab dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieLab(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="Hsv"/> into a <see cref="CieLab"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieLab"/></returns>
    public CieLab ToCieLab(in Hsv color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);
        return this.ToCieLab(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Hsv"/> into <see cref="CieLab"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<Hsv> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Hsv sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieLab destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Hsv sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieLab dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieLab(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="HunterLab"/> into a <see cref="CieLab"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieLab"/></returns>
    public CieLab ToCieLab(in HunterLab color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return this.ToCieLab(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="HunterLab"/> into <see cref="CieLab"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<HunterLab> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref HunterLab sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieLab destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref HunterLab sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieLab dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieLab(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="Lms"/> into a <see cref="CieLab"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieLab"/></returns>
    public CieLab ToCieLab(in Lms color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return this.ToCieLab(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Lms"/> into <see cref="CieLab"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<Lms> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Lms sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieLab destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Lms sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieLab dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieLab(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="LinearRgb"/> into a <see cref="CieLab"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieLab"/></returns>
    public CieLab ToCieLab(in LinearRgb color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return this.ToCieLab(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="LinearRgb"/> into <see cref="CieLab"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<LinearRgb> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref LinearRgb sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieLab destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref LinearRgb sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieLab dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieLab(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="Rgb"/> into a <see cref="CieLab"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieLab"/></returns>
    public CieLab ToCieLab(in Rgb color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return this.ToCieLab(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="LinearRgb"/> into <see cref="CieLab"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<Rgb> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Rgb sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieLab destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Rgb sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieLab dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieLab(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="YCbCr"/> into a <see cref="CieLab"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieLab"/></returns>
    public CieLab ToCieLab(in YCbCr color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return this.ToCieLab(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="YCbCr"/> into <see cref="CieLab"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<YCbCr> source, Span<CieLab> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref YCbCr sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieLab destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref YCbCr sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieLab dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieLab(sp);
        }
    }
}
