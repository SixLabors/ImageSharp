// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Tiff.Constants;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// The number of bits per component.
    /// </summary>
    public readonly struct TiffBitsPerSample : IEquatable<TiffBitsPerSample>
    {
        /// <summary>
        /// The bits for the channel 0.
        /// </summary>
        public readonly ushort Channel0;

        /// <summary>
        /// The bits for the channel 1.
        /// </summary>
        public readonly ushort Channel1;

        /// <summary>
        /// The bits for the channel 2.
        /// </summary>
        public readonly ushort Channel2;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffBitsPerSample"/> struct.
        /// </summary>
        /// <param name="channel0">The bits for the channel 0.</param>
        /// <param name="channel1">The bits for the channel 1.</param>
        /// <param name="channel2">The bits for the channel 2.</param>
        public TiffBitsPerSample(ushort channel0, ushort channel1, ushort channel2)
        {
            this.Channel0 = (ushort)Numerics.Clamp(channel0, 1, 32);
            this.Channel1 = (ushort)Numerics.Clamp(channel1, 0, 32);
            this.Channel2 = (ushort)Numerics.Clamp(channel2, 0, 32);
        }

        /// <summary>
        /// Tries to parse a ushort array and convert it into a TiffBitsPerSample struct.
        /// </summary>
        /// <param name="value">The value to parse.</param>
        /// <param name="sample">The tiff bits per sample.</param>
        /// <returns>True, if the value could be parsed.</returns>
        public static bool TryParse(ushort[] value, out TiffBitsPerSample sample)
        {
            if (value is null || value.Length == 0)
            {
                sample = default;
                return false;
            }

            ushort c2;
            ushort c1;
            ushort c0;
            switch (value.Length)
            {
                case 3:
                    c2 = value[2];
                    c1 = value[1];
                    c0 = value[0];
                    break;
                case 2:
                    c2 = 0;
                    c1 = value[1];
                    c0 = value[0];
                    break;
                default:
                    c2 = 0;
                    c1 = 0;
                    c0 = value[0];
                    break;
            }

            sample = new TiffBitsPerSample(c0, c1, c2);
            return true;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is TiffBitsPerSample sample && this.Equals(sample);

        /// <inheritdoc/>
        public bool Equals(TiffBitsPerSample other)
            => this.Channel0 == other.Channel0
               && this.Channel1 == other.Channel1
               && this.Channel2 == other.Channel2;

        /// <inheritdoc/>
        public override int GetHashCode()
            => HashCode.Combine(this.Channel0, this.Channel1, this.Channel2);

        /// <summary>
        /// Converts the bits per sample struct to an ushort array.
        /// </summary>
        /// <returns>Bits per sample as ushort array.</returns>
        public ushort[] ToArray()
        {
            if (this.Channel1 == 0)
            {
                return new[] { this.Channel0 };
            }

            if (this.Channel2 == 0)
            {
                return new[] { this.Channel0, this.Channel1 };
            }

            return new[] { this.Channel0, this.Channel1, this.Channel2 };
        }

        /// <summary>
        /// Maps an array of bits per sample to a concrete struct value.
        /// </summary>
        /// <param name="bitsPerSample">The bits per sample array.</param>
        /// <returns>TiffBitsPerSample enum value.</returns>
        public static TiffBitsPerSample? GetBitsPerSample(ushort[] bitsPerSample)
        {
            switch (bitsPerSample.Length)
            {
                case 3:
                    if (bitsPerSample[2] == TiffConstants.BitsPerSampleRgb16Bit.Channel2 &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb16Bit.Channel1 &&
                        bitsPerSample[0] == TiffConstants.BitsPerSampleRgb16Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSampleRgb16Bit;
                    }

                    if (bitsPerSample[2] == TiffConstants.BitsPerSampleRgb14Bit.Channel2 &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb14Bit.Channel1 &&
                        bitsPerSample[0] == TiffConstants.BitsPerSampleRgb14Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSampleRgb14Bit;
                    }

                    if (bitsPerSample[2] == TiffConstants.BitsPerSampleRgb12Bit.Channel2 &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb12Bit.Channel1 &&
                        bitsPerSample[0] == TiffConstants.BitsPerSampleRgb12Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSampleRgb12Bit;
                    }

                    if (bitsPerSample[2] == TiffConstants.BitsPerSampleRgb10Bit.Channel2 &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb10Bit.Channel1 &&
                        bitsPerSample[0] == TiffConstants.BitsPerSampleRgb10Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSampleRgb10Bit;
                    }

                    if (bitsPerSample[2] == TiffConstants.BitsPerSampleRgb8Bit.Channel2 &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb8Bit.Channel1 &&
                        bitsPerSample[0] == TiffConstants.BitsPerSampleRgb8Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSampleRgb8Bit;
                    }

                    if (bitsPerSample[2] == TiffConstants.BitsPerSampleRgb4Bit.Channel2 &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb4Bit.Channel1 &&
                        bitsPerSample[0] == TiffConstants.BitsPerSampleRgb4Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSampleRgb4Bit;
                    }

                    if (bitsPerSample[2] == TiffConstants.BitsPerSampleRgb2Bit.Channel2 &&
                        bitsPerSample[1] == TiffConstants.BitsPerSampleRgb2Bit.Channel1 &&
                        bitsPerSample[0] == TiffConstants.BitsPerSampleRgb2Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSampleRgb2Bit;
                    }

                    break;

                case 1:
                    if (bitsPerSample[0] == TiffConstants.BitsPerSample1Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSample1Bit;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample2Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSample2Bit;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample4Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSample4Bit;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample6Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSample6Bit;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample8Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSample8Bit;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample10Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSample10Bit;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample12Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSample12Bit;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample14Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSample14Bit;
                    }

                    if (bitsPerSample[0] == TiffConstants.BitsPerSample16Bit.Channel0)
                    {
                        return TiffConstants.BitsPerSample16Bit;
                    }

                    break;
            }

            return null;
        }

        /// <summary>
        /// Gets the bits per pixel for the given bits per sample.
        /// </summary>
        /// <returns>Bits per pixel.</returns>
        public TiffBitsPerPixel BitsPerPixel()
        {
            int bitsPerPixel = this.Channel0 + this.Channel1 + this.Channel2;
            return (TiffBitsPerPixel)bitsPerPixel;
        }

        /// <inheritdoc/>
        public override string ToString()
            => $"TiffBitsPerSample({this.Channel0}, {this.Channel1}, {this.Channel2})";
    }
}
