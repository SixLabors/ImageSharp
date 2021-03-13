// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <content>
    /// Provides methods to write ICC data types
    /// </content>
    internal sealed partial class IccDataWriter
    {
        /// <summary>
        /// Writes an 8bit lookup table
        /// </summary>
        /// <param name="value">The LUT to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteLut8(IccLut value)
        {
            foreach (float item in value.Values)
            {
                this.WriteByte((byte)Numerics.Clamp((item * byte.MaxValue) + 0.5F, 0, byte.MaxValue));
            }

            return value.Values.Length;
        }

        /// <summary>
        /// Writes an 16bit lookup table
        /// </summary>
        /// <param name="value">The LUT to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteLut16(IccLut value)
        {
            foreach (float item in value.Values)
            {
                this.WriteUInt16((ushort)Numerics.Clamp((item * ushort.MaxValue) + 0.5F, 0, ushort.MaxValue));
            }

            return value.Values.Length * 2;
        }

        /// <summary>
        /// Writes an color lookup table
        /// </summary>
        /// <param name="value">The CLUT to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteClut(IccClut value)
        {
            int count = this.WriteArray(value.GridPointCount);
            count += this.WriteEmpty(16 - value.GridPointCount.Length);

            switch (value.DataType)
            {
                case IccClutDataType.Float:
                    return count + this.WriteClutF32(value);
                case IccClutDataType.UInt8:
                    count += this.WriteByte(1);
                    count += this.WriteEmpty(3);
                    return count + this.WriteClut8(value);
                case IccClutDataType.UInt16:
                    count += this.WriteByte(2);
                    count += this.WriteEmpty(3);
                    return count + this.WriteClut16(value);

                default:
                    throw new InvalidIccProfileException($"Invalid CLUT data type of {value.DataType}");
            }
        }

        /// <summary>
        /// Writes a 8bit color lookup table
        /// </summary>
        /// <param name="value">The CLUT to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteClut8(IccClut value)
        {
            int count = 0;
            foreach (float[] inArray in value.Values)
            {
                foreach (float item in inArray)
                {
                    count += this.WriteByte((byte)Numerics.Clamp((item * byte.MaxValue) + 0.5F, 0, byte.MaxValue));
                }
            }

            return count;
        }

        /// <summary>
        /// Writes a 16bit color lookup table
        /// </summary>
        /// <param name="value">The CLUT to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteClut16(IccClut value)
        {
            int count = 0;
            foreach (float[] inArray in value.Values)
            {
                foreach (float item in inArray)
                {
                    count += this.WriteUInt16((ushort)Numerics.Clamp((item * ushort.MaxValue) + 0.5F, 0, ushort.MaxValue));
                }
            }

            return count;
        }

        /// <summary>
        /// Writes a 32bit float color lookup table
        /// </summary>
        /// <param name="value">The CLUT to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteClutF32(IccClut value)
        {
            int count = 0;
            foreach (float[] inArray in value.Values)
            {
                foreach (float item in inArray)
                {
                    count += this.WriteSingle(item);
                }
            }

            return count;
        }
    }
}
