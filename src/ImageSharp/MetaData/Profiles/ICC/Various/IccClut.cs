// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
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
            this.DataType = type;
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

            const float Max = ushort.MaxValue;

            this.Values = new float[values.Length][];
            for (int i = 0; i < values.Length; i++)
            {
                this.Values[i] = new float[values[i].Length];
                for (int j = 0; j < values[i].Length; j++)
                {
                    this.Values[i][j] = values[i][j] / Max;
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

            const float Max = byte.MaxValue;

            this.Values = new float[values.Length][];
            for (int i = 0; i < values.Length; i++)
            {
                this.Values[i] = new float[values[i].Length];
                for (int j = 0; j < values[i].Length; j++)
                {
                    this.Values[i][j] = values[i][j] / Max;
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
        /// Gets the CLUT data type (important when writing a profile)
        /// </summary>
        public IccClutDataType DataType { get; }

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
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.EqualsValuesArray(other)
                && this.DataType == other.DataType
                && this.InputChannelCount == other.InputChannelCount
                && this.OutputChannelCount == other.OutputChannelCount
                && this.GridPointCount.AsSpan().SequenceEqual(other.GridPointCount);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is IccClut other && this.Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.Values?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ (int)this.DataType;
                hashCode = (hashCode * 397) ^ this.InputChannelCount;
                hashCode = (hashCode * 397) ^ this.OutputChannelCount;
                hashCode = (hashCode * 397) ^ (this.GridPointCount?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        private bool EqualsValuesArray(IccClut other)
        {
            if (this.Values.Length != other.Values.Length)
            {
                return false;
            }

            for (int i = 0; i < this.Values.Length; i++)
            {
                if (!this.Values[i].AsSpan().SequenceEqual(other.Values[i]))
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
            Guard.IsFalse(isLengthDifferent, nameof(this.Values), "The number of output values varies");

            int length = 0;
            for (int i = 0; i < this.InputChannelCount; i++)
            {
                length += (int)Math.Pow(this.GridPointCount[i], this.InputChannelCount);
            }

            length /= this.InputChannelCount;

            Guard.IsTrue(this.Values.Length == length, nameof(this.Values), "Length of values array does not match the grid points");
        }
    }
}