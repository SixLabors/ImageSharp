// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce
{
    /// <summary>
    /// Represents an <see cref="IRgbWorkingSpace"/> that implements L* companding
    /// </summary>
    /// <remarks>
    /// For more info see:
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
    /// </remarks>
    internal class RgbLWorkingSpace : IRgbWorkingSpace, IEquatable<RgbLWorkingSpace>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RgbLWorkingSpace"/> class.
        /// </summary>
        /// <param name="referenceWhite">The reference white point.</param>
        /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
        public RgbLWorkingSpace(CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
        {
            this.WhitePoint = referenceWhite;
            this.ChromaticityCoordinates = chromaticityCoordinates;
        }

        /// <inheritdoc/>
        public CieXyz WhitePoint { get; }

        /// <inheritdoc/>
        public RgbPrimariesChromaticityCoordinates ChromaticityCoordinates { get; }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Expand(float channel)
        {
            return channel <= 0.08 ? 100 * channel / CieConstants.Kappa : MathF.Pow((channel + 0.16F) / 1.16F, 3);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Compress(float channel)
        {
            return channel <= CieConstants.Epsilon
                ? channel * CieConstants.Kappa / 100F
                : MathF.Pow(1.16F * channel, 0.3333333F) - 0.16F;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is RgbLWorkingSpace space && this.Equals(space);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(IRgbWorkingSpace other)
        {
            return other is RgbLWorkingSpace space && this.Equals(space);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(RgbLWorkingSpace other)
        {
            return other != null &&
                   this.WhitePoint.Equals(other.WhitePoint) &&
                   this.ChromaticityCoordinates.Equals(other.ChromaticityCoordinates);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashHelpers.Combine(
                this.WhitePoint.GetHashCode(),
                this.ChromaticityCoordinates.GetHashCode());
        }
    }
}