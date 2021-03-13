// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed vector type containing unsigned normalized values ranging from 0 to 1.
    /// The x, y and z components use 10 bits, and the w component uses 2 bits.
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    public partial struct Rgba1010102 : IPixel<Rgba1010102>, IPackedVector<uint>
    {
        private static readonly Vector4 Multiplier = new Vector4(1023F, 1023F, 1023F, 3F);

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba1010102"/> struct.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        public Rgba1010102(float x, float y, float z, float w)
            : this(new Vector4(x, y, z, w))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba1010102"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the component values.</param>
        public Rgba1010102(Vector4 vector) => this.PackedValue = Pack(ref vector);

        /// <inheritdoc/>
        public uint PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Rgba1010102"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgba1010102"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgba1010102"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(Rgba1010102 left, Rgba1010102 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Rgba1010102"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgba1010102"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgba1010102"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Rgba1010102 left, Rgba1010102 right) => !left.Equals(right);

        /// <inheritdoc />
        public readonly PixelOperations<Rgba1010102> CreatePixelOperations() => new PixelOperations();

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
                (this.PackedValue >> 0) & 0x03FF,
                (this.PackedValue >> 10) & 0x03FF,
                (this.PackedValue >> 20) & 0x03FF,
                (this.PackedValue >> 30) & 0x03) / Multiplier;
        }

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
        public void FromBgra5551(Bgra5551 source) => this.FromScaledVector4(source.ToScaledVector4());

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
        public override readonly bool Equals(object obj) => obj is Rgba1010102 other && this.Equals(other);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool Equals(Rgba1010102 other) => this.PackedValue == other.PackedValue;

        /// <inheritdoc />
        public override readonly string ToString()
        {
            var vector = this.ToVector4();
            return FormattableString.Invariant($"Rgba1010102({vector.X:#0.##}, {vector.Y:#0.##}, {vector.Z:#0.##}, {vector.W:#0.##})");
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint Pack(ref Vector4 vector)
        {
            vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One) * Multiplier;

            return (uint)(
                (((int)Math.Round(vector.X) & 0x03FF) << 0)
                | (((int)Math.Round(vector.Y) & 0x03FF) << 10)
                | (((int)Math.Round(vector.Z) & 0x03FF) << 20)
                | (((int)Math.Round(vector.W) & 0x03) << 30));
        }
    }
}
