// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// A specific color with a name
    /// </summary>
    internal readonly struct IccNamedColor : IEquatable<IccNamedColor>
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
        public static bool operator ==(IccNamedColor left, IccNamedColor right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="IccNamedColor"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="IccNamedColor"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="IccNamedColor"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(IccNamedColor left, IccNamedColor right) => !left.Equals(right);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is IccNamedColor other && this.Equals(other);

        /// <inheritdoc/>
        public bool Equals(IccNamedColor other)
        {
            return this.Name.Equals(other.Name)
                && this.PcsCoordinates.AsSpan().SequenceEqual(other.PcsCoordinates)
                && this.DeviceCoordinates.AsSpan().SequenceEqual(other.DeviceCoordinates);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.Name,
                this.PcsCoordinates,
                this.DeviceCoordinates);
        }

        /// <inheritdoc/>
        public override string ToString() => this.Name;
    }
}
