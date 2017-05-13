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
        PixelOperations<TSelf> CreateBulkOperations();
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
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        /// <param name="z">The z-component.</param>
        /// <param name="w">The w-component.</param>
        void PackFromBytes(byte x, byte y, byte z, byte w);

        /// <summary>
        /// Expands the packed representation into a given byte array.
        /// Output is expanded to X-> Y-> Z order. Equivalent to R-> G-> B in <see cref="Rgba32"/>
        /// </summary>
        /// <param name="bytes">The bytes to set the color in.</param>
        /// <param name="startIndex">The starting index of the <paramref name="bytes"/>.</param>
        void ToXyzBytes(Span<byte> bytes, int startIndex);

        /// <summary>
        /// Expands the packed representation into a given byte array.
        /// Output is expanded to X-> Y-> Z-> W order. Equivalent to R-> G-> B-> A in <see cref="Rgba32"/>
        /// </summary>
        /// <param name="bytes">The bytes to set the color in.</param>
        /// <param name="startIndex">The starting index of the <paramref name="bytes"/>.</param>
        void ToXyzwBytes(Span<byte> bytes, int startIndex);

        /// <summary>
        /// Expands the packed representation into a given byte array.
        /// Output is expanded to Z-> Y-> X order. Equivalent to B-> G-> R in <see cref="Rgba32"/>
        /// </summary>
        /// <param name="bytes">The bytes to set the color in.</param>
        /// <param name="startIndex">The starting index of the <paramref name="bytes"/>.</param>
        void ToZyxBytes(Span<byte> bytes, int startIndex);

        /// <summary>
        /// Expands the packed representation into a given byte array.
        /// Output is expanded to Z-> Y-> X-> W order. Equivalent to B-> G-> R-> A in <see cref="Rgba32"/>
        /// </summary>
        /// <param name="bytes">The bytes to set the color in.</param>
        /// <param name="startIndex">The starting index of the <paramref name="bytes"/>.</param>
        void ToZyxwBytes(Span<byte> bytes, int startIndex);
    }
}