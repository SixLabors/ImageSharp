// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Represents an interval of rows in a <see cref="Rectangle"/> and/or <see cref="Buffer2D{T}"/>
    /// </summary>
    internal readonly struct RowInterval : IEquatable<RowInterval>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RowInterval"/> struct.
        /// </summary>
        public RowInterval(int min, int max)
        {
            DebugGuard.MustBeLessThan(min, max, nameof(min));

            this.Min = min;
            this.Max = max;
        }

        /// <summary>
        /// Gets the INCLUSIVE minimum.
        /// </summary>
        public int Min { get; }

        /// <summary>
        /// Gets the EXCLUSIVE maximum.
        /// </summary>
        public int Max { get; }

        /// <summary>
        /// Gets the difference (<see cref="Max"/> - <see cref="Min"/>).
        /// </summary>
        public int Height => this.Max - this.Min;

        public static bool operator ==(RowInterval left, RowInterval right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RowInterval left, RowInterval right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public override string ToString() => $"RowInterval [{this.Min}->{this.Max}]";

        public RowInterval Slice(int start) => new RowInterval(this.Min + start, this.Max);

        public RowInterval Slice(int start, int length) => new RowInterval(this.Min + start, this.Min + start + length);

        public bool Equals(RowInterval other)
        {
            return this.Min == other.Min && this.Max == other.Max;
        }

        public override bool Equals(object obj)
        {
            return !ReferenceEquals(null, obj) && obj is RowInterval other && this.Equals(other);
        }

        public override int GetHashCode() => HashCode.Combine(this.Min, this.Max);
    }
}