// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing a single 16 bit normalized gray values.
    /// <para>
    /// Ranges from [0, 0, 0, 1] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    public partial struct Gray16 : IPixel<Gray16>, IPackedVector<ushort>
    {
        private const float Max = ushort.MaxValue;
        private const float Average = 1 / 3F;

        /// <summary>
        /// Initializes a new instance of the <see cref="Gray16"/> struct.
        /// </summary>
        /// <param name="luminance">The luminance component</param>
        public Gray16(ushort luminance) => this.PackedValue = luminance;

        /// <inheritdoc />
        public ushort PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Gray16"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Gray16"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Gray16"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(Gray16 left, Gray16 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Gray16"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Gray16"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Gray16"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Gray16 left, Gray16 right) => !left.Equals(right);

        /// <inheritdoc />
        public PixelOperations<Gray16> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromScaledVector4(Vector4 vector) => this.FromVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Vector4 ToScaledVector4() => this.ToVector4();

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromVector4(Vector4 vector)
        {
            vector = Vector4.Clamp(vector, Vector4.Zero, Vector4.One) * Max * Average;
            this.PackedValue = (ushort)MathF.Round(vector.X + vector.Y + vector.Z);
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public Vector4 ToVector4()
        {
            float scaled = this.PackedValue / Max;
            return new Vector4(scaled, scaled, scaled, 1F);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromArgb32(Argb32 source)
        {
            this.PackedValue = ImageMaths.Get16BitBT709Luminance(
                ImageMaths.UpscaleFrom8BitTo16Bit(source.R),
                ImageMaths.UpscaleFrom8BitTo16Bit(source.G),
                ImageMaths.UpscaleFrom8BitTo16Bit(source.B));
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgr24(Bgr24 source)
        {
            this.PackedValue = ImageMaths.Get16BitBT709Luminance(
                ImageMaths.UpscaleFrom8BitTo16Bit(source.R),
                ImageMaths.UpscaleFrom8BitTo16Bit(source.G),
                ImageMaths.UpscaleFrom8BitTo16Bit(source.B));
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra32(Bgra32 source)
        {
            this.PackedValue = ImageMaths.Get16BitBT709Luminance(
                ImageMaths.UpscaleFrom8BitTo16Bit(source.R),
                ImageMaths.UpscaleFrom8BitTo16Bit(source.G),
                ImageMaths.UpscaleFrom8BitTo16Bit(source.B));
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromGray8(Gray8 source) => this.PackedValue = ImageMaths.UpscaleFrom8BitTo16Bit(source.PackedValue);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromGray16(Gray16 source) => this.PackedValue = source.PackedValue;

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb24(Rgb24 source)
        {
            this.PackedValue = ImageMaths.Get16BitBT709Luminance(
                ImageMaths.UpscaleFrom8BitTo16Bit(source.R),
                ImageMaths.UpscaleFrom8BitTo16Bit(source.G),
                ImageMaths.UpscaleFrom8BitTo16Bit(source.B));
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba32(Rgba32 source)
        {
            this.PackedValue = ImageMaths.Get16BitBT709Luminance(
                ImageMaths.UpscaleFrom8BitTo16Bit(source.R),
                ImageMaths.UpscaleFrom8BitTo16Bit(source.G),
                ImageMaths.UpscaleFrom8BitTo16Bit(source.B));
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ToRgba32(ref Rgba32 dest)
        {
            byte rgb = ImageMaths.DownScaleFrom16BitTo8Bit(this.PackedValue);
            dest.R = rgb;
            dest.G = rgb;
            dest.B = rgb;
            dest.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb48(Rgb48 source) => this.PackedValue = ImageMaths.Get16BitBT709Luminance(source.R, source.G, source.B);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba64(Rgba64 source) => this.PackedValue = ImageMaths.Get16BitBT709Luminance(source.R, source.G, source.B);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Gray16 other && this.Equals(other);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(Gray16 other) => this.PackedValue.Equals(other.PackedValue);

        /// <inheritdoc />
        public override string ToString() => $"Gray16({this.PackedValue})";

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override int GetHashCode() => this.PackedValue.GetHashCode();

        [MethodImpl(InliningOptions.ShortMethod)]
        internal void ConvertFromRgbaScaledVector4(Vector4 vector)
        {
            vector = Vector4.Clamp(vector, Vector4.Zero, Vector4.One) * Max;
            this.PackedValue = ImageMaths.Get16BitBT709Luminance(
                (ushort)MathF.Round(vector.X),
                (ushort)MathF.Round(vector.Y),
                (ushort)MathF.Round(vector.Z));
        }
    }
}