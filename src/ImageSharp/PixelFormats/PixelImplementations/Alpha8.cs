// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing a single 8 bit normalized W values.
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [0, 0, 0, 1] in vector form.
    /// </para>
    /// </summary>
    public struct Alpha8 : IPixel<Alpha8>, IPackedVector<byte>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Alpha8"/> struct.
        /// </summary>
        /// <param name="alpha">The alpha component.</param>
        public Alpha8(byte alpha) => this.PackedValue = alpha;

        /// <summary>
        /// Initializes a new instance of the <see cref="Alpha8"/> struct.
        /// </summary>
        /// <param name="alpha">The alpha component.</param>
        public Alpha8(float alpha) => this.PackedValue = Pack(alpha);

        /// <inheritdoc />
        public byte PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Alpha8"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Alpha8"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Alpha8"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(Alpha8 left, Alpha8 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Alpha8"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Alpha8"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Alpha8"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Alpha8 left, Alpha8 right) => !left.Equals(right);

        /// <inheritdoc />
        public PixelOperations<Alpha8> CreatePixelOperations() => new PixelOperations<Alpha8>();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromScaledVector4(Vector4 vector) => this.FromVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Vector4 ToScaledVector4() => this.ToVector4();

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromVector4(Vector4 vector) => this.PackedValue = Pack(vector.W);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public Vector4 ToVector4() => new Vector4(0, 0, 0, this.PackedValue / 255F);

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
        public void FromGray8(Gray8 source) => this.PackedValue = byte.MaxValue;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromGray16(Gray16 source) => this.PackedValue = byte.MaxValue;

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
        public override bool Equals(object obj) => obj is Alpha8 other && this.Equals(other);

        /// <summary>
        /// Compares another Alpha8 packed vector with the packed vector.
        /// </summary>
        /// <param name="other">The Alpha8 packed vector to compare.</param>
        /// <returns>True if the packed vectors are equal.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(Alpha8 other) => this.PackedValue.Equals(other.PackedValue);

        /// <summary>
        /// Gets a string representation of the packed vector.
        /// </summary>
        /// <returns>A string representation of the packed vector.</returns>
        public override string ToString() => $"Alpha8({this.PackedValue})";

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override int GetHashCode() => this.PackedValue.GetHashCode();

        /// <summary>
        /// Packs a <see cref="float"/> into a byte.
        /// </summary>
        /// <param name="alpha">The float containing the value to pack.</param>
        /// <returns>The <see cref="byte"/> containing the packed values.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static byte Pack(float alpha) => (byte)Math.Round(alpha.Clamp(0, 1F) * 255F);
    }
}