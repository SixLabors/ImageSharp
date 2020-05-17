// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Collections.Generic;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Struct to curry <see cref="Image{TPixel}"/> and <see cref="IImageFormat"/> for return from async overloads.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public readonly struct FormattedImage<TPixel> : IEquatable<FormattedImage<TPixel>>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FormattedImage{TPixel}"/> struct.
        /// </summary>
        /// <param name="image">The <see cref="Image{TPixel}"/>.</param>
        /// <param name="format">The <see cref="IImageFormat"/>.</param>
        public FormattedImage(Image<TPixel> image, IImageFormat format)
        {
            this.Image = image;
            this.Format = format;
        }

        /// <summary>
        /// Gets the Image.
        /// </summary>
        public readonly Image<TPixel> Image { get; }

        /// <summary>
        /// Gets the Format.
        /// </summary>
        public readonly IImageFormat Format { get; }

        /// <summary>
        /// Converts <see cref="FormattedImage{TPixel}"/> to <see cref="ValueTuple"/>.
        /// </summary>
        /// <param name="value">The <see cref="FormattedImage{TPixel}"/> to convert.</param>
        public static implicit operator (Image<TPixel> image, IImageFormat format)(FormattedImage<TPixel> value)
            => (value.Image, value.Format);

        /// <summary>
        /// Converts <see cref="ValueTuple"/> to <see cref="FormattedImage{TPixel}"/>
        /// </summary>
        /// <param name="value">The <see cref="ValueTuple"/> to convert.</param>
        public static implicit operator FormattedImage<TPixel>((Image<TPixel> image, IImageFormat format) value)
            => new FormattedImage<TPixel>(value.image, value.format);

        /// <summary>
        /// Compares two <see cref="FormattedImage{TPixel}"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="FormattedImage{TPixel}"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="FormattedImage{TPixel}"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(FormattedImage<TPixel> left, FormattedImage<TPixel> right)
            => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="FormattedImage{TPixel}"/> objects for inequality.
        /// </summary>
        /// <param name="left">The <see cref="FormattedImage{TPixel}"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="FormattedImage{TPixel}"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(FormattedImage<TPixel> left, FormattedImage<TPixel> right)
            => !(left == right);

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is FormattedImage<TPixel> image && this.Equals(image);

        /// <inheritdoc/>
        public bool Equals(FormattedImage<TPixel> other)
            => EqualityComparer<Image<TPixel>>.Default.Equals(this.Image, other.Image)
            && EqualityComparer<IImageFormat>.Default.Equals(this.Format, other.Format);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.Image, this.Format);

        /// <summary>
        /// Deconstructs <see cref="FormattedImage"/> into component parts.
        /// </summary>
        /// <param name="image">The <see cref="Image{TPixel}"/>.</param>
        /// <param name="format">The <see cref="IImageFormat"/>.</param>
        public void Deconstruct(out Image<TPixel> image, out IImageFormat format)
        {
            image = this.Image;
            format = this.Format;
        }
    }
}
