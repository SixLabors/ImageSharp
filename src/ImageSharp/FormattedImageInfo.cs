// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Collections.Generic;
using SixLabors.ImageSharp.Formats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Struct to curry <see cref="IImageInfo"/> and <see cref="IImageFormat"/> for return from async overloads.
    /// </summary>
    public readonly struct FormattedImageInfo : IEquatable<FormattedImageInfo>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FormattedImageInfo"/> struct.
        /// </summary>
        /// <param name="imageInfo">The <see cref="FormattedImageInfo"/>.</param>
        /// <param name="format">The <see cref="IImageFormat"/>.</param>
        public FormattedImageInfo(IImageInfo imageInfo, IImageFormat format)
        {
            this.ImageInfo = imageInfo;
            this.Format = format;
        }

        /// <summary>
        /// Gets the Image Info.
        /// </summary>
        public readonly IImageInfo ImageInfo { get; }

        /// <summary>
        /// Gets the Format.
        /// </summary>
        public readonly IImageFormat Format { get; }

        /// <summary>
        /// Converts <see cref="FormattedImageInfo"/> to a <see cref="ValueTuple"/>
        /// </summary>
        /// <param name="value">The <see cref="FormattedImageInfo"/> to convert.</param>
        public static implicit operator (IImageInfo imageInfo, IImageFormat format)(FormattedImageInfo value)
            => (value.ImageInfo, value.Format);

        /// <summary>
        /// Converts <see cref="ValueTuple"/> to <see cref="FormattedImageInfo"/>
        /// </summary>
        /// <param name="value">The <see cref="ValueTuple"/> to convert.</param>
        public static implicit operator FormattedImageInfo((IImageInfo imageInfo, IImageFormat format) value)
            => new FormattedImageInfo(value.imageInfo, value.format);

        /// <summary>
        /// Compares two <see cref="FormattedImageInfo"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="FormattedImageInfo"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="FormattedImageInfo"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(FormattedImageInfo left, FormattedImageInfo right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="FormattedImageInfo"/> objects for inequality.
        /// </summary>
        /// <param name="left">The <see cref="FormattedImageInfo"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="FormattedImageInfo"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(FormattedImageInfo left, FormattedImageInfo right) => !(left == right);

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is FormattedImageInfo info && this.Equals(info);

        /// <inheritdoc/>
        public bool Equals(FormattedImageInfo other)
            => EqualityComparer<IImageInfo>.Default.Equals(this.ImageInfo, other.ImageInfo)
            && EqualityComparer<IImageFormat>.Default.Equals(this.Format, other.Format);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.ImageInfo, this.Format);

        /// <summary>
        /// Deconstructs <see cref="FormattedImageInfo"/> into component parts.
        /// </summary>
        /// <param name="imageInfo">The <see cref="FormattedImageInfo"/>.</param>
        /// <param name="format">The <see cref="IImageFormat"/>.</param>
        public void Deconstruct(out IImageInfo imageInfo, out IImageFormat format)
        {
            imageInfo = this.ImageInfo;
            format = this.Format;
        }
    }
}
