// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
    /// <summary>
    /// A segment of a curve
    /// </summary>
    internal abstract class IccCurveSegment : IEquatable<IccCurveSegment>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccCurveSegment"/> class.
        /// </summary>
        /// <param name="signature">The signature of this segment</param>
        protected IccCurveSegment(IccCurveSegmentSignature signature)
        {
            this.Signature = signature;
        }

        /// <summary>
        /// Gets the signature of this segment
        /// </summary>
        public IccCurveSegmentSignature Signature { get; }

        /// <inheritdoc/>
        public virtual bool Equals(IccCurveSegment other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Signature == other.Signature;
        }
    }
}