// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

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
        /// The number of channels.
        /// </summary>
        public readonly byte Channels;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffBitsPerSample"/> struct.
        /// </summary>
        /// <param name="channel0">The bits for the channel 0.</param>
        /// <param name="channel1">The bits for the channel 1.</param>
        /// <param name="channel2">The bits for the channel 2.</param>
        public TiffBitsPerSample(ushort channel0, ushort channel1, ushort channel2)
        {
            this.Channel0 = (ushort)Numerics.Clamp(channel0, 0, 32);
            this.Channel1 = (ushort)Numerics.Clamp(channel1, 0, 32);
            this.Channel2 = (ushort)Numerics.Clamp(channel2, 0, 32);

            this.Channels = 0;
            this.Channels += (byte)(this.Channel0 != 0 ? 1 : 0);
            this.Channels += (byte)(this.Channel1 != 0 ? 1 : 0);
            this.Channels += (byte)(this.Channel2 != 0 ? 1 : 0);
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
