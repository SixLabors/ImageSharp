// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageProperty.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Stores meta information about a image, like the name of the author,
//   the copyright information, the date, where the image was created
//   or some other information.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor
{
    using System;

    /// <summary>
    /// Stores meta information about a image, like the name of the author,
    /// the copyright information, the date, where the image was created
    /// or some other information.
    /// </summary>
    public struct ImageProperty : IEquatable<ImageProperty>
    {
        /// <summary>
        /// Gets the name of this <see cref="ImageProperty"/> indicating which kind of 
        /// information this property stores.
        /// </summary>
        /// <example>
        /// Typical properties are the author, copyright
        /// information or other meta information.
        /// </example>
        public string Name { get; }

        /// <summary>
        /// The value of this <see cref="ImageProperty"/>.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageProperty"/> struct.
        /// </summary>
        /// <param name="name">
        /// The name of the property.
        /// </param>
        /// <param name="value">
        /// The value of the property.
        /// </param>
        public ImageProperty(string name, string value)
        {
            this.Name = name;
            this.Value = value;
        }

        /// <summary>
        /// Compares two <see cref="ImageProperty"/> objects. The result specifies whether the values
        /// of the <see cref="ImageProperty.Name"/> or <see cref="ImageProperty.Value"/> properties of the two
        /// <see cref="ImageProperty"/> objects are equal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="ImageProperty"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="ImageProperty"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(ImageProperty left, ImageProperty right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="ImageProperty"/> objects. The result specifies whether the values
        /// of the <see cref="ImageProperty.Name"/> or <see cref="ImageProperty.Value"/> properties of the two
        /// <see cref="ImageProperty"/> objects are unequal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="ImageProperty"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="ImageProperty"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(ImageProperty left, ImageProperty right)
        {
            return !left.Equals(right);
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
            if (!(obj is ImageProperty))
            {
                return false;
            }

            ImageProperty other = (ImageProperty)obj;

            return other.Name == this.Name && other.Value == this.Value;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Name.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Value.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Returns the fully qualified type name of this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> containing a fully qualified type name.
        /// </returns>
        public override string ToString()
        {
            return $"ImageProperty [ Name={this.Name}, Value={this.Value} ]";
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// True if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(ImageProperty other)
        {
            return this.Name.Equals(other.Name) && this.Value.Equals(other.Value);
        }
    }
}
