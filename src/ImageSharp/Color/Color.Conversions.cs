// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <content>
    /// Contains constructors and implicit conversion methods.
    /// </content>
    public readonly partial struct Color
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Rgba64"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Rgba64 pixel)
        {
            RgbaVector vector = default;
            vector.FromRgba64(pixel);
            this.data = vector;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Rgba32"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Rgba32 pixel)
        {
            RgbaVector vector = default;
            vector.FromRgba32(pixel);
            this.data = vector;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Argb32"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Argb32 pixel)
        {
            RgbaVector vector = default;
            vector.FromArgb32(pixel);
            this.data = vector;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Bgra32"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Bgra32 pixel)
        {
            RgbaVector vector = default;
            vector.FromBgra32(pixel);
            this.data = vector;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Rgb24"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Rgb24 pixel)
        {
            RgbaVector vector = default;
            vector.FromRgb24(pixel);
            this.data = vector;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Bgr24"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Bgr24 pixel)
        {
            RgbaVector vector = default;
            vector.FromBgr24(pixel);
            this.data = vector;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="vector">The <see cref="Vector4"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Vector4 vector)
        {
            vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One);
            this.data = new RgbaVector(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <summary>
        /// Converts a <see cref="Color"/> to <see cref="Vector4"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/>.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        public static explicit operator Vector4(Color color) => color.data.ToScaledVector4();

        /// <summary>
        /// Converts an <see cref="Vector4"/> to <see cref="Color"/>.
        /// </summary>
        /// <param name="source">The <see cref="Vector4"/>.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static explicit operator Color(Vector4 source) => new(source);

        [MethodImpl(InliningOptions.ShortMethod)]
        internal Rgba32 ToRgba32()
        {
            Rgba32 result = default;
            result.FromScaledVector4(this.data.ToScaledVector4());
            return result;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal Bgra32 ToBgra32()
        {
            Bgra32 result = default;
            result.FromScaledVector4(this.data.ToScaledVector4());
            return result;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal Argb32 ToArgb32()
        {
            Argb32 result = default;
            result.FromScaledVector4(this.data.ToScaledVector4());
            return result;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal Rgb24 ToRgb24()
        {
            Rgb24 result = default;
            result.FromScaledVector4(this.data.ToScaledVector4());
            return result;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal Bgr24 ToBgr24()
        {
            Bgr24 result = default;
            result.FromScaledVector4(this.data.ToScaledVector4());
            return result;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal Vector4 ToVector4() => this.data.ToVector4();
    }
}
