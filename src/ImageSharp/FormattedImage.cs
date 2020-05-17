// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Collections.Generic;
using SixLabors.ImageSharp.Formats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Struct to curry <see cref="Image"/> and <see cref="IImageFormat"/> for return from async overloads.
    /// </summary>
    public readonly struct FormattedImage : IEquatable<FormattedImage>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FormattedImage"/> struct.
        /// </summary>
        /// <param name="image">The <see cref="Image"/>.</param>
        /// <param name="format">The <see cref="IImageFormat"/>.</param>
        public FormattedImage(Image image, IImageFormat format)
        {
            this.Image = image;
            this.Format = format;
        }

        /// <summary>
        /// Gets the Image.
        /// </summary>
        public readonly Image Image { get; }

        /// <summary>
        /// Gets the Format.
        /// </summary>
        public readonly IImageFormat Format { get; }

        /// <summary>
        /// Converts <see cref="FormattedImage"/> to <see cref="ValueTuple"/>.
        /// </summary>
        /// <param name="value">The <see cref="FormattedImage"/> to convert.</param>
        public static implicit operator (Image image, IImageFormat format)(FormattedImage value)
            => (value.Image, value.Format);

        /// <summary>
        /// Converts <see cref="ValueTuple"/> to <see cref="FormattedImage"/>
        /// </summary>
        /// <param name="value">The <see cref="ValueTuple"/> to convert.</param>
        public static implicit operator FormattedImage((Image image, IImageFormat format) value)
            => new FormattedImage(value.image, value.format);

        /// <summary>
        /// Compares two <see cref="FormattedImage"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="FormattedImage"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="FormattedImage"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(FormattedImage left, FormattedImage right)
            => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="FormattedImage"/> objects for inequality.
        /// </summary>
        /// <param name="left">The <see cref="FormattedImage"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="FormattedImage"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(FormattedImage left, FormattedImage right)
            => !(left == right);

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is FormattedImage image && this.Equals(image);

        /// <inheritdoc/>
        public bool Equals(FormattedImage other)
            => EqualityComparer<Image>.Default.Equals(this.Image, other.Image)
            && EqualityComparer<IImageFormat>.Default.Equals(this.Format, other.Format);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.Image, this.Format);

        /// <summary>
        /// Deconstructs <see cref="FormattedImage"/> into component parts.
        /// </summary>
        /// <param name="image">The <see cref="Image"/>.</param>
        /// <param name="format">The <see cref="IImageFormat"/>.</param>
        public void Deconstruct(out Image image, out IImageFormat format)
        {
            image = this.Image;
            format = this.Format;
        }
    }
}
