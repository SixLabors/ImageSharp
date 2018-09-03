using System;
using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Apng
{
    public struct ApngFrameControlChuck
    {
        public uint SequenceNumber { get; set; }

        public uint Width { get; set; }

        public uint Height { get; set; }

        public uint XOffset { get; set; }

        public uint YOffset { get; set; }

        public ushort DelayNum { get; set; }

        public ushort DelayDem { get; set; }

        public ApngDisposeMethod DisposeOp { get; set; }

        public ApngBlendMethod BlendOp { get; set; }

        public ApngFrameControlChuck Parse(ReadOnlySpan<byte> data)
        {
            return new ApngFrameControlChuck
            {
                SequenceNumber = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(0, 4)),
                Width = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4)),
                Height = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8, 4)),
                XOffset = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(12, 4)),
                YOffset = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(14, 4)),
                DelayNum = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(20, 2)),
                DelayDem = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(22, 2)),
                DisposeOp = (ApngDisposeMethod)data[24],
                BlendOp = (ApngBlendMethod)data[25]
            };
        }
    }
}