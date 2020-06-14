// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Represents the chromaticity coordinates of RGB primaries.
    /// One of the specifiers of <see cref="RgbWorkingSpace"/>.
    /// </summary>
    public readonly struct RgbPrimariesChromaticityCoordinates : IEquatable<RgbPrimariesChromaticityCoordinates>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RgbPrimariesChromaticityCoordinates"/> struct.
        /// </summary>
        /// <param name="r">The chromaticity coordinates of the red channel.</param>
        /// <param name="g">The chromaticity coordinates of the green channel.</param>
        /// <param name="b">The chromaticity coordinates of the blue channel.</param>
        public RgbPrimariesChromaticityCoordinates(CieXyChromaticityCoordinates r, CieXyChromaticityCoordinates g, CieXyChromaticityCoordinates b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
        }

        /// <summary>
        /// Gets the chromaticity coordinates of the red channel.
        /// </summary>
        public CieXyChromaticityCoordinates R { get; }

        /// <summary>
        /// Gets the chromaticity coordinates of the green channel.
        /// </summary>
        public CieXyChromaticityCoordinates G { get; }

        /// <summary>
        /// Gets the chromaticity coordinates of the blue channel.
        /// </summary>
        public CieXyChromaticityCoordinates B { get; }

        /// <summary>
        /// Compares two <see cref="RgbPrimariesChromaticityCoordinates"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="RgbPrimariesChromaticityCoordinates"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="RgbPrimariesChromaticityCoordinates"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(RgbPrimariesChromaticityCoordinates left, RgbPrimariesChromaticityCoordinates right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="RgbPrimariesChromaticityCoordinates"/> objects for inequality
        /// </summary>
        /// <param name="left">
        /// The <see cref="RgbPrimariesChromaticityCoordinates"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="RgbPrimariesChromaticityCoordinates"/> on the right side of the operand.
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
        public override int GetHashCode() => HashCode.Combine(this.R, this.G, this.B);
    }
}
