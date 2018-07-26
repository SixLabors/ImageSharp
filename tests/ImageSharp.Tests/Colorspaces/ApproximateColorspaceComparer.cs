// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.ColorSpaces;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Allows the approximate comparison of colorspace component values.
    /// </summary>
    internal class ApproximateColorSpaceComparer :
        IEqualityComparer<Rgb>,
        IEqualityComparer<CieLab>,
        IEqualityComparer<CieLch>,
        IEqualityComparer<CieLchuv>,
        IEqualityComparer<CieLuv>,
        IEqualityComparer<CieXyz>,
        IEqualityComparer<CieXyy>,
        IEqualityComparer<HunterLab>,
        IEqualityComparer<Lms>
    {
        private readonly float Epsilon;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApproximateColorSpaceComparer"/> class.
        /// </summary>
        /// <param name="epsilon">The comparison error difference epsilon to use.</param>
        public ApproximateColorSpaceComparer(float epsilon = 1F)
        {
            this.Epsilon = epsilon;
        }

        /// <inheritdoc/>
        public bool Equals(Rgb x, Rgb y)
        {
            return this.Equals(x.R, y.R)
             && this.Equals(x.G, y.G)
             && this.Equals(x.B, y.B);
        }

        /// <inheritdoc/>
        public int GetHashCode(Rgb obj)
        {
            return obj.GetHashCode();
        }

        /// <inheritdoc/>
        public bool Equals(CieLab x, CieLab y)
        {
            return this.Equals(x.L, y.L)
             && this.Equals(x.A, y.A)
             && this.Equals(x.B, y.B);
        }

        /// <inheritdoc/>
        public int GetHashCode(CieLab obj)
        {
            return obj.GetHashCode();
        }

        /// <inheritdoc/>
        public bool Equals(CieLch x, CieLch y)
        {
            return this.Equals(x.L, y.L)
             && this.Equals(x.C, y.C)
             && this.Equals(x.H, y.H);
        }

        /// <inheritdoc/>
        public int GetHashCode(CieLch obj)
        {
            return obj.GetHashCode();
        }

        /// <inheritdoc/>
        public bool Equals(CieLchuv x, CieLchuv y)
        {
            return this.Equals(x.L, y.L)
             && this.Equals(x.C, y.C)
             && this.Equals(x.H, y.H);
        }

        /// <inheritdoc/>
        public int GetHashCode(CieLchuv obj)
        {
            return obj.GetHashCode();
        }

        /// <inheritdoc/>
        public bool Equals(CieLuv x, CieLuv y)
        {
            return this.Equals(x.L, y.L)
             && this.Equals(x.U, y.U)
             && this.Equals(x.V, y.V);
        }

        /// <inheritdoc/>
        public int GetHashCode(CieLuv obj)
        {
            return obj.GetHashCode();
        }

        /// <inheritdoc/>
        public bool Equals(CieXyz x, CieXyz y)
        {
            return this.Equals(x.X, y.X)
             && this.Equals(x.Y, y.Y)
             && this.Equals(x.Z, y.Z);
        }

        /// <inheritdoc/>
        public int GetHashCode(CieXyz obj)
        {
            return obj.GetHashCode();
        }

        /// <inheritdoc/>
        public bool Equals(CieXyy x, CieXyy y)
        {
            return this.Equals(x.X, y.X)
             && this.Equals(x.Y, y.Y)
             && this.Equals(x.Yl, y.Yl);
        }

        /// <inheritdoc/>
        public int GetHashCode(CieXyy obj)
        {
            return obj.GetHashCode();
        }

        /// <inheritdoc/>
        public bool Equals(HunterLab x, HunterLab y)
        {
            return this.Equals(x.L, y.L)
             && this.Equals(x.A, y.A)
             && this.Equals(x.B, y.B);
        }

        /// <inheritdoc/>
        public int GetHashCode(HunterLab obj)
        {
            return obj.GetHashCode();
        }

        /// <inheritdoc/>
        public bool Equals(Lms x, Lms y)
        {
            return this.Equals(x.L, y.L)
             && this.Equals(x.M, y.M)
             && this.Equals(x.S, y.S);
        }

        /// <inheritdoc/>
        public int GetHashCode(Lms obj)
        {
            return obj.GetHashCode();
        }

        private bool Equals(float x, float y)
        {
            float d = x - y;
            return d >= -this.Epsilon && d <= this.Epsilon;
        }
    }
}