using System;
using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Apng
{
    public struct PngAnimationControlChuck
    {
        public uint NumberFrames { get; set; }

        public uint NumberPlays { get; set; }

        public PngAnimationControlChuck Parse(ReadOnlySpan<byte> data)
        {
            return new PngAnimationControlChuck
            {
                NumberFrames = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(0, 4)),
                NumberPlays = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4))
            };
        }
    }
}