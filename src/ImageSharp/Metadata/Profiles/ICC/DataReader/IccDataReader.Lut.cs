// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Provides methods to read ICC data types
    /// </summary>
    internal sealed partial class IccDataReader
    {
        /// <summary>
        /// Reads an 8bit lookup table
        /// </summary>
        /// <returns>The read LUT</returns>
        public IccLut ReadLut8()
        {
            return new IccLut(this.ReadBytes(256));
        }

        /// <summary>
        /// Reads a 16bit lookup table
        /// </summary>
        /// <param name="count">The number of entries</param>
        /// <returns>The read LUT</returns>
        public IccLut ReadLut16(int count)
        {
            var values = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = this.ReadUInt16();
            }

            return new IccLut(values);
        }

        /// <summary>
        /// Reads a CLUT depending on type
        /// </summary>
        /// <param name="inChannelCount">Input channel count</param>
        /// <param name="outChannelCount">Output channel count</param>
        /// <param name="isFloat">If true, it's read as CLUTf32,
        /// else read as either CLUT8 or CLUT16 depending on embedded information</param>
        /// <returns>The read CLUT</returns>
        public IccClut ReadClut(int inChannelCount, int outChannelCount, bool isFloat)
        {
            // Grid-points are always 16 bytes long but only 0-inChCount are used
            var gridPointCount = new byte[inChannelCount];
            Buffer.BlockCopy(this.data, this.AddIndex(16), gridPointCount, 0, inChannelCount);

            if (!isFloat)
            {
                byte size = this.data[this.AddIndex(4)];   // First byte is info, last 3 bytes are reserved
                if (size == 1)
                {
                    return this.ReadClut8(inChannelCount, outChannelCount, gridPointCount);
                }

                if (size == 2)
                {
                    return this.ReadClut16(inChannelCount, outChannelCount, gridPointCount);
                }

                throw new InvalidIccProfileException($"Invalid CLUT size of {size}");
            }

            return this.ReadClutF32(inChannelCount, outChannelCount, gridPointCount);
        }

        /// <summary>
        /// Reads an 8 bit CLUT
        /// </summary>
        /// <param name="inChannelCount">Input channel count</param>
        /// <param name="outChannelCount">Output channel count</param>
        /// <param name="gridPointCount">Grid point count for each CLUT channel</param>
        /// <returns>The read CLUT8</returns>
        public IccClut ReadClut8(int inChannelCount, int outChannelCount, byte[] gridPointCount)
        {
            int start = this.currentIndex;
            int length = 0;
            for (int i = 0; i < inChannelCount; i++)
            {
                length += (int)Math.Pow(gridPointCount[i], inChannelCount);
            }

            length /= inChannelCount;

            const float Max = byte.MaxValue;

            var values = new float[length][];
            for (int i = 0; i < length; i++)
            {
                values[i] = new float[outChannelCount];
                for (int j = 0; j < outChannelCount; j++)
                {
                    values[i][j] = this.data[this.currentIndex++] / Max;
                }
            }

            this.currentIndex = start + (length * outChannelCount);
            return new IccClut(values, gridPointCount, IccClutDataType.UInt8);
        }

        /// <summary>
        /// Reads a 16 bit CLUT
        /// </summary>
        /// <param name="inChannelCount">Input channel count</param>
        /// <param name="outChannelCount">Output channel count</param>
        /// <param name="gridPointCount">Grid point count for each CLUT channel</param>
        /// <returns>The read CLUT16</returns>
        public IccClut ReadClut16(int inChannelCount, int outChannelCount, byte[] gridPointCount)
        {
            int start = this.currentIndex;
            int length = 0;
            for (int i = 0; i < inChannelCount; i++)
            {
                length += (int)Math.Pow(gridPointCount[i], inChannelCount);
            }

            length /= inChannelCount;

            const float Max = ushort.MaxValue;

            var values = new float[length][];
            for (int i = 0; i < length; i++)
            {
                values[i] = new float[outChannelCount];
                for (int j = 0; j < outChannelCount; j++)
                {
                    values[i][j] = this.ReadUInt16() / Max;
                }
            }

            this.currentIndex = start + (length * outChannelCount * 2);
            return new IccClut(values, gridPointCount, IccClutDataType.UInt16);
        }

        /// <summary>
        /// Reads a 32bit floating point CLUT
        /// </summary>
        /// <param name="inChCount">Input channel count</param>
        /// <param name="outChCount">Output channel count</param>
        /// <param name="gridPointCount">Grid point count for each CLUT channel</param>
        /// <returns>The read CLUTf32</returns>
        public IccClut ReadClutF32(int inChCount, int outChCount, byte[] gridPointCount)
        {
            int start = this.currentIndex;
            int length = 0;
            for (int i = 0; i < inChCount; i++)
            {
                length += (int)Math.Pow(gridPointCount[i], inChCount);
            }

            length /= inChCount;

            var values = new float[length][];
            for (int i = 0; i < length; i++)
            {
                values[i] = new float[outChCount];
                for (int j = 0; j < outChCount; j++)
                {
                    values[i][j] = this.ReadSingle();
                }
            }

            this.currentIndex = start + (length * outChCount * 4);
            return new IccClut(values, gridPointCount, IccClutDataType.Float);
        }
    }
}
