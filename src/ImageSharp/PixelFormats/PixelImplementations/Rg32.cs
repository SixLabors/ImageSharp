// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing two 16-bit unsigned normalized values ranging from 0 to 1.
    /// <para>
    /// Ranges from [0, 0, 0, 1] to [1, 1, 0, 1] in vector form.
    /// </para>
    /// </summary>
    public partial struct Rg32 : IPixel<Rg32>, IPackedVector<uint>
    {
        private static readonly Vector2 Max = new Vector2(ushort.MaxValue);

        /// <summary>
        /// Initializes a new instance of the <see cref="Rg32"/> struct.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        public Rg32(float x, float y)
            : this(new Vector2(x, y))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rg32"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the component values.</param>
        public Rg32(Vector2 vector) => this.PackedValue = Pack(vector);

        /// <inheritdoc/>
        public uint PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Rg32"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rg32"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rg32"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(Rg32 left, Rg32 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Rg32"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rg32"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rg32"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Rg32 left, Rg32 right) => !left.Equals(right);

        /// <inheritdoc />
        public readonly PixelOperations<Rg32> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromScaledVector4(Vector4 vector) => this.FromVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToScaledVector4() => this.ToVector4();

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromVector4(Vector4 vector)
        {
            var vector2 = new Vector2(vector.X, vector.Y);
            this.PackedValue = Pack(vector2);
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToVector4() => new Vector4(this.ToVector2(), 0F, 1F);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromArgb32(Argb32 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgr24(Bgr24 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra32(Bgra32 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
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
        /// The vector components are typically expanded in least to greatest significance order.
        /// </summary>
        /// <returns>The <see cref="Vector2"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector2 ToVector2() => new Vector2(this.PackedValue & 0xFFFF, (this.PackedValue >> 16) & 0xFFFF) / Max;

        /// <inheritdoc />
        public override readonly bool Equals(object obj) => obj is Rg32 other && this.Equals(other);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool Equals(Rg32 other) => this.PackedValue.Equals(other.PackedValue);

        /// <inheritdoc />
        public override readonly string ToString()
        {
            var vector = this.ToVector2();
            return FormattableString.Invariant($"Rg32({vector.X:#0.##}, {vector.Y:#0.##})");
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

        [MethodImpl(InliningOptions.ShortMethod)]
        private static uint Pack(Vector2 vector)
        {
            vector = Vector2.Clamp(vector, Vector2.Zero, Vector2.One) * Max;
            return (uint)(((int)Math.Round(vector.X) & 0xFFFF) | (((int)Math.Round(vector.Y) & 0xFFFF) << 16));
        }
    }
}
