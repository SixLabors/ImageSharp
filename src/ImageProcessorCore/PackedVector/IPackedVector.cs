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
    /// <typeparam name="T">
    /// The type of object representing the packed value.
    /// </typeparam>
    public interface IPackedVector<T> : IPackedVector
        where T : struct
    {
        /// <summary>
        /// Gets or sets the packed representation of the value.
        /// Typically packed in least to greatest significance order.
        /// </summary>
        T PackedValue { get; set; }


    }

    /// <summary>
    /// An interface that converts packed vector types to and from <see cref="Vector4"/> values.
    /// </summary>
    public interface IPackedVector
    {
        void Add<U>(U value);

        void Subtract<U>(U value);

        void Multiply<U>(U value);

        void Multiply(float value);

        void Divide<U>(U value);

        void Divide(float value);

        //void Add(IPackedVector value);

        //void Subtract(IPackedVector value);

        //void Multiply(IPackedVector value);

        //void Multiply(float value);

        //void Divide(IPackedVector value);

        //void Divide(float value);

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
