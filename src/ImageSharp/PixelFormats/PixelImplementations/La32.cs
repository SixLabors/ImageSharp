// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing two 16-bit normalized values representing luminance and alpha.
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public partial struct La32 : IPixel<La32>, IPackedVector<uint>
    {
        private const float Max = ushort.MaxValue;

        /// <summary>
        /// Gets or sets the luminance component.
        /// </summary>
        [FieldOffset(0)]
        public ushort L;

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        [FieldOffset(2)]
        public ushort A;

        /// <summary>
        /// Initializes a new instance of the <see cref="La32"/> struct.
        /// </summary>
        /// <param name="l">The luminance component.</param>
        /// <param name="a">The alpha componant.</param>
        public La32(ushort l, ushort a)
        {
            this.L = l;
            this.A = a;
        }

        /// <inheritdoc/>
        public uint PackedValue
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            readonly get => Unsafe.As<La32, uint>(ref Unsafe.AsRef(this));

            [MethodImpl(InliningOptions.ShortMethod)]
            set => Unsafe.As<La32, uint>(ref this) = value;
        }

        /// <summary>
        /// Compares two <see cref="La32"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="La32"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="La32"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(La32 left, La32 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="La32"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="La32"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="La32"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(La32 left, La32 right) => !left.Equals(right);

        /// <inheritdoc/>
        public readonly PixelOperations<La32> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool Equals(La32 other) => this.PackedValue.Equals(other.PackedValue);

        /// <inheritdoc />
        public override readonly bool Equals(object obj) => obj is La32 other && this.Equals(other);

        /// <inheritdoc />
        public override readonly string ToString() => $"La32({this.L}, {this.A})";

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromArgb32(Argb32 source)
        {
            this.L = ColorNumerics.Get16BitBT709Luminance(
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.R),
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.G),
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.B));

            this.A = ColorNumerics.UpscaleFrom8BitTo16Bit(source.A);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgr24(Bgr24 source)
        {
            this.L = ColorNumerics.Get16BitBT709Luminance(
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.R),
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.G),
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.B));

            this.A = ushort.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra32(Bgra32 source)
        {
            this.L = ColorNumerics.Get16BitBT709Luminance(
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.R),
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.G),
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.B));

            this.A = ColorNumerics.UpscaleFrom8BitTo16Bit(source.A);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra5551(Bgra5551 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL16(L16 source)
        {
            this.L = source.PackedValue;
            this.A = ushort.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL8(L8 source)
        {
            this.L = ColorNumerics.UpscaleFrom8BitTo16Bit(source.PackedValue);
            this.A = ushort.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa16(La16 source)
        {
            this.L = ColorNumerics.UpscaleFrom8BitTo16Bit(source.L);
            this.A = ColorNumerics.UpscaleFrom8BitTo16Bit(source.A);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa32(La32 source) => this = source;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb24(Rgb24 source)
        {
            this.L = ColorNumerics.Get16BitBT709Luminance(
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.R),
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.G),
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.B));

            this.A = ushort.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb48(Rgb48 source)
        {
            this.L = ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B);
            this.A = ushort.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba32(Rgba32 source)
        {
            this.L = ColorNumerics.Get16BitBT709Luminance(
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.R),
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.G),
                ColorNumerics.UpscaleFrom8BitTo16Bit(source.B));

            this.A = ColorNumerics.UpscaleFrom8BitTo16Bit(source.A);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba64(Rgba64 source)
        {
            this.L = ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B);
            this.A = source.A;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromScaledVector4(Vector4 vector) => this.ConvertFromRgbaScaledVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromVector4(Vector4 vector) => this.ConvertFromRgbaScaledVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ToRgba32(ref Rgba32 dest)
        {
            byte rgb = ColorNumerics.DownScaleFrom16BitTo8Bit(this.L);
            dest.R = rgb;
            dest.G = rgb;
            dest.B = rgb;
            dest.A = ColorNumerics.DownScaleFrom16BitTo8Bit(this.A);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToScaledVector4() => this.ToVector4();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToVector4()
        {
            float scaled = this.L / Max;
            return new Vector4(scaled, scaled, scaled, this.A / Max);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal void ConvertFromRgbaScaledVector4(Vector4 vector)
        {
            vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One) * Max;
            this.L = ColorNumerics.Get16BitBT709Luminance(
                vector.X,
                vector.Y,
                vector.Z);

            this.A = (ushort)MathF.Round(vector.W);
        }
    }
}
