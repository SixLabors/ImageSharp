// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
    /// <summary>
    /// A response curve
    /// </summary>
    internal sealed class IccResponseCurve : IEquatable<IccResponseCurve>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccResponseCurve"/> class.
        /// </summary>
        /// <param name="curveType">The type of this curve</param>
        /// <param name="xyzValues">The XYZ values</param>
        /// <param name="responseArrays">The response arrays</param>
        public IccResponseCurve(IccCurveMeasurementEncodings curveType, Vector3[] xyzValues, IccResponseNumber[][] responseArrays)
        {
            Guard.NotNull(xyzValues, nameof(xyzValues));
            Guard.NotNull(responseArrays, nameof(responseArrays));

            Guard.IsTrue(xyzValues.Length == responseArrays.Length, $"{nameof(xyzValues)},{nameof(responseArrays)}", "Arrays must have same length");
            Guard.MustBeBetweenOrEqualTo(xyzValues.Length, 1, 15, nameof(xyzValues));

            this.CurveType = curveType;
            this.XyzValues = xyzValues;
            this.ResponseArrays = responseArrays;
        }

        /// <summary>
        /// Gets the type of this curve
        /// </summary>
        public IccCurveMeasurementEncodings CurveType { get; }

        /// <summary>
        /// Gets the XYZ values
        /// </summary>
        public Vector3[] XyzValues { get; }

        /// <summary>
        /// Gets the response arrays
        /// </summary>
        public IccResponseNumber[][] ResponseArrays { get; }

        /// <inheritdoc/>
        public bool Equals(IccResponseCurve other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.CurveType == other.CurveType
                && this.XyzValues.AsSpan().SequenceEqual(other.XyzValues)
                && this.EqualsResponseArray(other);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is IccResponseCurve other && this.Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)this.CurveType;
                hashCode = (hashCode * 397) ^ (this.XyzValues?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (this.ResponseArrays?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        private bool EqualsResponseArray(IccResponseCurve other)
        {
            if (this.ResponseArrays.Length != other.ResponseArrays.Length)
            {
                return false;
            }

            for (int i = 0; i < this.ResponseArrays.Length; i++)
            {
                if (!this.ResponseArrays[i].SequenceEqual(other.ResponseArrays[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
