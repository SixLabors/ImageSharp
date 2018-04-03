// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Allows the comparison of single-precision floating point values by precision.
    /// </summary>
    public struct FloatRoundingComparer : IEqualityComparer<float>, IEqualityComparer<Vector4>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FloatRoundingComparer"/> struct.
        /// </summary>
        /// <param name="precision">The number of decimal places (valid values: 0-7)</param>
        public FloatRoundingComparer(int precision)
        {
            Guard.MustBeBetweenOrEqualTo(precision, 0, 7, nameof(precision));
            this.Precision = precision;
        }

        /// <summary>
        /// Gets the number of decimal places (valid values: 0-7)
        /// </summary>
        public int Precision { get; }

        /// <inheritdoc />
        public bool Equals(float x, float y)
        {
            float xp = (float)Math.Round(x, this.Precision, MidpointRounding.AwayFromZero);
            float yp = (float)Math.Round(y, this.Precision, MidpointRounding.AwayFromZero);

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            return xp == yp;
        }

        /// <inheritdoc />
        public bool Equals(Vector4 x, Vector4 y)
        {
            return this.Equals(x.X, y.X) && this.Equals(x.Y, y.Y) && this.Equals(x.Z, y.Z) && this.Equals(x.W, y.W);
        }

        /// <inheritdoc />
        public int GetHashCode(float obj)
        {
            unchecked
            {
                int hashCode = obj.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Precision.GetHashCode();
                return hashCode;
            }
        }

        /// <inheritdoc />
        public int GetHashCode(Vector4 obj)
        {
            unchecked
            {
                int hashCode = obj.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Precision.GetHashCode();
                return hashCode;
            }
        }
    }
}