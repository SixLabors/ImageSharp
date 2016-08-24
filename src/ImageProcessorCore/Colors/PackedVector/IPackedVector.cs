// <copyright file="IPackedVector.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System.Numerics;

    /// <summary>
    /// An interface that converts packed vector types to and from <see cref="Vector4"/> values, 
    /// allowing multiple encodings to be manipulated in a generic manner.
    /// </summary>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public interface IPackedVector<TPacked> : IPackedVector
        where TPacked : struct
    {
        TPacked PackedValue { get; set; }
    }

    /// <summary>
    /// An interface that converts packed vector types to and from <see cref="Vector4"/> values.
    /// </summary>
    public interface IPackedVector
    {
        /// <summary>
        /// Sets the packed representation from a <see cref="Vector4"/>.
        /// </summary>
        /// <param name="vector">The vector to create the packed representation from.</param>
        void PackFromVector4(Vector4 vector);

        ///// <summary>
        ///// Sets the packed representation from a <see cref="T:byte[]"/>.
        ///// </summary>
        ///// <param name="x">The x-component to create the packed representation from.</param>
        ///// <param name="y">The y-component to create the packed representation from.</param>
        ///// <param name="z">The z-component to create the packed representation from.</param>
        ///// <param name="w">The w-component to create the packed representation from.</param>
        //void PackFromBytes(byte x, byte y, byte z, byte w);

        /// <summary>
        /// Expands the packed representation into a <see cref="Vector4"/>.
        /// The vector components are typically expanded in least to greatest significance order.
        /// </summary>
        /// <returns>The <see cref="Vector4"/>.</returns>
        Vector4 ToVector4();

        ///// <summary>
        ///// Expands the packed representation into a <see cref="T:byte[]"/>.
        ///// The bytes are typically expanded in least to greatest significance order.
        ///// Red -> Green -> Blue -> Alpha
        ///// </summary>
        ///// <returns>The <see cref="Vector4"/>.</returns>
        //byte[] ToBytes();
    }
}
