// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing a single 8-bit normalized luminance value.
    /// <para>
    /// Ranges from [0, 0, 0, 1] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    public partial struct L8 : IPixel<L8>, IPackedVector<byte>
    {
        private static readonly Vector4 MaxBytes = new Vector4(255F);
        private static readonly Vector4 Half = new Vector4(0.5F);

        /// <summary>
        /// Initializes a new instance of the <see cref="L8"/> struct.
        /// </summary>
        /// <param name="luminance">The luminance component.</param>
        public L8(byte luminance) => this.PackedValue = luminance;

        /// <inheritdoc />
        public byte PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="L8"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="L8"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="L8"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(L8 left, L8 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="L8"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="L8"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="L8"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(L8 left, L8 right) => !left.Equals(right);

        /// <inheritdoc />
        public readonly PixelOperations<L8> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromScaledVector4(Vector4 vector) => this.ConvertFromRgbaScaledVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToScaledVector4() => this.ToVector4();

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromVector4(Vector4 vector) => this.ConvertFromRgbaScaledVector4(vector);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToVector4()
        {
            float rgb = this.PackedValue / 255F;
            return new Vector4(rgb, rgb, rgb, 1F);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromArgb32(Argb32 source) => this.PackedValue = ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgr24(Bgr24 source) => this.PackedValue = ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra32(Bgra32 source) => this.PackedValue = ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra5551(Bgra5551 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL8(L8 source) => this = source;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL16(L16 source) => this.PackedValue = ColorNumerics.DownScaleFrom16BitTo8Bit(source.PackedValue);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa16(La16 source) => this.PackedValue = source.L;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa32(La32 source) => this.PackedValue = ColorNumerics.DownScaleFrom16BitTo8Bit(source.L);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb24(Rgb24 source) => this.PackedValue = ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba32(Rgba32 source) => this.PackedValue = ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ToRgba32(ref Rgba32 dest)
        {
            dest.R = this.PackedValue;
            dest.G = this.PackedValue;
            dest.B = this.PackedValue;
            dest.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb48(Rgb48 source)
            => this.PackedValue = ColorNumerics.Get8BitBT709Luminance(
                ColorNumerics.DownScaleFrom16BitTo8Bit(source.R),
                ColorNumerics.DownScaleFrom16BitTo8Bit(source.G),
                ColorNumerics.DownScaleFrom16BitTo8Bit(source.B));

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba64(Rgba64 source)
            => this.PackedValue = ColorNumerics.Get8BitBT709Luminance(
                ColorNumerics.DownScaleFrom16BitTo8Bit(source.R),
                ColorNumerics.DownScaleFrom16BitTo8Bit(source.G),
                ColorNumerics.DownScaleFrom16BitTo8Bit(source.B));

        /// <inheritdoc />
        public override readonly bool Equals(object obj) => obj is L8 other && this.Equals(other);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool Equals(L8 other) => this.PackedValue.Equals(other.PackedValue);

        /// <inheritdoc />
        public override readonly string ToString() => $"L8({this.PackedValue})";

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

        [MethodImpl(InliningOptions.ShortMethod)]
        internal void ConvertFromRgbaScaledVector4(Vector4 vector)
        {
            vector *= MaxBytes;
            vector += Half;
            vector = Numerics.Clamp(vector, Vector4.Zero, MaxBytes);
            this.PackedValue = ColorNumerics.Get8BitBT709Luminance((byte)vector.X, (byte)vector.Y, (byte)vector.Z);
        }
    }
}
