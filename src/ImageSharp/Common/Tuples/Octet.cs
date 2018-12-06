using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Tuples
{
    /// <summary>
    /// Contains 8 element value tuples of various types.
    /// </summary>
    internal static class Octet
    {
        /// <summary>
        /// Value tuple of <see cref="uint"/>-s
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
                return $"{nameof(Octet)}.{nameof(OfUInt32)}({this.V0},{this.V1},{this.V2},{this.V3},{this.V4},{this.V5},{this.V6},{this.V7})";
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void LoadFrom(ref OfByte src)
            {
                this.V0 = src.V0;
                this.V1 = src.V1;
                this.V2 = src.V2;
                this.V3 = src.V3;
                this.V4 = src.V4;
                this.V5 = src.V5;
                this.V6 = src.V6;
                this.V7 = src.V7;
            }
        }

        /// <summary>
        /// Value tuple of <see cref="byte"/>-s
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
                return $"{nameof(Octet)}.{nameof(OfByte)}({this.V0},{this.V1},{this.V2},{this.V3},{this.V4},{this.V5},{this.V6},{this.V7})";
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void LoadFrom(ref OfUInt32 src)
            {
                this.V0 = (byte)src.V0;
                this.V1 = (byte)src.V1;
                this.V2 = (byte)src.V2;
                this.V3 = (byte)src.V3;
                this.V4 = (byte)src.V4;
                this.V5 = (byte)src.V5;
                this.V6 = (byte)src.V6;
                this.V7 = (byte)src.V7;
            }
        }
    }
}