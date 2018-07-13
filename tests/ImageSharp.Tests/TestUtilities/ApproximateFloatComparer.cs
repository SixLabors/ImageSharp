// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce;

namespace SixLabors.ImageSharp.Tests
{
    internal struct ApproximateFloatComparer :
        IEqualityComparer<float>,
        IEqualityComparer<Vector4>,
        IEqualityComparer<CieXyChromaticityCoordinates>,
        IEqualityComparer<RgbPrimariesChromaticityCoordinates>,
        IEqualityComparer<CieXyz>,
        IEqualityComparer<RgbWorkingSpace>
    {
        private readonly float Eps;

        public ApproximateFloatComparer(float eps = 1f)
        {
            this.Eps = eps;
        }

        public bool Equals(float x, float y)
        {
            float d = x - y;

            return d >= -this.Eps && d <= this.Eps;
        }

        public int GetHashCode(float obj)
        {
            throw new InvalidOperationException();
        }

        public bool Equals(Vector4 a, Vector4 b)
        {
            return this.Equals(a.X, b.X) && this.Equals(a.Y, b.Y) && this.Equals(a.Z, b.Z) && this.Equals(a.W, b.W);
        }

        public int GetHashCode(Vector4 obj)
        {
            throw new InvalidOperationException();
        }

        public bool Equals(CieXyChromaticityCoordinates x, CieXyChromaticityCoordinates y)
        {
            return this.Equals(x.X, y.X) && this.Equals(x.Y, y.Y);
        }

        public int GetHashCode(CieXyChromaticityCoordinates obj)
        {
            throw new NotImplementedException();
        }

        public bool Equals(RgbPrimariesChromaticityCoordinates x, RgbPrimariesChromaticityCoordinates y)
        {
            return this.Equals(x.R, y.R) && this.Equals(x.G, y.G) && this.Equals(x.B, y.B);
        }

        public int GetHashCode(RgbPrimariesChromaticityCoordinates obj)
        {
            throw new NotImplementedException();
        }

        public bool Equals(CieXyz x, CieXyz y)
        {
            return this.Equals(x.X, y.X) && this.Equals(x.Y, y.Y) && this.Equals(x.Z, y.Z);
        }

        public int GetHashCode(CieXyz obj)
        {
            throw new NotImplementedException();
        }

        public bool Equals(RgbWorkingSpace x, RgbWorkingSpace y)
        {
            if (x is RgbWorkingSpace g1 && y is RgbWorkingSpace g2)
            {
                return this.Equals(g1.WhitePoint, g2.WhitePoint)
                    && this.Equals(g1.ChromaticityCoordinates, g2.ChromaticityCoordinates);
            }

            return this.Equals(x.WhitePoint, y.WhitePoint)
                && this.Equals(x.ChromaticityCoordinates, y.ChromaticityCoordinates);
        }

        public int GetHashCode(RgbWorkingSpace obj)
        {
            throw new NotImplementedException();
        }
    }
}