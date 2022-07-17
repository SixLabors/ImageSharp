// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
            this.data = pixel;
            this.boxedHighPrecisionPixel = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Rgb48"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Rgb48 pixel)
        {
            this.data = new Rgba64(pixel.R, pixel.G, pixel.B, ushort.MaxValue);
            this.boxedHighPrecisionPixel = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="La32"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(La32 pixel)
        {
            this.data = new Rgba64(pixel.L, pixel.L, pixel.L, pixel.A);
            this.boxedHighPrecisionPixel = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="L16"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(L16 pixel)
        {
            this.data = new Rgba64(pixel.PackedValue, pixel.PackedValue, pixel.PackedValue, ushort.MaxValue);
            this.boxedHighPrecisionPixel = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Rgba32"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Rgba32 pixel)
        {
            this.data = new Rgba64(pixel);
            this.boxedHighPrecisionPixel = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Argb32"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Argb32 pixel)
        {
            this.data = new Rgba64(pixel);
            this.boxedHighPrecisionPixel = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Bgra32"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Bgra32 pixel)
        {
            this.data = new Rgba64(pixel);
            this.boxedHighPrecisionPixel = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Abgr32"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Abgr32 pixel)
        {
            this.data = new Rgba64(pixel);
            this.boxedHighPrecisionPixel = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Rgb24"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Rgb24 pixel)
        {
            this.data = new Rgba64(pixel);
            this.boxedHighPrecisionPixel = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Bgr24"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Bgr24 pixel)
        {
            this.data = new Rgba64(pixel);
            this.boxedHighPrecisionPixel = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="vector">The <see cref="Vector4"/> containing the color information.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Color(Vector4 vector)
        {
            vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One);
            this.boxedHighPrecisionPixel = new RgbaVector(vector.X, vector.Y, vector.Z, vector.W);
            this.data = default;
        }

        /// <summary>
        /// Converts a <see cref="Color"/> to <see cref="Vector4"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/>.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        public static explicit operator Vector4(Color color) => color.ToVector4();

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
            if (this.boxedHighPrecisionPixel is null)
            {
                return this.data.ToRgba32();
            }

            Rgba32 value = default;
            this.boxedHighPrecisionPixel.ToRgba32(ref value);
            return value;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal Bgra32 ToBgra32()
        {
            if (this.boxedHighPrecisionPixel is null)
            {
                return this.data.ToBgra32();
            }

            Bgra32 value = default;
            value.FromScaledVector4(this.boxedHighPrecisionPixel.ToScaledVector4());
            return value;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal Argb32 ToArgb32()
        {
            if (this.boxedHighPrecisionPixel is null)
            {
                return this.data.ToArgb32();
            }

            Argb32 value = default;
            value.FromScaledVector4(this.boxedHighPrecisionPixel.ToScaledVector4());
            return value;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal Abgr32 ToAbgr32()
        {
            if (this.boxedHighPrecisionPixel is null)
            {
                return this.data.ToAbgr32();
            }

            Abgr32 value = default;
            value.FromScaledVector4(this.boxedHighPrecisionPixel.ToScaledVector4());
            return value;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal Rgb24 ToRgb24()
        {
            if (this.boxedHighPrecisionPixel is null)
            {
                return this.data.ToRgb24();
            }

            Rgb24 value = default;
            value.FromScaledVector4(this.boxedHighPrecisionPixel.ToScaledVector4());
            return value;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal Bgr24 ToBgr24()
        {
            if (this.boxedHighPrecisionPixel is null)
            {
                return this.data.ToBgr24();
            }

            Bgr24 value = default;
            value.FromScaledVector4(this.boxedHighPrecisionPixel.ToScaledVector4());
            return value;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        internal Vector4 ToVector4()
        {
            if (this.boxedHighPrecisionPixel is null)
            {
                return this.data.ToScaledVector4();
            }

            return this.boxedHighPrecisionPixel.ToScaledVector4();
        }
    }
}
