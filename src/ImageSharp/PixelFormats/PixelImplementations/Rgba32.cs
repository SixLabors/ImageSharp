// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing four 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in red, green, blue, and alpha order (least significant to most significant byte).
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Rgba32 : IPixel<Rgba32>, IPackedVector<uint>
    {
        /// <summary>
        /// Gets or sets the red component.
        /// </summary>
        public byte R;

        /// <summary>
        /// Gets or sets the green component.
        /// </summary>
        public byte G;

        /// <summary>
        /// Gets or sets the blue component.
        /// </summary>
        public byte B;

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        public byte A;

        private static readonly Vector4 MaxBytes = new Vector4(byte.MaxValue);
        private static readonly Vector4 Half = new Vector4(0.5F);

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba32(byte r, byte g, byte b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = byte.MaxValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba32(byte r, byte g, byte b, byte a)
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba32(float r, float g, float b, float a = 1)
            : this() => this.Pack(r, g, b, a);

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba32(Vector3 vector)
            : this() => this.Pack(ref vector);

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba32(Vector4 vector)
            : this() => this = PackNew(ref vector);

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="packed">
        /// The packed value.
        /// </param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgba32(uint packed)
            : this() => this.Rgba = packed;

        /// <summary>
        /// Gets or sets the packed representation of the Rgba32 struct.
        /// </summary>
        public uint Rgba
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            readonly get => Unsafe.As<Rgba32, uint>(ref Unsafe.AsRef(this));

            [MethodImpl(InliningOptions.ShortMethod)]
            set => Unsafe.As<Rgba32, uint>(ref this) = value;
        }

        /// <summary>
        /// Gets or sets the RGB components of this struct as <see cref="Rgb24"/>
        /// </summary>
        public Rgb24 Rgb
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            readonly get => new Rgb24(this.R, this.G, this.B);

            [MethodImpl(InliningOptions.ShortMethod)]
            set
            {
                this.R = value.R;
                this.G = value.G;
                this.B = value.B;
            }
        }

        /// <summary>
        /// Gets or sets the RGB components of this struct as <see cref="Bgr24"/> reverting the component order.
        /// </summary>
        public Bgr24 Bgr
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            readonly get => new Bgr24(this.R, this.G, this.B);

            [MethodImpl(InliningOptions.ShortMethod)]
            set
            {
                this.R = value.R;
                this.G = value.G;
                this.B = value.B;
            }
        }

        /// <inheritdoc/>
        public uint PackedValue
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            readonly get => this.Rgba;

            [MethodImpl(InliningOptions.ShortMethod)]
            set => this.Rgba = value;
        }

        /// <summary>
        /// Converts an <see cref="Rgba32"/> to <see cref="Color"/>.
        /// </summary>
        /// <param name="source">The <see cref="Rgba32"/>.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static implicit operator Color(Rgba32 source) => new Color(source);

        /// <summary>
        /// Converts a <see cref="Color"/> to <see cref="Rgba32"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/>.</param>
        /// <returns>The <see cref="Rgba32"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static implicit operator Rgba32(Color color) => color.ToRgba32();

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="ColorSpaces.Rgb"/> to a
        /// <see cref="Rgba32"/>.
        /// </summary>
        /// <param name="color">The instance of <see cref="ColorSpaces.Rgb"/> to convert.</param>
        /// <returns>An instance of <see cref="Rgba32"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static implicit operator Rgba32(ColorSpaces.Rgb color)
        {
            var vector = new Vector4(color.ToVector3(), 1F);

            Rgba32 rgba = default;
            rgba.FromScaledVector4(vector);
            return rgba;
        }

        /// <summary>
        /// Compares two <see cref="Rgba32"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgba32"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgba32"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(Rgba32 left, Rgba32 right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Rgba32"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Rgba32"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Rgba32"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Rgba32 left, Rgba32 right) => !left.Equals(right);

        /// <summary>
        /// Creates a new instance of the <see cref="Rgba32"/> struct
        /// from the given hexadecimal string.
        /// </summary>
        /// <param name="hex">
        /// The hexadecimal representation of the combined color components arranged
        /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
        /// </param>
        /// <returns>
        /// The <see cref="Rgba32"/>.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Rgba32 ParseHex(string hex)
        {
            Guard.NotNull(hex, nameof(hex));

            if (!TryParseHex(hex, out Rgba32 rgba))
            {
                throw new ArgumentException("Hexadecimal string is not in the correct format.", nameof(hex));
            }

            return rgba;
        }

        /// <summary>
        /// Attempts to creates a new instance of the <see cref="Rgba32"/> struct
        /// from the given hexadecimal string.
        /// </summary>
        /// <param name="hex">
        /// The hexadecimal representation of the combined color components arranged
        /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
        /// </param>
        /// <param name="result">When this method returns, contains the <see cref="Rgba32"/> equivalent of the hexadecimal input.</param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool TryParseHex(string hex, out Rgba32 result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            hex = ToRgbaHex(hex);

            if (hex is null || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint packedValue))
            {
                return false;
            }

            packedValue = BinaryPrimitives.ReverseEndianness(packedValue);
            result = Unsafe.As<uint, Rgba32>(ref packedValue);
            return true;
        }

        /// <inheritdoc />
        public readonly PixelOperations<Rgba32> CreatePixelOperations() => new PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromScaledVector4(Vector4 vector) => this.FromVector4(vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToScaledVector4() => this.ToVector4();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromVector4(Vector4 vector) => this.Pack(ref vector);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly Vector4 ToVector4() => new Vector4(this.R, this.G, this.B, this.A) / MaxBytes;

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromArgb32(Argb32 source)
        {
            this.R = source.R;
            this.G = source.G;
            this.B = source.B;
            this.A = source.A;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgr24(Bgr24 source)
        {
            this.Bgr = source;
            this.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra32(Bgra32 source)
        {
            this.R = source.R;
            this.G = source.G;
            this.B = source.B;
            this.A = source.A;
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromBgra5551(Bgra5551 source) => this.FromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL8(L8 source)
        {
            this.R = source.PackedValue;
            this.G = source.PackedValue;
            this.B = source.PackedValue;
            this.A = byte.MaxValue;
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromL16(L16 source)
        {
            byte rgb = ColorNumerics.DownScaleFrom16BitTo8Bit(source.PackedValue);
            this.R = rgb;
            this.G = rgb;
            this.B = rgb;
            this.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa16(La16 source)
        {
            this.R = source.L;
            this.G = source.L;
            this.B = source.L;
            this.A = source.A;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromLa32(La32 source)
        {
            byte rgb = ColorNumerics.DownScaleFrom16BitTo8Bit(source.L);
            this.R = rgb;
            this.G = rgb;
            this.B = rgb;
            this.A = ColorNumerics.DownScaleFrom16BitTo8Bit(source.A);
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb24(Rgb24 source)
        {
            this.Rgb = source;
            this.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba32(Rgba32 source) => this = source;

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ToRgba32(ref Rgba32 dest)
        {
            dest = this;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgb48(Rgb48 source)
        {
            this.R = ColorNumerics.DownScaleFrom16BitTo8Bit(source.R);
            this.G = ColorNumerics.DownScaleFrom16BitTo8Bit(source.G);
            this.B = ColorNumerics.DownScaleFrom16BitTo8Bit(source.B);
            this.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void FromRgba64(Rgba64 source)
        {
            this.R = ColorNumerics.DownScaleFrom16BitTo8Bit(source.R);
            this.G = ColorNumerics.DownScaleFrom16BitTo8Bit(source.G);
            this.B = ColorNumerics.DownScaleFrom16BitTo8Bit(source.B);
            this.A = ColorNumerics.DownScaleFrom16BitTo8Bit(source.A);
        }

        /// <summary>
        /// Converts the value of this instance to a hexadecimal string.
        /// </summary>
        /// <returns>A hexadecimal string representation of the value.</returns>
        public readonly string ToHex()
        {
            uint hexOrder = (uint)(this.A << 0 | this.B << 8 | this.G << 16 | this.R << 24);
            return hexOrder.ToString("X8");
        }

        /// <inheritdoc/>
        public override readonly bool Equals(object obj) => obj is Rgba32 rgba32 && this.Equals(rgba32);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public readonly bool Equals(Rgba32 other) => this.Rgba.Equals(other.Rgba);

        /// <inheritdoc/>
        public override readonly string ToString() => $"Rgba32({this.R}, {this.G}, {this.B}, {this.A})";

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public override readonly int GetHashCode() => this.Rgba.GetHashCode();

        /// <summary>
        /// Packs a <see cref="Vector4"/> into a color returning a new instance as a result.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        /// <returns>The <see cref="Rgba32"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static Rgba32 PackNew(ref Vector4 vector)
        {
            vector *= MaxBytes;
            vector += Half;
            vector = Numerics.Clamp(vector, Vector4.Zero, MaxBytes);

            return new Rgba32((byte)vector.X, (byte)vector.Y, (byte)vector.Z, (byte)vector.W);
        }

        /// <summary>
        /// Packs the four floats into a color.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void Pack(float x, float y, float z, float w)
        {
            var value = new Vector4(x, y, z, w);
            this.Pack(ref value);
        }

        /// <summary>
        /// Packs a <see cref="Vector3"/> into a uint.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void Pack(ref Vector3 vector)
        {
            var value = new Vector4(vector, 1F);
            this.Pack(ref value);
        }

        /// <summary>
        /// Packs a <see cref="Vector4"/> into a color.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private void Pack(ref Vector4 vector)
        {
            vector *= MaxBytes;
            vector += Half;
            vector = Numerics.Clamp(vector, Vector4.Zero, MaxBytes);

            this.R = (byte)vector.X;
            this.G = (byte)vector.Y;
            this.B = (byte)vector.Z;
            this.A = (byte)vector.W;
        }

        /// <summary>
        /// Converts the specified hex value to an rrggbbaa hex value.
        /// </summary>
        /// <param name="hex">The hex value to convert.</param>
        /// <returns>
        /// A rrggbbaa hex value.
        /// </returns>
        private static string ToRgbaHex(string hex)
        {
            if (hex[0] == '#')
            {
                hex = hex.Substring(1);
            }

            if (hex.Length == 8)
            {
                return hex;
            }

            if (hex.Length == 6)
            {
                return hex + "FF";
            }

            if (hex.Length < 3 || hex.Length > 4)
            {
                return null;
            }

            char r = hex[0];
            char g = hex[1];
            char b = hex[2];
            char a = hex.Length == 3 ? 'F' : hex[3];

            return new string(new[] { r, r, g, g, b, b, a, a });
        }
    }
}
