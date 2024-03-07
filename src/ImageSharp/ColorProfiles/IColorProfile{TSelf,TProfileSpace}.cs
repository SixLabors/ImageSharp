// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Defines the contract for all color profiles.
/// </summary>
/// <typeparam name="TSelf">The type of color profile.</typeparam>
/// <typeparam name="TProfileSpace">The type of color profile connecting space.</typeparam>
public interface IColorProfile<TSelf, TProfileSpace> : IEquatable<TSelf>
    where TSelf : IColorProfile<TSelf, TProfileSpace>
    where TProfileSpace : struct, IProfileConnectingSpace
{
    /// <summary>
    /// Converts the color to the profile connection space.
    /// </summary>
    /// <param name="options">The color profile conversion options.</param>
    /// <returns>The <typeparamref name="TProfileSpace"/>.</returns>
    public TProfileSpace ToProfileConnectingSpace(ColorConversionOptions options);

#pragma warning disable CA1000 // Do not declare static members on generic types
    /// <summary>
    /// Converts the color from the profile connection space.
    /// </summary>
    /// <param name="options">The color profile conversion options.</param>
    /// <param name="source">The color profile connecting space.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    public static abstract TSelf FromProfileConnectingSpace(ColorConversionOptions options, in TProfileSpace source);

    /// <summary>
    /// Converts the span of colors to the profile connection space.
    /// </summary>
    /// <param name="options">The color profile conversion options.</param>
    /// <param name="source">The color span to convert from.</param>
    /// <param name="destination">The color profile span to write the results to.</param>
    public static abstract void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<TSelf> source, Span<TProfileSpace> destination);

    /// <summary>
    /// Converts the span of colors from the profile connection space.
    /// </summary>
    /// <param name="options">The color profile conversion options.</param>
    /// <param name="source">The color profile span to convert from.</param>
    /// <param name="destination">The color span to write the results to.</param>
    public static abstract void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<TProfileSpace> source, Span<TSelf> destination);
#pragma warning restore CA1000 // Do not declare static members on generic types
}
