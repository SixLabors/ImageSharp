// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce
{
    /// <summary>
    /// Represents an <see cref="IRgbWorkingSpace"/> that implements gamma companding
    /// </summary>
    /// <remarks>
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
    /// </remarks>
    internal class RgbGammaWorkingSpace : IRgbWorkingSpace, IEquatable<RgbGammaWorkingSpace>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RgbGammaWorkingSpace"/> class.
        /// </summary>
        /// <param name="referenceWhite">The reference white point.</param>
        /// <param name="gamma">The gamma value.</param>
        /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
        public RgbGammaWorkingSpace(CieXyz referenceWhite, float gamma, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
        {
            this.WhitePoint = referenceWhite;
            this.Gamma = gamma;
            this.ChromaticityCoordinates = chromaticityCoordinates;
        }

        /// <inheritdoc/>
        public CieXyz WhitePoint { get; }

        /// <inheritdoc/>
        public RgbPrimariesChromaticityCoordinates ChromaticityCoordinates { get; }

        /// <summary>
        /// Gets the gamma value
        /// </summary>
        public float Gamma { get; }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Expand(float channel)
        {
            return MathF.Pow(channel, this.Gamma);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Compress(float channel)
        {
            return MathF.Pow(channel, 1 / this.Gamma);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is RgbGammaWorkingSpace space && this.Equals(space);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(IRgbWorkingSpace other)
        {
            return other is RgbGammaWorkingSpace space && this.Equals(space);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(RgbGammaWorkingSpace other)
        {
            return other != null &&
                   this.WhitePoint.Equals(other.WhitePoint) &&
                   this.ChromaticityCoordinates.Equals(other.ChromaticityCoordinates) &&
                   this.Gamma == other.Gamma;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashHelpers.Combine(
                this.WhitePoint.GetHashCode(),
                HashHelpers.Combine(
                    this.ChromaticityCoordinates.GetHashCode(),
                    this.Gamma.GetHashCode()));
        }
    }
}