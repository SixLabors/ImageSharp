// <copyright file="IccMeasurementTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Numerics;

    /// <summary>
    /// The measurementType information refers only to the internal
    /// profile data and is meant to provide profile makers an alternative
    /// to the default measurement specifications.
    /// </summary>
    internal sealed class IccMeasurementTagDataEntry : IccTagDataEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccMeasurementTagDataEntry"/> class.
        /// </summary>
        /// <param name="observer">Observer</param>
        /// <param name="xyzBacking">XYZ Backing values</param>
        /// <param name="geometry">Geometry</param>
        /// <param name="flare">Flare</param>
        /// <param name="illuminant">Illuminant</param>
        public IccMeasurementTagDataEntry(IccStandardObserver observer, Vector3 xyzBacking, IccMeasurementGeometry geometry, float flare, IccStandardIlluminant illuminant)
            : this(observer, xyzBacking, geometry, flare, illuminant, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccMeasurementTagDataEntry"/> class.
        /// </summary>
        /// <param name="observer">Observer</param>
        /// <param name="xyzBacking">XYZ Backing values</param>
        /// <param name="geometry">Geometry</param>
        /// <param name="flare">Flare</param>
        /// <param name="illuminant">Illuminant</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccMeasurementTagDataEntry(IccStandardObserver observer, Vector3 xyzBacking, IccMeasurementGeometry geometry, float flare, IccStandardIlluminant illuminant, IccProfileTag tagSignature)
            : base(IccTypeSignature.Measurement, tagSignature)
        {
            this.Observer = observer;
            this.XyzBacking = xyzBacking;
            this.Geometry = geometry;
            this.Flare = flare;
            this.Illuminant = illuminant;
        }

        /// <summary>
        /// Gets the observer
        /// </summary>
        public IccStandardObserver Observer { get; }

        /// <summary>
        /// Gets the XYZ Backing values
        /// </summary>
        public Vector3 XyzBacking { get; }

        /// <summary>
        /// Gets the geometry
        /// </summary>
        public IccMeasurementGeometry Geometry { get; }

        /// <summary>
        /// Gets the flare
        /// </summary>
        public float Flare { get; }

        /// <summary>
        /// Gets the illuminant
        /// </summary>
        public IccStandardIlluminant Illuminant { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccMeasurementTagDataEntry entry)
            {
                return this.Observer == entry.Observer
                    && this.XyzBacking == entry.XyzBacking
                    && this.Geometry == entry.Geometry
                    && this.Flare == entry.Flare
                    && this.Illuminant == entry.Illuminant;
            }

            return false;
        }
    }
}
