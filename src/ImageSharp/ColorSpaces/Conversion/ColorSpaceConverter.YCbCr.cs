// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion;

/// <content>
/// Allows conversion to <see cref="YCbCr"/>.
/// </content>
public partial class ColorSpaceConverter
{
    /// <summary>
    /// Performs the bulk conversion from <see cref="CieLab"/> into <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    public void Convert(ReadOnlySpan<CieLab> source, Span<YCbCr> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieLab sourceRef = ref MemoryMarshal.GetReference(source);
        ref YCbCr destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieLab sp = ref Unsafe.Add(ref sourceRef, i);
            ref YCbCr dp = ref Unsafe.Add(ref destRef, i);
            dp = this.ToYCbCr(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieLch"/> into <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    public void Convert(ReadOnlySpan<CieLch> source, Span<YCbCr> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieLch sourceRef = ref MemoryMarshal.GetReference(source);
        ref YCbCr destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieLch sp = ref Unsafe.Add(ref sourceRef, i);
            ref YCbCr dp = ref Unsafe.Add(ref destRef, i);
            dp = this.ToYCbCr(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieLuv"/> into <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<CieLuv> source, Span<YCbCr> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieLuv sourceRef = ref MemoryMarshal.GetReference(source);
        ref YCbCr destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieLuv sp = ref Unsafe.Add(ref sourceRef, i);
            ref YCbCr dp = ref Unsafe.Add(ref destRef, i);
            dp = this.ToYCbCr(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieXyy"/> into <see cref="YCbCr"/>
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<CieXyy> source, Span<YCbCr> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieXyy sourceRef = ref MemoryMarshal.GetReference(source);
        ref YCbCr destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieXyy sp = ref Unsafe.Add(ref sourceRef, i);
            ref YCbCr dp = ref Unsafe.Add(ref destRef, i);
            dp = this.ToYCbCr(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieXyz"/> into <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<CieXyz> source, Span<YCbCr> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieXyz sourceRef = ref MemoryMarshal.GetReference(source);
        ref YCbCr destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieXyz sp = ref Unsafe.Add(ref sourceRef, i);
            ref YCbCr dp = ref Unsafe.Add(ref destRef, i);
            dp = this.ToYCbCr(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Cmyk"/> into <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public static void Convert(ReadOnlySpan<Cmyk> source, Span<YCbCr> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Cmyk sourceRef = ref MemoryMarshal.GetReference(source);
        ref YCbCr destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Cmyk sp = ref Unsafe.Add(ref sourceRef, i);
            ref YCbCr dp = ref Unsafe.Add(ref destRef, i);
            dp = ToYCbCr(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Hsl"/> into <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public static void Convert(ReadOnlySpan<Hsl> source, Span<YCbCr> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Hsl sourceRef = ref MemoryMarshal.GetReference(source);
        ref YCbCr destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Hsl sp = ref Unsafe.Add(ref sourceRef, i);
            ref YCbCr dp = ref Unsafe.Add(ref destRef, i);
            dp = ToYCbCr(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Hsv"/> into <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public static void Convert(ReadOnlySpan<Hsv> source, Span<YCbCr> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Hsv sourceRef = ref MemoryMarshal.GetReference(source);
        ref YCbCr destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Hsv sp = ref Unsafe.Add(ref sourceRef, i);
            ref YCbCr dp = ref Unsafe.Add(ref destRef, i);
            dp = ToYCbCr(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="HunterLab"/> into <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<HunterLab> source, Span<YCbCr> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref HunterLab sourceRef = ref MemoryMarshal.GetReference(source);
        ref YCbCr destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref HunterLab sp = ref Unsafe.Add(ref sourceRef, i);
            ref YCbCr dp = ref Unsafe.Add(ref destRef, i);
            dp = this.ToYCbCr(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="LinearRgb"/> into <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public static void Convert(ReadOnlySpan<LinearRgb> source, Span<YCbCr> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref LinearRgb sourceRef = ref MemoryMarshal.GetReference(source);
        ref YCbCr destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref LinearRgb sp = ref Unsafe.Add(ref sourceRef, i);
            ref YCbCr dp = ref Unsafe.Add(ref destRef, i);
            dp = ToYCbCr(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Lms"/> into <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<Lms> source, Span<YCbCr> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Lms sourceRef = ref MemoryMarshal.GetReference(source);
        ref YCbCr destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Lms sp = ref Unsafe.Add(ref sourceRef, i);
            ref YCbCr dp = ref Unsafe.Add(ref destRef, i);
            dp = this.ToYCbCr(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Rgb"/> into <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public static void Convert(ReadOnlySpan<Rgb> source, Span<YCbCr> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Rgb sourceRef = ref MemoryMarshal.GetReference(source);
        ref YCbCr destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Rgb sp = ref Unsafe.Add(ref sourceRef, i);
            ref YCbCr dp = ref Unsafe.Add(ref destRef, i);
            dp = ToYCbCr(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="CieLab"/> into a <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="YCbCr"/></returns>
    public YCbCr ToYCbCr(in CieLab color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return this.ToYCbCr(xyzColor);
    }

    /// <summary>
    /// Converts a <see cref="CieLch"/> into a <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="YCbCr"/></returns>
    public YCbCr ToYCbCr(in CieLch color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return this.ToYCbCr(xyzColor);
    }

    /// <summary>
    /// Converts a <see cref="CieLuv"/> into a <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="YCbCr"/></returns>
    public YCbCr ToYCbCr(in CieLuv color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return this.ToYCbCr(xyzColor);
    }

    /// <summary>
    /// Converts a <see cref="CieXyy"/> into a <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="YCbCr"/></returns>
    public YCbCr ToYCbCr(in CieXyy color)
    {
        CieXyz xyzColor = ToCieXyz(color);

        return this.ToYCbCr(xyzColor);
    }

    /// <summary>
    /// Converts a <see cref="CieXyz"/> into a <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="YCbCr"/></returns>
    public YCbCr ToYCbCr(in CieXyz color)
    {
        Rgb rgb = this.ToRgb(color);

        return YCbCrAndRgbConverter.Convert(rgb);
    }

    /// <summary>
    /// Converts a <see cref="Cmyk"/> into a <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="YCbCr"/></returns>
    public static YCbCr ToYCbCr(in Cmyk color)
    {
        Rgb rgb = ToRgb(color);

        return YCbCrAndRgbConverter.Convert(rgb);
    }

    /// <summary>
    /// Converts a <see cref="Hsl"/> into a <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="YCbCr"/></returns>
    public static YCbCr ToYCbCr(in Hsl color)
    {
        Rgb rgb = ToRgb(color);

        return YCbCrAndRgbConverter.Convert(rgb);
    }

    /// <summary>
    /// Converts a <see cref="Hsv"/> into a <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="YCbCr"/></returns>
    public static YCbCr ToYCbCr(in Hsv color)
    {
        Rgb rgb = ToRgb(color);

        return YCbCrAndRgbConverter.Convert(rgb);
    }

    /// <summary>
    /// Converts a <see cref="HunterLab"/> into a <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="YCbCr"/></returns>
    public YCbCr ToYCbCr(in HunterLab color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return this.ToYCbCr(xyzColor);
    }

    /// <summary>
    /// Converts a <see cref="LinearRgb"/> into a <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="YCbCr"/></returns>
    public static YCbCr ToYCbCr(in LinearRgb color)
    {
        Rgb rgb = ToRgb(color);

        return YCbCrAndRgbConverter.Convert(rgb);
    }

    /// <summary>
    /// Converts a <see cref="Lms"/> into a <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="YCbCr"/></returns>
    public YCbCr ToYCbCr(in Lms color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);

        return this.ToYCbCr(xyzColor);
    }

    /// <summary>
    /// Converts a <see cref="Rgb"/> into a <see cref="YCbCr"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="YCbCr"/></returns>
    public static YCbCr ToYCbCr(in Rgb color) => YCbCrAndRgbConverter.Convert(color);
}
