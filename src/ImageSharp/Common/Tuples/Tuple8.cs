using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Common.Tuples
{
    /// <summary>
    /// Contains value type tuples of 8 elements.
    /// TODO: Should T4 this stuff to be DRY
    /// </summary>
    internal static class Tuple8
    {
        /// <summary>
        /// Value type tuple of 8 <see cref="float"/>-s
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 8 * sizeof(float))]
        public struct OfSingle
        {
            [FieldOffset(0 * sizeof(float))]
            public float V0;

            [FieldOffset(1 * sizeof(float))]
            public float V1;

            [FieldOffset(2 * sizeof(float))]
            public float V2;

            [FieldOffset(3 * sizeof(float))]
            public float V3;

            [FieldOffset(4 * sizeof(float))]
            public float V4;

            [FieldOffset(5 * sizeof(float))]
            public float V5;

            [FieldOffset(6 * sizeof(float))]
            public float V6;

            [FieldOffset(7 * sizeof(float))]
            public float V7;

            public override string ToString()
            {
                return $"[{this.V0},{this.V1},{this.V2},{this.V3},{this.V4},{this.V5},{this.V6},{this.V7}]";
            }
        }

        /// <summary>
        /// Value type tuple of 8 <see cref="int"/>-s
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 8 * sizeof(int))]
        public struct OfInt32
        {
            [FieldOffset(0 * sizeof(int))]
            public int V0;

            [FieldOffset(1 * sizeof(int))]
            public int V1;

            [FieldOffset(2 * sizeof(int))]
            public int V2;

            [FieldOffset(3 * sizeof(int))]
            public int V3;

            [FieldOffset(4 * sizeof(int))]
            public int V4;

            [FieldOffset(5 * sizeof(int))]
            public int V5;

            [FieldOffset(6 * sizeof(int))]
            public int V6;

            [FieldOffset(7 * sizeof(int))]
            public int V7;

            public override string ToString()
            {
                return $"[{this.V0},{this.V1},{this.V2},{this.V3},{this.V4},{this.V5},{this.V6},{this.V7}]";
            }
        }

        /// <summary>
        /// Value type tuple of 8 <see cref="uint"/>-s
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 8 * sizeof(uint))]
        public struct OfUInt32
        {
            [FieldOffset(0 * sizeof(uint))]
            public uint V0;

            [FieldOffset(1 * sizeof(uint))]
            public uint V1;

            [FieldOffset(2 * sizeof(uint))]
            public uint V2;

            [FieldOffset(3 * sizeof(uint))]
            public uint V3;

            [FieldOffset(4 * sizeof(uint))]
            public uint V4;

            [FieldOffset(5 * sizeof(uint))]
            public uint V5;

            [FieldOffset(6 * sizeof(uint))]
            public uint V6;

            [FieldOffset(7 * sizeof(uint))]
            public uint V7;

            public override string ToString()
            {
                return $"[{this.V0},{this.V1},{this.V2},{this.V3},{this.V4},{this.V5},{this.V6},{this.V7}]";
            }

            public void LoadFrom(ref OfUInt16 i)
            {
                this.V0 = i.V0;
                this.V1 = i.V1;
                this.V2 = i.V2;
                this.V3 = i.V3;
                this.V4 = i.V4;
                this.V5 = i.V5;
                this.V6 = i.V6;
                this.V7 = i.V7;
            }
        }

        /// <summary>
        /// Value type tuple of 8 <see cref="ushort"/>-s
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 8 * sizeof(ushort))]
        public struct OfUInt16
        {
            [FieldOffset(0 * sizeof(ushort))]
            public ushort V0;

            [FieldOffset(1 * sizeof(ushort))]
            public ushort V1;

            [FieldOffset(2 * sizeof(ushort))]
            public ushort V2;

            [FieldOffset(3 * sizeof(ushort))]
            public ushort V3;

            [FieldOffset(4 * sizeof(ushort))]
            public ushort V4;

            [FieldOffset(5 * sizeof(ushort))]
            public ushort V5;

            [FieldOffset(6 * sizeof(ushort))]
            public ushort V6;

            [FieldOffset(7 * sizeof(ushort))]
            public ushort V7;

            public override string ToString()
            {
                return $"[{this.V0},{this.V1},{this.V2},{this.V3},{this.V4},{this.V5},{this.V6},{this.V7}]";
            }
        }

        /// <summary>
        /// Value type tuple of 8 <see cref="ushort"/>-s
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 8 * sizeof(short))]
        public struct OfInt16
        {
            [FieldOffset(0 * sizeof(short))]
            public short V0;

            [FieldOffset(1 * sizeof(short))]
            public short V1;

            [FieldOffset(2 * sizeof(short))]
            public short V2;

            [FieldOffset(3 * sizeof(short))]
            public short V3;

            [FieldOffset(4 * sizeof(short))]
            public short V4;

            [FieldOffset(5 * sizeof(short))]
            public short V5;

            [FieldOffset(6 * sizeof(short))]
            public short V6;

            [FieldOffset(7 * sizeof(short))]
            public short V7;

            public override string ToString()
            {
                return $"[{this.V0},{this.V1},{this.V2},{this.V3},{this.V4},{this.V5},{this.V6},{this.V7}]";
            }
        }

        /// <summary>
        /// Value type tuple of 8 <see cref="byte"/>-s
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 8)]
        public struct OfByte
        {
            [FieldOffset(0)]
            public byte V0;

            [FieldOffset(1)]
            public byte V1;

            [FieldOffset(2)]
            public byte V2;

            [FieldOffset(3)]
            public byte V3;

            [FieldOffset(4)]
            public byte V4;

            [FieldOffset(5)]
            public byte V5;

            [FieldOffset(6)]
            public byte V6;

            [FieldOffset(7)]
            public byte V7;

            public override string ToString()
            {
                return $"[{this.V0},{this.V1},{this.V2},{this.V3},{this.V4},{this.V5},{this.V6},{this.V7}]";
            }

            public void LoadFrom(ref OfUInt32 i)
            {
                this.V0 = (byte)i.V0;
                this.V1 = (byte)i.V1;
                this.V2 = (byte)i.V2;
                this.V3 = (byte)i.V3;
                this.V4 = (byte)i.V4;
                this.V5 = (byte)i.V5;
                this.V6 = (byte)i.V6;
                this.V7 = (byte)i.V7;
            }
        }
    }
}