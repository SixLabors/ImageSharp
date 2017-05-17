// <copyright file="IccCurveTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;

    /// <summary>
    /// The type contains a one-dimensional table of double values.
    /// </summary>
    internal sealed class IccCurveTagDataEntry : IccTagDataEntry, IEquatable<IccCurveTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccCurveTagDataEntry"/> class.
        /// </summary>
        public IccCurveTagDataEntry()
            : this(new float[0], IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccCurveTagDataEntry"/> class.
        /// </summary>
        /// <param name="gamma">Gamma value</param>
        public IccCurveTagDataEntry(float gamma)
            : this(new[] { gamma }, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccCurveTagDataEntry"/> class.
        /// </summary>
        /// <param name="curveData">Curve Data</param>
        public IccCurveTagDataEntry(float[] curveData)
            : this(curveData, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccCurveTagDataEntry"/> class.
        /// </summary>
        /// <param name="tagSignature">Tag Signature</param>
        public IccCurveTagDataEntry(IccProfileTag tagSignature)
            : this(new float[0], tagSignature)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccCurveTagDataEntry"/> class.
        /// </summary>
        /// <param name="gamma">Gamma value</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccCurveTagDataEntry(float gamma, IccProfileTag tagSignature)
            : this(new[] { gamma }, tagSignature)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccCurveTagDataEntry"/> class.
        /// </summary>
        /// <param name="curveData">Curve Data</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccCurveTagDataEntry(float[] curveData, IccProfileTag tagSignature)
            : base(IccTypeSignature.Curve, tagSignature)
        {
            this.CurveData = curveData ?? new float[0];
        }

        /// <summary>
        /// Gets the curve data
        /// </summary>
        public float[] CurveData { get; }

        /// <summary>
        /// Gets the gamma value.
        /// Only valid if <see cref="IsGamma"/> is true
        /// </summary>
        public float Gamma => this.IsGamma ? this.CurveData[0] : 0;

        /// <summary>
        /// Gets a value indicating whether the curve maps input directly to output
        /// </summary>
        public bool IsIdentityResponse => this.CurveData.Length == 0;

        /// <summary>
        /// Gets a value indicating whether the curve is a gamma curve
        /// </summary>
        public bool IsGamma => this.CurveData.Length == 1;

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            var entry = other as IccCurveTagDataEntry;
            return entry != null && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccCurveTagDataEntry other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && this.CurveData.SequenceEqual(other.CurveData);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is IccCurveTagDataEntry && this.Equals((IccCurveTagDataEntry)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (this.CurveData != null ? this.CurveData.GetHashCode() : 0);
            }
        }
    }
}