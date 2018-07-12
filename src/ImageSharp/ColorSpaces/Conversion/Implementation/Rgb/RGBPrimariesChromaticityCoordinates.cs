// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce
{
    /// <summary>
    /// Represents the chromaticity coordinates of RGB primaries.
    /// One of the specifiers of <see cref="RgbWorkingSpace"/>.
    /// </summary>
    internal readonly struct RgbPrimariesChromaticityCoordinates : IEquatable<RgbPrimariesChromaticityCoordinates>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RgbPrimariesChromaticityCoordinates"/> struct.
        /// </summary>
        /// <param name="r">The chomaticity coordinates of the red channel.</param>
        /// <param name="g">The chomaticity coordinates of the green channel.</param>
        /// <param name="b">The chomaticity coordinates of the blue channel.</param>
        public RgbPrimariesChromaticityCoordinates(CieXyChromaticityCoordinates r, CieXyChromaticityCoordinates g, CieXyChromaticityCoordinates b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
        }

        /// <summary>
        /// Gets the chomaticity coordinates of the red channel.
        /// </summary>
        public CieXyChromaticityCoordinates R { get; }

        /// <summary>
        /// Gets the chomaticity coordinates of the green channel.
        /// </summary>
        public CieXyChromaticityCoordinates G { get; }

        /// <summary>
        /// Gets the chomaticity coordinates of the blue channel.
        /// </summary>
        public CieXyChromaticityCoordinates B { get; }

        /// <summary>
        /// Compares two <see cref="CieLab"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieLab"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieLab"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(RgbPrimariesChromaticityCoordinates left, RgbPrimariesChromaticityCoordinates right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="CieLab"/> objects for inequality
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieLab"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieLab"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(RgbPrimariesChromaticityCoordinates left, RgbPrimariesChromaticityCoordinates right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is RgbPrimariesChromaticityCoordinates other && this.Equals(other);
        }

        /// <inheritdoc/>
        public bool Equals(RgbPrimariesChromaticityCoordinates other)
        {
            return this.R.Equals(other.R) && this.G.Equals(other.G) && this.B.Equals(other.B);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.R.GetHashCode();
                hashCode = (hashCode * 397) ^ this.G.GetHashCode();
                hashCode = (hashCode * 397) ^ this.B.GetHashCode();
                return hashCode;
            }
        }
    }
}