// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Collections.Generic;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Struct to curry <see cref="ImageSharp.Image{TPixel}"/> and <see cref="IImageFormat"/> for return from async overloads.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public readonly struct FormattedImage<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FormattedImage{TPixel}"/> struct.
        /// </summary>
        /// <param name="image">The <see cref="ImageSharp.Image{TPixel}"/>.</param>
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
        {
            return (value.Image, value.Format);
        }

        /// <summary>
        /// Converts <see cref="ValueTuple"/> to <see cref="FormattedImage{TPixel}"/>
        /// </summary>
        /// <param name="value">The <see cref="ValueTuple"/> to convert.</param>
        public static implicit operator FormattedImage<TPixel>((Image<TPixel> image, IImageFormat format) value)
        {
            return new FormattedImage<TPixel>(value.image, value.format);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is FormattedImage<TPixel> other &&
                   EqualityComparer<Image<TPixel>>.Default.Equals(this.Image, other.Image) &&
                   EqualityComparer<IImageFormat>.Default.Equals(this.Format, other.Format);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(this.Image, this.Format);
        }

        /// <summary>
        /// Deconstructs <see cref="FormattedImage"/> into component parts.
        /// </summary>
        /// <param name="image">The <see cref="ImageSharp.Image{TPixel}"/>.</param>
        /// <param name="format">The <see cref="IImageFormat"/>.</param>
        public void Deconstruct(out Image<TPixel> image, out IImageFormat format)
        {
            image = this.Image;
            format = this.Format;
        }
    }
}
