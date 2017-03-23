// <copyright file="IccClut.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;

    /// <summary>
    /// Color Lookup Table
    /// </summary>
    internal sealed class IccClut : IEquatable<IccClut>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccClut"/> class.
        /// </summary>
        /// <param name="values">The CLUT values</param>
        /// <param name="gridPointCount">The gridpoint count</param>
        /// <param name="type">The data type of this CLUT</param>
        public IccClut(float[][] values, byte[] gridPointCount, IccClutDataType type)
        {
            Guard.NotNull(values, nameof(values));
            Guard.NotNull(gridPointCount, nameof(gridPointCount));

            this.Values = values;
            this.DataType = IccClutDataType.Float;
            this.InputChannelCount = gridPointCount.Length;
            this.OutputChannelCount = values[0].Length;
            this.GridPointCount = gridPointCount;
            this.CheckValues();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccClut"/> class.
        /// </summary>
        /// <param name="values">The CLUT values</param>
        /// <param name="gridPointCount">The gridpoint count</param>
        public IccClut(ushort[][] values, byte[] gridPointCount)
        {
            Guard.NotNull(values, nameof(values));
            Guard.NotNull(gridPointCount, nameof(gridPointCount));

            const float max = ushort.MaxValue;

            this.Values = new float[values.Length][];
            for (int i = 0; i < values.Length; i++)
            {
                this.Values[i] = new float[values[i].Length];
                for (int j = 0; j < values[i].Length; j++)
                {
                    this.Values[i][j] = values[i][j] / max;
                }
            }

            this.DataType = IccClutDataType.UInt16;
            this.InputChannelCount = gridPointCount.Length;
            this.OutputChannelCount = values[0].Length;
            this.GridPointCount = gridPointCount;
            this.CheckValues();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccClut"/> class.
        /// </summary>
        /// <param name="values">The CLUT values</param>
        /// <param name="gridPointCount">The gridpoint count</param>
        public IccClut(byte[][] values, byte[] gridPointCount)
        {
            Guard.NotNull(values, nameof(values));
            Guard.NotNull(gridPointCount, nameof(gridPointCount));

            const float max = byte.MaxValue;

            this.Values = new float[values.Length][];
            for (int i = 0; i < values.Length; i++)
            {
                this.Values[i] = new float[values[i].Length];
                for (int j = 0; j < values[i].Length; j++)
                {
                    this.Values[i][j] = values[i][j] / max;
                }
            }

            this.DataType = IccClutDataType.UInt8;
            this.InputChannelCount = gridPointCount.Length;
            this.OutputChannelCount = values[0].Length;
            this.GridPointCount = gridPointCount;
            this.CheckValues();
        }

        /// <summary>
        /// Gets the values that make up this table
        /// </summary>
        public float[][] Values { get; }

        /// <summary>
        /// Gets or sets the CLUT data type (important when writing a profile)
        /// </summary>
        public IccClutDataType DataType { get; set; }

        /// <summary>
        /// Gets the number of input channels
        /// </summary>
        public int InputChannelCount { get; }

        /// <summary>
        /// Gets the number of output channels
        /// </summary>
        public int OutputChannelCount { get; }

        /// <summary>
        /// Gets the number of grid points per input channel
        /// </summary>
        public byte[] GridPointCount { get; }

        /// <inheritdoc/>
        public bool Equals(IccClut other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.DataType == other.DataType
                && this.InputChannelCount == other.InputChannelCount
                && this.OutputChannelCount == other.OutputChannelCount
                && this.GridPointCount.SequenceEqual(other.GridPointCount)
                && this.EqualsValuesArray(other);
        }

        private bool EqualsValuesArray(IccClut other)
        {
            if (this.Values.Length != other.Values.Length)
            {
                return false;
            }

            for (int i = 0; i < this.Values.Length; i++)
            {
                if (!this.Values[i].SequenceEqual(other.Values[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void CheckValues()
        {
            Guard.MustBeBetweenOrEqualTo(this.InputChannelCount, 1, 15, nameof(this.InputChannelCount));
            Guard.MustBeBetweenOrEqualTo(this.OutputChannelCount, 1, 15, nameof(this.OutputChannelCount));

            bool isLengthDifferent = this.Values.Any(t => t.Length != this.OutputChannelCount);
            Guard.IsTrue(isLengthDifferent, nameof(this.Values), "The number of output values varies");

            int length = 0;
            for (int i = 0; i < this.InputChannelCount; i++)
            {
                length += (int)Math.Pow(this.GridPointCount[i], this.InputChannelCount);
            }

            length /= this.InputChannelCount;

            Guard.IsTrue(this.Values.Length != length, nameof(this.Values), "Length of values array does not match the grid points");
        }
    }
}
