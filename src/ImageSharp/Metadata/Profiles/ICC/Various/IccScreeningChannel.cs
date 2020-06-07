// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// A single channel of a <see cref="IccScreeningTagDataEntry"/>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct IccScreeningChannel : IEquatable<IccScreeningChannel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccScreeningChannel"/> struct.
        /// </summary>
        /// <param name="frequency">Screen frequency</param>
        /// <param name="angle">Angle in degrees</param>
        /// <param name="spotShape">Spot shape</param>
        public IccScreeningChannel(float frequency, float angle, IccScreeningSpotType spotShape)
        {
            this.Frequency = frequency;
            this.Angle = angle;
            this.SpotShape = spotShape;
        }

        /// <summary>
        /// Gets the screen frequency.
        /// </summary>
        public float Frequency { get; }

        /// <summary>
        /// Gets the angle in degrees.
        /// </summary>
        public float Angle { get; }

        /// <summary>
        /// Gets the spot shape
        /// </summary>
        public IccScreeningSpotType SpotShape { get; }

        /// <summary>
        /// Compares two <see cref="IccScreeningChannel"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="IccScreeningChannel"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="IccScreeningChannel"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(IccScreeningChannel left, IccScreeningChannel right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="IccScreeningChannel"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="IccScreeningChannel"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="IccScreeningChannel"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(IccScreeningChannel left, IccScreeningChannel right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public bool Equals(IccScreeningChannel other) =>
            this.Frequency == other.Frequency &&
            this.Angle == other.Angle &&
            this.SpotShape == other.SpotShape;

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is IccScreeningChannel other && this.Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(this.Frequency, this.Angle, this.SpotShape);
        }

        /// <inheritdoc/>
        public override string ToString() => $"{this.Frequency}Hz; {this.Angle}°; {this.SpotShape}";
    }
}