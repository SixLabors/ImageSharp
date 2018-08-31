// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
    /// <summary>
    /// The parametricCurveType describes a one-dimensional curve by
    /// specifying one of a predefined set of functions using the parameters.
    /// </summary>
    internal sealed class IccParametricCurveTagDataEntry : IccTagDataEntry, IEquatable<IccParametricCurveTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccParametricCurveTagDataEntry"/> class.
        /// </summary>
        /// <param name="curve">The Curve</param>
        public IccParametricCurveTagDataEntry(IccParametricCurve curve)
            : this(curve, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccParametricCurveTagDataEntry"/> class.
        /// </summary>
        /// <param name="curve">The Curve</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccParametricCurveTagDataEntry(IccParametricCurve curve, IccProfileTag tagSignature)
            : base(IccTypeSignature.ParametricCurve, tagSignature)
        {
            this.Curve = curve;
        }

        /// <summary>
        /// Gets the Curve
        /// </summary>
        public IccParametricCurve Curve { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            return other is IccParametricCurveTagDataEntry entry && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccParametricCurveTagDataEntry other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && this.Curve.Equals(other.Curve);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is IccParametricCurveTagDataEntry other && this.Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (this.Curve?.GetHashCode() ?? 0);
            }
        }
    }
}
