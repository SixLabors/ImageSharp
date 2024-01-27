// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion;

/// <content>
/// Allows conversion to <see cref="LinearRgb"/>.
/// </content>
public partial class ColorSpaceConverter
{
    /// <summary>
    /// Performs the bulk conversion from <see cref="CieLab"/> into <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    public void Convert(ReadOnlySpan<CieLab> source, Span<LinearRgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieLab sourceRef = ref MemoryMarshal.GetReference(source);
        ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieLab sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref LinearRgb dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToLinearRgb(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieLch"/> into <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    public void Convert(ReadOnlySpan<CieLch> source, Span<LinearRgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieLch sourceRef = ref MemoryMarshal.GetReference(source);
        ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieLch sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref LinearRgb dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToLinearRgb(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieLchuv"/> into <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    public void Convert(ReadOnlySpan<CieLchuv> source, Span<LinearRgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieLchuv sourceRef = ref MemoryMarshal.GetReference(source);
        ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieLchuv sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref LinearRgb dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToLinearRgb(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieLuv"/> into <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    public void Convert(ReadOnlySpan<CieLuv> source, Span<LinearRgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieLuv sourceRef = ref MemoryMarshal.GetReference(source);
        ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieLuv sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref LinearRgb dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToLinearRgb(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieXyy"/> into <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    public void Convert(ReadOnlySpan<CieXyy> source, Span<LinearRgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieXyy sourceRef = ref MemoryMarshal.GetReference(source);
        ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieXyy sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref LinearRgb dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToLinearRgb(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="CieXyz"/> into <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    public void Convert(ReadOnlySpan<CieXyz> source, Span<LinearRgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref CieXyz sourceRef = ref MemoryMarshal.GetReference(source);
        ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref CieXyz sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref LinearRgb dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToLinearRgb(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Cmyk"/> into <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    public static void Convert(ReadOnlySpan<Cmyk> source, Span<LinearRgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Cmyk sourceRef = ref MemoryMarshal.GetReference(source);
        ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Cmyk sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref LinearRgb dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = ToLinearRgb(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Hsl"/> into <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    public static void Convert(ReadOnlySpan<Hsl> source, Span<LinearRgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Hsl sourceRef = ref MemoryMarshal.GetReference(source);
        ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Hsl sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref LinearRgb dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = ToLinearRgb(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Hsv"/> into <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    public static void Convert(ReadOnlySpan<Hsv> source, Span<LinearRgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Hsv sourceRef = ref MemoryMarshal.GetReference(source);
        ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Hsv sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref LinearRgb dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = ToLinearRgb(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="HunterLab"/> into <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    public void Convert(ReadOnlySpan<HunterLab> source, Span<LinearRgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref HunterLab sourceRef = ref MemoryMarshal.GetReference(source);
        ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref HunterLab sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref LinearRgb dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToLinearRgb(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Lms"/> into <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="source">The span to the source colors.</param>
    /// <param name="destination">The span to the destination colors.</param>
    public void Convert(ReadOnlySpan<Lms> source, Span<LinearRgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Lms sourceRef = ref MemoryMarshal.GetReference(source);
        ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Lms sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref LinearRgb dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToLinearRgb(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="Rgb"/> into <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public static void Convert(ReadOnlySpan<Rgb> source, Span<LinearRgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref Rgb sourceRef = ref MemoryMarshal.GetReference(source);
        ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref Rgb sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref LinearRgb dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = ToLinearRgb(sp);
        }
    }

    /// <summary>
    /// Performs the bulk conversion from <see cref="YCbCr"/> into <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="source">The span to the source colors</param>
    /// <param name="destination">The span to the destination colors</param>
    public void Convert(ReadOnlySpan<YCbCr> source, Span<LinearRgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        int count = source.Length;

        ref YCbCr sourceRef = ref MemoryMarshal.GetReference(source);
        ref LinearRgb destRef = ref MemoryMarshal.GetReference(destination);

        for (nuint i = 0; i < (uint)count; i++)
        {
            ref YCbCr sp = ref Extensions.UnsafeAdd(ref sourceRef, i);
            ref LinearRgb dp = ref Extensions.UnsafeAdd(ref destRef, i);
            dp = this.ToLinearRgb(sp);
        }
    }

    /// <summary>
    /// Converts a <see cref="CieLab"/> into a <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="LinearRgb"/></returns>
    public LinearRgb ToLinearRgb(in CieLab color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);
        return this.ToLinearRgb(xyzColor);
    }

    /// <summary>
    /// Converts a <see cref="CieLch"/> into a <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="LinearRgb"/></returns>
    public LinearRgb ToLinearRgb(in CieLch color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);
        return this.ToLinearRgb(xyzColor);
    }

    /// <summary>
    /// Converts a <see cref="CieLchuv"/> into a <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="LinearRgb"/></returns>
    public LinearRgb ToLinearRgb(in CieLchuv color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);
        return this.ToLinearRgb(xyzColor);
    }

    /// <summary>
    /// Converts a <see cref="CieLuv"/> into a <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="LinearRgb"/></returns>
    public LinearRgb ToLinearRgb(in CieLuv color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);
        return this.ToLinearRgb(xyzColor);
    }

    /// <summary>
    /// Converts a <see cref="CieXyy"/> into a <see cref="LinearRgb"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="LinearRgb"/></returns>
    public LinearRgb ToLinearRgb(in CieXyy color)
    {
        CieXyz xyzColor = ToCieXyz(color);
        return this.ToLinearRgb(xyzColor);
    }

    /// <summary>
    /// Converts a <see cref="CieXyz"/> into a <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="LinearRgb"/></returns>
    public LinearRgb ToLinearRgb(in CieXyz color)
    {
        // Adaptation
        CieXyz adapted = this.Adapt(color, this.whitePoint, this.targetRgbWorkingSpace.WhitePoint);

        // Conversion
        return this.cieXyzToLinearRgbConverter.Convert(adapted);
    }

    /// <summary>
    /// Converts a <see cref="Cmyk"/> into a <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="LinearRgb"/></returns>
    public static LinearRgb ToLinearRgb(in Cmyk color)
    {
        Rgb rgb = ToRgb(color);
        return ToLinearRgb(rgb);
    }

    /// <summary>
    /// Converts a <see cref="Hsl"/> into a <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="LinearRgb"/></returns>
    public static LinearRgb ToLinearRgb(in Hsl color)
    {
        Rgb rgb = ToRgb(color);
        return ToLinearRgb(rgb);
    }

    /// <summary>
    /// Converts a <see cref="Hsv"/> into a <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="LinearRgb"/></returns>
    public static LinearRgb ToLinearRgb(in Hsv color)
    {
        Rgb rgb = ToRgb(color);
        return ToLinearRgb(rgb);
    }

    /// <summary>
    /// Converts a <see cref="HunterLab"/> into a <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="LinearRgb"/></returns>
    public LinearRgb ToLinearRgb(in HunterLab color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);
        return this.ToLinearRgb(xyzColor);
    }

    /// <summary>
    /// Converts a <see cref="Lms"/> into a <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="LinearRgb"/></returns>
    public LinearRgb ToLinearRgb(in Lms color)
    {
        CieXyz xyzColor = this.ToCieXyz(color);
        return this.ToLinearRgb(xyzColor);
    }

    /// <summary>
    /// Converts a <see cref="Rgb"/> into a <see cref="LinearRgb"/>
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="LinearRgb"/></returns>
    public static LinearRgb ToLinearRgb(in Rgb color)
        => RgbToLinearRgbConverter.Convert(color);

    /// <summary>
    /// Converts a <see cref="YCbCr"/> into a <see cref="LinearRgb"/>.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    /// <returns>The <see cref="LinearRgb"/></returns>
    public LinearRgb ToLinearRgb(in YCbCr color)
    {
        Rgb rgb = this.ToRgb(color);
        return ToLinearRgb(rgb);
    }
}
