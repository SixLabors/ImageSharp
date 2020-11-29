// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Unpacked pixel type containing four 32-bit floating-point values typically ranging from 0 to 1.
    /// The color components are stored in red, green, blue, and alpha order.
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public partial struct RgbaVector : IPixel<RgbaVector>
    {
        /// <summary>
        /// Gets or sets the red component.
        /// </summary>
        public float R;

        /// <summary>
        /// Gets or sets the green component.
        /// </summary>
        public float G;

        /// <summary>
        /// Gets or sets the blue component.
        /// </summary>
        public float B;

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        public float A;

        private const float MaxBytes = byte.MaxValue;
        private static readonly Vector4 Max = new Vector4(MaxBytes);
        private static readonly Vector4 Half = new Vector4(0.5F);

        /// <summary>
        /// Initializes a new instance of the <see cref="RgbaVector"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public RgbaVector(float r, float g, float b, float a = 1)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        /// <summary>
        /// Compares two <see cref="RgbaVector"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="RgbaVector"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="RgbaVector"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(RgbaVector left, RgbaVector right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="RgbaVector"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="RgbaVector"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="RgbaVector"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(RgbaVector left, RgbaVector right) => !left.Equals(right);

        /// <summary>
        /// Creates a new instance of the <see cref="RgbaVector"/> struct.
        /// </summary>
        /// <param name="hex">
        /// The hexadecimal representation of the combined color components arranged
        /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
        /// </param>
        /// <returns>
        /// The <see cref="RgbaVector"/>.
        /// </returns>
        public static RgbaVector FromHex(string hex) => Color.ParseHex(hex).ToPixel<RgbaVector>();

        /// <inheritdoc />
        public readonly PixelOperations<RgbaVector> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromScaledVector4(Vector4 vector) => this.FromVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToScaledVector4() => this.ToVector4();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromVector4(Vector4 vector)
        {
            vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One);
            this.R = vector.X;
            this.G = vector.Y;
            this.B = vector.Z;
            this.A = vector.W;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToVector4() => new Vector4(this.R, this.G, this.B, this.A);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromArgb32(Argb32 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgr24(Bgr24 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra32(Bgra32 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra5551(Bgra5551 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL8(L8 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
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

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba32(Rgba32 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ToRgba32(ref Rgba32 dest) => dest.FromScaledVector4(this.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb48(Rgb48 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba64(Rgba64 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <summary>
        /// Converts the value of this instance to a hexadecimal string.
        /// </summary>
        /// <returns>A hexadecimal string representation of the value.</returns>
        public readonly string ToHex()
        {
            // Hex is RRGGBBAA
            Vector4 vector = this.ToVector4() * Max;
            vector += Half;
            uint hexOrder = (uint)((byte)vector.W | (byte)vector.Z << 8 | (byte)vector.Y << 16 | (byte)vector.X << 24);
            return hexOrder.ToString("X8");
        }

        /// <inheritdoc/>
        public override readonly bool Equals(object obj) => obj is RgbaVector other && this.Equals(other);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool Equals(RgbaVector other) =>
            this.R.Equals(other.R)
            && this.G.Equals(other.G)
            && this.B.Equals(other.B)
            && this.A.Equals(other.A);

        /// <inheritdoc/>
        public override readonly string ToString()
        {
            return FormattableString.Invariant($"RgbaVector({this.R:#0.##}, {this.G:#0.##}, {this.B:#0.##}, {this.A:#0.##})");
        }

        /// <inheritdoc/>
        public override readonly int GetHashCode() => HashCode.Combine(this.R, this.G, this.B, this.A);
    }
}
