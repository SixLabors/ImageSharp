// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing a single 16 bit floating point value.
    /// <para>
    /// Ranges from [-1, 0, 0, 1] to [1, 0, 0, 1] in vector form.
    /// </para>
    /// </summary>
    public struct HalfSingle : IPixel<HalfSingle>, IPackedVector<ushort>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HalfSingle"/> struct.
        /// </summary>
        /// <param name="single">The single component.</param>
        public HalfSingle(float single) => this.PackedValue = HalfTypeHelper.Pack(single);

        /// <inheritdoc/>
        public ushort PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="HalfSingle"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="HalfSingle"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="HalfSingle"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(HalfSingle left, HalfSingle right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="HalfSingle"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="HalfSingle"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="HalfSingle"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(HalfSingle left, HalfSingle right) => !left.Equals(right);

        /// <inheritdoc />
        public PixelOperations<HalfSingle> CreatePixelOperations() => new PixelOperations<HalfSingle>();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromScaledVector4(Vector4 vector)
        {
            float scaled = vector.X;
            scaled *= 2F;
            scaled--;
            this.PackedValue = HalfTypeHelper.Pack(scaled);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Vector4 ToScaledVector4()
        {
            float single = this.ToSingle() + 1F;
            single /= 2F;
            return new Vector4(single, 0, 0, 1F);
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromVector4(Vector4 vector) => this.PackedValue = HalfTypeHelper.Pack(vector.X);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public Vector4 ToVector4() => new Vector4(this.ToSingle(), 0, 0, 1F);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromArgb32(Argb32 source) => this.PackFromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromBgr24(Bgr24 source) => this.PackFromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromBgra32(Bgra32 source) => this.PackFromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromGray8(Gray8 source) => this.PackFromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromGray16(Gray16 source) => this.PackFromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromRgb24(Rgb24 source) => this.PackFromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromRgba32(Rgba32 source) => this.PackFromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba32 ToRgba32() => new Rgba32(this.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromRgb48(Rgb48 source) => this.PackFromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void PackFromRgba64(Rgba64 source) => this.PackFromScaledVector4(source.ToScaledVector4());

        /// <summary>
        /// Expands the packed representation into a <see cref="float"/>.
        /// </summary>
        /// <returns>The <see cref="float"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public float ToSingle() => HalfTypeHelper.Unpack(this.PackedValue);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is HalfSingle other && this.Equals(other);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(HalfSingle other) => this.PackedValue.Equals(other.PackedValue);

        /// <inheritdoc />
        public override string ToString() => $"HalfSingle({this.ToSingle():#0.##})";

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override int GetHashCode() => this.PackedValue.GetHashCode();
    }
}