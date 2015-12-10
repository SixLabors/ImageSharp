// <copyright file="Ellipse.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;
    using System.ComponentModel;
    using System.Numerics;

    public struct Ellipse : IEquatable<Ellipse>
    {
        /// <summary>
        /// The center point.
        /// </summary>
        private Point center;

        /// <summary>
        /// The epsilon for comparing floating point numbers.
        /// </summary>
        private const float Epsilon = 0.0001f;

        /// <summary>
        /// Represents a <see cref="Ellipse"/> that has X and Y values set to zero.
        /// </summary>
        public static readonly Ellipse Empty = default(Ellipse);

        public Ellipse(Point center, float radiusX, float radiusY)
        {
            this.center = center;
            this.RadiusX = radiusX;
            this.RadiusY = radiusY;
        }

        /// <summary>
        /// Gets the x-radius of this <see cref="Ellipse"/>.
        /// </summary>
        public float RadiusX { get; }

        /// <summary>
        /// Gets the y-radius of this <see cref="Ellipse"/>.
        /// </summary>
        public float RadiusY { get; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Ellipse"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.center.IsEmpty
            && Math.Abs(this.RadiusX) < Epsilon
            && Math.Abs(this.RadiusY) < Epsilon;

        /// <summary>
        /// Compares two <see cref="Ellipse"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Ellipse"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Ellipse"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Ellipse left, Ellipse right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="Ellipse"/> objects for inequality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Ellipse"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Ellipse"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Ellipse left, Ellipse right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Returns the center point of the given <see cref="Ellipse"/>
        /// </summary>
        /// <param name="ellipse">The ellipse</param>
        /// <returns><see cref="Vector2"/></returns>
        public static Vector2 Center(Ellipse ellipse)
        {
            return new Vector2(ellipse.center.X, ellipse.center.Y);
        }

        /// <summary>
        /// Determines if the specfied point is contained within the rectangular region defined by
        /// this <see cref="Rectangle"/>.
        /// </summary>
        /// <param name="x">The x-coordinate of the given point.</param>
        /// <param name="y">The y-coordinate of the given point.</param>
        /// <returns>The <see cref="bool"/></returns>
        public bool Contains(int x, int y)
        {
            if (this.RadiusX <= 0 || this.RadiusY <= 0)
            {
                return false;
            }

            //This is a more general form of the circle equation
            // X^2/a^2 + Y^2/b^2 <= 1
            Point normalized = new Point(x - this.center.X, y - this.center.Y);
            int nX = normalized.X;
            int nY = normalized.Y;

            //return (double)(nX * nX) / (this.RadiusX * this.RadiusX)
            //    + (double)(nY * nY) / (this.RadiusY * this.RadiusY)
            //    <= 1.0;

            return ((double)(nX * nX) / (this.RadiusX * this.RadiusX))
                     + ((double)(nY * nY) / (this.RadiusY * this.RadiusY))
                <= 1.0;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (!(obj is Ellipse))
            {
                return false;
            }

            Ellipse other = (Ellipse)obj;

            return other.center == this.center
                && Math.Abs(other.RadiusX - this.RadiusX) < Epsilon
                && Math.Abs(other.RadiusY - this.RadiusY) < Epsilon;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.GetHashCode(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.IsEmpty)
            {
                return "Ellipse [ Empty ]";
            }

            return
                $"Ellipse [ RadiusX={this.RadiusX}, RadiusY={this.RadiusX}, Centre={this.center.X},{this.center.Y} ]";
        }

        /// <inheritdoc/>
        public bool Equals(Ellipse other)
        {
            return this.center.Equals(other.center)
                && this.RadiusX.Equals(other.RadiusX)
                && this.RadiusX.Equals(other.RadiusY);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="point">
        /// The instance of <see cref="Point"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(Ellipse point)
        {
            unchecked
            {
                int hashCode = point.center.GetHashCode();
                hashCode = (hashCode * 397) ^ point.RadiusX.GetHashCode();
                hashCode = (hashCode * 397) ^ point.RadiusY.GetHashCode();
                return hashCode;
            }
        }
    }
}
