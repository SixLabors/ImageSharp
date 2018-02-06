// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce
{
    /// <summary>
    /// Represents an <see cref="IRgbWorkingSpace"/> that implements Rec. 2020 companding (for 12-bits).
    /// </summary>
    /// <remarks>
    /// <see href="http://en.wikipedia.org/wiki/Rec._2020"/>
    /// For 10-bits, companding is identical to <see cref="RgbRec709WorkingSpace"/>
    /// </remarks>
    internal class RgbRec2020WorkingSpace : IRgbWorkingSpace, IEquatable<RgbRec2020WorkingSpace>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RgbRec2020WorkingSpace"/> class.
        /// </summary>
        /// <param name="referenceWhite">The reference white point.</param>
        /// <param name="chromaticityCoordinates">The chromaticity of the rgb primaries.</param>
        public RgbRec2020WorkingSpace(CieXyz referenceWhite, RgbPrimariesChromaticityCoordinates chromaticityCoordinates)
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
            return channel < 0.08145F ? channel / 4.5F : MathF.Pow((channel + 0.0993F) / 1.0993F, 2.222222F);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Compress(float channel)
        {
            return channel < 0.0181F ? 4500F * channel : (1.0993F * channel) - 0.0993F;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is RgbRec2020WorkingSpace space && this.Equals(space);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(IRgbWorkingSpace other)
        {
            return other is RgbRec2020WorkingSpace space && this.Equals(space);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(RgbRec2020WorkingSpace other)
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