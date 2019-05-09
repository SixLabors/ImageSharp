// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

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
        public Color(Rgba64 pixel)
        {
            this.data = pixel;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Rgba32"/> containing the color information.</param>
        public Color(Rgba32 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Argb32"/> containing the color information.</param>
        public Color(Argb32 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Bgra32"/> containing the color information.</param>
        public Color(Bgra32 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Rgb24"/> containing the color information.</param>
        public Color(Rgb24 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="pixel">The <see cref="Bgr24"/> containing the color information.</param>
        public Color(Bgr24 pixel)
        {
            this.data = new Rgba64(pixel);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="vector">The <see cref="Vector4"/> containing the color information.</param>
        public Color(Vector4 vector)
        {
            this.data = new Rgba64(vector);
        }

        /// <summary>
        /// Converts an <see cref="Rgba64"/> to <see cref="Color"/>.
        /// </summary>
        /// <param name="source">The <see cref="Rgba64"/>.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        public static implicit operator Color(Rgba64 source) => new Color(source);

        /// <summary>
        /// Converts an <see cref="Rgba32"/> to <see cref="Color"/>.
        /// </summary>
        /// <param name="source">The <see cref="Rgba32"/>.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        public static implicit operator Color(Rgba32 source) => new Color(source);

        /// <summary>
        /// Converts an <see cref="Bgra32"/> to <see cref="Color"/>.
        /// </summary>
        /// <param name="source">The <see cref="Bgra32"/>.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        public static implicit operator Color(Bgra32 source) => new Color(source);

        /// <summary>
        /// Converts an <see cref="Argb32"/> to <see cref="Color"/>.
        /// </summary>
        /// <param name="source">The <see cref="Argb32"/>.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        public static implicit operator Color(Argb32 source) => new Color(source);

        /// <summary>
        /// Converts an <see cref="Rgb24"/> to <see cref="Color"/>.
        /// </summary>
        /// <param name="source">The <see cref="Rgb24"/>.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        public static implicit operator Color(Rgb24 source) => new Color(source);

        /// <summary>
        /// Converts an <see cref="Bgr24"/> to <see cref="Color"/>.
        /// </summary>
        /// <param name="source">The <see cref="Bgr24"/>.</param>
        /// <returns>The <see cref="Color"/>.</returns>
        public static implicit operator Color(Bgr24 source) => new Color(source);

        /// <summary>
        /// Converts a <see cref="Color"/> to <see cref="Rgba64"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/>.</param>
        /// <returns>The <see cref="Rgba64"/>.</returns>
        public static implicit operator Rgba64(Color color) => color.data;

        /// <summary>
        /// Converts a <see cref="Color"/> to <see cref="Rgba32"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/>.</param>
        /// <returns>The <see cref="Rgba32"/>.</returns>
        public static implicit operator Rgba32(Color color) => color.data.ToRgba32();

        /// <summary>
        /// Converts a <see cref="Color"/> to <see cref="Bgra32"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/>.</param>
        /// <returns>The <see cref="Bgra32"/>.</returns>
        public static implicit operator Bgra32(Color color) => color.data.ToBgra32();

        /// <summary>
        /// Converts a <see cref="Color"/> to <see cref="Argb32"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/>.</param>
        /// <returns>The <see cref="Argb32"/>.</returns>
        public static implicit operator Argb32(Color color) => color.data.ToArgb32();

        /// <summary>
        /// Converts a <see cref="Color"/> to <see cref="Rgb24"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/>.</param>
        /// <returns>The <see cref="Rgb24"/>.</returns>
        public static implicit operator Rgb24(Color color) => color.data.ToRgb24();

        /// <summary>
        /// Converts a <see cref="Color"/> to <see cref="Bgr24"/>.
        /// </summary>
        /// <param name="color">The <see cref="Color"/>.</param>
        /// <returns>The <see cref="Bgr24"/>.</returns>
        public static implicit operator Bgr24(Color color) => color.data.ToBgr24();
    }
}