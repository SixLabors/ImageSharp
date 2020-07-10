// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

// ReSharper disable CompareOfFloatsByEqualityOperator
namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Represents the coordinates of CIEXY chromaticity space.
    /// </summary>
    public readonly struct CieXyChromaticityCoordinates : IEquatable<CieXyChromaticityCoordinates>
    {
        /// <summary>
        /// Gets the chromaticity X-coordinate.
        /// </summary>
        /// <remarks>
        /// Ranges usually from 0 to 1.
        /// </remarks>
        public readonly float X;

        /// <summary>
        /// Gets the chromaticity Y-coordinate
        /// </summary>
        /// <remarks>
        /// Ranges usually from 0 to 1.
        /// </remarks>
        public readonly float Y;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyChromaticityCoordinates"/> struct.
        /// </summary>
        /// <param name="x">Chromaticity coordinate x (usually from 0 to 1)</param>
        /// <param name="y">Chromaticity coordinate y (usually from 0 to 1)</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieXyChromaticityCoordinates(float x, float y)
        {
            this.X = x;
            this.Y = y;
        }

        /// <summary>
        /// Compares two <see cref="CieXyChromaticityCoordinates"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="CieXyChromaticityCoordinates"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="CieXyChromaticityCoordinates"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(CieXyChromaticityCoordinates left, CieXyChromaticityCoordinates right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="CieXyChromaticityCoordinates"/> objects for inequality
        /// </summary>
        /// <param name="left">The <see cref="CieXyChromaticityCoordinates"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="CieXyChromaticityCoordinates"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(CieXyChromaticityCoordinates left, CieXyChromaticityCoordinates right) => !left.Equals(right);

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public override int GetHashCode() => HashCode.Combine(this.X, this.Y);

        /// <inheritdoc/>
        public override string ToString() => FormattableString.Invariant($"CieXyChromaticityCoordinates({this.X:#0.##}, {this.Y:#0.##})");

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is CieXyChromaticityCoordinates other && this.Equals(other);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(CieXyChromaticityCoordinates other) => this.X.Equals(other.X) && this.Y.Equals(other.Y);
    }
}
