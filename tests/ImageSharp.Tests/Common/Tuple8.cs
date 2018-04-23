using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Common.Tuples
{
    /// <summary>
    /// Contains value type tuples of 8 elements.
    /// TODO: We should T4 this stuff to be DRY
    /// </summary>
    internal static class Tuple8
    {
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

            /// <summary>
            /// Sets the values of this tuple by casting all elements of the given <see cref="OfUInt32"/> tuple to <see cref="byte"/>.
            /// </summary>
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