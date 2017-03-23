// <copyright file="IccNamedColor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;

    /// <summary>
    /// A specific color with a name
    /// </summary>
    internal struct IccNamedColor : IEquatable<IccNamedColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccNamedColor"/> struct.
        /// </summary>
        /// <param name="name">Name of the color</param>
        /// <param name="pcsCoordinates">Coordinates of the color in the profiles PCS</param>
        /// <param name="deviceCoordinates">Coordinates of the color in the profiles Device-Space</param>
        public IccNamedColor(string name, ushort[] pcsCoordinates, ushort[] deviceCoordinates)
        {
            Guard.NotNull(name, nameof(name));
            Guard.NotNull(pcsCoordinates, nameof(pcsCoordinates));
            Guard.IsTrue(pcsCoordinates.Length == 3, nameof(pcsCoordinates), "Must have a length of 3");

            this.Name = name;
            this.PcsCoordinates = pcsCoordinates;
            this.DeviceCoordinates = deviceCoordinates;
        }

        /// <summary>
        /// Gets the name of the color
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the coordinates of the color in the profiles PCS
        /// </summary>
        public ushort[] PcsCoordinates { get; }

        /// <summary>
        /// Gets the coordinates of the color in the profiles Device-Space
        /// </summary>
        public ushort[] DeviceCoordinates { get; }

        /// <summary>
        /// Compares two <see cref="IccNamedColor"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="IccNamedColor"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="IccNamedColor"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(IccNamedColor left, IccNamedColor right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="IccNamedColor"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="IccNamedColor"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="IccNamedColor"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(IccNamedColor left, IccNamedColor right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public override bool Equals(object other)
        {
            return (other is IccNamedColor) && this.Equals((IccNamedColor)other);
        }

        /// <inheritdoc/>
        public bool Equals(IccNamedColor other)
        {
            return this.Name == other.Name
                && this.PcsCoordinates.SequenceEqual(other.PcsCoordinates)
                && this.DeviceCoordinates.SequenceEqual(other.DeviceCoordinates);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Name.GetHashCode();
                hashCode = (hashCode * 397) ^ this.PcsCoordinates.GetHashCode();
                hashCode = (hashCode * 397) ^ this.DeviceCoordinates.GetHashCode();
                return hashCode;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.Name;
        }
    }
}
