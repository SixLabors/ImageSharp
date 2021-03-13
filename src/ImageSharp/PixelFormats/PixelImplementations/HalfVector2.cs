// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing two 16-bit floating-point values.
    /// <para>
    /// Ranges from [-1, -1, 0, 1] to [1, 1, 0, 1] in vector form.
    /// </para>
    /// </summary>
    public partial struct HalfVector2 : IPixel<HalfVector2>, IPackedVector<uint>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HalfVector2"/> struct.
        /// </summary>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        public HalfVector2(float x, float y) => this.PackedValue = Pack(x, y);

        /// <summary>
        /// Initializes a new instance of the <see cref="HalfVector2"/> struct.
        /// </summary>
        /// <param name="vector">A vector containing the initial values for the components.</param>
        public HalfVector2(Vector2 vector) => this.PackedValue = Pack(vector.X, vector.Y);

        /// <inheritdoc/>
        public uint PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="HalfVector2"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="HalfVector2"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="HalfVector2"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(HalfVector2 left, HalfVector2 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="HalfVector2"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="HalfVector2"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="HalfVector2"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(HalfVector2 left, HalfVector2 right) => !left.Equals(right);

        /// <inheritdoc />
        public readonly PixelOperations<HalfVector2> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromScaledVector4(Vector4 vector)
        {
            Vector2 scaled = new Vector2(vector.X, vector.Y) * 2F;
            scaled -= Vector2.One;
            this.PackedValue = Pack(scaled.X, scaled.Y);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToScaledVector4()
        {
            var scaled = this.ToVector2();
            scaled += Vector2.One;
            scaled /= 2F;
            return new Vector4(scaled, 0F, 1F);
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromVector4(Vector4 vector) => this.PackedValue = Pack(vector.X, vector.Y);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToVector4()
        {
            var vector = this.ToVector2();
            return new Vector4(vector.X, vector.Y, 0F, 1F);
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

        /// <summary>
        /// Expands the packed representation into a <see cref="Vector2"/>.
        /// </summary>
        /// <returns>The <see cref="Vector2"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector2 ToVector2()
        {
            Vector2 vector;
            vector.X = HalfTypeHelper.Unpack((ushort)this.PackedValue);
            vector.Y = HalfTypeHelper.Unpack((ushort)(this.PackedValue >> 0x10));
            return vector;
        }

        /// <inheritdoc />
        public override readonly bool Equals(object obj) => obj is HalfVector2 other && this.Equals(other);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool Equals(HalfVector2 other) => this.PackedValue.Equals(other.PackedValue);

        /// <inheritdoc />
        public override readonly string ToString()
        {
            var vector = this.ToVector2();
            return FormattableString.Invariant($"HalfVector2({vector.X:#0.##}, {vector.Y:#0.##})");
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint Pack(float x, float y)
        {
            uint num2 = HalfTypeHelper.Pack(x);
            uint num = (uint)(HalfTypeHelper.Pack(y) << 0x10);
            return num2 | num;
        }
    }
}
