// <copyright file="IccDataWriter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.IO;
    using System.Numerics;
    using System.Text;

    /// <summary>
    /// Provides methods to write ICC data types
    /// </summary>
    internal sealed class IccDataWriter
    {
        private static readonly bool IsLittleEndian = BitConverter.IsLittleEndian;

        private static readonly double[,] IdentityMatrix = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };

        /// <summary>
        /// The underlying stream where the data is written to
        /// </summary>
        private readonly MemoryStream dataStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="IccDataWriter"/> class.
        /// </summary>
        public IccDataWriter()
        {
            this.dataStream = new MemoryStream();
        }

        /// <summary>
        /// Gets the currently written length in bytes
        /// </summary>
        public uint Length
        {
            get { return (uint)this.dataStream.Length; }
        }

        /// <summary>
        /// Gets the written data bytes
        /// </summary>
        /// <returns>The written data</returns>
        public byte[] GetData()
        {
            return this.dataStream.ToArray();
        }

        /// <summary>
        /// Sets the writing position to the given value
        /// </summary>
        /// <param name="index">The new index position</param>
        public void SetIndex(int index)
        {
            this.dataStream.Position = index;
        }

        /// <summary>
        /// Writes a byte
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public int WriteByte(byte value)
        {
            this.dataStream.WriteByte(value);
            return 1;
        }

        /// <summary>
        /// Writes an ushort
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public unsafe int WriteUInt16(ushort value)
        {
            return this.WriteBytes((byte*)&value, 2);
        }

        /// <summary>
        /// Writes a short
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public unsafe int WriteInt16(short value)
        {
            return this.WriteBytes((byte*)&value, 2);
        }

        /// <summary>
        /// Writes an uint
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public unsafe int WriteUInt32(uint value)
        {
            return this.WriteBytes((byte*)&value, 4);
        }

        /// <summary>
        /// Writes an int
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public unsafe int WriteInt32(int value)
        {
            return this.WriteBytes((byte*)&value, 4);
        }

        /// <summary>
        /// Writes an ulong
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public unsafe int WriteUInt64(ulong value)
        {
            return this.WriteBytes((byte*)&value, 8);
        }

        /// <summary>
        /// Writes a long
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public unsafe int WriteInt64(long value)
        {
            return this.WriteBytes((byte*)&value, 8);
        }

        /// <summary>
        /// Writes a float
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public unsafe int WriteSingle(float value)
        {
            return this.WriteBytes((byte*)&value, 4);
        }

        /// <summary>
        /// Writes a double
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public unsafe int WriteDouble(double value)
        {
            return this.WriteBytes((byte*)&value, 8);
        }

        /// <summary>
        /// Writes a signed 32bit number with 1 sign bit, 15 value bits and 16 fractional bits
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public int WriteFix16(double value)
        {
            const double max = short.MaxValue + (65535d / 65536d);
            const double min = short.MinValue;

            value = value.Clamp(min, max);
            value *= 65536d;

            return this.WriteInt32((int)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Writes an unsigned 32bit number with 16 value bits and 16 fractional bits
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public int WriteUFix16(double value)
        {
            const double max = ushort.MaxValue + (65535d / 65536d);
            const double min = ushort.MinValue;

            value = value.Clamp(min, max);
            value *= 65536d;

            return this.WriteUInt32((uint)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Writes an unsigned 16bit number with 1 value bit and 15 fractional bits
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public int WriteU1Fix15(double value)
        {
            const double max = 1 + (32767d / 32768d);
            const double min = 0;

            value = value.Clamp(min, max);
            value *= 32768d;

            return this.WriteUInt16((ushort)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Writes an unsigned 16bit number with 8 value bits and 8 fractional bits
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public int WriteUFix8(double value)
        {
            const double max = byte.MaxValue + (255d / 256d);
            const double min = byte.MinValue;

            value = value.Clamp(min, max);
            value *= 256d;

            return this.WriteUInt16((ushort)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Writes an ASCII encoded string
        /// </summary>
        /// <param name="value">the string to write</param>
        /// <returns>the number of bytes written</returns>
        public int WriteASCIIString(string value)
        {
            // Encoding.ASCII is missing in netstandard1.1, use UTF8 instead because it's compatible with ASCII
            byte[] data = Encoding.UTF8.GetBytes(value);
            this.dataStream.Write(data, 0, data.Length);
            return data.Length;
        }

        /// <summary>
        /// Writes an ASCII encoded string resizes it to the given length
        /// </summary>
        /// <param name="value">The string to write</param>
        /// <param name="length">The desired length of the string including 1 padding character</param>
        /// <param name="paddingChar">The character to pad to the given length</param>
        /// <returns>the number of bytes written</returns>
        public int WriteASCIIString(string value, int length, char paddingChar)
        {
            value = value.Substring(0, Math.Min(length - 1, value.Length));

            // Encoding.ASCII is missing in netstandard1.1, use UTF8 instead because it's compatible with ASCII
            byte[] textData = Encoding.UTF8.GetBytes(value);
            int actualLength = Math.Min(length - 1, textData.Length);
            this.dataStream.Write(textData, 0, actualLength);
            for (int i = 0; i < length - actualLength; i++)
            {
                this.dataStream.WriteByte((byte)paddingChar);
            }

            return length;
        }

        /// <summary>
        /// Writes an UTF-16 big-endian encoded string
        /// </summary>
        /// <param name="value">the string to write</param>
        /// <returns>the number of bytes written</returns>
        public int WriteUnicodeString(string value)
        {
            byte[] data = Encoding.BigEndianUnicode.GetBytes(value);
            this.dataStream.Write(data, 0, data.Length);
            return data.Length;
        }

        /// <summary>
        /// Writes a short ignoring endianness
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public unsafe int WriteDirect16(short value)
        {
            return this.WriteBytesDirect((byte*)&value, 2);
        }

        /// <summary>
        /// Writes an int ignoring endianness
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public unsafe int WriteDirect32(int value)
        {
            return this.WriteBytesDirect((byte*)&value, 4);
        }

        /// <summary>
        /// Writes a long ignoring endianness
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public unsafe int WriteDirect64(long value)
        {
            return this.WriteBytesDirect((byte*)&value, 8);
        }

        /// <summary>
        /// Writes a DateTime
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public int WriteDateTime(DateTime value)
        {
            return this.WriteUInt16((ushort)value.Year)
                 + this.WriteUInt16((ushort)value.Month)
                 + this.WriteUInt16((ushort)value.Day)
                 + this.WriteUInt16((ushort)value.Hour)
                 + this.WriteUInt16((ushort)value.Minute)
                 + this.WriteUInt16((ushort)value.Second);
        }

        /// <summary>
        /// Writes an ICC profile version number
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public int WriteVersionNumber(Version value)
        {
            int major = value.Major.Clamp(0, byte.MaxValue);
            int minor = value.Minor.Clamp(0, 15);
            int bugfix = value.Build.Clamp(0, 15);
            byte mb = (byte)((minor << 4) | bugfix);

            int version = (major << 24) | (minor << 20) | (bugfix << 16);
            return this.WriteInt32(version);
        }

        /// <summary>
        /// Writes an XYZ number
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public int WriteXYZNumber(Vector3 value)
        {
            return this.WriteFix16(value.X)
                 + this.WriteFix16(value.Y)
                 + this.WriteFix16(value.Z);
        }

        /// <summary>
        /// Writes a profile ID
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public int WriteProfileId(IccProfileId value)
        {
            return this.WriteUInt32(value.Part1)
                 + this.WriteUInt32(value.Part2)
                 + this.WriteUInt32(value.Part3)
                 + this.WriteUInt32(value.Part4);
        }

        /// <summary>
        /// Writes a position number
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public int WritePositionNumber(IccPositionNumber value)
        {
            return this.WriteUInt32(value.Offset)
                 + this.WriteUInt32(value.Size);
        }

        /// <summary>
        /// Writes a response number
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public int WriteResponseNumber(IccResponseNumber value)
        {
            return this.WriteUInt16(value.DeviceCode)
                 + this.WriteFix16(value.MeasurementValue);
        }

        /// <summary>
        /// Writes a named color
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public int WriteNamedColor(IccNamedColor value)
        {
            return this.WriteASCIIString(value.Name, 32, '\0')
                 + this.WriteArray(value.PcsCoordinates)
                 + this.WriteArray(value.DeviceCoordinates);
        }

        /// <summary>
        /// Writes a profile description
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <returns>the number of bytes written</returns>
        public int WriteProfileDescription(IccProfileDescription value)
        {
            return this.WriteUInt32(value.DeviceManufacturer)
                 + this.WriteUInt32(value.DeviceModel)
                 + this.WriteDirect64((long)value.DeviceAttributes)
                 + this.WriteUInt32((uint)value.TechnologyInformation)
                 + this.WriteTagDataEntryHeader(IccTypeSignature.MultiLocalizedUnicode)
                 + this.WriteMultiLocalizedUnicodeTagDataEntry(new IccMultiLocalizedUnicodeTagDataEntry(value.DeviceManufacturerInfo))
                 + this.WriteTagDataEntryHeader(IccTypeSignature.MultiLocalizedUnicode)
                 + this.WriteMultiLocalizedUnicodeTagDataEntry(new IccMultiLocalizedUnicodeTagDataEntry(value.DeviceModelInfo));
        }

        /// <summary>
        /// Writes a tag data entry
        /// </summary>
        /// <param name="data">The entry to write</param>
        /// <param name="table">The table entry for the written data entry</param>
        /// <returns>The number of bytes written (excluding padding)</returns>
        public int WriteTagDataEntry(IccTagDataEntry data, out IccTagTableEntry table)
        {
            uint offset = (uint)this.dataStream.Position;
            int count = this.WriteTagDataEntry(data);
            this.WritePadding();
            table = new IccTagTableEntry(data.TagSignature, offset, (uint)count);
            return count;
        }

        /// <summary>
        /// Writes a tag data entry (without padding)
        /// </summary>
        /// <param name="entry">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteTagDataEntry(IccTagDataEntry entry)
        {
            int count = this.WriteTagDataEntryHeader(entry.Signature);

            switch (entry.Signature)
            {
                case IccTypeSignature.Chromaticity:
                    count += this.WriteChromaticityTagDataEntry(entry as IccChromaticityTagDataEntry);
                    break;
                case IccTypeSignature.ColorantOrder:
                    count += this.WriteColorantOrderTagDataEntry(entry as IccColorantOrderTagDataEntry);
                    break;
                case IccTypeSignature.ColorantTable:
                    count += this.WriteColorantTableTagDataEntry(entry as IccColorantTableTagDataEntry);
                    break;
                case IccTypeSignature.Curve:
                    count += this.WriteCurveTagDataEntry(entry as IccCurveTagDataEntry);
                    break;
                case IccTypeSignature.Data:
                    count += this.WriteDataTagDataEntry(entry as IccDataTagDataEntry);
                    break;
                case IccTypeSignature.DateTime:
                    count += this.WriteDateTimeTagDataEntry(entry as IccDateTimeTagDataEntry);
                    break;
                case IccTypeSignature.Lut16:
                    count += this.WriteLut16TagDataEntry(entry as IccLut16TagDataEntry);
                    break;
                case IccTypeSignature.Lut8:
                    count += this.WriteLut8TagDataEntry(entry as IccLut8TagDataEntry);
                    break;
                case IccTypeSignature.LutAToB:
                    count += this.WriteLutAToBTagDataEntry(entry as IccLutAToBTagDataEntry);
                    break;
                case IccTypeSignature.LutBToA:
                    count += this.WriteLutBToATagDataEntry(entry as IccLutBToATagDataEntry);
                    break;
                case IccTypeSignature.Measurement:
                    count += this.WriteMeasurementTagDataEntry(entry as IccMeasurementTagDataEntry);
                    break;
                case IccTypeSignature.MultiLocalizedUnicode:
                    count += this.WriteMultiLocalizedUnicodeTagDataEntry(entry as IccMultiLocalizedUnicodeTagDataEntry);
                    break;
                case IccTypeSignature.MultiProcessElements:
                    count += this.WriteMultiProcessElementsTagDataEntry(entry as IccMultiProcessElementsTagDataEntry);
                    break;
                case IccTypeSignature.NamedColor2:
                    count += this.WriteNamedColor2TagDataEntry(entry as IccNamedColor2TagDataEntry);
                    break;
                case IccTypeSignature.ParametricCurve:
                    count += this.WriteParametricCurveTagDataEntry(entry as IccParametricCurveTagDataEntry);
                    break;
                case IccTypeSignature.ProfileSequenceDesc:
                    count += this.WriteProfileSequenceDescTagDataEntry(entry as IccProfileSequenceDescTagDataEntry);
                    break;
                case IccTypeSignature.ProfileSequenceIdentifier:
                    count += this.WriteProfileSequenceIdentifierTagDataEntry(entry as IccProfileSequenceIdentifierTagDataEntry);
                    break;
                case IccTypeSignature.ResponseCurveSet16:
                    count += this.WriteResponseCurveSet16TagDataEntry(entry as IccResponseCurveSet16TagDataEntry);
                    break;
                case IccTypeSignature.S15Fixed16Array:
                    count += this.WriteFix16ArrayTagDataEntry(entry as IccFix16ArrayTagDataEntry);
                    break;
                case IccTypeSignature.Signature:
                    count += this.WriteSignatureTagDataEntry(entry as IccSignatureTagDataEntry);
                    break;
                case IccTypeSignature.Text:
                    count += this.WriteTextTagDataEntry(entry as IccTextTagDataEntry);
                    break;
                case IccTypeSignature.U16Fixed16Array:
                    count += this.WriteUFix16ArrayTagDataEntry(entry as IccUFix16ArrayTagDataEntry);
                    break;
                case IccTypeSignature.UInt16Array:
                    count += this.WriteUInt16ArrayTagDataEntry(entry as IccUInt16ArrayTagDataEntry);
                    break;
                case IccTypeSignature.UInt32Array:
                    count += this.WriteUInt32ArrayTagDataEntry(entry as IccUInt32ArrayTagDataEntry);
                    break;
                case IccTypeSignature.UInt64Array:
                    count += this.WriteUInt64ArrayTagDataEntry(entry as IccUInt64ArrayTagDataEntry);
                    break;
                case IccTypeSignature.UInt8Array:
                    count += this.WriteUInt8ArrayTagDataEntry(entry as IccUInt8ArrayTagDataEntry);
                    break;
                case IccTypeSignature.ViewingConditions:
                    count += this.WriteViewingConditionsTagDataEntry(entry as IccViewingConditionsTagDataEntry);
                    break;
                case IccTypeSignature.Xyz:
                    count += this.WriteXyzTagDataEntry(entry as IccXyzTagDataEntry);
                    break;

                // V2 Type:
                case IccTypeSignature.TextDescription:
                    count += this.WriteTextDescriptionTagDataEntry(entry as IccTextDescriptionTagDataEntry);
                    break;

                case IccTypeSignature.Unknown:
                default:
                    count += this.WriteUnknownTagDataEntry(entry as IccUnknownTagDataEntry);
                    break;
            }

            return count;
        }

        /// <summary>
        /// Writes the header of a <see cref="IccTagDataEntry"/>
        /// </summary>
        /// <param name="signature">The signature of the entry</param>
        /// <returns>The number of bytes written</returns>
        public int WriteTagDataEntryHeader(IccTypeSignature signature)
        {
            return this.WriteUInt32((uint)signature)
                 + this.WriteEmpty(4);
        }

        /// <summary>
        /// Writes a <see cref="IccUnknownTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteUnknownTagDataEntry(IccUnknownTagDataEntry value)
        {
            return this.WriteArray(value.Data);
        }

        /// <summary>
        /// Writes a <see cref="IccChromaticityTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteChromaticityTagDataEntry(IccChromaticityTagDataEntry value)
        {
            int count = this.WriteUInt16((ushort)value.ChannelCount);
            count += this.WriteUInt16((ushort)value.ColorantType);

            for (int i = 0; i < value.ChannelCount; i++)
            {
                count += this.WriteUFix16(value.ChannelValues[i][0]);
                count += this.WriteUFix16(value.ChannelValues[i][1]);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccColorantOrderTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteColorantOrderTagDataEntry(IccColorantOrderTagDataEntry value)
        {
            return this.WriteUInt32((uint)value.ColorantNumber.Length)
                 + this.WriteArray(value.ColorantNumber);
        }

        /// <summary>
        /// Writes a <see cref="IccColorantTableTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteColorantTableTagDataEntry(IccColorantTableTagDataEntry value)
        {
            int count = this.WriteUInt32((uint)value.ColorantData.Length);
            foreach (IccColorantTableEntry colorant in value.ColorantData)
            {
                count += this.WriteASCIIString(colorant.Name, 32, '\0');
                count += this.WriteUInt16(colorant.Pcs1);
                count += this.WriteUInt16(colorant.Pcs2);
                count += this.WriteUInt16(colorant.Pcs3);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccCurveTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteCurveTagDataEntry(IccCurveTagDataEntry value)
        {
            int count = 0;

            if (value.IsIdentityResponse)
            {
                count += this.WriteUInt32(0);
            }
            else if (value.IsGamma)
            {
                count += this.WriteUInt32(1);
                count += this.WriteUFix8(value.Gamma);
            }
            else
            {
                count += this.WriteUInt32((uint)value.CurveData.Length);
                for (int i = 0; i < value.CurveData.Length; i++)
                {
                    count += this.WriteUInt16((ushort)((value.CurveData[i] * ushort.MaxValue) + 0.5f).Clamp(0, ushort.MaxValue));
                }
            }

            return count;

            // TODO: Page 48: If the input is PCSXYZ, 1+(32 767/32 768) shall be mapped to the value 1,0. If the output is PCSXYZ, the value 1,0 shall be mapped to 1+(32 767/32 768).
        }

        /// <summary>
        /// Writes a <see cref="IccDataTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteDataTagDataEntry(IccDataTagDataEntry value)
        {
            return this.WriteEmpty(3)
                 + this.WriteByte((byte)(value.IsAscii ? 0x01 : 0x00))
                 + this.WriteArray(value.Data);
        }

        /// <summary>
        /// Writes a <see cref="IccDateTimeTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteDateTimeTagDataEntry(IccDateTimeTagDataEntry value)
        {
            return this.WriteDateTime(value.Value);
        }

        /// <summary>
        /// Writes a <see cref="IccLut16TagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteLut16TagDataEntry(IccLut16TagDataEntry value)
        {
            int count = this.WriteByte((byte)value.InputValues.Length);
            count += this.WriteByte((byte)value.OutputValues.Length);
            count += this.WriteByte(value.ClutValues.GridPointCount[0]);
            count += this.WriteEmpty(1);

            count += this.WriteMatrix(value.Matrix, false);

            count += this.WriteUInt16((ushort)value.InputValues[0].Values.Length);
            count += this.WriteUInt16((ushort)value.OutputValues[0].Values.Length);

            foreach (IccLut lut in value.InputValues)
            {
                count += this.WriteLUT16(lut);
            }

            count += this.WriteCLUT16(value.ClutValues);

            foreach (IccLut lut in value.OutputValues)
            {
                count += this.WriteLUT16(lut);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccLut8TagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteLut8TagDataEntry(IccLut8TagDataEntry value)
        {
            int count = this.WriteByte((byte)value.InputChannelCount);
            count += this.WriteByte((byte)value.OutputChannelCount);
            count += this.WriteByte((byte)value.ClutValues.Values[0].Length);
            count += this.WriteEmpty(1);

            count += this.WriteMatrix(value.Matrix, false);

            foreach (IccLut lut in value.InputValues)
            {
                count += this.WriteLUT8(lut);
            }

            count += this.WriteCLUT8(value.ClutValues);

            foreach (IccLut lut in value.OutputValues)
            {
                count += this.WriteLUT8(lut);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccLutAToBTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteLutAToBTagDataEntry(IccLutAToBTagDataEntry value)
        {
            long start = this.dataStream.Position - 8;  // 8 is the tag header size

            int count = this.WriteByte((byte)value.InputChannelCount);
            count += this.WriteByte((byte)value.OutputChannelCount);
            count += this.WriteEmpty(2);

            long bCurveOffset = 0;
            long matrixOffset = 0;
            long mCurveOffset = 0;
            long clutOffset = 0;
            long aCurveOffset = 0;

            // Jump over offset values
            long offsetpos = this.dataStream.Position;
            this.dataStream.Position += 5 * 4;

            if (value.CurveB != null)
            {
                bCurveOffset = this.dataStream.Position;
                count += this.WriteCurves(value.CurveB);
                count += this.WritePadding();
            }

            if (value.Matrix3x1 != null && value.Matrix3x3 != null)
            {
                matrixOffset = this.dataStream.Position;
                count += this.WriteMatrix(value.Matrix3x3.Value, false);
                count += this.WriteMatrix(value.Matrix3x1.Value, false);
                count += this.WritePadding();
            }

            if (value.CurveM != null)
            {
                mCurveOffset = this.dataStream.Position;
                count += this.WriteCurves(value.CurveM);
                count += this.WritePadding();
            }

            if (value.ClutValues != null)
            {
                clutOffset = this.dataStream.Position;
                count += this.WriteCLUT(value.ClutValues);
                count += this.WritePadding();
            }

            if (value.CurveA != null)
            {
                aCurveOffset = this.dataStream.Position;
                count += this.WriteCurves(value.CurveA);
                count += this.WritePadding();
            }

            // Set offset values
            long lpos = this.dataStream.Position;
            this.dataStream.Position = offsetpos;

            if (bCurveOffset != 0)
            {
                bCurveOffset -= start;
            }

            if (matrixOffset != 0)
            {
                matrixOffset -= start;
            }

            if (mCurveOffset != 0)
            {
                mCurveOffset -= start;
            }

            if (clutOffset != 0)
            {
                clutOffset -= start;
            }

            if (aCurveOffset != 0)
            {
                aCurveOffset -= start;
            }

            count += this.WriteUInt32((uint)bCurveOffset);
            count += this.WriteUInt32((uint)matrixOffset);
            count += this.WriteUInt32((uint)mCurveOffset);
            count += this.WriteUInt32((uint)clutOffset);
            count += this.WriteUInt32((uint)aCurveOffset);

            this.dataStream.Position = lpos;
            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccLutBToATagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteLutBToATagDataEntry(IccLutBToATagDataEntry value)
        {
            long start = this.dataStream.Position - 8;  // 8 is the tag header size

            int count = this.WriteByte((byte)value.InputChannelCount);
            count += this.WriteByte((byte)value.OutputChannelCount);
            count += this.WriteEmpty(2);

            long bCurveOffset = 0;
            long matrixOffset = 0;
            long mCurveOffset = 0;
            long clutOffset = 0;
            long aCurveOffset = 0;

            // Jump over offset values
            long offsetpos = this.dataStream.Position;
            this.dataStream.Position += 5 * 4;

            if (value.CurveB != null)
            {
                bCurveOffset = this.dataStream.Position;
                count += this.WriteCurves(value.CurveB);
                count += this.WritePadding();
            }

            if (value.Matrix3x1 != null && value.Matrix3x3 != null)
            {
                matrixOffset = this.dataStream.Position;
                count += this.WriteMatrix(value.Matrix3x3.Value, false);
                count += this.WriteMatrix(value.Matrix3x1.Value, false);
                count += this.WritePadding();
            }

            if (value.CurveM != null)
            {
                mCurveOffset = this.dataStream.Position;
                count += this.WriteCurves(value.CurveM);
                count += this.WritePadding();
            }

            if (value.ClutValues != null)
            {
                clutOffset = this.dataStream.Position;
                count += this.WriteCLUT(value.ClutValues);
                count += this.WritePadding();
            }

            if (value.CurveA != null)
            {
                aCurveOffset = this.dataStream.Position;
                count += this.WriteCurves(value.CurveA);
                count += this.WritePadding();
            }

            // Set offset values
            long lpos = this.dataStream.Position;
            this.dataStream.Position = offsetpos;

            if (bCurveOffset != 0)
            {
                bCurveOffset -= start;
            }

            if (matrixOffset != 0)
            {
                matrixOffset -= start;
            }

            if (mCurveOffset != 0)
            {
                mCurveOffset -= start;
            }

            if (clutOffset != 0)
            {
                clutOffset -= start;
            }

            if (aCurveOffset != 0)
            {
                aCurveOffset -= start;
            }

            count += this.WriteUInt32((uint)bCurveOffset);
            count += this.WriteUInt32((uint)matrixOffset);
            count += this.WriteUInt32((uint)mCurveOffset);
            count += this.WriteUInt32((uint)clutOffset);
            count += this.WriteUInt32((uint)aCurveOffset);

            this.dataStream.Position = lpos;
            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccMeasurementTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMeasurementTagDataEntry(IccMeasurementTagDataEntry value)
        {
            return this.WriteUInt32((uint)value.Observer)
                 + this.WriteXYZNumber(value.XyzBacking)
                 + this.WriteUInt32((uint)value.Geometry)
                 + this.WriteUFix16(value.Flare)
                 + this.WriteUInt32((uint)value.Illuminant);
        }

        /// <summary>
        /// Writes a <see cref="IccMultiLocalizedUnicodeTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMultiLocalizedUnicodeTagDataEntry(IccMultiLocalizedUnicodeTagDataEntry value)
        {
            long start = this.dataStream.Position - 8;  // 8 is the tag header size

            int cultureCount = value.Texts.Length;

            int count = this.WriteUInt32((uint)cultureCount);
            count += this.WriteUInt32(12);  // One record has always 12 bytes size

            // Jump over position table
            long tpos = this.dataStream.Position;
            this.dataStream.Position += cultureCount * 12;

            uint[] offset = new uint[cultureCount];
            int[] lengths = new int[cultureCount];

            for (int i = 0; i < cultureCount; i++)
            {
                offset[i] = (uint)(this.dataStream.Position - start);
                count += lengths[i] = this.WriteUnicodeString(value.Texts[i].Text);
            }

            // Write position table
            long lpos = this.dataStream.Position;
            this.dataStream.Position = tpos;
            for (int i = 0; i < cultureCount; i++)
            {
                string[] code = value.Texts[i].Culture.Name.Split('-');

                count += this.WriteASCIIString(code[0].ToLower(), 2, ' ');
                count += this.WriteASCIIString(code[1].ToUpper(), 2, ' ');

                count += this.WriteUInt32((uint)lengths[i]);
                count += this.WriteUInt32(offset[i]);
            }

            this.dataStream.Position = lpos;
            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccMultiProcessElementsTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMultiProcessElementsTagDataEntry(IccMultiProcessElementsTagDataEntry value)
        {
            long start = this.dataStream.Position - 8;  // 8 is the tag header size

            int count = this.WriteUInt16((ushort)value.InputChannelCount);
            count += this.WriteUInt16((ushort)value.OutputChannelCount);
            count += this.WriteUInt32((uint)value.Data.Length);

            // Jump over position table
            long tpos = this.dataStream.Position;
            this.dataStream.Position += value.Data.Length * 8;

            IccPositionNumber[] posTable = new IccPositionNumber[value.Data.Length];
            for (int i = 0; i < value.Data.Length; i++)
            {
                uint offset = (uint)(this.dataStream.Position - start);
                int size = this.WriteMultiProcessElement(value.Data[i]);
                count += this.WritePadding();
                posTable[i] = new IccPositionNumber(offset, (uint)size);
                count += size;
            }

            // Write position table
            long lpos = this.dataStream.Position;
            this.dataStream.Position = tpos;
            foreach (IccPositionNumber pos in posTable)
            {
                count += this.WritePositionNumber(pos);
            }

            this.dataStream.Position = lpos;
            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccNamedColor2TagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteNamedColor2TagDataEntry(IccNamedColor2TagDataEntry value)
        {
            int count = this.WriteDirect32(value.VendorFlags)
                      + this.WriteUInt32((uint)value.Colors.Length)
                      + this.WriteUInt32((uint)value.CoordinateCount)
                      + this.WriteASCIIString(value.Prefix, 32, '\0')
                      + this.WriteASCIIString(value.Suffix, 32, '\0');

            foreach (IccNamedColor color in value.Colors)
            {
                count += this.WriteNamedColor(color);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccParametricCurveTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteParametricCurveTagDataEntry(IccParametricCurveTagDataEntry value)
        {
            return this.WriteParametricCurve(value.Curve);
        }

        /// <summary>
        /// Writes a <see cref="IccProfileSequenceDescTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteProfileSequenceDescTagDataEntry(IccProfileSequenceDescTagDataEntry value)
        {
            int count = this.WriteUInt32((uint)value.Descriptions.Length);
            foreach (IccProfileDescription desc in value.Descriptions)
            {
                count += this.WriteProfileDescription(desc);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccProfileSequenceIdentifierTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteProfileSequenceIdentifierTagDataEntry(IccProfileSequenceIdentifierTagDataEntry value)
        {
            long start = this.dataStream.Position - 8;  // 8 is the tag header size
            int length = value.Data.Length;

            int count = this.WriteUInt32((uint)length);

            // Jump over position table
            long tablePosition = this.dataStream.Position;
            this.dataStream.Position += length * 8;
            IccPositionNumber[] table = new IccPositionNumber[length];

            for (int i = 0; i < length; i++)
            {
                uint offset = (uint)(this.dataStream.Position - start);
                int size = this.WriteProfileId(value.Data[i].Id);
                size += this.WriteTagDataEntry(new IccMultiLocalizedUnicodeTagDataEntry(value.Data[i].Description));
                size += this.WritePadding();
                table[i] = new IccPositionNumber(offset, (uint)size);
                count += size;
            }

            // Write position table
            long lpos = this.dataStream.Position;
            this.dataStream.Position = tablePosition;
            foreach (IccPositionNumber pos in table)
            {
                count += this.WritePositionNumber(pos);
            }

            this.dataStream.Position = lpos;
            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccResponseCurveSet16TagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteResponseCurveSet16TagDataEntry(IccResponseCurveSet16TagDataEntry value)
        {
            long start = this.dataStream.Position - 8;

            int count = this.WriteUInt16(value.ChannelCount);
            count += this.WriteUInt16((ushort)value.Curves.Length);

            // Jump over position table
            long tablePosition = this.dataStream.Position;
            this.dataStream.Position += value.Curves.Length * 4;

            uint[] offset = new uint[value.Curves.Length];

            for (int i = 0; i < value.Curves.Length; i++)
            {
                offset[i] = (uint)(this.dataStream.Position - start);
                count += this.WriteResponseCurve(value.Curves[i]);
                count += this.WritePadding();
            }

            // Write position table
            long lpos = this.dataStream.Position;
            this.dataStream.Position = tablePosition;
            count += this.WriteArray(offset);

            this.dataStream.Position = lpos;
            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccFix16ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteFix16ArrayTagDataEntry(IccFix16ArrayTagDataEntry value)
        {
            int count = 0;
            for (int i = 0; i < value.Data.Length; i++)
            {
                count += this.WriteFix16(value.Data[i] * 256d);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccSignatureTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteSignatureTagDataEntry(IccSignatureTagDataEntry value)
        {
            return this.WriteASCIIString(value.SignatureData, 4, ' ');
        }

        /// <summary>
        /// Writes a <see cref="IccTextTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteTextTagDataEntry(IccTextTagDataEntry value)
        {
            return this.WriteASCIIString(value.Text);
        }

        /// <summary>
        /// Writes a <see cref="IccUFix16ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteUFix16ArrayTagDataEntry(IccUFix16ArrayTagDataEntry value)
        {
            int count = 0;
            for (int i = 0; i < value.Data.Length; i++)
            {
                count += this.WriteUFix16(value.Data[i]);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccUInt16ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteUInt16ArrayTagDataEntry(IccUInt16ArrayTagDataEntry value)
        {
            return this.WriteArray(value.Data);
        }

        /// <summary>
        /// Writes a <see cref="IccUInt32ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteUInt32ArrayTagDataEntry(IccUInt32ArrayTagDataEntry value)
        {
            return this.WriteArray(value.Data);
        }

        /// <summary>
        /// Writes a <see cref="IccUInt64ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteUInt64ArrayTagDataEntry(IccUInt64ArrayTagDataEntry value)
        {
            return this.WriteArray(value.Data);
        }

        /// <summary>
        /// Writes a <see cref="IccUInt8ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteUInt8ArrayTagDataEntry(IccUInt8ArrayTagDataEntry value)
        {
            return this.WriteArray(value.Data);
        }

        /// <summary>
        /// Writes a <see cref="IccViewingConditionsTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteViewingConditionsTagDataEntry(IccViewingConditionsTagDataEntry value)
        {
            return this.WriteXYZNumber(value.IlluminantXyz)
                 + this.WriteXYZNumber(value.SurroundXyz)
                 + this.WriteUInt32((uint)value.Illuminant);
        }

        /// <summary>
        /// Writes a <see cref="IccXyzTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteXyzTagDataEntry(IccXyzTagDataEntry value)
        {
            int count = 0;
            for (int i = 0; i < value.Data.Length; i++)
            {
                count += this.WriteXYZNumber(value.Data[i]);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccTextDescriptionTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteTextDescriptionTagDataEntry(IccTextDescriptionTagDataEntry value)
        {
            int size, count = 0;

            if (value.Ascii == null)
            {
                count += this.WriteUInt32(0);
            }
            else
            {
                this.dataStream.Position += 4;
                count += size = this.WriteASCIIString(value.Ascii + '\0');
                this.dataStream.Position -= size + 4;
                count += this.WriteUInt32((uint)size);
                this.dataStream.Position += size;
            }

            if (value.Unicode == null)
            {
                count += this.WriteUInt32(0);
                count += this.WriteUInt32(0);
            }
            else
            {
                this.dataStream.Position += 8;
                count += size = this.WriteUnicodeString(value.Unicode + '\0');
                this.dataStream.Position -= size + 8;
                count += this.WriteUInt32(value.UnicodeLanguageCode);
                count += this.WriteUInt32((uint)value.Unicode.Length + 1);
                this.dataStream.Position += size;
            }

            if (value.ScriptCode == null)
            {
                count += this.WriteUInt16(0);
                count += this.WriteByte(0);
                count += this.WriteEmpty(67);
            }
            else
            {
                this.dataStream.Position += 3;
                count += size = this.WriteASCIIString(value.ScriptCode, 67, '\0');
                this.dataStream.Position -= size + 3;
                count += this.WriteUInt16(value.ScriptCodeCode);
                count += this.WriteByte((byte)(value.ScriptCode.Length > 66 ? 67 : value.ScriptCode.Length));
                this.dataStream.Position += size;
            }

            return count;
        }

        /// <summary>
        /// Writes a two dimensional matrix
        /// </summary>
        /// <param name="value">The matrix to write</param>
        /// <param name="isSingle">True if the values are encoded as Single; false if encoded as Fix16</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMatrix(Matrix4x4 value, bool isSingle)
        {
            int count = 0;

            if (isSingle)
            {
                count += this.WriteSingle(value.M11);
                count += this.WriteSingle(value.M21);
                count += this.WriteSingle(value.M31);

                count += this.WriteSingle(value.M12);
                count += this.WriteSingle(value.M22);
                count += this.WriteSingle(value.M32);

                count += this.WriteSingle(value.M13);
                count += this.WriteSingle(value.M23);
                count += this.WriteSingle(value.M33);
            }
            else
            {
                count += this.WriteFix16(value.M11);
                count += this.WriteFix16(value.M21);
                count += this.WriteFix16(value.M31);

                count += this.WriteFix16(value.M12);
                count += this.WriteFix16(value.M22);
                count += this.WriteFix16(value.M32);

                count += this.WriteFix16(value.M13);
                count += this.WriteFix16(value.M23);
                count += this.WriteFix16(value.M33);
            }

            return count;
        }

        /// <summary>
        /// Writes a two dimensional matrix
        /// </summary>
        /// <param name="value">The matrix to write</param>
        /// <param name="isSingle">True if the values are encoded as Single; false if encoded as Fix16</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMatrix(Fast2DArray<float> value, bool isSingle)
        {
            int count = 0;
            for (int y = 0; y < value.Height; y++)
            {
                for (int x = 0; x < value.Width; x++)
                {
                    if (isSingle)
                    {
                        count += this.WriteSingle(value[x, y]);
                    }
                    else
                    {
                        count += this.WriteFix16(value[x, y]);
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Writes a two dimensional matrix
        /// </summary>
        /// <param name="value">The matrix to write</param>
        /// <param name="isSingle">True if the values are encoded as Single; false if encoded as Fix16</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMatrix(float[,] value, bool isSingle)
        {
            int count = 0;
            for (int y = 0; y < value.GetLength(1); y++)
            {
                for (int x = 0; x < value.GetLength(0); x++)
                {
                    if (isSingle)
                    {
                        count += this.WriteSingle(value[x, y]);
                    }
                    else
                    {
                        count += this.WriteFix16(value[x, y]);
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Writes a one dimensional matrix
        /// </summary>
        /// <param name="value">The matrix to write</param>
        /// <param name="isSingle">True if the values are encoded as Single; false if encoded as Fix16</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMatrix(Vector3 value, bool isSingle)
        {
            int count = 0;
            if (isSingle)
            {
                count += this.WriteSingle(value.X);
                count += this.WriteSingle(value.X);
                count += this.WriteSingle(value.X);
            }
            else
            {
                count += this.WriteFix16(value.X);
                count += this.WriteFix16(value.X);
                count += this.WriteFix16(value.X);
            }

            return count;
        }

        /// <summary>
        /// Writes a one dimensional matrix
        /// </summary>
        /// <param name="value">The matrix to write</param>
        /// <param name="isSingle">True if the values are encoded as Single; false if encoded as Fix16</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMatrix(float[] value, bool isSingle)
        {
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (isSingle)
                {
                    count += this.WriteSingle(value[i]);
                }
                else
                {
                    count += this.WriteFix16(value[i]);
                }
            }

            return count;
        }

        /// <summary>
        /// Writes an 8bit lookup table
        /// </summary>
        /// <param name="value">The LUT to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteLUT8(IccLut value)
        {
            foreach (double item in value.Values)
            {
                this.WriteByte((byte)((item * byte.MaxValue) + 0.5f).Clamp(0, byte.MaxValue));
            }

            return value.Values.Length;
        }

        /// <summary>
        /// Writes an 16bit lookup table
        /// </summary>
        /// <param name="value">The LUT to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteLUT16(IccLut value)
        {
            foreach (double item in value.Values)
            {
                this.WriteUInt16((ushort)((item * ushort.MaxValue) + 0.5f).Clamp(0, ushort.MaxValue));
            }

            return value.Values.Length * 2;
        }

        /// <summary>
        /// Writes an color lookup table
        /// </summary>
        /// <param name="value">The CLUT to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteCLUT(IccClut value)
        {
            int count = this.WriteArray(value.GridPointCount);
            count += this.WriteEmpty(16 - value.GridPointCount.Length);

            switch (value.DataType)
            {
                case IccClutDataType.Float:
                    return count + this.WriteCLUTf32(value);
                case IccClutDataType.UInt8:
                    count += this.WriteByte(1);
                    count += this.WriteEmpty(3);
                    return count + this.WriteCLUT8(value);
                case IccClutDataType.UInt16:
                    count += this.WriteByte(2);
                    count += this.WriteEmpty(3);
                    return count + this.WriteCLUT16(value);

                default:
                    throw new InvalidIccProfileException($"Invalid CLUT data type of {value.DataType}");
            }
        }

        /// <summary>
        /// Writes a 8bit color lookup table
        /// </summary>
        /// <param name="value">The CLUT to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteCLUT8(IccClut value)
        {
            int count = 0;
            foreach (float[] inArray in value.Values)
            {
                foreach (float item in inArray)
                {
                    count += this.WriteByte((byte)((item * byte.MaxValue) + 0.5f).Clamp(0, byte.MaxValue));
                }
            }

            return count;
        }

        /// <summary>
        /// Writes a 16bit color lookup table
        /// </summary>
        /// <param name="value">The CLUT to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteCLUT16(IccClut value)
        {
            int count = 0;
            foreach (float[] inArray in value.Values)
            {
                foreach (float item in inArray)
                {
                    count += this.WriteUInt16((ushort)((item * ushort.MaxValue) + 0.5f).Clamp(0, ushort.MaxValue));
                }
            }

            return count;
        }

        /// <summary>
        /// Writes a 32bit float color lookup table
        /// </summary>
        /// <param name="value">The CLUT to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteCLUTf32(IccClut value)
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

        /// <summary>
        /// Writes a <see cref="IccMultiProcessElement"/>
        /// </summary>
        /// <param name="value">The element to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMultiProcessElement(IccMultiProcessElement value)
        {
            int count = this.WriteUInt32((uint)value.Signature);
            count += this.WriteUInt16((ushort)value.InputChannelCount);
            count += this.WriteUInt16((ushort)value.OutputChannelCount);

            switch (value.Signature)
            {
                case IccMultiProcessElementSignature.CurveSet:
                    return count + this.WriteCurveSetProcessElement(value as IccCurveSetProcessElement);
                case IccMultiProcessElementSignature.Matrix:
                    return count + this.WriteMatrixProcessElement(value as IccMatrixProcessElement);
                case IccMultiProcessElementSignature.Clut:
                    return count + this.WriteCLUTProcessElement(value as IccClutProcessElement);

                case IccMultiProcessElementSignature.BAcs:
                case IccMultiProcessElementSignature.EAcs:
                    return count + this.WriteEmpty(8);

                default:
                    throw new InvalidIccProfileException($"Invalid MultiProcessElement type of {value.Signature}");
            }
        }

        /// <summary>
        /// Writes a CurveSet <see cref="IccMultiProcessElement"/>
        /// </summary>
        /// <param name="value">The element to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteCurveSetProcessElement(IccCurveSetProcessElement value)
        {
            int count = 0;
            foreach (IccOneDimensionalCurve curve in value.Curves)
            {
                count += this.WriteOneDimensionalCurve(curve);
                count += this.WritePadding();
            }

            return count;
        }

        /// <summary>
        /// Writes a Matrix <see cref="IccMultiProcessElement"/>
        /// </summary>
        /// <param name="value">The element to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteMatrixProcessElement(IccMatrixProcessElement value)
        {
            return this.WriteMatrix(value.MatrixIxO, true)
                 + this.WriteMatrix(value.MatrixOx1, true);
        }

        /// <summary>
        /// Writes a CLUT <see cref="IccMultiProcessElement"/>
        /// </summary>
        /// <param name="value">The element to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteCLUTProcessElement(IccClutProcessElement value)
        {
            return this.WriteCLUT(value.ClutValue);
        }

        /// <summary>
        /// Writes a <see cref="IccOneDimensionalCurve"/>
        /// </summary>
        /// <param name="value">The curve to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteOneDimensionalCurve(IccOneDimensionalCurve value)
        {
            int count = this.WriteUInt16((ushort)value.Segments.Length);
            count += this.WriteEmpty(2);

            foreach (double point in value.BreakPoints)
            {
                count += this.WriteSingle((float)point);
            }

            foreach (IccCurveSegment segment in value.Segments)
            {
                count += this.WriteCurveSegment(segment);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccResponseCurve"/>
        /// </summary>
        /// <param name="value">The curve to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteResponseCurve(IccResponseCurve value)
        {
            int count = this.WriteUInt32((uint)value.CurveType);
            int channels = value.XyzValues.Length;

            foreach (IccResponseNumber[] responseArray in value.ResponseArrays)
            {
                count += this.WriteUInt32((uint)responseArray.Length);
            }

            foreach (Vector3 xyz in value.XyzValues)
            {
                count += this.WriteXYZNumber(xyz);
            }

            foreach (IccResponseNumber[] responseArray in value.ResponseArrays)
            {
                foreach (IccResponseNumber response in responseArray)
                {
                    count += this.WriteResponseNumber(response);
                }
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccParametricCurve"/>
        /// </summary>
        /// <param name="value">The curve to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteParametricCurve(IccParametricCurve value)
        {
            int count = this.WriteUInt16(value.Type);
            count += this.WriteEmpty(2);

            if (value.Type >= 0 && value.Type <= 4)
            {
                count += this.WriteFix16(value.G);
            }

            if (value.Type > 0 && value.Type <= 4)
            {
                count += this.WriteFix16(value.A);
                count += this.WriteFix16(value.B);
            }

            if (value.Type > 1 && value.Type <= 4)
            {
                count += this.WriteFix16(value.C);
            }

            if (value.Type > 2 && value.Type <= 4)
            {
                count += this.WriteFix16(value.D);
            }

            if (value.Type == 4)
            {
                count += this.WriteFix16(value.E);
                count += this.WriteFix16(value.F);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccCurveSegment"/>
        /// </summary>
        /// <param name="value">The curve to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteCurveSegment(IccCurveSegment value)
        {
            int count = this.WriteUInt32((uint)value.Signature);
            count += this.WriteEmpty(4);

            switch (value.Signature)
            {
                case IccCurveSegmentSignature.FormulaCurve:
                    return count + this.WriteFormulaCurveElement(value as IccFormulaCurveElement);
                case IccCurveSegmentSignature.SampledCurve:
                    return count + this.WriteSampledCurveElement(value as IccSampledCurveElement);
                default:
                    throw new InvalidIccProfileException($"Invalid CurveSegment type of {value.Signature}");
            }
        }

        /// <summary>
        /// Writes a <see cref="IccFormulaCurveElement"/>
        /// </summary>
        /// <param name="value">The curve to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteFormulaCurveElement(IccFormulaCurveElement value)
        {
            int count = this.WriteUInt16(value.Type);
            count += this.WriteEmpty(2);

            if (value.Type == 0 || value.Type == 1)
            {
                count += this.WriteSingle((float)value.Gamma);
            }

            if (value.Type >= 0 && value.Type <= 2)
            {
                count += this.WriteSingle((float)value.A);
                count += this.WriteSingle((float)value.B);
                count += this.WriteSingle((float)value.C);
            }

            if (value.Type == 1 || value.Type == 2)
            {
                count += this.WriteSingle((float)value.D);
            }

            if (value.Type == 2)
            {
                count += this.WriteSingle((float)value.E);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccSampledCurveElement"/>
        /// </summary>
        /// <param name="value">The curve to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteSampledCurveElement(IccSampledCurveElement value)
        {
            int count = this.WriteUInt32((uint)value.CurveEntries.Length);
            foreach (double entry in value.CurveEntries)
            {
                count += this.WriteSingle((float)entry);
            }

            return count;
        }

        /// <summary>
        /// Writes a byte array
        /// </summary>
        /// <param name="data">The array to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteArray(byte[] data)
        {
            this.dataStream.Write(data, 0, data.Length);
            return data.Length;
        }

        /// <summary>
        /// Writes a ushort array
        /// </summary>
        /// <param name="data">The array to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteArray(ushort[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                this.WriteUInt16(data[i]);
            }

            return data.Length * 2;
        }

        /// <summary>
        /// Writes a short array
        /// </summary>
        /// <param name="data">The array to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteArray(short[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                this.WriteInt16(data[i]);
            }

            return data.Length * 2;
        }

        /// <summary>
        /// Writes a uint array
        /// </summary>
        /// <param name="data">The array to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteArray(uint[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                this.WriteUInt32(data[i]);
            }

            return data.Length * 4;
        }

        /// <summary>
        /// Writes an int array
        /// </summary>
        /// <param name="data">The array to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteArray(int[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                this.WriteInt32(data[i]);
            }

            return data.Length * 4;
        }

        /// <summary>
        /// Writes a ulong array
        /// </summary>
        /// <param name="data">The array to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteArray(ulong[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                this.WriteUInt64(data[i]);
            }

            return data.Length * 8;
        }

        /// <summary>
        /// Write a number of empty bytes
        /// </summary>
        /// <param name="length">The number of bytes to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteEmpty(int length)
        {
            for (int i = 0; i < length; i++)
            {
                this.dataStream.WriteByte(0);
            }

            return length;
        }

        /// <summary>
        /// Writes empty bytes to a 4-byte margin
        /// </summary>
        /// <returns>The number of bytes written</returns>
        public int WritePadding()
        {
            int p = 4 - ((int)this.dataStream.Position % 4);
            return this.WriteEmpty(p >= 4 ? 0 : p);
        }

        /// <summary>
        /// Writes given bytes from pointer
        /// </summary>
        /// <param name="data">Pointer to the bytes to write</param>
        /// <param name="length">The number of bytes to write</param>
        /// <returns>The number of bytes written</returns>
        private unsafe int WriteBytes(byte* data, int length)
        {
            if (IsLittleEndian)
            {
                for (int i = length - 1; i >= 0; i--)
                {
                    this.dataStream.WriteByte(data[i]);
                }
            }
            else
            {
                this.WriteBytesDirect(data, length);
            }

            return length;
        }

        /// <summary>
        /// Writes given bytes from pointer ignoring endianness
        /// </summary>
        /// <param name="data">Pointer to the bytes to write</param>
        /// <param name="length">The number of bytes to write</param>
        /// <returns>The number of bytes written</returns>
        private unsafe int WriteBytesDirect(byte* data, int length)
        {
            for (int i = 0; i < length; i++)
            {
                this.dataStream.WriteByte(data[i]);
            }

            return length;
        }

        /// <summary>
        /// Writes curve data
        /// </summary>
        /// <param name="curves">The curves to write</param>
        /// <returns>The number of bytes written</returns>
        private int WriteCurves(IccTagDataEntry[] curves)
        {
            int count = 0;
            foreach (IccTagDataEntry curve in curves)
            {
                if (curve.Signature != IccTypeSignature.Curve && curve.Signature != IccTypeSignature.ParametricCurve)
                {
                    throw new InvalidIccProfileException($"Curve has to be either \"{nameof(IccTypeSignature)}.{nameof(IccTypeSignature.Curve)}\" or" +
                        $" \"{nameof(IccTypeSignature)}.{nameof(IccTypeSignature.ParametricCurve)}\" for LutAToB- and LutBToA-TagDataEntries");
                }

                count += this.WriteTagDataEntry(curve);
                count += this.WritePadding();
            }

            return count;
        }
    }
}
