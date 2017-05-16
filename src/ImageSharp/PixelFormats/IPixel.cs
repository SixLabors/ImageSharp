// <copyright file="IPixel.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System;
    using System.Numerics;

    /// <summary>
    /// An interface that represents a generic pixel type.
    /// </summary>
    /// <typeparam name="TSelf">The type implementing this interface</typeparam>
    public interface IPixel<TSelf> : IPixel, IEquatable<TSelf>
        where TSelf : struct, IPixel<TSelf>
    {
        /// <summary>
        /// Creates a <see cref="PixelOperations{TPixel}"/> instance for this pixel type.
        /// This method is not intended to be consumed directly. Use <see cref="PixelOperations{TPixel}.Instance"/> instead.
        /// </summary>
        /// <returns>The <see cref="PixelOperations{TPixel}"/> instance.</returns>
        PixelOperations<TSelf> CreatePixelOperations();
    }

    /// <summary>
    /// An interface that represents a pixel type.
    /// </summary>
    public interface IPixel
    {
        /// <summary>
        /// Sets the packed representation from a <see cref="Vector4"/>.
        /// </summary>
        /// <param name="vector">The vector to create the packed representation from.</param>
        void PackFromVector4(Vector4 vector);

        /// <summary>
        /// Expands the packed representation into a <see cref="Vector4"/>.
        /// The vector components are typically expanded in least to greatest significance order.
        /// </summary>
        /// <returns>The <see cref="Vector4"/>.</returns>
        Vector4 ToVector4();

        /// <summary>
        /// Sets the packed representation from the given byte array.
        /// </summary>
        /// <remarks>
        /// TODO: Refactor this, defining multiple PackFromAsdf42(ref Asdf42 source) methods instead. Should be faster on many execution paths!
        /// </remarks>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        /// <param name="z">The z-component.</param>
        /// <param name="w">The w-component.</param>
        void PackFromBytes(byte x, byte y, byte z, byte w);

        /// <summary>
        /// Converts the pixel to <see cref="Rgb24"/> format.
        /// </summary>
        /// <param name="dest">The destination pixel to write to</param>
        void ToRgb24(ref Rgb24 dest);

        /// <summary>
        /// Converts the pixel to <see cref="Rgba32"/> format.
        /// </summary>
        /// <param name="dest">The destination pixel to write to</param>
        void ToRgba32(ref Rgba32 dest);

        /// <summary>
        /// Converts the pixel to <see cref="Bgr24"/> format.
        /// </summary>
        /// <param name="dest">The destination pixel to write to</param>
        void ToBgr24(ref Bgr24 dest);

        /// <summary>
        /// Converts the pixel to <see cref="Bgra32"/> format.
        /// </summary>
        /// <param name="dest">The destination pixel to write to</param>
        void ToBgra32(ref Bgra32 dest);
    }
}