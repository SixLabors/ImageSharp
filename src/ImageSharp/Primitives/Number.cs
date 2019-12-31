// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;

namespace SixLabors.ImageSharp.Primitives
{
    /// <summary>
    /// Represents an integral number.
    /// </summary>
    public struct Number : IEquatable<Number>, IComparable<Number>
    {
        private readonly uint value;

        /// <summary>
        /// Initializes a new instance of the <see cref="Number"/> struct.
        /// </summary>
        /// <param name="value">The value of the number.</param>
        public Number(uint value) => this.value = value;

        /// <summary>
        /// Converts the specified <see cref="uint"/> to an instance of this type.
        /// </summary>
        /// <param name="value">The value.</param>
        public static implicit operator Number(uint value) => new Number(value);

        /// <summary>
        /// Converts the specified <see cref="ushort"/> to an instance of this type.
        /// </summary>
        /// <param name="value">The value.</param>
        public static implicit operator Number(ushort value) => new Number(value);

        /// <summary>
        /// Converts the specified <see cref="Number"/> to a <see cref="uint"/>.
        /// </summary>
        /// <param name="number">The <see cref="Number"/> to convert.</param>
        public static explicit operator uint(Number number) => number.value;

        /// <summary>
        /// Converts the specified <see cref="Number"/> to a <see cref="ushort"/>.
        /// </summary>
        /// <param name="number">The <see cref="Number"/> to convert.</param>
        public static explicit operator ushort(Number number) => (ushort)number.value;

        /// <summary>
        /// Determines whether the specified <see cref="Number"/> instances are considered equal.
        /// </summary>
        /// <param name="left">The first <see cref="Number"/> to compare.</param>
        /// <param name="right"> The second <see cref="Number"/> to compare.</param>
        public static bool operator ==(Number left, Number right) => Equals(left, right);

        /// <summary>
        /// Determines whether the specified <see cref="Number"/> instances are not considered equal.
        /// </summary>
        /// <param name="left">The first <see cref="Number"/> to compare.</param>
        /// <param name="right"> The second <see cref="Number"/> to compare.</param>
        public static bool operator !=(Number left, Number right) => !Equals(left, right);

        /// <summary>
        /// Determines whether the first <see cref="Number"/> is more than the second <see cref="Number"/>.
        /// </summary>
        /// <param name="left">The first <see cref="Number"/> to compare.</param>
        /// <param name="right"> The second <see cref="Number"/> to compare.</param>
        public static bool operator >(Number left, Number right) => left.CompareTo(right) == 1;

        /// <summary>
        /// Determines whether the first <see cref="Number"/> is less than the second <see cref="Number"/>.
        /// </summary>
        /// <param name="left">The first <see cref="Number"/> to compare.</param>
        /// <param name="right"> The second <see cref="Number"/> to compare.</param>
        public static bool operator <(Number left, Number right) => left.CompareTo(right) == -1;

        /// <summary>
        /// Determines whether the first <see cref="Number"/> is more than or equal to the second <see cref="Number"/>.
        /// </summary>
        /// <param name="left">The first <see cref="Number"/> to compare.</param>
        /// <param name="right"> The second <see cref="Number"/> to compare.</param>
        public static bool operator >=(Number left, Number right) => left.CompareTo(right) >= 0;

        /// <summary>
        /// Determines whether the first <see cref="Number"/> is less than or equal to the second <see cref="Number"/>.
        /// </summary>
        /// <param name="left">The first <see cref="Number"/> to compare.</param>
        /// <param name="right"> The second <see cref="Number"/> to compare.</param>
        public static bool operator <=(Number left, Number right) => left.CompareTo(right) <= 0;

        /// <inheritdoc/>
        public int CompareTo(Number other) => this.value.CompareTo(other.value);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Number other && this.Equals(other);

        /// <inheritdoc/>
        public bool Equals(Number other) => this.value.Equals(other.value);

        /// <inheritdoc/>
        public override int GetHashCode() => this.value.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => this.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Converts the numeric value of this instance to its equivalent string representation using the specified culture-specific format information.
        /// </summary>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The string representation of the value of this instance, which consists of a sequence of digits ranging from 0 to 9, without a sign or leading zeros.</returns>
        public string ToString(IFormatProvider provider) => this.value.ToString(provider);
    }
}
