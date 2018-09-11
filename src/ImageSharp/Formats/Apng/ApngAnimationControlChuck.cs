using System;
using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Apng
{
    /// <summary>
    /// The `acTL` chunk is an ancillary chunk as defined in the PNG Specification.
    /// </summary>
    internal readonly struct ApngAnimationControlChuck
    {
        public const int Size = 8;

        public ApngAnimationControlChuck(uint numberFrames, uint numberPlays)
        {
            this.NumberFrames = numberFrames;
            this.NumberPlays = numberPlays;
        }

        /// <summary>
        /// Gets the total number of frames in the APNG animation.
        /// </summary>
        public uint NumberFrames { get; }

        /// <summary>
        /// Gets the number of times to loop this APNG.
        /// 0 indicates infinite looping.
        /// </summary>
        public uint NumberPlays { get; }

        /// <summary>
        /// Parses the ApngAnimationControlChuck structure.
        /// </summary>
        /// <param name="data">The raw data of the structure.</param>
        /// <returns>The parsed ApngAnimationControlChuck.</returns>
        public static ApngAnimationControlChuck Parse(ReadOnlySpan<byte> data)
        {
            if (data.Length != Size)
            {
                throw new ArgumentException("Must be 8 bytes", nameof(data));
            }

            return new ApngAnimationControlChuck(
                numberFrames: BinaryPrimitives.ReadUInt32BigEndian(data.Slice(0, 4)),
                numberPlays: BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4))
            );
        }
    }
}