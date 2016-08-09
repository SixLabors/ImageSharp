// <copyright file="Color.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Packed vector type containing four 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in red, green, blue, and alpha order.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit)]
    public partial struct Color : IPackedVector<uint>, IEquatable<Color>
    {
        /// <summary>
        /// Gets or sets the blue component.
        /// </summary>
        [FieldOffset(0)]
        public byte R;

        /// <summary>
        /// Gets or sets the green component.
        /// </summary>
        [FieldOffset(1)]
        public byte G;

        /// <summary>
        /// Gets or sets the red component.
        /// </summary>
        [FieldOffset(2)]
        public byte B;

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        [FieldOffset(3)]
        public byte A;

        /// <summary>
        /// The packed value.
        /// </summary>
        [FieldOffset(0)]
        private uint packedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct. 
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        public Color(byte r, byte g, byte b, byte a = 255)
            : this()
        {
            this.R = r;
            this.G = g;
            this.B = b;
            this.A = a;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="hex">
        /// The hexadecimal representation of the combined color components arranged
        /// in rgb, rrggbb, or aarrggbb format to match web syntax.
        /// </param>
        public Color(string hex)
            : this()
        {
            // Hexadecimal representations are layed out AARRGGBB to we need to do some reordering.
            hex = hex.StartsWith("#") ? hex.Substring(1) : hex;

            if (hex.Length != 8 && hex.Length != 6 && hex.Length != 3)
            {
                throw new ArgumentException("Hexadecimal string is not in the correct format.", nameof(hex));
            }

            if (hex.Length == 8)
            {
                this.R = Convert.ToByte(hex.Substring(2, 2), 16);
                this.G = Convert.ToByte(hex.Substring(4, 2), 16);
                this.B = Convert.ToByte(hex.Substring(6, 2), 16);
                this.A = Convert.ToByte(hex.Substring(0, 2), 16);
            }
            else if (hex.Length == 6)
            {
                this.R = Convert.ToByte(hex.Substring(0, 2), 16);
                this.G = Convert.ToByte(hex.Substring(2, 2), 16);
                this.B = Convert.ToByte(hex.Substring(4, 2), 16);
                this.A = 255;
            }
            else
            {
                string rh = char.ToString(hex[0]);
                string gh = char.ToString(hex[1]);
                string bh = char.ToString(hex[2]);

                this.R = Convert.ToByte(rh + rh, 16);
                this.G = Convert.ToByte(gh + gh, 16);
                this.B = Convert.ToByte(bh + bh, 16);
                this.A = 255;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct. 
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        public Color(float r, float g, float b, float a = 1)
            : this()
        {
            Vector4 clamped = Vector4.Clamp(new Vector4(r, g, b, a), Vector4.Zero, Vector4.One) * 255F;
            this.R = (byte)Math.Round(clamped.X);
            this.G = (byte)Math.Round(clamped.Y);
            this.B = (byte)Math.Round(clamped.Z);
            this.A = (byte)Math.Round(clamped.W);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct. 
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        public Color(Vector3 vector)
            : this()
        {
            Vector3 clamped = Vector3.Clamp(vector, Vector3.Zero, Vector3.One) * 255F;
            this.R = (byte)Math.Round(clamped.X);
            this.G = (byte)Math.Round(clamped.Y);
            this.B = (byte)Math.Round(clamped.Z);
            this.A = 255;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct. 
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        public Color(Vector4 vector)
            : this()
        {
            Vector4 clamped = Vector4.Clamp(vector, Vector4.Zero, Vector4.One) * 255F;
            this.R = (byte)Math.Round(clamped.X);
            this.G = (byte)Math.Round(clamped.Y);
            this.B = (byte)Math.Round(clamped.Z);
            this.A = (byte)Math.Round(clamped.W);
        }

        /// <summary>
        /// Compares two <see cref="Color"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Color"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Color"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Color left, Color right)
        {
            return left.packedValue == right.packedValue;
        }

        /// <summary>
        /// Compares two <see cref="Color"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Color"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Color"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Color left, Color right)
        {
            return left.packedValue != right.packedValue;
        }

        /// <inheritdoc/>
        public uint GetPackedValue()
        {
            return this.packedValue;
        }

        /// <inheritdoc/>
        public void SetPackedValue(uint value)
        {
            this.packedValue = value;
        }

        /// <inheritdoc/>
        public void PackFromVector4(Vector4 vector)
        {
            Vector4 clamped = Vector4.Clamp(vector, Vector4.Zero, Vector4.One) * 255F;
            this.R = (byte)Math.Round(clamped.X);
            this.G = (byte)Math.Round(clamped.Y);
            this.B = (byte)Math.Round(clamped.Z);
            this.A = (byte)Math.Round(clamped.W);
        }

        /// <inheritdoc/>
        public void PackFromBytes(byte x, byte y, byte z, byte w)
        {
            this.R = x;
            this.G = y;
            this.B = z;
            this.A = w;
        }

        /// <inheritdoc/>
        public Vector4 ToVector4()
        {
            return new Vector4(this.R, this.G, this.B, this.A) / 255F;
        }

        /// <inheritdoc/>
        public byte[] ToBytes()
        {
            return new[] { this.R, this.G, this.B, this.A };
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return (obj is Color) && this.Equals((Color)obj);
        }

        /// <inheritdoc/>
        public bool Equals(Color other)
        {
            return this.packedValue == other.packedValue;
        }

        /// <summary>
        /// Gets a string representation of the packed vector.
        /// </summary>
        /// <returns>A string representation of the packed vector.</returns>
        public override string ToString()
        {
            return this.ToVector4().ToString();
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.GetHashCode(this);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="packed">
        /// The instance of <see cref="Color"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetHashCode(Color packed)
        {
            return packed.packedValue.GetHashCode();
        }
    }
}