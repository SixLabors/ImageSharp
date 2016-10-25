// <copyright file="IPackedBytes.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// An interface that converts packed vector types to and from <see cref="T:byte[]"/> values, 
    /// allowing multiple encodings to be manipulated in a generic manner.
    /// </summary>
    public interface IPackedBytes
    {
        /// <summary>
        /// Gets the packed representation from the gives bytes.
        /// </summary>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        /// <param name="z">The z-component.</param>
        /// <param name="w">The w-component.</param>
        void PackFromBytes(byte x, byte y, byte z, byte w);

        /// <summary>
        /// Sets the packed representation into the gives bytes.
        /// </summary>
        /// <param name="bytes">The bytes to set the color in.</param>
        /// <param name="startIndex">The starting index of the <paramref name="bytes"/>.</param>
        /// <param name="componentOrder">The order of the components.</param>
        void ToBytes(byte[] bytes, int startIndex, ComponentOrder componentOrder);
    }
}
