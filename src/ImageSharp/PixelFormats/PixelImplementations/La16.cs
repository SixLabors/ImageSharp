// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing two 8-bit normalized values representing luminance and alpha.
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public partial struct La16 : IPixel<La16>, IPackedVector<ushort>
    {
        private static readonly Vector4 MaxBytes = new Vector4(255F);
        private static readonly Vector4 Half = new Vector4(0.5F);

        /// <summary>
        /// Gets or sets the luminance component.
        /// </summary>
        [FieldOffset(0)]
        public byte L;

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        [FieldOffset(1)]
        public byte A;

        /// <summary>
        /// Initializes a new instance of the <see cref="La16"/> struct.
        /// </summary>
        /// <param name="l">The luminance component.</param>
        /// <param name="a">The alpha componant.</param>
        public La16(byte l, byte a)
        {
            this.L = l;
            this.A = a;
        }

        /// <inheritdoc/>
        public ushort PackedValue
        {
            readonly get => Unsafe.As<La16, ushort>(ref Unsafe.AsRef(this));
            set => Unsafe.As<La16, ushort>(ref this) = value;
        }

        /// <summary>
        /// Compares two <see cref="La16"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="La16"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="La16"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(La16 left, La16 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="La16"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="La16"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="La16"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(La16 left, La16 right) => !left.Equals(right);

        /// <inheritdoc/>
        public readonly PixelOperations<La16> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool Equals(La16 other) => this.PackedValue.Equals(other.PackedValue);

        /// <inheritdoc />
        public override readonly bool Equals(object obj) => obj is La16 other && this.Equals(other);

        /// <inheritdoc />
        public override readonly string ToString() => $"La16({this.L}, {this.A})";

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromArgb32(Argb32 source)
        {
            this.L = ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B);
            this.A = source.A;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgr24(Bgr24 source)
        {
            this.L = ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B);
            this.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra32(Bgra32 source)
        {
            this.L = ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B);
            this.A = source.A;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra5551(Bgra5551 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL16(L16 source)
        {
            this.L = ColorNumerics.DownScaleFrom16BitTo8Bit(source.PackedValue);
            this.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL8(L8 source)
        {
            this.L = source.PackedValue;
            this.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa16(La16 source) => this = source;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa32(La32 source)
        {
            this.L = ColorNumerics.DownScaleFrom16BitTo8Bit(source.L);
            this.A = ColorNumerics.DownScaleFrom16BitTo8Bit(source.A);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb24(Rgb24 source)
        {
            this.L = ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B);
            this.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb48(Rgb48 source)
        {
            this.L = ColorNumerics.Get8BitBT709Luminance(
                ColorNumerics.DownScaleFrom16BitTo8Bit(source.R),
                ColorNumerics.DownScaleFrom16BitTo8Bit(source.G),
                ColorNumerics.DownScaleFrom16BitTo8Bit(source.B));

            this.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba32(Rgba32 source)
        {
            this.L = ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B);
            this.A = source.A;
        }

        /// <inheritdoc/>
        public void FromRgba64(Rgba64 source)
        {
            this.L = ColorNumerics.Get8BitBT709Luminance(
                ColorNumerics.DownScaleFrom16BitTo8Bit(source.R),
                ColorNumerics.DownScaleFrom16BitTo8Bit(source.G),
                ColorNumerics.DownScaleFrom16BitTo8Bit(source.B));

            this.A = ColorNumerics.DownScaleFrom16BitTo8Bit(source.A);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromScaledVector4(Vector4 vector) => this.ConvertFromRgbaScaledVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromVector4(Vector4 vector) => this.ConvertFromRgbaScaledVector4(vector);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ToRgba32(ref Rgba32 dest)
        {
            dest.R = this.L;
            dest.G = this.L;
            dest.B = this.L;
            dest.A = this.A;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToScaledVector4() => this.ToVector4();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToVector4()
        {
            const float Max = 255F;
            float rgb = this.L / Max;
            return new Vector4(rgb, rgb, rgb, this.A / Max);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal void ConvertFromRgbaScaledVector4(Vector4 vector)
        {
            vector *= MaxBytes;
            vector += Half;
            vector = Numerics.Clamp(vector, Vector4.Zero, MaxBytes);
            this.L = ColorNumerics.Get8BitBT709Luminance((byte)vector.X, (byte)vector.Y, (byte)vector.Z);
            this.A = (byte)vector.W;
        }
    }
}
