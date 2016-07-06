namespace GenericImage.PackedVectors
{
    using System.Numerics;

    /// <summary>
    /// An interface that converts packed vector types to and from <see cref="Vector4"/> values, 
    /// allowing multiple encodings to be manipulated in a generic way.
    /// </summary>
    /// <typeparam name="TPacked">
    /// The type of object representing the packed value.
    /// </typeparam>
    public interface IPackedVector<TPacked> : IPackedVector
        where TPacked : struct
    {
        /// <summary>
        /// Gets or sets the packed representation of the value.
        /// </summary>
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
        /// </summary>
        /// <returns>The <see cref="Vector4"/>.</returns>
        Vector4 ToVector4();

        /// <summary>
        /// Expands the packed representation into a <see cref="T:byte[]"/>.
        /// </summary>
        /// <returns>The <see cref="Vector4"/>.</returns>
        byte[] ToBytes();
    }
}
