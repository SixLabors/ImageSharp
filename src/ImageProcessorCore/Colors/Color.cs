// <copyright file="Color.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Packed vector type containing four 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in red, green, blue, and alpha order.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public partial struct Color : IPackedVector<uint>, IEquatable<Color>
    {
        /// <summary>
        /// The maximum byte value
        /// </summary>
        private const float MaxBytes = 255F;

        /// <summary>
        /// The minimum vector value
        /// </summary>
        private const float Zero = 0F;

        /// <summary>
        /// The maximum vector value
        /// </summary>
        private const float One = 1F;

        /// <summary>
        /// The packed value
        /// </summary>
        private uint packedValue;

        /// <summary>
        /// Gets or sets the red component.
        /// </summary>
        public byte R
        {
            get
            {
                return (byte)this.packedValue;
            }
            set
            {
                // AABBGGRR
                this.packedValue = (uint)(this.packedValue & -0x100 | value);
            }
        }


        /// <summary>
        /// Gets or sets the green component.
        /// </summary>
        public byte G
        {
            get
            {
                return (byte)(this.packedValue >> 8);
            }
            set
            {
                // AABBGGRR
                this.packedValue = (uint)(this.packedValue & -0xFF01 | (uint)value << 8);
            }
        }

        /// <summary>
        /// Gets or sets the blue component.
        /// </summary>
        public byte B
        {
            get
            {
                return (byte)(this.packedValue >> 16);
            }
            set
            {
                // AABBGGRR
                this.packedValue = (uint)(this.packedValue & -0xFF0001 | (uint)(value << 16));
            }
        }

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        public byte A
        {
            get
            {
                return (byte)(this.packedValue >> 24);
            }
            set
            {
                // AABBGGRR
                this.packedValue = this.packedValue & 0xFFFFFF | (uint)value << 24;
            }
        }

        /// <summary>
        /// The packed value.
        /// </summary>
        public uint PackedValue { get { return this.packedValue; } set { this.packedValue = value; } }

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
            this.packedValue = (uint)(r | g << 8 | b << 16 | a << 24);
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
                this.packedValue =
                    (uint)(Convert.ToByte(hex.Substring(2, 2), 16)
                    | Convert.ToByte(hex.Substring(4, 2), 16) << 8
                    | Convert.ToByte(hex.Substring(6, 2), 16) << 16
                    | Convert.ToByte(hex.Substring(0, 2), 16) << 24);
            }
            else if (hex.Length == 6)
            {
                this.packedValue =
                    (uint)(Convert.ToByte(hex.Substring(0, 2), 16)
                    | Convert.ToByte(hex.Substring(2, 2), 16) << 8
                    | Convert.ToByte(hex.Substring(4, 2), 16) << 16
                    | 255 << 24);
            }
            else
            {
                string rh = char.ToString(hex[0]);
                string gh = char.ToString(hex[1]);
                string bh = char.ToString(hex[2]);

                this.packedValue =
                    (uint)(Convert.ToByte(rh + rh, 16)
                    | Convert.ToByte(gh + gh, 16) << 8
                    | Convert.ToByte(bh + bh, 16) << 16
                    | 255 << 24);
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
            this.packedValue = Pack(r, g, b, a);
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
            this.packedValue = Pack(ref vector);
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
            this.packedValue = Pack(ref vector);
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
        public void PackFromVector4(Vector4 vector)
        {
            this.packedValue = Pack(ref vector);
        }

        /// <inheritdoc/>
        public Vector4 ToVector4()
        {
            return new Vector4(this.R, this.G, this.B, this.A) / MaxBytes;
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
        /// Packs a <see cref="Vector4"/> into a uint.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        /// <returns>The ulong containing the packed values.</returns>
        private static uint Pack(ref Vector4 vector)
        {
            return Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <summary>
        /// Packs a <see cref="Vector3"/> into a uint.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        /// <returns>The ulong containing the packed values.</returns>
        private static uint Pack(ref Vector3 vector)
        {
            return Pack(vector.X, vector.Y, vector.Z, 1);
        }

        /// <summary>
        /// Packs the four floats into a uint.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        /// <returns>The <see cref="uint"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Pack(float x, float y, float z, float w)
        {
            return (uint)((byte)Math.Round(x.Clamp(Zero, One) * MaxBytes)
                   | ((byte)Math.Round(y.Clamp(Zero, One) * MaxBytes) << 8)
                   | (byte)Math.Round(z.Clamp(Zero, One) * MaxBytes) << 16
                   | (byte)Math.Round(w.Clamp(Zero, One) * MaxBytes) << 24);
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