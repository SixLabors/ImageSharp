// <copyright file="IPackedVector.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System.Numerics;

    /// <summary>
    /// An interface that converts packed vector types to and from <see cref="Vector4"/> values, 
    /// allowing multiple encodings to be manipulated in a generic way.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public interface IPackedVector<T, TP> : IPackedVector
        where TP : struct
    {
        /// <summary>
        /// Gets the packed representation of the value.
        /// Typically packed in least to greatest significance order.
        /// </summary>
        /// <returns>
        /// The <see cref="TP"/>.
        /// </returns>
        TP PackedValue();

        /// <summary>
        /// Adds the given <see cref="T"/> to the current instance.
        /// </summary>
        /// <param name="value">The packed vector to add.</param>
        void Add(T value);

        /// <summary>
        /// Subtracts the given <see cref="T"/> from the current instance.
        /// </summary>
        /// <param name="value">The packed vector to subtract.</param>
        void Subtract(T value);

        /// <summary>
        /// Multiplies the given current instance by given the <see cref="T"/>.
        /// </summary>
        /// <param name="value">The packed vector to multiply by.</param>
        void Multiply(T value);

        /// <summary>
        /// Multiplies the given current instance by given the value.
        /// </summary>
        /// <param name="value">The value to multiply by.</param>
        void Multiply(float value);

        /// <summary>
        /// Divides the given current instance by given the <see cref="T"/>.
        /// </summary>
        /// <param name="value">The packed vector to divide by.</param>
        void Divide(T value);

        /// <summary>
        /// Divides the given current instance by given the value.
        /// </summary>
        /// <param name="value">The value to divide by.</param>
        void Divide(float value);
    }

    /// <summary>
    /// An interface that converts packed vector types to and from <see cref="Vector4"/> values.
    /// </summary>
    public interface IPackedVector
    {
        /// <summary>
        /// Sets the packed representation from a <see cref="Vector4"/>.
        /// </summary>
        /// <param name="vector">The vector to pack.</param>
        void PackVector(Vector4 vector);

        /// <summary>
        /// Sets the packed representation from a <see cref="Vector4"/>.
        /// </summary>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        /// <param name="z">The z-component.</param>
        /// <param name="w">The w-component.</param>
        void PackBytes(byte x, byte y, byte z, byte w);

        /// <summary>
        /// Expands the packed representation into a <see cref="Vector4"/>.
        /// The vector components are typically expanded in least to greatest significance order.
        /// </summary>
        /// <returns>The <see cref="Vector4"/>.</returns>
        Vector4 ToVector4();

        /// <summary>
        /// Expands the packed representation into a <see cref="T:byte[]"/>.
        /// The bytes are typically expanded in least to greatest significance order.
        /// </summary>
        /// <returns>The <see cref="Vector4"/>.</returns>
        byte[] ToBytes();
    }
}
