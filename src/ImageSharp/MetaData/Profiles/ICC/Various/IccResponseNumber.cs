// <copyright file="IccResponseNumber.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// Associates a normalized device code with a measurement value
    /// </summary>
    internal struct IccResponseNumber : IEquatable<IccResponseNumber>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccResponseNumber"/> struct.
        /// </summary>
        /// <param name="deviceCode">Device Code</param>
        /// <param name="measurementValue">Measurement Value</param>
        public IccResponseNumber(ushort deviceCode, float measurementValue)
        {
            this.DeviceCode = deviceCode;
            this.MeasurementValue = measurementValue;
        }

        /// <summary>
        /// Gets the device code
        /// </summary>
        public ushort DeviceCode { get; }

        /// <summary>
        /// Gets the measurement value
        /// </summary>
        public float MeasurementValue { get; }

        /// <summary>
        /// Compares two <see cref="IccResponseNumber"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="IccResponseNumber"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="IccResponseNumber"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(IccResponseNumber left, IccResponseNumber right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="IccResponseNumber"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="IccResponseNumber"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="IccResponseNumber"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(IccResponseNumber left, IccResponseNumber right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public override bool Equals(object other)
        {
            return (other is IccResponseNumber) && this.Equals((IccResponseNumber)other);
        }

        /// <inheritdoc/>
        public bool Equals(IccResponseNumber other)
        {
            return this.DeviceCode == other.DeviceCode
                && this.MeasurementValue == other.MeasurementValue;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.DeviceCode.GetHashCode();
                hashCode = (hashCode * 397) ^ this.MeasurementValue.GetHashCode();
                return hashCode;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Code: {this.DeviceCode}; Value: {this.MeasurementValue}";
        }
    }
}
