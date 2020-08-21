// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Allows the approximate comparison of colorspace component values.
    /// </summary>
    internal readonly struct ApproximateColorSpaceComparer :
        IEqualityComparer<Rgb>,
        IEqualityComparer<LinearRgb>,
        IEqualityComparer<CieLab>,
        IEqualityComparer<CieLch>,
        IEqualityComparer<CieLchuv>,
        IEqualityComparer<CieLuv>,
        IEqualityComparer<CieXyz>,
        IEqualityComparer<CieXyy>,
        IEqualityComparer<Cmyk>,
        IEqualityComparer<HunterLab>,
        IEqualityComparer<Hsl>,
        IEqualityComparer<Hsv>,
        IEqualityComparer<Lms>,
        IEqualityComparer<YCbCr>,
        IEqualityComparer<CieXyChromaticityCoordinates>,
        IEqualityComparer<RgbPrimariesChromaticityCoordinates>,
        IEqualityComparer<GammaWorkingSpace>,
        IEqualityComparer<RgbWorkingSpace>
    {
        private readonly float epsilon;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApproximateColorSpaceComparer"/> class.
        /// </summary>
        /// <param name="epsilon">The comparison error difference epsilon to use.</param>
        public ApproximateColorSpaceComparer(float epsilon = 1F) => this.epsilon = epsilon;

        /// <inheritdoc/>
        public bool Equals(Rgb x, Rgb y)
        {
            return this.Equals(x.R, y.R)
             && this.Equals(x.G, y.G)
             && this.Equals(x.B, y.B);
        }

        /// <inheritdoc/>
        public int GetHashCode(Rgb obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(LinearRgb x, LinearRgb y)
        {
            return this.Equals(x.R, y.R)
             && this.Equals(x.G, y.G)
             && this.Equals(x.B, y.B);
        }

        /// <inheritdoc/>
        public int GetHashCode(LinearRgb obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(CieLab x, CieLab y)
        {
            return this.Equals(x.L, y.L)
             && this.Equals(x.A, y.A)
             && this.Equals(x.B, y.B);
        }

        /// <inheritdoc/>
        public int GetHashCode(CieLab obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(CieLch x, CieLch y)
        {
            return this.Equals(x.L, y.L)
             && this.Equals(x.C, y.C)
             && this.Equals(x.H, y.H);
        }

        /// <inheritdoc/>
        public int GetHashCode(CieLch obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(CieLchuv x, CieLchuv y)
        {
            return this.Equals(x.L, y.L)
             && this.Equals(x.C, y.C)
             && this.Equals(x.H, y.H);
        }

        /// <inheritdoc/>
        public int GetHashCode(CieLchuv obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(CieLuv x, CieLuv y)
        {
            return this.Equals(x.L, y.L)
             && this.Equals(x.U, y.U)
             && this.Equals(x.V, y.V);
        }

        /// <inheritdoc/>
        public int GetHashCode(CieLuv obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(CieXyz x, CieXyz y)
        {
            return this.Equals(x.X, y.X)
             && this.Equals(x.Y, y.Y)
             && this.Equals(x.Z, y.Z);
        }

        /// <inheritdoc/>
        public int GetHashCode(CieXyz obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(CieXyy x, CieXyy y)
        {
            return this.Equals(x.X, y.X)
             && this.Equals(x.Y, y.Y)
             && this.Equals(x.Yl, y.Yl);
        }

        /// <inheritdoc/>
        public int GetHashCode(CieXyy obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(Cmyk x, Cmyk y)
        {
            return this.Equals(x.C, y.C)
             && this.Equals(x.M, y.M)
             && this.Equals(x.Y, y.Y)
             && this.Equals(x.K, y.K);
        }

        /// <inheritdoc/>
        public int GetHashCode(Cmyk obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(HunterLab x, HunterLab y)
        {
            return this.Equals(x.L, y.L)
             && this.Equals(x.A, y.A)
             && this.Equals(x.B, y.B);
        }

        /// <inheritdoc/>
        public int GetHashCode(HunterLab obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(Hsl x, Hsl y)
        {
            return this.Equals(x.H, y.H)
             && this.Equals(x.S, y.S)
             && this.Equals(x.L, y.L);
        }

        /// <inheritdoc/>
        public int GetHashCode(Hsl obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(Hsv x, Hsv y)
        {
            return this.Equals(x.H, y.H)
             && this.Equals(x.S, y.S)
             && this.Equals(x.V, y.V);
        }

        /// <inheritdoc/>
        public int GetHashCode(Hsv obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(Lms x, Lms y)
        {
            return this.Equals(x.L, y.L)
             && this.Equals(x.M, y.M)
             && this.Equals(x.S, y.S);
        }

        /// <inheritdoc/>
        public int GetHashCode(Lms obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(YCbCr x, YCbCr y)
        {
            return this.Equals(x.Y, y.Y)
             && this.Equals(x.Cb, y.Cb)
             && this.Equals(x.Cr, y.Cr);
        }

        /// <inheritdoc/>
        public int GetHashCode(YCbCr obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(CieXyChromaticityCoordinates x, CieXyChromaticityCoordinates y) => this.Equals(x.X, y.X) && this.Equals(x.Y, y.Y);

        /// <inheritdoc/>
        public int GetHashCode(CieXyChromaticityCoordinates obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(RgbPrimariesChromaticityCoordinates x, RgbPrimariesChromaticityCoordinates y) => this.Equals(x.R, y.R) && this.Equals(x.G, y.G) && this.Equals(x.B, y.B);

        /// <inheritdoc/>
        public int GetHashCode(RgbPrimariesChromaticityCoordinates obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(GammaWorkingSpace x, GammaWorkingSpace y)
        {
            if (x is GammaWorkingSpace g1 && y is GammaWorkingSpace g2)
            {
                return this.Equals(g1.Gamma, g2.Gamma)
                    && this.Equals(g1.WhitePoint, g2.WhitePoint)
                    && this.Equals(g1.ChromaticityCoordinates, g2.ChromaticityCoordinates);
            }

            return false;
        }

        /// <inheritdoc/>
        public int GetHashCode(GammaWorkingSpace obj) => obj.GetHashCode();

        /// <inheritdoc/>
        public bool Equals(RgbWorkingSpace x, RgbWorkingSpace y)
        {
            return this.Equals(x.WhitePoint, y.WhitePoint)
                && this.Equals(x.ChromaticityCoordinates, y.ChromaticityCoordinates);
        }

        /// <inheritdoc/>
        public int GetHashCode(RgbWorkingSpace obj) => obj.GetHashCode();

        private bool Equals(float x, float y)
        {
            float d = x - y;
            return d >= -this.epsilon && d <= this.epsilon;
        }
    }
}
