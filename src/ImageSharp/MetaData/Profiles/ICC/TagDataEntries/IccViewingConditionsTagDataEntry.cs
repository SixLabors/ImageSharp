// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
    /// <summary>
    /// This type represents a set of viewing condition parameters.
    /// </summary>
    internal sealed class IccViewingConditionsTagDataEntry : IccTagDataEntry, IEquatable<IccViewingConditionsTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccViewingConditionsTagDataEntry"/> class.
        /// </summary>
        /// <param name="illuminantXyz">XYZ values of Illuminant</param>
        /// <param name="surroundXyz">XYZ values of Surrounding</param>
        /// <param name="illuminant">Illuminant</param>
        public IccViewingConditionsTagDataEntry(Vector3 illuminantXyz, Vector3 surroundXyz, IccStandardIlluminant illuminant)
            : this(illuminantXyz, surroundXyz, illuminant, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccViewingConditionsTagDataEntry"/> class.
        /// </summary>
        /// <param name="illuminantXyz">XYZ values of Illuminant</param>
        /// <param name="surroundXyz">XYZ values of Surrounding</param>
        /// <param name="illuminant">Illuminant</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccViewingConditionsTagDataEntry(Vector3 illuminantXyz, Vector3 surroundXyz, IccStandardIlluminant illuminant, IccProfileTag tagSignature)
            : base(IccTypeSignature.ViewingConditions, tagSignature)
        {
            this.IlluminantXyz = illuminantXyz;
            this.SurroundXyz = surroundXyz;
            this.Illuminant = illuminant;
        }

        /// <summary>
        /// Gets the XYZ values of illuminant.
        /// </summary>
        public Vector3 IlluminantXyz { get; }

        /// <summary>
        /// Gets the XYZ values of Surrounding
        /// </summary>
        public Vector3 SurroundXyz { get; }

        /// <summary>
        /// Gets the illuminant.
        /// </summary>
        public IccStandardIlluminant Illuminant { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            return other is IccViewingConditionsTagDataEntry entry && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccViewingConditionsTagDataEntry other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other)
                && this.IlluminantXyz.Equals(other.IlluminantXyz)
                && this.SurroundXyz.Equals(other.SurroundXyz)
                && this.Illuminant == other.Illuminant;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is IccViewingConditionsTagDataEntry other && this.Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ this.IlluminantXyz.GetHashCode();
                hashCode = (hashCode * 397) ^ this.SurroundXyz.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)this.Illuminant;
                return hashCode;
            }
        }
    }
}
