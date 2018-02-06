// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce
{
    /// <summary>
    /// Represents an <see cref="IRgbWorkingSpace"/> that implements sRGB companding
    /// </summary>
    /// <remarks>
    /// For more info see:
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
    /// </remarks>
    internal class RgbSRgbWorkingSpace : IRgbWorkingSpace, IEquatable<RgbSRgbWorkingSpace>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RgbSRgbWorkingSpace"/> class.
        /// </summary>
        /// <param name="referenceWhite">The reference white point.</param>
        /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
        public RgbSRgbWorkingSpace(CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
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
            return channel <= 0.04045F ? channel / 12.92F : MathF.Pow((channel + 0.055F) / 1.055F, 2.4F);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Compress(float channel)
        {
            return channel <= 0.0031308F ? 12.92F * channel : (1.055F * MathF.Pow(channel, 0.416666666666667F)) - 0.055F;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is RgbSRgbWorkingSpace space && this.Equals(space);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(IRgbWorkingSpace other)
        {
            return other is RgbSRgbWorkingSpace space && this.Equals(space);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(RgbSRgbWorkingSpace other)
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