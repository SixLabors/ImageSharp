// Copyright(c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.ParallelUtils
{
    /// <summary>
    /// Represents an interval of rows in a <see cref="Rectangle"/> and/or <see cref="Buffer2D{T}"/>
    /// </summary>
    internal readonly struct RowInterval
    {
        public RowInterval(int min, int max)
        {
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
    }
}