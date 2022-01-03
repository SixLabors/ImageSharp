// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Pixel type containing four 32-bit unsigned normalized values ranging from 0 to 4294967295.
    /// The color components are stored in red, green, blue and alpha.
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Rgba128 : IPixel<Rgba128>
    {
        private const float InvMax = 1.0f / uint.MaxValue;

        private const double Max = uint.MaxValue;

        /// <summary>
        /// Gets the red component.
        /// </summary>
        public uint R;

        /// <summary>
        /// Gets the green component.
        /// </summary>
        public uint G;

        /// <summary>
        /// Gets the blue component.
        /// </summary>
        public uint B;

        /// <summary>
        /// Gets the alpha channel.
        /// </summary>
        public uint A;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba128"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgba128(uint r, uint g, uint b, uint a)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        /// <summary>
        /// Compares two <see cref="Rgba128"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgba128"/> on the left side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="right">The <see cref="Rgba128"/> on the right side of the operand.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Rgba128 left, Rgba128 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Rgba128"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgba128"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgba128"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Rgba128 left, Rgba128 right) => !left.Equals(right);

        /// <inheritdoc />
        public PixelOperations<Rgba128> CreatePixelOperations() => new();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromScaledVector4(Vector4 vector) => this.FromVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToScaledVector4() => this.ToVector4();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromVector4(Vector4 vector)
        {
            vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One);
            this.R = (uint)(vector.X * Max);
            this.G = (uint)(vector.Y * Max);
            this.B = (uint)(vector.Z * Max);
            this.A = (uint)(vector.W * Max);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4() => new(
                this.R * InvMax,
                this.G * InvMax,
                this.B * InvMax,
                this.A * InvMax);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromBgra5551(Bgra5551 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromArgb32(Argb32 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromBgr24(Bgr24 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromBgra32(Bgra32 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromL8(L8 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromL16(L16 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromLa16(La16 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromLa32(La32 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromRgb24(Rgb24 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromRgba32(Rgba32 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromAbgr32(Abgr32 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest) => dest.FromScaledVector4(this.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromRgb48(Rgb48 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromRgba64(Rgba64 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Rgba128 other && this.Equals(other);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => HashCode.Combine(this.R, this.G, this.B, this.A);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Rgba128 other) => this.R.Equals(other.R) && this.G.Equals(other.G) && this.B.Equals(other.B) && this.A.Equals(other.A);

        /// <inheritdoc />
        public override string ToString() => FormattableString.Invariant($"Rgba128({this.R}, {this.G}, {this.B}, {this.A})");
    }
}
