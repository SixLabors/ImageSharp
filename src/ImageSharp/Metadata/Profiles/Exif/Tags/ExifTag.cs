// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <summary>
    /// Class that represents an exif tag from the Exif standard 2.31.
    /// </summary>
    public abstract partial class ExifTag : IEquatable<ExifTag>
    {
        private readonly ushort value;

        internal ExifTag(ushort value) => this.value = value;

        /// <summary>
        /// Converts the specified <see cref="ExifTag"/> to a <see cref="ushort"/>.
        /// </summary>
        /// <param name="tag">The <see cref="ExifTag"/> to convert.</param>
        public static explicit operator ushort(ExifTag tag) => tag?.value ?? (ushort)ExifTagValue.Unknown;

        /// <summary>
        /// Determines whether the specified <see cref="ExifTag"/> instances are considered equal.
        /// </summary>
        /// <param name="left">The first <see cref="ExifTag"/> to compare.</param>
        /// <param name="right"> The second <see cref="ExifTag"/> to compare.</param>
        public static bool operator ==(ExifTag left, ExifTag right) => Equals(left, right);

        /// <summary>
        /// Determines whether the specified <see cref="ExifTag"/> instances are not considered equal.
        /// </summary>
        /// <param name="left">The first <see cref="ExifTag"/> to compare.</param>
        /// <param name="right"> The second <see cref="ExifTag"/> to compare.</param>
        public static bool operator !=(ExifTag left, ExifTag right) => !Equals(left, right);

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is ExifTag value)
            {
                return this.Equals(value);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(ExifTag other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.value == other.value;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => this.value.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => ((ExifTagValue)this.value).ToString();
    }
}
