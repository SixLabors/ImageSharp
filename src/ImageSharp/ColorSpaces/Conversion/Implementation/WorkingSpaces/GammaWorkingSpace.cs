// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.ColorSpaces.Companding;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// The gamma working space.
    /// </summary>
    public sealed class GammaWorkingSpace : RgbWorkingSpace
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GammaWorkingSpace" /> class.
        /// </summary>
        /// <param name="gamma">The gamma value.</param>
        /// <param name="referenceWhite">The reference white point.</param>
        /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
        public GammaWorkingSpace(float gamma, CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
            : base(referenceWhite, chromaticityCoordinates) => this.Gamma = gamma;

        /// <summary>
        /// Gets the gamma value.
        /// </summary>
        public float Gamma { get; }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public override float Compress(float channel) => GammaCompanding.Compress(channel, this.Gamma);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public override float Expand(float channel) => GammaCompanding.Expand(channel, this.Gamma);

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

            if (obj is GammaWorkingSpace other)
            {
                return this.Gamma.Equals(other.Gamma)
                    && this.WhitePoint.Equals(other.WhitePoint)
                    && this.ChromaticityCoordinates.Equals(other.ChromaticityCoordinates);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(
            this.WhitePoint,
            this.ChromaticityCoordinates,
            this.Gamma);
    }
}
