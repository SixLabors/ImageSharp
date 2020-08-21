// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// A parametric curve
    /// </summary>
    internal sealed class IccParametricCurve : IEquatable<IccParametricCurve>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccParametricCurve"/> class.
        /// </summary>
        /// <param name="g">G curve parameter</param>
        public IccParametricCurve(float g)
            : this(IccParametricCurveType.Type1, g, 0, 0, 0, 0, 0, 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccParametricCurve"/> class.
        /// </summary>
        /// <param name="g">G curve parameter</param>
        /// <param name="a">A curve parameter</param>
        /// <param name="b">B curve parameter</param>
        public IccParametricCurve(float g, float a, float b)
            : this(IccParametricCurveType.Cie122_1996, g, a, b, 0, 0, 0, 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccParametricCurve"/> class.
        /// </summary>
        /// <param name="g">G curve parameter</param>
        /// <param name="a">A curve parameter</param>
        /// <param name="b">B curve parameter</param>
        /// <param name="c">C curve parameter</param>
        public IccParametricCurve(float g, float a, float b, float c)
            : this(IccParametricCurveType.Iec61966_3, g, a, b, c, 0, 0, 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccParametricCurve"/> class.
        /// </summary>
        /// <param name="g">G curve parameter</param>
        /// <param name="a">A curve parameter</param>
        /// <param name="b">B curve parameter</param>
        /// <param name="c">C curve parameter</param>
        /// <param name="d">D curve parameter</param>
        public IccParametricCurve(float g, float a, float b, float c, float d)
            : this(IccParametricCurveType.SRgb, g, a, b, c, d, 0, 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccParametricCurve"/> class.
        /// </summary>
        /// <param name="g">G curve parameter</param>
        /// <param name="a">A curve parameter</param>
        /// <param name="b">B curve parameter</param>
        /// <param name="c">C curve parameter</param>
        /// <param name="d">D curve parameter</param>
        /// <param name="e">E curve parameter</param>
        /// <param name="f">F curve parameter</param>
        public IccParametricCurve(float g, float a, float b, float c, float d, float e, float f)
            : this(IccParametricCurveType.Type5, g, a, b, c, d, e, f)
        {
        }

        private IccParametricCurve(IccParametricCurveType type, float g, float a, float b, float c, float d, float e, float f)
        {
            this.Type = type;
            this.G = g;
            this.A = a;
            this.B = b;
            this.C = c;
            this.D = d;
            this.E = e;
            this.F = f;
        }

        /// <summary>
        /// Gets the type of this curve
        /// </summary>
        public IccParametricCurveType Type { get; }

        /// <summary>
        /// Gets the G curve parameter
        /// </summary>
        public float G { get; }

        /// <summary>
        /// Gets the A curve parameter
        /// </summary>
        public float A { get; }

        /// <summary>
        /// Gets the B curve parameter
        /// </summary>
        public float B { get; }

        /// <summary>
        /// Gets the C curve parameter
        /// </summary>
        public float C { get; }

        /// <summary>
        /// Gets the D curve parameter
        /// </summary>
        public float D { get; }

        /// <summary>
        /// Gets the E curve parameter
        /// </summary>
        public float E { get; }

        /// <summary>
        /// Gets the F curve parameter
        /// </summary>
        public float F { get; }

        /// <inheritdoc/>
        public bool Equals(IccParametricCurve other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Type == other.Type
                && this.G.Equals(other.G)
                && this.A.Equals(other.A)
                && this.B.Equals(other.B)
                && this.C.Equals(other.C)
                && this.D.Equals(other.D)
                && this.E.Equals(other.E)
                && this.F.Equals(other.F);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is IccParametricCurve other && this.Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.Type,
                this.G.GetHashCode(),
                this.A.GetHashCode(),
                this.B.GetHashCode(),
                this.C.GetHashCode(),
                this.D.GetHashCode(),
                this.E.GetHashCode(),
                this.F.GetHashCode());
        }
    }
}
