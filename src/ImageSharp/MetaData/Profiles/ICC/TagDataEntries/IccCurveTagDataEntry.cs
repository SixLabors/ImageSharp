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
            : this(new float[] { gamma }, IccProfileTag.Unknown)
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
            : this(new float[] { gamma }, tagSignature)
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
        public float Gamma
        {
            get
            {
                if (this.IsGamma)
                {
                    return this.CurveData[0];
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the curve maps input directly to output
        /// </summary>
        public bool IsIdentityResponse
        {
            get { return this.CurveData.Length == 0; }
        }

        /// <summary>
        /// Gets a value indicating whether the curve is a gamma curve
        /// </summary>
        public bool IsGamma
        {
            get { return this.CurveData.Length == 1; }
        }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccCurveTagDataEntry entry)
            {
                return this.CurveData.SequenceEqual(entry.CurveData);
            }

            return false;
        }

        /// <inheritdoc />
        public bool Equals(IccCurveTagDataEntry other)
        {
            return this.Equals((IccTagDataEntry)other);
        }
    }
}
