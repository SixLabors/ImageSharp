// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// An interface that represents a generic pixel type.
/// The naming convention of each pixel format is to order the color components from least significant to most significant, reading from left to right.
/// For example in the <see cref="Rgba32"/> pixel format the R component is the least significant byte, and the A component is the most significant.
/// </summary>
/// <typeparam name="TSelf">The type implementing this interface</typeparam>
public interface IPixel<TSelf> : IPixel, IEquatable<TSelf>
    where TSelf : unmanaged, IPixel<TSelf>
{
#pragma warning disable CA1000 // Do not declare static members on generic types
    /// <summary>
    /// Creates a <see cref="PixelOperations{TPixel}"/> instance for this pixel type.
    /// This method is not intended to be consumed directly. Use <see cref="PixelOperations{TPixel}.Instance"/> instead.
    /// </summary>
    /// <returns>The <see cref="PixelOperations{TPixel}"/> instance.</returns>
    static abstract PixelOperations<TSelf> CreatePixelOperations();

    /// <summary>
    /// Initializes the pixel instance from a generic a generic ("scaled") <see cref="Vector4"/> representation
    /// with values scaled and clamped between <value>0</value> and <value>1</value>
    /// </summary>
    /// <param name="source">The vector to load the pixel from.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromScaledVector4(Vector4 source);

    /// <summary>
    /// Initializes the pixel instance from a <see cref="Vector4"/> which is specific to the current pixel type.
    /// </summary>
    /// <param name="source">The vector to load the pixel from.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromVector4(Vector4 source);

    /// <summary>
    /// Initializes the pixel instance from an <see cref="Abgr32"/> value.
    /// </summary>
    /// <param name="source">The <see cref="Abgr32"/> value.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromAbgr32(Abgr32 source);

    /// <summary>
    /// Initializes the pixel instance from an <see cref="Argb32"/> value.
    /// </summary>
    /// <param name="source">The <see cref="Argb32"/> value.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromArgb32(Argb32 source);

    /// <summary>
    /// Initializes the pixel instance from an <see cref="Bgra5551"/> value.
    /// </summary>
    /// <param name="source">The <see cref="Bgra5551"/> value.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromBgra5551(Bgra5551 source);

    /// <summary>
    /// Initializes the pixel instance from an <see cref="Bgr24"/> value.
    /// </summary>
    /// <param name="source">The <see cref="Bgr24"/> value.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromBgr24(Bgr24 source);

    /// <summary>
    /// Initializes the pixel instance from an <see cref="Bgra32"/> value.
    /// </summary>
    /// <param name="source">The <see cref="Bgra32"/> value.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromBgra32(Bgra32 source);

    /// <summary>
    /// Initializes the pixel instance from an <see cref="L8"/> value.
    /// </summary>
    /// <param name="source">The <see cref="L8"/> value.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromL8(L8 source);

    /// <summary>
    /// Initializes the pixel instance from an <see cref="L16"/> value.
    /// </summary>
    /// <param name="source">The <see cref="L16"/> value.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromL16(L16 source);

    /// <summary>
    /// Initializes the pixel instance from an <see cref="La16"/> value.
    /// </summary>
    /// <param name="source">The <see cref="La16"/> value.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromLa16(La16 source);

    /// <summary>
    /// Initializes the pixel instance from an <see cref="La32"/> value.
    /// </summary>
    /// <param name="source">The <see cref="La32"/> value.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromLa32(La32 source);

    /// <summary>
    /// Initializes the pixel instance from an <see cref="Rgb24"/> value.
    /// </summary>
    /// <param name="source">The <see cref="Rgb24"/> value.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromRgb24(Rgb24 source);

    /// <summary>
    /// Initializes the pixel instance from an <see cref="Rgba32"/> value.
    /// </summary>
    /// <param name="source">The <see cref="Rgba32"/> value.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromRgba32(Rgba32 source);

    /// <summary>
    /// Initializes the pixel instance from an <see cref="Rgb48"/> value.
    /// </summary>
    /// <param name="source">The <see cref="Rgb48"/> value.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromRgb48(Rgb48 source);

    /// <summary>
    /// Initializes the pixel instance from an <see cref="Rgba64"/> value.
    /// </summary>
    /// <param name="source">The <see cref="Rgba64"/> value.</param>
    /// <returns>The <typeparamref name="TSelf"/>.</returns>
    static abstract TSelf FromRgba64(Rgba64 source);
#pragma warning restore CA1000 // Do not declare static members on generic types
}

/// <summary>
/// A base interface for all pixels, defining the mandatory operations to be implemented by a pixel type.
/// </summary>
public interface IPixel
{
    /// <summary>
    /// Gets the pixel type information.
    /// </summary>
    /// <returns>The <see cref="PixelTypeInfo"/>.</returns>
    static abstract PixelTypeInfo GetPixelTypeInfo();

    /// <summary>
    /// Convert the pixel instance into <see cref="Rgba32"/> representation.
    /// </summary>
    /// <returns>The <see cref="Rgba32"/></returns>
    Rgba32 ToRgba32();

    /// <summary>
    /// Expands the pixel into a generic ("scaled") <see cref="Vector4"/> representation
    /// with values scaled and clamped between <value>0</value> and <value>1</value>.
    /// The vector components are typically expanded in least to greatest significance order.
    /// </summary>
    /// <returns>The <see cref="Vector4"/>.</returns>
    Vector4 ToScaledVector4();

    /// <summary>
    /// Expands the pixel into a <see cref="Vector4"/> which is specific to the current pixel type.
    /// The vector components are typically expanded in least to greatest significance order.
    /// </summary>
    /// <returns>The <see cref="Vector4"/>.</returns>
    Vector4 ToVector4();
}
