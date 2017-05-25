// <copyright file="RgbWorkingSpace.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.Rgb
{
    /// <summary>
    /// Trivial implementation of <see cref="IRgbWorkingSpace"/>
    /// </summary>
    internal struct RgbWorkingSpace : IRgbWorkingSpace
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RgbWorkingSpace"/> struct.
        /// </summary>
        /// <param name="referenceWhite">The reference white point.</param>
        /// <param name="companding">The function pair for converting to <see cref="CieXyz"/> and back.</param>
        /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
        public RgbWorkingSpace(CieXyz referenceWhite, ICompanding companding, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
        {
            this.WhitePoint = referenceWhite;
            this.Companding = companding;
            this.ChromaticityCoordinates = chromaticityCoordinates;
        }

        /// <summary>
        /// Gets the reference white point
        /// </summary>
        public CieXyz WhitePoint { get; }

        /// <summary>
        /// Gets the function pair for converting to <see cref="CieXyz"/> and back.
        /// </summary>
        public ICompanding Companding { get; }

        /// <summary>
        /// Gets the chromaticity of the rgb primaries.
        /// </summary>
        public RgbPrimariesChromaticityCoordinates ChromaticityCoordinates { get; }

        /// <summary>
        /// Compares two <see cref="RgbWorkingSpace"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="RgbWorkingSpace"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="RgbWorkingSpace"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(RgbWorkingSpace left, RgbWorkingSpace right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Compares two <see cref="RgbWorkingSpace"/> objects for inequality
        /// </summary>
        /// <param name="left">
        /// The <see cref="RgbWorkingSpace"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="RgbWorkingSpace"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(RgbWorkingSpace left, RgbWorkingSpace right)
        {
            return !Equals(left, right);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is RgbWorkingSpace)
            {
                return this.Equals((RgbWorkingSpace)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(IRgbWorkingSpace other)
        {
            // TODO: Object.Equals for ICompanding will be slow.
            return this.WhitePoint.Equals(other.WhitePoint)
                && this.ChromaticityCoordinates.Equals(other.ChromaticityCoordinates)
                && Equals(this.Companding, other.Companding);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.WhitePoint.GetHashCode();
                hashCode = (hashCode * 397) ^ this.ChromaticityCoordinates.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.Companding?.GetHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}