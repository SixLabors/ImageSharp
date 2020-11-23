// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing a single 8-bit normalized alpha value.
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [0, 0, 0, 1] in vector form.
    /// </para>
    /// </summary>
    public partial struct A8 : IPixel<A8>, IPackedVector<byte>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="A8"/> struct.
        /// </summary>
        /// <param name="alpha">The alpha component.</param>
        public A8(byte alpha) => this.PackedValue = alpha;

        /// <summary>
        /// Initializes a new instance of the <see cref="A8"/> struct.
        /// </summary>
        /// <param name="alpha">The alpha component.</param>
        public A8(float alpha) => this.PackedValue = Pack(alpha);

        /// <inheritdoc />
        public byte PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="A8"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="A8"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="A8"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(A8 left, A8 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="A8"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="A8"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="A8"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(A8 left, A8 right) => !left.Equals(right);

        /// <inheritdoc />
        public readonly PixelOperations<A8> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromScaledVector4(Vector4 vector) => this.FromVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToScaledVector4() => this.ToVector4();

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromVector4(Vector4 vector) => this.PackedValue = Pack(vector.W);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToVector4() => new Vector4(0, 0, 0, this.PackedValue / 255F);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromArgb32(Argb32 source) => this.PackedValue = source.A;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgr24(Bgr24 source) => this.PackedValue = byte.MaxValue;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra32(Bgra32 source) => this.PackedValue = source.A;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra5551(Bgra5551 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL8(L8 source) => this.PackedValue = byte.MaxValue;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL16(L16 source) => this.PackedValue = byte.MaxValue;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa16(La16 source) => this.PackedValue = source.A;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa32(La32 source) => this.PackedValue = ColorNumerics.DownScaleFrom16BitTo8Bit(source.A);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb24(Rgb24 source) => this.PackedValue = byte.MaxValue;

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba32(Rgba32 source) => this.PackedValue = source.A;

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ToRgba32(ref Rgba32 dest)
        {
            dest = default;
            dest.A = this.PackedValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb48(Rgb48 source) => this.PackedValue = byte.MaxValue;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba64(Rgba64 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <summary>
        /// Compares an object with the packed vector.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if the object is equal to the packed vector.</returns>
        public override readonly bool Equals(object obj) => obj is A8 other && this.Equals(other);

        /// <summary>
        /// Compares another A8 packed vector with the packed vector.
        /// </summary>
        /// <param name="other">The A8 packed vector to compare.</param>
        /// <returns>True if the packed vectors are equal.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool Equals(A8 other) => this.PackedValue.Equals(other.PackedValue);

        /// <summary>
        /// Gets a string representation of the packed vector.
        /// </summary>
        /// <returns>A string representation of the packed vector.</returns>
        public override readonly string ToString() => $"A8({this.PackedValue})";

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

        /// <summary>
        /// Packs a <see cref="float"/> into a byte.
        /// </summary>
        /// <param name="alpha">The float containing the value to pack.</param>
        /// <returns>The <see cref="byte"/> containing the packed values.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static byte Pack(float alpha) => (byte)Math.Round(Numerics.Clamp(alpha, 0, 1F) * 255F);
    }
}
