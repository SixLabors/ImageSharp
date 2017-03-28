// <copyright file="IccDataWriter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

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
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Type == other.Type
                && this.G == other.G
                && this.A == other.A
                && this.B == other.B
                && this.C == other.C
                && this.D == other.D
                && this.E == other.E
                && this.F == other.F;
        }
    }
}
