// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Unpacked pixel type containing four 16-bit floating-point values typically ranging from 0 to 1.
    /// The color components are stored in red, green, blue, and alpha order.
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public partial struct RgbaVector : IPixel<RgbaVector>
    {
        /// <summary>
        /// The maximum byte value.
        /// </summary>
        private static readonly Vector4 MaxBytes = new Vector4(255);

        /// <summary>
        /// The half vector value.
        /// </summary>
        private static readonly Vector4 Half = new Vector4(0.5F);

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private Vector4 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="RgbaVector"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RgbaVector(byte r, byte g, byte b, byte a = 255)
            : this()
        {
            this.backingVector = new Vector4(r, g, b, a) / MaxBytes;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RgbaVector"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RgbaVector(float r, float g, float b, float a = 1)
            : this()
        {
            this.backingVector = new Vector4(r, g, b, a);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RgbaVector"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RgbaVector(Vector3 vector)
            : this()
        {
            this.backingVector = new Vector4(vector, 1);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RgbaVector"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RgbaVector(Vector4 vector)
            : this()
        {
            this.backingVector = vector;
        }

        /// <summary>
        /// Gets or sets the red component.
        /// </summary>
        public float R
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return this.backingVector.X;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.backingVector.X = value;
            }
        }

        /// <summary>
        /// Gets or sets the green component.
        /// </summary>
        public float G
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return this.backingVector.Y;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.backingVector.Y = value;
            }
        }

        /// <summary>
        /// Gets or sets the blue component.
        /// </summary>
        public float B
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return this.backingVector.Z;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.backingVector.Z = value;
            }
        }

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        public float A
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return this.backingVector.W;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.backingVector.W = value;
            }
        }

        /// <summary>
        /// Compares two <see cref="RgbaVector"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="RgbaVector"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="RgbaVector"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(RgbaVector left, RgbaVector right)
        {
            return left.backingVector == right.backingVector;
        }

        /// <summary>
        /// Compares two <see cref="RgbaVector"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="RgbaVector"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="RgbaVector"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(RgbaVector left, RgbaVector right)
        {
            return left.backingVector != right.backingVector;
        }

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
        public static RgbaVector FromHex(string hex)
        {
            return ColorBuilder<RgbaVector>.FromHex(hex);
        }

        /// <inheritdoc />
        public PixelOperations<RgbaVector> CreatePixelOperations() => new RgbaVector.PixelOperations();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            this.backingVector = source.ToVector4();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromArgb32(Argb32 source)
        {
            this.backingVector = source.ToVector4();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromBgra32(Bgra32 source)
        {
            this.backingVector = source.ToVector4();
        }

        /// <summary>
        /// Converts the value of this instance to a hexadecimal string.
        /// </summary>
        /// <returns>A hexadecimal string representation of the value.</returns>
        public string ToHex()
        {
            // Hex is RRGGBBAA
            Vector4 vector = this.backingVector * MaxBytes;
            vector += Half;
            uint hexOrder = (uint)((byte)vector.W | (byte)vector.Z << 8 | (byte)vector.Y << 16 | (byte)vector.X << 24);
            return hexOrder.ToString("X8");
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
            dest.A = (byte)MathF.Round(vector.W);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToArgb32(ref Argb32 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
            dest.A = (byte)MathF.Round(vector.W);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
            dest.A = (byte)MathF.Round(vector.W);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgb48(Rgb48 source) => this.PackFromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb48(ref Rgb48 dest) => dest.PackFromScaledVector4(this.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba64(Rgba64 source) => this.PackFromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba64(ref Rgba64 dest) => dest.PackFromScaledVector4(this.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromScaledVector4(Vector4 vector)
        {
            this.PackFromVector4(vector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToScaledVector4()
        {
            return this.ToVector4();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            this.backingVector = vector;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return this.backingVector;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is RgbaVector other && this.Equals(other);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(RgbaVector other)
        {
            return this.backingVector == other.backingVector;
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
            return this.backingVector.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector4 ToByteScaledVector4() => Vector4.Clamp(this.backingVector, Vector4.Zero, Vector4.One) * MaxBytes;
    }
}