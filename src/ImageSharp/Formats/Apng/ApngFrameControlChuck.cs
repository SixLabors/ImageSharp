using System;
using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Apng
{
    internal struct ApngFrameControlChuck
    {
        public const int Size = 26;

        public uint SequenceNumber { get; set; }

        public uint Width { get; set; }

        public uint Height { get; set; }

        public uint XOffset { get; set; }

        public uint YOffset { get; set; }

        public ushort DelayNum { get; set; }

        public ushort DelayDem { get; set; }

        public ApngDisposeMethod DisposeOp { get; set; }

        public ApngBlendMethod BlendOp { get; set; }

        public static ApngFrameControlChuck Parse(ReadOnlySpan<byte> data)
        {
            if (data.Length != Size)
            {
                throw new ArgumentException($"Must be {Size} bytes", nameof(data));
            }

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