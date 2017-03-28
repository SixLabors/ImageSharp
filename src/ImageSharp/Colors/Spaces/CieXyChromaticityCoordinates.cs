// <copyright file="CieXyChromaticityCoordinates.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces
{
    using System;
    using System.ComponentModel;
    using System.Numerics;

    /// <summary>
    /// Represents the coordinates of CIEXY chromaticity space
    /// </summary>
    public struct CieXyChromaticityCoordinates : IEquatable<CieXyChromaticityCoordinates>, IAlmostEquatable<CieXyChromaticityCoordinates, float>
    {
        /// <summary>
        /// Represents a <see cref="CieXyChromaticityCoordinates"/> that has X, Y values set to zero.
        /// </summary>
        public static readonly CieXyChromaticityCoordinates Empty = default(CieXyChromaticityCoordinates);

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private readonly Vector2 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyChromaticityCoordinates"/> struct.
        /// </summary>
        /// <param name="x">Chromaticity coordinate x (usually from 0 to 1)</param>
        /// <param name="y">Chromaticity coordinate y (usually from 0 to 1)</param>
        public CieXyChromaticityCoordinates(float x, float y)
            : this(new Vector2(x, y))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyChromaticityCoordinates"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the XY Chromaticity coordinates</param>
        public CieXyChromaticityCoordinates(Vector2 vector)
        {
            this.backingVector = vector;
        }

        /// <summary>
        /// Gets the chromaticity X-coordinate.
        /// </summary>
        /// <remarks>
        /// Ranges usually from 0 to 1.
        /// </remarks>
        public float X => this.backingVector.X;

        /// <summary>
        /// Gets the chromaticity Y-coordinate
        /// </summary>
        /// <remarks>
        /// Ranges usually from 0 to 1.
        /// </remarks>
        public float Y => this.backingVector.Y;

        /// <summary>
        /// Gets a value indicating whether this <see cref="CieXyChromaticityCoordinates"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

        /// <summary>
        /// Compares two <see cref="CieXyChromaticityCoordinates"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieXyChromaticityCoordinates"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieXyChromaticityCoordinates"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(CieXyChromaticityCoordinates left, CieXyChromaticityCoordinates right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="CieXyChromaticityCoordinates"/> objects for inequality
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieXyChromaticityCoordinates"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieXyChromaticityCoordinates"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(CieXyChromaticityCoordinates left, CieXyChromaticityCoordinates right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.backingVector.GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.IsEmpty)
            {
                return "CieXyChromaticityCoordinates [Empty]";
            }

            return $"CieXyChromaticityCoordinates [ X={this.X:#0.##}, Y={this.Y:#0.##}]";
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is CieXyChromaticityCoordinates)
            {
                return this.Equals((CieXyChromaticityCoordinates)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(CieXyChromaticityCoordinates other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <inheritdoc/>
        public bool AlmostEquals(CieXyChromaticityCoordinates other, float precision)
        {
            Vector2 result = Vector2.Abs(this.backingVector - other.backingVector);

            return result.X <= precision
                && result.Y <= precision;
        }
    }
}