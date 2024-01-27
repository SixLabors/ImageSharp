// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion;

/// <content>
/// Allows conversion to <see cref="CieXyy"/>.
/// </content>
public partial class ColorSpaceConverter
{
    /// <summary>
    /// Converts a <see cref="CieLab"/> into a <see cref="CieXyy"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieXyy"/></returns>
    public CieXyy ToCieXyy(in CieLab color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return ToCieXyy(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieLab"/> into <see cref="CieXyy"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<CieLab> source, Span<CieXyy> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieLab sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieXyy destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieLab sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieXyy dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieXyy(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="CieLch"/> into a <see cref="CieXyy"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieXyy"/></returns>
    public CieXyy ToCieXyy(in CieLch color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return ToCieXyy(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieLch"/> into <see cref="CieXyy"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<CieLch> source, Span<CieXyy> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieLch sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieXyy destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieLch sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieXyy dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieXyy(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="CieLchuv"/> into a <see cref="CieXyy"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieXyy"/></returns>
    public CieXyy ToCieXyy(in CieLchuv color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return ToCieXyy(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieLchuv"/> into <see cref="CieXyy"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<CieLchuv> source, Span<CieXyy> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieLchuv sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieXyy destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieLchuv sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieXyy dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieXyy(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="CieLuv"/> into a <see cref="CieXyy"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieXyy"/></returns>
    public CieXyy ToCieXyy(in CieLuv color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return ToCieXyy(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieLuv"/> into <see cref="CieXyy"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<CieLuv> source, Span<CieXyy> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieLuv sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieXyy destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieLuv sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieXyy dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieXyy(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="CieXyz"/> into a <see cref="CieXyy"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieXyy"/></returns>
    public static CieXyy ToCieXyy(in CieXyz color) => CieXyzAndCieXyyConverter.Convert(color);

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieXyz"/> into <see cref="CieXyy"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public static void Convert(ReadOnlySpan<CieXyz> source, Span<CieXyy> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieXyz sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieXyy destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieXyz sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieXyy dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = ToCieXyy(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="Cmyk"/> into a <see cref="CieXyy"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieXyy"/></returns>
    public CieXyy ToCieXyy(in Cmyk color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return ToCieXyy(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Cmyk"/> into <see cref="CieXyy"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<Cmyk> source, Span<CieXyy> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Cmyk sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieXyy destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Cmyk sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieXyy dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieXyy(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="Hsl"/> into a <see cref="CieXyy"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieXyy"/></returns>
    public CieXyy ToCieXyy(Hsl color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return ToCieXyy(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Hsl"/> into <see cref="CieXyy"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<Hsl> source, Span<CieXyy> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Hsl sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieXyy destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Hsl sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieXyy dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieXyy(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="Hsv"/> into a <see cref="CieXyy"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieXyy"/></returns>
    public CieXyy ToCieXyy(in Hsv color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return ToCieXyy(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Hsv"/> into <see cref="CieXyy"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<Hsv> source, Span<CieXyy> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Hsv sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieXyy destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Hsv sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieXyy dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieXyy(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="HunterLab"/> into a <see cref="CieXyy"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieXyy"/></returns>
    public CieXyy ToCieXyy(in HunterLab color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return ToCieXyy(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="HunterLab"/> into <see cref="CieXyy"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<HunterLab> source, Span<CieXyy> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref HunterLab sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieXyy destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref HunterLab sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieXyy dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieXyy(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="LinearRgb"/> into a <see cref="CieXyy"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieXyy"/></returns>
    public CieXyy ToCieXyy(in LinearRgb color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return ToCieXyy(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="LinearRgb"/> into <see cref="CieXyy"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<LinearRgb> source, Span<CieXyy> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref LinearRgb sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieXyy destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref LinearRgb sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieXyy dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieXyy(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="Lms"/> into a <see cref="CieXyy"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieXyy"/></returns>
    public CieXyy ToCieXyy(in Lms color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return ToCieXyy(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Lms"/> into <see cref="CieXyy"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<Lms> source, Span<CieXyy> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Lms sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieXyy destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Lms sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieXyy dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieXyy(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="Rgb"/> into a <see cref="CieXyy"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieXyy"/></returns>
    public CieXyy ToCieXyy(in Rgb color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return ToCieXyy(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Rgb"/> into <see cref="CieXyy"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<Rgb> source, Span<CieXyy> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Rgb sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieXyy destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Rgb sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieXyy dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieXyy(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="YCbCr"/> into a <see cref="CieXyy"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="CieXyy"/></returns>
    public CieXyy ToCieXyy(in YCbCr color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return ToCieXyy(xyzColor);
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="YCbCr"/> into <see cref="CieXyy"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<YCbCr> source, Span<CieXyy> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref YCbCr sourceRef = ref MemoryMarshal.GetReference(source);
        ref CieXyy destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref YCbCr sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref CieXyy dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToCieXyy(sp);
        }
    }
}
