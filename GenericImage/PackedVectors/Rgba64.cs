namespace GenericImage.PackedVectors
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Packed vector type containing four 16-bit unsigned normalized values ranging from 0 to 1.
    /// </summary>
    public struct Rgba64 : IPackedVector<ulong>, IEquatable<Rgba64>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct. 
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        public Rgba64(float r, float g, float b, float a)
        {
            this.PackedValue = Pack(r, g, b, a);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba64"/> struct. 
        /// </summary>
        /// <param name="vector">
        /// Vector containing the components for the packed vector.
        /// </param>
        public Rgba64(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <inheritdoc/>
        public ulong PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Rgba64"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Rgba64"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Rgba64"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Rgba64 left, Rgba64 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="Rgba64"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgba64"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgba64"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Rgba64 left, Rgba64 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc/>
        public void PackVector(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <inheritdoc/>
        public Vector4 ToVector4()
        {
            return new Vector4(
                (this.PackedValue & 0xFFFF) / 65535f,
                ((this.PackedValue >> 16) & 0xFFFF) / 65535f,
                ((this.PackedValue >> 32) & 0xFFFF) / 65535f,
                ((this.PackedValue >> 48) & 0xFFFF) / 65535f);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return (obj is Rgba64) && this.Equals((Rgba64)obj);
        }

        /// <inheritdoc/>
        public bool Equals(Rgba64 other)
        {
            return this.PackedValue == other.PackedValue;
        }

        /// <summary>
        /// Gets a string representation of the packed vector.
        /// </summary>
        /// <returns>A string representation of the packed vector.</returns>
        public override string ToString()
        {
            return this.ToVector4().ToString();
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.GetHashCode(this);
        }

        /// <summary>
        /// Sets the packed representation from the given component values.
        /// </summary>
        /// <param name="x">The x component.</param>
        /// <param name="y">The y component.</param>
        /// <param name="z">The z component.</param>
        /// <param name="w">The w component.</param>
        /// <returns>
        /// The <see cref="ulong"/>.
        /// </returns>
        private static ulong Pack(float x, float y, float z, float w)
        {
            return (ulong)Math.Round(ImageMaths.Clamp(x, 0, 1) * 65535f) |
                   ((ulong)Math.Round(ImageMaths.Clamp(y, 0, 1) * 65535f) << 16) |
                   ((ulong)Math.Round(ImageMaths.Clamp(z, 0, 1) * 65535f) << 32) |
                   ((ulong)Math.Round(ImageMaths.Clamp(w, 0, 1) * 65535f) << 48);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="packed">
        /// The instance of <see cref="Rgba64"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(Rgba64 packed)
        {
            return packed.PackedValue.GetHashCode();
        }
    }
}
