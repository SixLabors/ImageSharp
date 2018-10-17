// Copyright(c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Represents an interval of rows in a <see cref="Rectangle"/> and/or <see cref="Buffer2D{T}"/>
    /// </summary>
    internal readonly struct RowInterval
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
        /// Gets the INCLUSIVE minimum
        /// </summary>
        public int Min { get; }

        /// <summary>
        /// Gets the EXCLUSIVE maximum
        /// </summary>
        public int Max { get; }

        /// <summary>
        /// Gets the difference (<see cref="Max"/> - <see cref="Min"/>)
        /// </summary>
        public int Height => this.Max - this.Min;

        /// <inheritdoc />
        public override string ToString()
        {
            return $"RowInterval [{this.Min}->{this.Max}[";
        }
    }
}