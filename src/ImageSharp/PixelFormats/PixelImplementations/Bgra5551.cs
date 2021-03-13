// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing unsigned normalized values ranging from 0 to 1.
    /// The x , y and z components use 5 bits, and the w component uses 1 bit.
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    public partial struct Bgra5551 : IPixel<Bgra5551>, IPackedVector<ushort>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra5551"/> struct.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        public Bgra5551(float x, float y, float z, float w)
            : this(new Vector4(x, y, z, w))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra5551"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        public Bgra5551(Vector4 vector) => this.PackedValue = Pack(ref vector);

        /// <inheritdoc/>
        public ushort PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Bgra5551"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Bgra5551"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Bgra5551"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(Bgra5551 left, Bgra5551 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Bgra5551"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Bgra5551"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Bgra5551"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Bgra5551 left, Bgra5551 right) => !left.Equals(right);

        /// <inheritdoc />
        public readonly PixelOperations<Bgra5551> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromScaledVector4(Vector4 vector) => this.FromVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToScaledVector4() => this.ToVector4();

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromVector4(Vector4 vector) => this.PackedValue = Pack(ref vector);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToVector4()
        {
            return new Vector4(
                        ((this.PackedValue >> 10) & 0x1F) / 31F,
                        ((this.PackedValue >> 5) & 0x1F) / 31F,
                        ((this.PackedValue >> 0) & 0x1F) / 31F,
                        (this.PackedValue >> 15) & 0x01);
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra5551(Bgra5551 source) => this = source;

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromArgb32(Argb32 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgr24(Bgr24 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra32(Bgra32 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL8(L8 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL16(L16 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa16(La16 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa32(La32 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb24(Rgb24 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba32(Rgba32 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ToRgba32(ref Rgba32 dest)
        {
            dest.FromScaledVector4(this.ToScaledVector4());
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb48(Rgb48 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba64(Rgba64 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Bgra5551 other && this.Equals(other);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool Equals(Bgra5551 other) => this.PackedValue.Equals(other.PackedValue);

        /// <inheritdoc />
        public override readonly string ToString()
        {
            var vector = this.ToVector4();
            return FormattableString.Invariant($"Bgra5551({vector.Z:#0.##}, {vector.Y:#0.##}, {vector.X:#0.##}, {vector.W:#0.##})");
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

        [MethodImpl(InliningOptions.ShortMethod)]
        private static ushort Pack(ref Vector4 vector)
        {
            vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One);
            return (ushort)(
                   (((int)Math.Round(vector.X * 31F) & 0x1F) << 10)
                   | (((int)Math.Round(vector.Y * 31F) & 0x1F) << 5)
                   | (((int)Math.Round(vector.Z * 31F) & 0x1F) << 0)
                   | (((int)Math.Round(vector.W) & 0x1) << 15));
        }
    }
}
