// <copyright file="IccDataReader.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Globalization;
    using System.Numerics;
    using System.Text;
    using ImageSharp.IO;

    /// <summary>
    /// Provides methods to read ICC data types
    /// </summary>
    internal sealed class IccDataReader
    {
        private static readonly bool IsLittleEndian = BitConverter.IsLittleEndian;
        private static readonly Encoding AsciiEncoding = Encoding.GetEncoding("ASCII");

        /// <summary>
        /// The data that is read
        /// </summary>
        private readonly byte[] data;

        /// <summary>
        /// The current reading position
        /// </summary>
        private int index;

        private EndianBitConverter converter = new BigEndianBitConverter();

        /// <summary>
        /// Initializes a new instance of the <see cref="IccDataReader"/> class.
        /// </summary>
        /// <param name="data">The data to read</param>
        public IccDataReader(byte[] data)
        {
            this.data = data;
        }

        /// <summary>
        /// Sets the reading position to the given value
        /// </summary>
        /// <param name="index">The new index position</param>
        public void SetIndex(int index)
        {
            this.index = index.Clamp(0, this.data.Length);
        }

        #region Read Primitives

        /// <summary>
        /// Reads an ushort
        /// </summary>
        /// <returns>the value</returns>
        public ushort ReadUInt16()
        {
            return this.converter.ToUInt16(this.data, this.AddIndex(2));
        }

        /// <summary>
        /// Reads a short
        /// </summary>
        /// <returns>the value</returns>
        public short ReadInt16()
        {
            return this.converter.ToInt16(this.data, this.AddIndex(2));
        }

        /// <summary>
        /// Reads an uint
        /// </summary>
        /// <returns>the value</returns>
        public uint ReadUInt32()
        {
            return this.converter.ToUInt32(this.data, this.AddIndex(4));
        }

        /// <summary>
        /// Reads an int
        /// </summary>
        /// <returns>the value</returns>
        public int ReadInt32()
        {
            return this.converter.ToInt32(this.data, this.AddIndex(4));
        }

        /// <summary>
        /// Reads an ulong
        /// </summary>
        /// <returns>the value</returns>
        public ulong ReadUInt64()
        {
            return this.converter.ToUInt64(this.data, this.AddIndex(8));
        }

        /// <summary>
        /// Reads a long
        /// </summary>
        /// <returns>the value</returns>
        public long ReadInt64()
        {
            return this.converter.ToInt64(this.data, this.AddIndex(8));
        }

        /// <summary>
        /// Reads a float
        /// </summary>
        /// <returns>the value</returns>
        public float ReadSingle()
        {
            return this.converter.ToSingle(this.data, this.AddIndex(4));
        }

        /// <summary>
        /// Reads a double
        /// </summary>
        /// <returns>the value</returns>
        public double ReadDouble()
        {
            return this.converter.ToDouble(this.data, this.AddIndex(8));
        }

        /// <summary>
        /// Reads an ASCII encoded string
        /// </summary>
        /// <param name="length">number of bytes to read</param>
        /// <returns>The value as a string</returns>
        public string ReadAsciiString(int length)
        {
            string value = AsciiEncoding.GetString(this.data, this.AddIndex(length), length);

            // remove data after (potential) null terminator
            int pos = value.IndexOf('\0');
            if (pos >= 0)
            {
                value = value.Substring(0, pos);
            }

            return value;
        }

        /// <summary>
        /// Reads an UTF-16 big-endian encoded string
        /// </summary>
        /// <param name="length">number of bytes to read</param>
        /// <returns>The value as a string</returns>
        public string ReadUnicodeString(int length)
        {
            return Encoding.BigEndianUnicode.GetString(this.data, this.AddIndex(length), length);
        }

        /// <summary>
        /// Reads a signed 32bit number with 1 sign bit, 15 value bits and 16 fractional bits
        /// </summary>
        /// <returns>The number as double</returns>
        public float ReadFix16()
        {
            return this.ReadInt32() / 65536f;
        }

        /// <summary>
        /// Reads an unsigned 32bit number with 16 value bits and 16 fractional bits
        /// </summary>
        /// <returns>The number as double</returns>
        public float ReadUFix16()
        {
            return this.ReadUInt32() / 65536f;
        }

        /// <summary>
        /// Reads an unsigned 16bit number with 1 value bit and 15 fractional bits
        /// </summary>
        /// <returns>The number as double</returns>
        public float ReadU1Fix15()
        {
            return this.ReadUInt16() / 32768f;
        }

        /// <summary>
        /// Reads an unsigned 16bit number with 8 value bits and 8 fractional bits
        /// </summary>
        /// <returns>The number as double</returns>
        public float ReadUFix8()
        {
            return this.ReadUInt16() / 256f;
        }

        /// <summary>
        /// Reads a 16bit value ignoring endianness
        /// </summary>
        /// <returns>the value</returns>
        public short ReadDirect16()
        {
            return BitConverter.ToInt16(this.data, this.AddIndex(2));
        }

        /// <summary>
        /// Reads a 32bit value ignoring endianness
        /// </summary>
        /// <returns>the value</returns>
        public int ReadDirect32()
        {
            return BitConverter.ToInt32(this.data, this.AddIndex(4));
        }

        /// <summary>
        /// Reads a 64bit value ignoring endianness
        /// </summary>
        /// <returns>the value</returns>
        public long ReadDirect64()
        {
            return BitConverter.ToInt64(this.data, this.AddIndex(8));
        }

        /// <summary>
        /// Reads a number of bytes and advances the index
        /// </summary>
        /// <param name="count">The number of bytes to read</param>
        /// <returns>The read bytes</returns>
        public byte[] ReadBytes(int count)
        {
            byte[] bytes = new byte[count];
            Buffer.BlockCopy(this.data, this.AddIndex(count), bytes, 0, count);
            return bytes;
        }

        #endregion

        #region Read Non-Primitives

        /// <summary>
        /// Reads a DateTime
        /// </summary>
        /// <returns>the value</returns>
        public DateTime ReadDateTime()
        {
            try
            {
                return new DateTime(
                    year: this.ReadUInt16(),
                    month: this.ReadUInt16(),
                    day: this.ReadUInt16(),
                    hour: this.ReadUInt16(),
                    minute: this.ReadUInt16(),
                    second: this.ReadUInt16(),
                    kind: DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Reads an ICC profile version number
        /// </summary>
        /// <returns>the version number</returns>
        public Version ReadVersionNumber()
        {
            int version = this.ReadDirect32();

            int major = version >> 24;
            int minor = (version >> 20) & 0x0F;
            int bugfix = (version >> 16) & 0x0F;

            return new Version(major, minor, bugfix);
        }

        /// <summary>
        /// Reads an XYZ number
        /// </summary>
        /// <returns>the XYZ number</returns>
        public Vector3 ReadXyzNumber()
        {
            return new Vector3(
                x: this.ReadFix16(),
                y: this.ReadFix16(),
                z: this.ReadFix16());
        }

        /// <summary>
        /// Reads a profile ID
        /// </summary>
        /// <returns>the profile ID</returns>
        public IccProfileId ReadProfileId()
        {
            return new IccProfileId(
                p1: this.ReadUInt32(),
                p2: this.ReadUInt32(),
                p3: this.ReadUInt32(),
                p4: this.ReadUInt32());
        }

        /// <summary>
        /// Reads a position number
        /// </summary>
        /// <returns>the position number</returns>
        public IccPositionNumber ReadPositionNumber()
        {
            return new IccPositionNumber(
                offset: this.ReadUInt32(),
                size: this.ReadUInt32());
        }

        /// <summary>
        /// Reads a response number
        /// </summary>
        /// <returns>the response number</returns>
        public IccResponseNumber ReadResponseNumber()
        {
            return new IccResponseNumber(
                deviceCode: this.ReadUInt16(),
                measurementValue: this.ReadFix16());
        }

        /// <summary>
        /// Reads a named color
        /// </summary>
        /// <param name="deviceCoordCount">Number of device coordinates</param>
        /// <returns>the named color</returns>
        public IccNamedColor ReadNamedColor(uint deviceCoordCount)
        {
            string name = this.ReadAsciiString(32);
            ushort[] pcsCoord = new ushort[3] { this.ReadUInt16(), this.ReadUInt16(), this.ReadUInt16() };
            ushort[] deviceCoord = new ushort[deviceCoordCount];

            for (int i = 0; i < deviceCoordCount; i++)
            {
                deviceCoord[i] = this.ReadUInt16();
            }

            return new IccNamedColor(name, pcsCoord, deviceCoord);
        }

        /// <summary>
        /// Reads a profile description
        /// </summary>
        /// <returns>the profile description</returns>
        public IccProfileDescription ReadProfileDescription()
        {
            uint manufacturer = this.ReadUInt32();
            uint model = this.ReadUInt32();
            IccDeviceAttribute attributes = (IccDeviceAttribute)this.ReadDirect64();
            IccProfileTag technologyInfo = (IccProfileTag)this.ReadUInt32();
            this.ReadCheckTagDataEntryHeader(IccTypeSignature.MultiLocalizedUnicode);
            IccMultiLocalizedUnicodeTagDataEntry manufacturerInfo = this.ReadMultiLocalizedUnicodeTagDataEntry();
            this.ReadCheckTagDataEntryHeader(IccTypeSignature.MultiLocalizedUnicode);
            IccMultiLocalizedUnicodeTagDataEntry modelInfo = this.ReadMultiLocalizedUnicodeTagDataEntry();

            return new IccProfileDescription(
                manufacturer,
                model,
                attributes,
                technologyInfo,
                manufacturerInfo.Texts,
                modelInfo.Texts);
        }

        /// <summary>
        /// Reads a colorant table entry
        /// </summary>
        /// <returns>the profile description</returns>
        public IccColorantTableEntry ReadColorantTableEntry()
        {
            return new IccColorantTableEntry(
                name: this.ReadAsciiString(32),
                pcs1: this.ReadUInt16(),
                pcs2: this.ReadUInt16(),
                pcs3: this.ReadUInt16());
        }

        #endregion

        #region Read Tag Data Entries

        /// <summary>
        /// Reads a tag data entry
        /// </summary>
        /// <param name="info">The table entry with reading information</param>
        /// <returns>the tag data entry</returns>
        public IccTagDataEntry ReadTagDataEntry(IccTagTableEntry info)
        {
            this.index = (int)info.Offset;
            IccTypeSignature type = this.ReadTagDataEntryHeader();

            switch (type)
            {
                case IccTypeSignature.Chromaticity:
                    return this.ReadChromaticityTagDataEntry();
                case IccTypeSignature.ColorantOrder:
                    return this.ReadColorantOrderTagDataEntry();
                case IccTypeSignature.ColorantTable:
                    return this.ReadColorantTableTagDataEntry();
                case IccTypeSignature.Curve:
                    return this.ReadCurveTagDataEntry();
                case IccTypeSignature.Data:
                    return this.ReadDataTagDataEntry(info.DataSize);
                case IccTypeSignature.DateTime:
                    return this.ReadDateTimeTagDataEntry();
                case IccTypeSignature.Lut16:
                    return this.ReadLut16TagDataEntry();
                case IccTypeSignature.Lut8:
                    return this.ReadLut8TagDataEntry();
                case IccTypeSignature.LutAToB:
                    return this.ReadLutAToBTagDataEntry();
                case IccTypeSignature.LutBToA:
                    return this.ReadLutBToATagDataEntry();
                case IccTypeSignature.Measurement:
                    return this.ReadMeasurementTagDataEntry();
                case IccTypeSignature.MultiLocalizedUnicode:
                    return this.ReadMultiLocalizedUnicodeTagDataEntry();
                case IccTypeSignature.MultiProcessElements:
                    return this.ReadMultiProcessElementsTagDataEntry();
                case IccTypeSignature.NamedColor2:
                    return this.ReadNamedColor2TagDataEntry();
                case IccTypeSignature.ParametricCurve:
                    return this.ReadParametricCurveTagDataEntry();
                case IccTypeSignature.ProfileSequenceDesc:
                    return this.ReadProfileSequenceDescTagDataEntry();
                case IccTypeSignature.ProfileSequenceIdentifier:
                    return this.ReadProfileSequenceIdentifierTagDataEntry();
                case IccTypeSignature.ResponseCurveSet16:
                    return this.ReadResponseCurveSet16TagDataEntry();
                case IccTypeSignature.S15Fixed16Array:
                    return this.ReadFix16ArrayTagDataEntry(info.DataSize);
                case IccTypeSignature.Signature:
                    return this.ReadSignatureTagDataEntry();
                case IccTypeSignature.Text:
                    return this.ReadTextTagDataEntry(info.DataSize);
                case IccTypeSignature.U16Fixed16Array:
                    return this.ReadUFix16ArrayTagDataEntry(info.DataSize);
                case IccTypeSignature.UInt16Array:
                    return this.ReadUInt16ArrayTagDataEntry(info.DataSize);
                case IccTypeSignature.UInt32Array:
                    return this.ReadUInt32ArrayTagDataEntry(info.DataSize);
                case IccTypeSignature.UInt64Array:
                    return this.ReadUInt64ArrayTagDataEntry(info.DataSize);
                case IccTypeSignature.UInt8Array:
                    return this.ReadUInt8ArrayTagDataEntry(info.DataSize);
                case IccTypeSignature.ViewingConditions:
                    return this.ReadViewingConditionsTagDataEntry(info.DataSize);
                case IccTypeSignature.Xyz:
                    return this.ReadXyzTagDataEntry(info.DataSize);

                // V2 Type:
                case IccTypeSignature.TextDescription:
                    return this.ReadTextDescriptionTagDataEntry();

                case IccTypeSignature.Unknown:
                default:
                    return this.ReadUnknownTagDataEntry(info.DataSize);
            }
        }

        /// <summary>
        /// Reads the header of a <see cref="IccTagDataEntry"/>
        /// </summary>
        /// <returns>The read signature</returns>
        public IccTypeSignature ReadTagDataEntryHeader()
        {
            IccTypeSignature type = (IccTypeSignature)this.ReadUInt32();
            this.AddIndex(4); // 4 bytes are not used
            return type;
        }

        /// <summary>
        /// Reads the header of a <see cref="IccTagDataEntry"/> and checks if it's the expected value
        /// </summary>
        /// <param name="expected">expected value to check against</param>
        public void ReadCheckTagDataEntryHeader(IccTypeSignature expected)
        {
            IccTypeSignature type = this.ReadTagDataEntryHeader();
            if (expected != (IccTypeSignature)uint.MaxValue && type != expected)
            {
                throw new InvalidIccProfileException($"Read signature {type} is not the expected {expected}");
            }
        }

        /// <summary>
        /// Reads a <see cref="IccTagDataEntry"/> with an unknown <see cref="IccTypeSignature"/>
        /// </summary>
        /// <param name="size">The size of the entry in bytes</param>
        /// <returns>The read entry</returns>
        public IccUnknownTagDataEntry ReadUnknownTagDataEntry(uint size)
        {
            int count = (int)size - 8;  // 8 is the tag header size
            return new IccUnknownTagDataEntry(this.ReadBytes(count));
        }

        /// <summary>
        /// Reads a <see cref="IccChromaticityTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccChromaticityTagDataEntry ReadChromaticityTagDataEntry()
        {
            ushort channelCount = this.ReadUInt16();
            IccColorantEncoding colorant = (IccColorantEncoding)this.ReadUInt16();

            if (Enum.IsDefined(typeof(IccColorantEncoding), colorant) && colorant != IccColorantEncoding.Unknown)
            {
                // The type is known and so are the values (they are constant)
                // channelCount should always be 3 but it doesn't really matter if it's not
                return new IccChromaticityTagDataEntry(colorant);
            }
            else
            {
                // The type is not know, so the values need be read
                double[][] values = new double[channelCount][];
                for (int i = 0; i < channelCount; i++)
                {
                    values[i] = new double[2];
                    values[i][0] = this.ReadUFix16();
                    values[i][1] = this.ReadUFix16();
                }

                return new IccChromaticityTagDataEntry(values);
            }
        }

        /// <summary>
        /// Reads a <see cref="IccColorantOrderTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccColorantOrderTagDataEntry ReadColorantOrderTagDataEntry()
        {
            uint colorantCount = this.ReadUInt32();
            byte[] number = this.ReadBytes((int)colorantCount);
            return new IccColorantOrderTagDataEntry(number);
        }

        /// <summary>
        /// Reads a <see cref="IccColorantTableTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccColorantTableTagDataEntry ReadColorantTableTagDataEntry()
        {
            uint colorantCount = this.ReadUInt32();
            IccColorantTableEntry[] cdata = new IccColorantTableEntry[colorantCount];
            for (int i = 0; i < colorantCount; i++)
            {
                cdata[i] = this.ReadColorantTableEntry();
            }

            return new IccColorantTableTagDataEntry(cdata);
        }

        /// <summary>
        /// Reads a <see cref="IccCurveTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccCurveTagDataEntry ReadCurveTagDataEntry()
        {
            uint pointCount = this.ReadUInt32();

            if (pointCount == 0)
            {
                return new IccCurveTagDataEntry();
            }
            else if (pointCount == 1)
            {
                return new IccCurveTagDataEntry(this.ReadUFix8());
            }
            else
            {
                float[] cdata = new float[pointCount];
                for (int i = 0; i < pointCount; i++)
                {
                    cdata[i] = this.ReadUInt16() / 65535f;
                }

                return new IccCurveTagDataEntry(cdata);
            }

            // TODO: If the input is PCSXYZ, 1+(32 767/32 768) shall be mapped to the value 1,0. If the output is PCSXYZ, the value 1,0 shall be mapped to 1+(32 767/32 768).
        }

        /// <summary>
        /// Reads a <see cref="IccDataTagDataEntry"/>
        /// </summary>
        /// <param name="size">The size of the entry in bytes</param>
        /// <returns>The read entry</returns>
        public IccDataTagDataEntry ReadDataTagDataEntry(uint size)
        {
            this.AddIndex(3);   // first 3 bytes are zero
            byte b = this.data[this.AddIndex(1)];

            // last bit of 4th byte is either 0 = ASCII or 1 = binary
            bool ascii = this.GetBit(b, 7);
            int length = (int)size - 12;
            byte[] cdata = this.ReadBytes(length);

            return new IccDataTagDataEntry(cdata, ascii);
        }

        /// <summary>
        /// Reads a <see cref="IccDateTimeTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccDateTimeTagDataEntry ReadDateTimeTagDataEntry()
        {
            return new IccDateTimeTagDataEntry(this.ReadDateTime());
        }

        /// <summary>
        /// Reads a <see cref="IccLut16TagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccLut16TagDataEntry ReadLut16TagDataEntry()
        {
            byte inChCount = this.data[this.AddIndex(1)];
            byte outChCount = this.data[this.AddIndex(1)];
            byte clutPointCount = this.data[this.AddIndex(1)];
            this.AddIndex(1);   // 1 byte reserved

            float[,] matrix = this.ReadMatrix(3, 3, false);

            ushort inTableCount = this.ReadUInt16();
            ushort outTableCount = this.ReadUInt16();

            // Input LUT
            IccLut[] inValues = new IccLut[inChCount];
            byte[] gridPointCount = new byte[inChCount];
            for (int i = 0; i < inChCount; i++)
            {
                inValues[i] = this.ReadLUT16(inTableCount);
                gridPointCount[i] = clutPointCount;
            }

            // CLUT
            IccClut clut = this.ReadCLUT16(inChCount, outChCount, gridPointCount);

            // Output LUT
            IccLut[] outValues = new IccLut[outChCount];
            for (int i = 0; i < outChCount; i++)
            {
                outValues[i] = this.ReadLUT16(outTableCount);
            }

            return new IccLut16TagDataEntry(matrix, inValues, clut, outValues);
        }

        /// <summary>
        /// Reads a <see cref="IccLut8TagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccLut8TagDataEntry ReadLut8TagDataEntry()
        {
            byte inChCount = this.data[this.AddIndex(1)];
            byte outChCount = this.data[this.AddIndex(1)];
            byte clutPointCount = this.data[this.AddIndex(1)];
            this.AddIndex(1);   // 1 byte reserved

            float[,] matrix = this.ReadMatrix(3, 3, false);

            // Input LUT
            IccLut[] inValues = new IccLut[inChCount];
            byte[] gridPointCount = new byte[inChCount];
            for (int i = 0; i < inChCount; i++)
            {
                inValues[i] = this.ReadLUT8();
                gridPointCount[i] = clutPointCount;
            }

            // CLUT
            IccClut clut = this.ReadCLUT8(inChCount, outChCount, gridPointCount);

            // Output LUT
            IccLut[] outValues = new IccLut[outChCount];
            for (int i = 0; i < outChCount; i++)
            {
                outValues[i] = this.ReadLUT8();
            }

            return new IccLut8TagDataEntry(matrix, inValues, clut, outValues);
        }

        /// <summary>
        /// Reads a <see cref="IccLutAToBTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccLutAToBTagDataEntry ReadLutAToBTagDataEntry()
        {
            int start = this.index - 8; // 8 is the tag header size

            byte inChCount = this.data[this.AddIndex(1)];
            byte outChCount = this.data[this.AddIndex(1)];
            this.AddIndex(2);   // 2 bytes reserved

            uint bCurveOffset = this.ReadUInt32();
            uint matrixOffset = this.ReadUInt32();
            uint mCurveOffset = this.ReadUInt32();
            uint clutOffset = this.ReadUInt32();
            uint aCurveOffset = this.ReadUInt32();

            IccTagDataEntry[] bCurve = null;
            IccTagDataEntry[] mCurve = null;
            IccTagDataEntry[] aCurve = null;
            IccClut clut = null;
            float[,] matrix3x3 = null;
            float[] matrix3x1 = null;

            if (bCurveOffset != 0)
            {
                this.index = (int)bCurveOffset + start;
                bCurve = this.ReadCurves(outChCount);
            }

            if (mCurveOffset != 0)
            {
                this.index = (int)mCurveOffset + start;
                mCurve = this.ReadCurves(outChCount);
            }

            if (aCurveOffset != 0)
            {
                this.index = (int)aCurveOffset + start;
                aCurve = this.ReadCurves(inChCount);
            }

            if (clutOffset != 0)
            {
                this.index = (int)clutOffset + start;
                clut = this.ReadCLUT(inChCount, outChCount, false);
            }

            if (matrixOffset != 0)
            {
                this.index = (int)matrixOffset + start;
                matrix3x3 = this.ReadMatrix(3, 3, false);
                matrix3x1 = this.ReadMatrix(3, false);
            }

            return new IccLutAToBTagDataEntry(bCurve, matrix3x3, matrix3x1, mCurve, clut, aCurve);
        }

        /// <summary>
        /// Reads a <see cref="IccLutBToATagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccLutBToATagDataEntry ReadLutBToATagDataEntry()
        {
            int start = this.index - 8; // 8 is the tag header size

            byte inChCount = this.data[this.AddIndex(1)];
            byte outChCount = this.data[this.AddIndex(1)];
            this.AddIndex(2);   // 2 bytes reserved

            uint bCurveOffset = this.ReadUInt32();
            uint matrixOffset = this.ReadUInt32();
            uint mCurveOffset = this.ReadUInt32();
            uint clutOffset = this.ReadUInt32();
            uint aCurveOffset = this.ReadUInt32();

            IccTagDataEntry[] bCurve = null;
            IccTagDataEntry[] mCurve = null;
            IccTagDataEntry[] aCurve = null;
            IccClut clut = null;
            float[,] matrix3x3 = null;
            float[] matrix3x1 = null;

            if (bCurveOffset != 0)
            {
                this.index = (int)bCurveOffset + start;
                bCurve = this.ReadCurves(inChCount);
            }

            if (mCurveOffset != 0)
            {
                this.index = (int)mCurveOffset + start;
                mCurve = this.ReadCurves(inChCount);
            }

            if (aCurveOffset != 0)
            {
                this.index = (int)aCurveOffset + start;
                aCurve = this.ReadCurves(outChCount);
            }

            if (clutOffset != 0)
            {
                this.index = (int)clutOffset + start;
                clut = this.ReadCLUT(inChCount, outChCount, false);
            }

            if (matrixOffset != 0)
            {
                this.index = (int)matrixOffset + start;
                matrix3x3 = this.ReadMatrix(3, 3, false);
                matrix3x1 = this.ReadMatrix(3, false);
            }

            return new IccLutBToATagDataEntry(bCurve, matrix3x3, matrix3x1, mCurve, clut, aCurve);
        }

        /// <summary>
        /// Reads a <see cref="IccMeasurementTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccMeasurementTagDataEntry ReadMeasurementTagDataEntry()
        {
            return new IccMeasurementTagDataEntry(
                observer: (IccStandardObserver)this.ReadUInt32(),
                xyzBacking: this.ReadXyzNumber(),
                geometry: (IccMeasurementGeometry)this.ReadUInt32(),
                flare: this.ReadUFix16(),
                illuminant: (IccStandardIlluminant)this.ReadUInt32());
        }

        /// <summary>
        /// Reads a <see cref="IccMultiLocalizedUnicodeTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccMultiLocalizedUnicodeTagDataEntry ReadMultiLocalizedUnicodeTagDataEntry()
        {
            int start = this.index - 8; // 8 is the tag header size
            uint recordCount = this.ReadUInt32();
            uint recordSize = this.ReadUInt32();
            IccLocalizedString[] text = new IccLocalizedString[recordCount];

            string[] culture = new string[recordCount];
            uint[] length = new uint[recordCount];
            uint[] offset = new uint[recordCount];

            for (int i = 0; i < recordCount; i++)
            {
                culture[i] = $"{this.ReadAsciiString(2)}-{this.ReadAsciiString(2)}";
                length[i] = this.ReadUInt32();
                offset[i] = this.ReadUInt32();
            }

            for (int i = 0; i < recordCount; i++)
            {
                this.index = (int)(start + offset[i]);
                text[i] = new IccLocalizedString(new CultureInfo(culture[i]), this.ReadUnicodeString((int)length[i]));
            }

            return new IccMultiLocalizedUnicodeTagDataEntry(text);
        }

        /// <summary>
        /// Reads a <see cref="IccMultiProcessElementsTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccMultiProcessElementsTagDataEntry ReadMultiProcessElementsTagDataEntry()
        {
            int start = this.index - 8;

            ushort inChannelCount = this.ReadUInt16();
            ushort outChannelCount = this.ReadUInt16();
            uint elementCount = this.ReadUInt32();

            IccPositionNumber[] positionTable = new IccPositionNumber[elementCount];
            for (int i = 0; i < elementCount; i++)
            {
                positionTable[i] = this.ReadPositionNumber();
            }

            IccMultiProcessElement[] elements = new IccMultiProcessElement[elementCount];
            for (int i = 0; i < elementCount; i++)
            {
                this.index = (int)positionTable[i].Offset + start;
                elements[i] = this.ReadMultiProcessElement();
            }

            return new IccMultiProcessElementsTagDataEntry(elements);
        }

        /// <summary>
        /// Reads a <see cref="IccNamedColor2TagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccNamedColor2TagDataEntry ReadNamedColor2TagDataEntry()
        {
            int vendorFlag = this.ReadDirect32();
            uint colorCount = this.ReadUInt32();
            uint coordCount = this.ReadUInt32();
            string prefix = this.ReadAsciiString(32);
            string suffix = this.ReadAsciiString(32);

            IccNamedColor[] colors = new IccNamedColor[colorCount];
            for (int i = 0; i < colorCount; i++)
            {
                colors[i] = this.ReadNamedColor(coordCount);
            }

            return new IccNamedColor2TagDataEntry(vendorFlag, prefix, suffix, colors);
        }

        /// <summary>
        /// Reads a <see cref="IccParametricCurveTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccParametricCurveTagDataEntry ReadParametricCurveTagDataEntry()
        {
            return new IccParametricCurveTagDataEntry(this.ReadParametricCurve());
        }

        /// <summary>
        /// Reads a <see cref="IccProfileSequenceDescTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccProfileSequenceDescTagDataEntry ReadProfileSequenceDescTagDataEntry()
        {
            uint count = this.ReadUInt32();
            IccProfileDescription[] description = new IccProfileDescription[count];
            for (int i = 0; i < count; i++)
            {
                description[i] = this.ReadProfileDescription();
            }

            return new IccProfileSequenceDescTagDataEntry(description);
        }

        /// <summary>
        /// Reads a <see cref="IccProfileSequenceIdentifierTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccProfileSequenceIdentifierTagDataEntry ReadProfileSequenceIdentifierTagDataEntry()
        {
            int start = this.index - 8; // 8 is the tag header size
            uint count = this.ReadUInt32();
            IccPositionNumber[] table = new IccPositionNumber[count];
            for (int i = 0; i < count; i++)
            {
                table[i] = this.ReadPositionNumber();
            }

            IccProfileSequenceIdentifier[] entries = new IccProfileSequenceIdentifier[count];
            for (int i = 0; i < count; i++)
            {
                this.index = (int)(start + table[i].Offset);
                IccProfileId id = this.ReadProfileId();
                this.ReadCheckTagDataEntryHeader(IccTypeSignature.MultiLocalizedUnicode);
                IccMultiLocalizedUnicodeTagDataEntry description = this.ReadMultiLocalizedUnicodeTagDataEntry();
                entries[i] = new IccProfileSequenceIdentifier(id, description.Texts);
            }

            return new IccProfileSequenceIdentifierTagDataEntry(entries);
        }

        /// <summary>
        /// Reads a <see cref="IccResponseCurveSet16TagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccResponseCurveSet16TagDataEntry ReadResponseCurveSet16TagDataEntry()
        {
            int start = this.index - 8; // 8 is the tag header size
            ushort channelCount = this.ReadUInt16();
            ushort measurmentCount = this.ReadUInt16();

            uint[] offset = new uint[measurmentCount];
            for (int i = 0; i < measurmentCount; i++)
            {
                offset[i] = this.ReadUInt32();
            }

            IccResponseCurve[] curves = new IccResponseCurve[measurmentCount];
            for (int i = 0; i < measurmentCount; i++)
            {
                this.index = (int)(start + offset[i]);
                curves[i] = this.ReadResponseCurve(channelCount);
            }

            return new IccResponseCurveSet16TagDataEntry(curves);
        }

        /// <summary>
        /// Reads a <see cref="IccFix16ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="size">The size of the entry in bytes</param>
        /// <returns>The read entry</returns>
        public IccFix16ArrayTagDataEntry ReadFix16ArrayTagDataEntry(uint size)
        {
            uint count = (size - 8) / 4;
            float[] arrayData = new float[count];
            for (int i = 0; i < count; i++)
            {
                arrayData[i] = this.ReadFix16() / 256f;
            }

            return new IccFix16ArrayTagDataEntry(arrayData);
        }

        /// <summary>
        /// Reads a <see cref="IccSignatureTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccSignatureTagDataEntry ReadSignatureTagDataEntry()
        {
            return new IccSignatureTagDataEntry(this.ReadAsciiString(4));
        }

        /// <summary>
        /// Reads a <see cref="IccTextTagDataEntry"/>
        /// </summary>
        /// <param name="size">The size of the entry in bytes</param>
        /// <returns>The read entry</returns>
        public IccTextTagDataEntry ReadTextTagDataEntry(uint size)
        {
            return new IccTextTagDataEntry(this.ReadAsciiString((int)size - 8)); // 8 is the tag header size
        }

        /// <summary>
        /// Reads a <see cref="IccUFix16ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="size">The size of the entry in bytes</param>
        /// <returns>The read entry</returns>
        public IccUFix16ArrayTagDataEntry ReadUFix16ArrayTagDataEntry(uint size)
        {
            uint count = (size - 8) / 4;
            float[] arrayData = new float[count];
            for (int i = 0; i < count; i++)
            {
                arrayData[i] = this.ReadUFix16();
            }

            return new IccUFix16ArrayTagDataEntry(arrayData);
        }

        /// <summary>
        /// Reads a <see cref="IccUInt16ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="size">The size of the entry in bytes</param>
        /// <returns>The read entry</returns>
        public IccUInt16ArrayTagDataEntry ReadUInt16ArrayTagDataEntry(uint size)
        {
            uint count = (size - 8) / 2;
            ushort[] arrayData = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                arrayData[i] = this.ReadUInt16();
            }

            return new IccUInt16ArrayTagDataEntry(arrayData);
        }

        /// <summary>
        /// Reads a <see cref="IccUInt32ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="size">The size of the entry in bytes</param>
        /// <returns>The read entry</returns>
        public IccUInt32ArrayTagDataEntry ReadUInt32ArrayTagDataEntry(uint size)
        {
            uint count = (size - 8) / 4;
            uint[] arrayData = new uint[count];
            for (int i = 0; i < count; i++)
            {
                arrayData[i] = this.ReadUInt32();
            }

            return new IccUInt32ArrayTagDataEntry(arrayData);
        }

        /// <summary>
        /// Reads a <see cref="IccUInt64ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="size">The size of the entry in bytes</param>
        /// <returns>The read entry</returns>
        public IccUInt64ArrayTagDataEntry ReadUInt64ArrayTagDataEntry(uint size)
        {
            uint count = (size - 8) / 8;
            ulong[] arrayData = new ulong[count];
            for (int i = 0; i < count; i++)
            {
                arrayData[i] = this.ReadUInt64();
            }

            return new IccUInt64ArrayTagDataEntry(arrayData);
        }

        /// <summary>
        /// Reads a <see cref="IccUInt8ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="size">The size of the entry in bytes</param>
        /// <returns>The read entry</returns>
        public IccUInt8ArrayTagDataEntry ReadUInt8ArrayTagDataEntry(uint size)
        {
            int count = (int)size - 8; // 8 is the tag header size
            byte[] adata = this.ReadBytes(count);

            return new IccUInt8ArrayTagDataEntry(adata);
        }

        /// <summary>
        /// Reads a <see cref="IccViewingConditionsTagDataEntry"/>
        /// </summary>
        /// <param name="size">The size of the entry in bytes</param>
        /// <returns>The read entry</returns>
        public IccViewingConditionsTagDataEntry ReadViewingConditionsTagDataEntry(uint size)
        {
            return new IccViewingConditionsTagDataEntry(
                illuminantXyz: this.ReadXyzNumber(),
                surroundXyz: this.ReadXyzNumber(),
                illuminant: (IccStandardIlluminant)this.ReadUInt32());
        }

        /// <summary>
        /// Reads a <see cref="IccXyzTagDataEntry"/>
        /// </summary>
        /// <param name="size">The size of the entry in bytes</param>
        /// <returns>The read entry</returns>
        public IccXyzTagDataEntry ReadXyzTagDataEntry(uint size)
        {
            uint count = (size - 8) / 12;
            Vector3[] arrayData = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                arrayData[i] = this.ReadXyzNumber();
            }

            return new IccXyzTagDataEntry(arrayData);
        }

        /// <summary>
        /// Reads a <see cref="IccTextDescriptionTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccTextDescriptionTagDataEntry ReadTextDescriptionTagDataEntry()
        {
            string asciiValue, unicodeValue, scriptcodeValue;
            asciiValue = unicodeValue = scriptcodeValue = null;

            int asciiCount = (int)this.ReadUInt32();
            if (asciiCount > 0)
            {
                asciiValue = this.ReadAsciiString(asciiCount - 1);
                this.AddIndex(1);  // Null terminator
            }

            uint unicodeLangCode = this.ReadUInt32();
            int unicodeCount = (int)this.ReadUInt32();
            if (unicodeCount > 0)
            {
                unicodeValue = this.ReadUnicodeString((unicodeCount * 2) - 2);
                this.AddIndex(2);  // Null terminator
            }

            ushort scriptcodeCode = this.ReadUInt16();
            int scriptcodeCount = Math.Min(this.data[this.AddIndex(1)], (byte)67);
            if (scriptcodeCount > 0)
            {
                scriptcodeValue = this.ReadAsciiString(scriptcodeCount - 1);
                this.AddIndex(1);  // Null terminator
            }

            return new IccTextDescriptionTagDataEntry(
                asciiValue,
                unicodeValue,
                scriptcodeValue,
                unicodeLangCode,
                scriptcodeCode);
        }

        #endregion

        #region Read Matrix

        /// <summary>
        /// Reads a two dimensional matrix
        /// </summary>
        /// <param name="xCount">Number of values in X</param>
        /// <param name="yCount">Number of values in Y</param>
        /// <param name="isSingle">True if the values are encoded as Single; false if encoded as Fix16</param>
        /// <returns>The read matrix</returns>
        public float[,] ReadMatrix(int xCount, int yCount, bool isSingle)
        {
            float[,] matrix = new float[xCount, yCount];
            for (int y = 0; y < yCount; y++)
            {
                for (int x = 0; x < xCount; x++)
                {
                    if (isSingle)
                    {
                        matrix[x, y] = this.ReadSingle();
                    }
                    else
                    {
                        matrix[x, y] = this.ReadFix16();
                    }
                }
            }

            return matrix;
        }

        /// <summary>
        /// Reads a one dimensional matrix
        /// </summary>
        /// <param name="yCount">Number of values</param>
        /// <param name="isSingle">True if the values are encoded as Single; false if encoded as Fix16</param>
        /// <returns>The read matrix</returns>
        public float[] ReadMatrix(int yCount, bool isSingle)
        {
            float[] matrix = new float[yCount];
            for (int i = 0; i < yCount; i++)
            {
                if (isSingle)
                {
                    matrix[i] = this.ReadSingle();
                }
                else
                {
                    matrix[i] = this.ReadFix16();
                }
            }

            return matrix;
        }

        #endregion

        #region Read (C)LUT

        /// <summary>
        /// Reads an 8bit lookup table
        /// </summary>
        /// <returns>The read LUT</returns>
        public IccLut ReadLUT8()
        {
            return new IccLut(this.ReadBytes(256));
        }

        /// <summary>
        /// Reads a 16bit lookup table
        /// </summary>
        /// <param name="count">The number of entries</param>
        /// <returns>The read LUT</returns>
        public IccLut ReadLUT16(int count)
        {
            ushort[] values = new ushort[count];
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
        public IccClut ReadCLUT(int inChannelCount, int outChannelCount, bool isFloat)
        {
            // Grid-points are always 16 bytes long but only 0-inChCount are used
            byte[] gridPointCount = new byte[inChannelCount];
            Buffer.BlockCopy(this.data, this.AddIndex(16), gridPointCount, 0, inChannelCount);

            if (!isFloat)
            {
                byte size = this.data[this.AddIndex(4)];   // First byte is info, last 3 bytes are reserved
                if (size == 1)
                {
                    return this.ReadCLUT8(inChannelCount, outChannelCount, gridPointCount);
                }
                else if (size == 2)
                {
                    return this.ReadCLUT16(inChannelCount, outChannelCount, gridPointCount);
                }
                else
                {
                    throw new InvalidIccProfileException($"Invalid CLUT size of {size}");
                }
            }
            else
            {
                return this.ReadCLUTf32(inChannelCount, outChannelCount, gridPointCount);
            }
        }

        /// <summary>
        /// Reads an 8 bit CLUT
        /// </summary>
        /// <param name="inChannelCount">Input channel count</param>
        /// <param name="outChannelCount">Output channel count</param>
        /// <param name="gridPointCount">Grid point count for each CLUT channel</param>
        /// <returns>The read CLUT8</returns>
        public IccClut ReadCLUT8(int inChannelCount, int outChannelCount, byte[] gridPointCount)
        {
            int start = this.index;
            int length = 0;
            for (int i = 0; i < inChannelCount; i++)
            {
                length += (int)Math.Pow(gridPointCount[i], inChannelCount);
            }

            length /= inChannelCount;

            const float max = byte.MaxValue;

            float[][] values = new float[length][];
            for (int i = 0; i < length; i++)
            {
                values[i] = new float[outChannelCount];
                for (int j = 0; j < outChannelCount; j++)
                {
                    values[i][j] = this.data[this.index++] / max;
                }
            }

            this.index = start + (length * outChannelCount);
            return new IccClut(values, gridPointCount, IccClutDataType.UInt8);
        }

        /// <summary>
        /// Reads a 16 bit CLUT
        /// </summary>
        /// <param name="inChannelCount">Input channel count</param>
        /// <param name="outChannelCount">Output channel count</param>
        /// <param name="gridPointCount">Grid point count for each CLUT channel</param>
        /// <returns>The read CLUT16</returns>
        public IccClut ReadCLUT16(int inChannelCount, int outChannelCount, byte[] gridPointCount)
        {
            int start = this.index;
            int length = 0;
            for (int i = 0; i < inChannelCount; i++)
            {
                length += (int)Math.Pow(gridPointCount[i], inChannelCount);
            }

            length /= inChannelCount;

            const float max = ushort.MaxValue;

            float[][] values = new float[length][];
            for (int i = 0; i < length; i++)
            {
                values[i] = new float[outChannelCount];
                for (int j = 0; j < outChannelCount; j++)
                {
                    values[i][j] = this.ReadUInt16() / max;
                }
            }

            this.index = start + (length * outChannelCount * 2);
            return new IccClut(values, gridPointCount, IccClutDataType.UInt16);
        }

        /// <summary>
        /// Reads a 32bit floating point CLUT
        /// </summary>
        /// <param name="inChCount">Input channel count</param>
        /// <param name="outChCount">Output channel count</param>
        /// <param name="gridPointCount">Grid point count for each CLUT channel</param>
        /// <returns>The read CLUTf32</returns>
        public IccClut ReadCLUTf32(int inChCount, int outChCount, byte[] gridPointCount)
        {
            int start = this.index;
            int length = 0;
            for (int i = 0; i < inChCount; i++)
            {
                length += (int)Math.Pow(gridPointCount[i], inChCount);
            }

            length /= inChCount;

            float[][] values = new float[length][];
            for (int i = 0; i < length; i++)
            {
                values[i] = new float[outChCount];
                for (int j = 0; j < outChCount; j++)
                {
                    values[i][j] = this.ReadSingle();
                }
            }

            this.index = start + (length * outChCount * 4);
            return new IccClut(values, gridPointCount, IccClutDataType.Float);
        }

        #endregion

        #region Read MultiProcessElement

        /// <summary>
        /// Reads a <see cref="IccMultiProcessElement"/>
        /// </summary>
        /// <returns>The read <see cref="IccMultiProcessElement"/></returns>
        public IccMultiProcessElement ReadMultiProcessElement()
        {
            IccMultiProcessElementSignature signature = (IccMultiProcessElementSignature)this.ReadUInt32();
            ushort inChannelCount = this.ReadUInt16();
            ushort outChannelCount = this.ReadUInt16();

            switch (signature)
            {
                case IccMultiProcessElementSignature.CurveSet:
                    return this.ReadCurveSetProcessElement(inChannelCount, outChannelCount);
                case IccMultiProcessElementSignature.Matrix:
                    return this.ReadMatrixProcessElement(inChannelCount, outChannelCount);
                case IccMultiProcessElementSignature.Clut:
                    return this.ReadCLUTProcessElement(inChannelCount, outChannelCount);

                // Currently just placeholders for future ICC expansion
                case IccMultiProcessElementSignature.BAcs:
                    this.AddIndex(8);
                    return new IccBAcsProcessElement(inChannelCount, outChannelCount);
                case IccMultiProcessElementSignature.EAcs:
                    this.AddIndex(8);
                    return new IccEAcsProcessElement(inChannelCount, outChannelCount);

                default:
                    throw new InvalidIccProfileException($"Invalid MultiProcessElement type of {signature}");
            }
        }

        /// <summary>
        /// Reads a CurveSet <see cref="IccMultiProcessElement"/>
        /// </summary>
        /// <param name="inChannelCount">Number of input channels</param>
        /// <param name="outChannelCount">Number of output channels</param>
        /// <returns>The read <see cref="IccCurveSetProcessElement"/></returns>
        public IccCurveSetProcessElement ReadCurveSetProcessElement(int inChannelCount, int outChannelCount)
        {
            IccOneDimensionalCurve[] curves = new IccOneDimensionalCurve[inChannelCount];
            for (int i = 0; i < inChannelCount; i++)
            {
                curves[i] = this.ReadOneDimensionalCurve();
                this.AddPadding();
            }

            return new IccCurveSetProcessElement(curves);
        }

        /// <summary>
        /// Reads a Matrix <see cref="IccMultiProcessElement"/>
        /// </summary>
        /// <param name="inChannelCount">Number of input channels</param>
        /// <param name="outChannelCount">Number of output channels</param>
        /// <returns>The read <see cref="IccMatrixProcessElement"/></returns>
        public IccMatrixProcessElement ReadMatrixProcessElement(int inChannelCount, int outChannelCount)
        {
            return new IccMatrixProcessElement(
                this.ReadMatrix(inChannelCount, outChannelCount, true),
                this.ReadMatrix(outChannelCount, true));
        }

        /// <summary>
        /// Reads a CLUT <see cref="IccMultiProcessElement"/>
        /// </summary>
        /// <param name="inChCount">Number of input channels</param>
        /// <param name="outChCount">Number of output channels</param>
        /// <returns>The read <see cref="IccClutProcessElement"/></returns>
        public IccClutProcessElement ReadCLUTProcessElement(int inChCount, int outChCount)
        {
            return new IccClutProcessElement(this.ReadCLUT(inChCount, outChCount, true));
        }

        #endregion

        #region Read Curves

        /// <summary>
        /// Reads a <see cref="IccOneDimensionalCurve"/>
        /// </summary>
        /// <returns>The read curve</returns>
        public IccOneDimensionalCurve ReadOneDimensionalCurve()
        {
            ushort segmentCount = this.ReadUInt16();
            this.AddIndex(2);   // 2 bytes reserved
            float[] breakPoints = new float[segmentCount - 1];
            for (int i = 0; i < breakPoints.Length; i++)
            {
                breakPoints[i] = this.ReadSingle();
            }

            IccCurveSegment[] segments = new IccCurveSegment[segmentCount];
            for (int i = 0; i < segmentCount; i++)
            {
                segments[i] = this.ReadCurveSegment();
            }

            return new IccOneDimensionalCurve(breakPoints, segments);
        }

        /// <summary>
        /// Reads a <see cref="IccResponseCurve"/>
        /// </summary>
        /// <param name="channelCount">The number of channels</param>
        /// <returns>The read curve</returns>
        public IccResponseCurve ReadResponseCurve(int channelCount)
        {
            IccCurveMeasurementEncodings type = (IccCurveMeasurementEncodings)this.ReadUInt32();
            uint[] measurment = new uint[channelCount];
            for (int i = 0; i < channelCount; i++)
            {
                measurment[i] = this.ReadUInt32();
            }

            Vector3[] xyzValues = new Vector3[channelCount];
            for (int i = 0; i < channelCount; i++)
            {
                xyzValues[i] = this.ReadXyzNumber();
            }

            IccResponseNumber[][] response = new IccResponseNumber[channelCount][];
            for (int i = 0; i < channelCount; i++)
            {
                response[i] = new IccResponseNumber[measurment[i]];
                for (uint j = 0; j < measurment[i]; j++)
                {
                    response[i][j] = this.ReadResponseNumber();
                }
            }

            return new IccResponseCurve(type, xyzValues, response);
        }

        /// <summary>
        /// Reads a <see cref="IccParametricCurve"/>
        /// </summary>
        /// <returns>The read curve</returns>
        public IccParametricCurve ReadParametricCurve()
        {
            ushort type = this.ReadUInt16();
            this.AddIndex(2);   // 2 bytes reserved
            double gamma, a, b, c, d, e, f;
            gamma = a = b = c = d = e = f = 0;

            if (type >= 0 && type <= 4)
            {
                gamma = this.ReadFix16();
            }

            if (type > 0 && type <= 4)
            {
                a = this.ReadFix16();
                b = this.ReadFix16();
            }

            if (type > 1 && type <= 4)
            {
                c = this.ReadFix16();
            }

            if (type > 2 && type <= 4)
            {
                d = this.ReadFix16();
            }

            if (type == 4)
            {
                e = this.ReadFix16();
                f = this.ReadFix16();
            }

            switch (type)
            {
                case 0: return new IccParametricCurve(gamma);
                case 1: return new IccParametricCurve(gamma, a, b);
                case 2: return new IccParametricCurve(gamma, a, b, c);
                case 3: return new IccParametricCurve(gamma, a, b, c, d);
                case 4: return new IccParametricCurve(gamma, a, b, c, d, e, f);
                default: throw new InvalidIccProfileException($"Invalid parametric curve type of {type}");
            }
        }

        /// <summary>
        /// Reads a <see cref="IccCurveSegment"/>
        /// </summary>
        /// <returns>The read segment</returns>
        public IccCurveSegment ReadCurveSegment()
        {
            IccCurveSegmentSignature signature = (IccCurveSegmentSignature)this.ReadUInt32();
            this.AddIndex(4);   // 4 bytes reserved

            switch (signature)
            {
                case IccCurveSegmentSignature.FormulaCurve:
                    return this.ReadFormulaCurveElement();
                case IccCurveSegmentSignature.SampledCurve:
                    return this.ReadSampledCurveElement();
                default:
                    throw new InvalidIccProfileException($"Invalid curve segment type of {signature}");
            }
        }

        /// <summary>
        /// Reads a <see cref="IccFormulaCurveElement"/>
        /// </summary>
        /// <returns>The read segment</returns>
        public IccFormulaCurveElement ReadFormulaCurveElement()
        {
            ushort type = this.ReadUInt16();
            this.AddIndex(2);   // 2 bytes reserved
            double gamma, a, b, c, d, e;
            gamma = a = b = c = d = e = 0;

            if (type == 0 || type == 1)
            {
                gamma = this.ReadSingle();
            }

            if (type >= 0 && type <= 2)
            {
                a = this.ReadSingle();
                b = this.ReadSingle();
                c = this.ReadSingle();
            }

            if (type == 1 || type == 2)
            {
                d = this.ReadSingle();
            }

            if (type == 2)
            {
                e = this.ReadSingle();
            }

            return new IccFormulaCurveElement(type, gamma, a, b, c, d, e);
        }

        /// <summary>
        /// Reads a <see cref="IccSampledCurveElement"/>
        /// </summary>
        /// <returns>The read segment</returns>
        public IccSampledCurveElement ReadSampledCurveElement()
        {
            uint count = this.ReadUInt32();
            float[] entries = new float[count];
            for (int i = 0; i < count; i++)
            {
                entries[i] = this.ReadSingle();
            }

            return new IccSampledCurveElement(entries);
        }

        /// <summary>
        /// Reads curve data
        /// </summary>
        /// <param name="count">Number of input channels</param>
        /// <returns>The curve data</returns>
        private IccTagDataEntry[] ReadCurves(int count)
        {
            IccTagDataEntry[] tdata = new IccTagDataEntry[count];
            for (int i = 0; i < count; i++)
            {
                IccTypeSignature type = this.ReadTagDataEntryHeader();
                if (type != IccTypeSignature.Curve && type != IccTypeSignature.ParametricCurve)
                {
                    throw new InvalidIccProfileException($"Curve has to be either \"{nameof(IccTypeSignature)}.{nameof(IccTypeSignature.Curve)}\" or" +
                        $" \"{nameof(IccTypeSignature)}.{nameof(IccTypeSignature.ParametricCurve)}\" for LutAToB- and LutBToA-TagDataEntries");
                }

                if (type == IccTypeSignature.Curve)
                {
                    tdata[i] = this.ReadCurveTagDataEntry();
                }
                else if (type == IccTypeSignature.ParametricCurve)
                {
                    tdata[i] = this.ReadParametricCurveTagDataEntry();
                }

                this.AddPadding();
            }

            return tdata;
        }

        #endregion

        #region Subroutines

        /// <summary>
        /// Returns the current <see cref="index"/> without increment and adds the given increment
        /// </summary>
        /// <param name="increment">The value to increment <see cref="index"/></param>
        /// <returns>The current <see cref="index"/> without the increment</returns>
        private int AddIndex(int increment)
        {
            int tmp = this.index;
            this.index += increment;
            return tmp;
        }

        /// <summary>
        /// Calculates the 4 byte padding and adds it to the <see cref="index"/> variable
        /// </summary>
        private void AddPadding()
        {
            this.index += this.CalcPadding();
        }

        /// <summary>
        /// Calculates the 4 byte padding
        /// </summary>
        /// <returns>the number of bytes to pad</returns>
        private int CalcPadding()
        {
            int p = 4 - (this.index % 4);
            return p >= 4 ? 0 : p;
        }

        /// <summary>
        /// Gets the bit value at a specified position
        /// </summary>
        /// <param name="value">The value from where the bit will be extracted</param>
        /// <param name="position">Position of the bit. Zero based index from left to right.</param>
        /// <returns>The bit value at specified position</returns>
        private bool GetBit(byte value, int position)
        {
            return ((value >> (7 - position)) & 1) == 1;
        }

        /// <summary>
        /// Gets the bit value at a specified position
        /// </summary>
        /// <param name="value">The value from where the bit will be extracted</param>
        /// <param name="position">Position of the bit. Zero based index from left to right.</param>
        /// <returns>The bit value at specified position</returns>
        private bool GetBit(ushort value, int position)
        {
            return ((value >> (15 - position)) & 1) == 1;
        }

        #endregion
    }
}
