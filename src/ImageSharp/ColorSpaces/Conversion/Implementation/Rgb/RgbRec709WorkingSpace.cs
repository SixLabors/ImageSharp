// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce
{
    /// <summary>
    /// Represents an <see cref="IRgbWorkingSpace"/> that implements Rec. 709 companding.
    /// </summary>
    /// <remarks>
    /// <see href="http://en.wikipedia.org/wiki/Rec._709"/>
    /// </remarks>
    internal class RgbRec709WorkingSpace : IRgbWorkingSpace, IEquatable<RgbRec709WorkingSpace>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RgbRec709WorkingSpace"/> class.
        /// </summary>
        /// <param name="referenceWhite">The reference white point.</param>
        /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
        public RgbRec709WorkingSpace(CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
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
            return channel < 0.081F ? channel / 4.5F : MathF.Pow((channel + 0.099F) / 1.099F, 2.222222F);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Compress(float channel)
        {
            return channel < 0.018F ? 4500F * channel : (1.099F * channel) - 0.099F;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is RgbRec709WorkingSpace space && this.Equals(space);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(IRgbWorkingSpace other)
        {
            return other is RgbRec709WorkingSpace space && this.Equals(space);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(RgbRec709WorkingSpace other)
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