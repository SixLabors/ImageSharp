// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing four 8-bit unsigned integer values, ranging from 0 to 255.
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [255, 255, 255, 255] in vector form.
    /// </para>
    /// </summary>
    public partial struct Byte4 : IPixel<Byte4>, IPackedVector<uint>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Byte4"/> struct.
        /// </summary>
        /// <param name="vector">
        /// A vector containing the initial values for the components of the Byte4 structure.
        /// </param>
        public Byte4(Vector4 vector) => this.PackedValue = Pack(ref vector);

        /// <summary>
        /// Initializes a new instance of the <see cref="Byte4"/> struct.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        public Byte4(float x, float y, float z, float w)
        {
            var vector = new Vector4(x, y, z, w);
            this.PackedValue = Pack(ref vector);
        }

        /// <inheritdoc/>
        public uint PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Byte4"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Byte4"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Byte4"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(Byte4 left, Byte4 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Byte4"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Byte4"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Byte4"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Byte4 left, Byte4 right) => !left.Equals(right);

        /// <inheritdoc />
        public readonly PixelOperations<Byte4> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromScaledVector4(Vector4 vector) => this.FromVector4(vector * 255F);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToScaledVector4() => this.ToVector4() / 255F;

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromVector4(Vector4 vector) => this.PackedValue = Pack(ref vector);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToVector4()
        {
            return new Vector4(
                this.PackedValue & 0xFF,
                (this.PackedValue >> 0x8) & 0xFF,
                (this.PackedValue >> 0x10) & 0xFF,
                (this.PackedValue >> 0x18) & 0xFF);
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
        public void FromBgra5551(Bgra5551 source) => this.FromScaledVector4(source.ToScaledVector4());

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
        public override readonly bool Equals(object obj) => obj is Byte4 byte4 && this.Equals(byte4);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool Equals(Byte4 other) => this.PackedValue.Equals(other.PackedValue);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

        /// <inheritdoc />
        public override readonly string ToString()
        {
            var vector = this.ToVector4();
            return FormattableString.Invariant($"Byte4({vector.X:#0.##}, {vector.Y:#0.##}, {vector.Z:#0.##}, {vector.W:#0.##})");
        }

        /// <summary>
        /// Packs a vector into a uint.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        /// <returns>The <see cref="uint"/> containing the packed values.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint Pack(ref Vector4 vector)
        {
            const float Max = 255F;

            // Clamp the value between min and max values
            vector = Numerics.Clamp(vector, Vector4.Zero, new Vector4(Max));

            uint byte4 = (uint)Math.Round(vector.X) & 0xFF;
            uint byte3 = ((uint)Math.Round(vector.Y) & 0xFF) << 0x8;
            uint byte2 = ((uint)Math.Round(vector.Z) & 0xFF) << 0x10;
            uint byte1 = ((uint)Math.Round(vector.W) & 0xFF) << 0x18;

            return byte4 | byte3 | byte2 | byte1;
        }
    }
}
