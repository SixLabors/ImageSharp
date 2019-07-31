// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata
{
    /// <summary>
    /// Stores meta information about a image, like the name of the author,
    /// the copyright information, the date, where the image was created
    /// or some other information.
    /// </summary>
    public readonly struct GifTextData : IEquatable<GifTextData>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GifTextData"/> struct.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        public GifTextData(string name, string value)
        {
            Guard.NotNullOrWhiteSpace(name, nameof(name));

            this.Name = name;
            this.Value = value;
        }

        /// <summary>
        /// Gets the name of this <see cref="GifTextData"/> indicating which kind of
        /// information this property stores.
        /// </summary>
        /// <example>
        /// Typical properties are the author, copyright
        /// information or other meta information.
        /// </example>
        public string Name { get; }

        /// <summary>
        /// Gets the value of this <see cref="GifTextData"/>.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Compares two <see cref="GifTextData"/> objects. The result specifies whether the values
        /// of the <see cref="GifTextData.Name"/> or <see cref="GifTextData.Value"/> properties of the two
        /// <see cref="GifTextData"/> objects are equal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="GifTextData"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="GifTextData"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(GifTextData left, GifTextData right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="GifTextData"/> objects. The result specifies whether the values
        /// of the <see cref="GifTextData.Name"/> or <see cref="GifTextData.Value"/> properties of the two
        /// <see cref="GifTextData"/> objects are unequal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="GifTextData"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="GifTextData"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(GifTextData left, GifTextData right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <param name="obj">
        /// The object to compare with the current instance.
        /// </param>
        /// <returns>
        /// true if <paramref name="obj"/> and this instance are the same type and represent the
        /// same value; otherwise, false.
        /// </returns>
        public override bool Equals(object obj)
        {
            return obj is GifTextData other && this.Equals(other);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        public override int GetHashCode() => HashCode.Combine(this.Name, this.Value);

        /// <summary>
        /// Returns the fully qualified type name of this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> containing a fully qualified type name.
        /// </returns>
        public override string ToString() => $"ImageProperty [ Name={this.Name}, Value={this.Value} ]";

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// True if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(GifTextData other)
        {
            return this.Name.Equals(other.Name) && Equals(this.Value, other.Value);
        }
    }
}
