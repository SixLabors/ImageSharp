// <copyright file="IccLut.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;

    /// <summary>
    /// Lookup Table
    /// </summary>
    internal sealed class IccLut : IEquatable<IccLut>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccLut"/> class.
        /// </summary>
        /// <param name="values">The LUT values</param>
        public IccLut(float[] values)
        {
            Guard.NotNull(values, nameof(values));
            this.Values = values;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccLut"/> class.
        /// </summary>
        /// <param name="values">The LUT values</param>
        public IccLut(ushort[] values)
        {
            Guard.NotNull(values, nameof(values));

            const float max = ushort.MaxValue;

            this.Values = new float[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                this.Values[i] = values[i] / max;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccLut"/> class.
        /// </summary>
        /// <param name="values">The LUT values</param>
        public IccLut(byte[] values)
        {
            Guard.NotNull(values, nameof(values));

            const float max = byte.MaxValue;

            this.Values = new float[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                this.Values[i] = values[i] / max;
            }
        }

        /// <summary>
        /// Gets the values that make up this table
        /// </summary>
        public float[] Values { get; }

        /// <inheritdoc/>
        public bool Equals(IccLut other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Values.SequenceEqual(other.Values);
        }
    }
}
