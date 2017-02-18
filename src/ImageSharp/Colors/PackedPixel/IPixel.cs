namespace ImageSharp
{
    using System;
    using System.Numerics;

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
        /// Output is expanded to X-> Y-> Z order. Equivalent to R-> G-> B in <see cref="Color"/>
        /// </summary>
        /// <param name="bytes">The bytes to set the color in.</param>
        /// <param name="startIndex">The starting index of the <paramref name="bytes"/>.</param>
        void ToXyzBytes(byte[] bytes, int startIndex);

        /// <summary>
        /// Expands the packed representation into a given byte array.
        /// Output is expanded to X-> Y-> Z-> W order. Equivalent to R-> G-> B-> A in <see cref="Color"/>
        /// </summary>
        /// <param name="bytes">The bytes to set the color in.</param>
        /// <param name="startIndex">The starting index of the <paramref name="bytes"/>.</param>
        void ToXyzwBytes(byte[] bytes, int startIndex);

        /// <summary>
        /// Expands the packed representation into a given byte array.
        /// Output is expanded to Z-> Y-> X order. Equivalent to B-> G-> R in <see cref="Color"/>
        /// </summary>
        /// <param name="bytes">The bytes to set the color in.</param>
        /// <param name="startIndex">The starting index of the <paramref name="bytes"/>.</param>
        void ToZyxBytes(byte[] bytes, int startIndex);

        /// <summary>
        /// Expands the packed representation into a given byte array.
        /// Output is expanded to Z-> Y-> X-> W order. Equivalent to B-> G-> R-> A in <see cref="Color"/>
        /// </summary>
        /// <param name="bytes">The bytes to set the color in.</param>
        /// <param name="startIndex">The starting index of the <paramref name="bytes"/>.</param>
        void ToZyxwBytes(byte[] bytes, int startIndex);
    }

    public interface IPixel<TSelf> : IPixel, IEquatable<TSelf>
        where TSelf : struct, IPixel<TSelf>
    {
        
    }
}