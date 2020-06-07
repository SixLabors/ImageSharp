// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Base class for all implementations of <see cref="RgbWorkingSpace"/>.
    /// </summary>
    public abstract class RgbWorkingSpace
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RgbWorkingSpace"/> class.
        /// </summary>
        /// <param name="referenceWhite">The reference white point.</param>
        /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
        protected RgbWorkingSpace(CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
        {
            this.WhitePoint = referenceWhite;
            this.ChromaticityCoordinates = chromaticityCoordinates;
        }

        /// <summary>
        /// Gets the reference white point
        /// </summary>
        public CieXyz WhitePoint { get; }

        /// <summary>
        /// Gets the chromaticity of the rgb primaries.
        /// </summary>
        public RgbPrimariesChromaticityCoordinates ChromaticityCoordinates { get; }

        /// <summary>
        /// Expands a companded channel to its linear equivalent with respect to the energy.
        /// </summary>
        /// <remarks>
        /// For more info see:
        /// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
        /// </remarks>
        /// <param name="channel">The channel value.</param>
        /// <returns>The <see cref="float"/> representing the linear channel value.</returns>
        public abstract float Expand(float channel);

        /// <summary>
        /// Compresses an uncompanded channel (linear) to its nonlinear equivalent (depends on the RGB color system).
        /// </summary>
        /// <remarks>
        /// For more info see:
        /// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
        /// </remarks>
        /// <param name="channel">The channel value.</param>
        /// <returns>The <see cref="float"/> representing the nonlinear channel value.</returns>
        public abstract float Compress(float channel);

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is RgbWorkingSpace other)
            {
                return this.WhitePoint.Equals(other.WhitePoint)
                    && this.ChromaticityCoordinates.Equals(other.ChromaticityCoordinates);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(this.WhitePoint, this.ChromaticityCoordinates);
        }
    }
}
