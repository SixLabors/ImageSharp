// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

// ReSharper disable CompareOfFloatsByEqualityOperator
namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Represents the coordinates of CIEXY chromaticity space
    /// </summary>
    internal readonly struct CieXyChromaticityCoordinates : IEquatable<CieXyChromaticityCoordinates>, IAlmostEquatable<CieXyChromaticityCoordinates, float>
    {
        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private readonly Vector2 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyChromaticityCoordinates"/> struct.
        /// </summary>
        /// <param name="x">Chromaticity coordinate x (usually from 0 to 1)</param>
        /// <param name="y">Chromaticity coordinate y (usually from 0 to 1)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyChromaticityCoordinates(float x, float y)
            : this(new Vector2(x, y))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyChromaticityCoordinates"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the XY Chromaticity coordinates</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        public float X
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.X;
        }

        /// <summary>
        /// Gets the chromaticity Y-coordinate
        /// </summary>
        /// <remarks>
        /// Ranges usually from 0 to 1.
        /// </remarks>
        public float Y
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Y;
        }

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CieXyChromaticityCoordinates left, CieXyChromaticityCoordinates right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return this.backingVector.GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.Equals(default)
                ? "CieXyChromaticityCoordinates [Empty]"
                : $"CieXyChromaticityCoordinates [ X={this.X:#0.##}, Y={this.Y:#0.##}]";
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is CieXyChromaticityCoordinates other && this.Equals(other);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CieXyChromaticityCoordinates other)
        {
            // The memberwise comparison here is a workaround for https://github.com/dotnet/coreclr/issues/16443
            return this.X == other.X && this.Y == other.Y;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AlmostEquals(CieXyChromaticityCoordinates other, float precision)
        {
            var result = Vector2.Abs(this.backingVector - other.backingVector);

            return result.X <= precision
                && result.Y <= precision;
        }
    }
}