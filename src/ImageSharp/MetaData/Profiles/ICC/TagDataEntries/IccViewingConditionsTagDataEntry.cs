// <copyright file="IccViewingConditionsTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Numerics;

    /// <summary>
    /// This type represents a set of viewing condition parameters.
    /// </summary>
    internal sealed class IccViewingConditionsTagDataEntry : IccTagDataEntry
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
        /// Gets the XYZ values of Illuminant
        /// </summary>
        public Vector3 IlluminantXyz { get; }

        /// <summary>
        /// Gets the XYZ values of Surrounding
        /// </summary>
        public Vector3 SurroundXyz { get; }

        /// <summary>
        /// Gets the illuminant
        /// </summary>
        public IccStandardIlluminant Illuminant { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccViewingConditionsTagDataEntry entry)
            {
                return this.IlluminantXyz == entry.IlluminantXyz
                    && this.SurroundXyz == entry.SurroundXyz
                    && this.Illuminant == entry.Illuminant;
            }

            return false;
        }
    }
}
