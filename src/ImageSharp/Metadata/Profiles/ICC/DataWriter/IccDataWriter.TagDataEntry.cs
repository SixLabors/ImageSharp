// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Provides methods to write ICC data types
    /// </summary>
    internal sealed partial class IccDataWriter
    {
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
                    count += this.WriteChromaticityTagDataEntry((IccChromaticityTagDataEntry)entry);
                    break;
                case IccTypeSignature.ColorantOrder:
                    count += this.WriteColorantOrderTagDataEntry((IccColorantOrderTagDataEntry)entry);
                    break;
                case IccTypeSignature.ColorantTable:
                    count += this.WriteColorantTableTagDataEntry((IccColorantTableTagDataEntry)entry);
                    break;
                case IccTypeSignature.Curve:
                    count += this.WriteCurveTagDataEntry((IccCurveTagDataEntry)entry);
                    break;
                case IccTypeSignature.Data:
                    count += this.WriteDataTagDataEntry((IccDataTagDataEntry)entry);
                    break;
                case IccTypeSignature.DateTime:
                    count += this.WriteDateTimeTagDataEntry((IccDateTimeTagDataEntry)entry);
                    break;
                case IccTypeSignature.Lut16:
                    count += this.WriteLut16TagDataEntry((IccLut16TagDataEntry)entry);
                    break;
                case IccTypeSignature.Lut8:
                    count += this.WriteLut8TagDataEntry((IccLut8TagDataEntry)entry);
                    break;
                case IccTypeSignature.LutAToB:
                    count += this.WriteLutAtoBTagDataEntry((IccLutAToBTagDataEntry)entry);
                    break;
                case IccTypeSignature.LutBToA:
                    count += this.WriteLutBtoATagDataEntry((IccLutBToATagDataEntry)entry);
                    break;
                case IccTypeSignature.Measurement:
                    count += this.WriteMeasurementTagDataEntry((IccMeasurementTagDataEntry)entry);
                    break;
                case IccTypeSignature.MultiLocalizedUnicode:
                    count += this.WriteMultiLocalizedUnicodeTagDataEntry((IccMultiLocalizedUnicodeTagDataEntry)entry);
                    break;
                case IccTypeSignature.MultiProcessElements:
                    count += this.WriteMultiProcessElementsTagDataEntry((IccMultiProcessElementsTagDataEntry)entry);
                    break;
                case IccTypeSignature.NamedColor2:
                    count += this.WriteNamedColor2TagDataEntry((IccNamedColor2TagDataEntry)entry);
                    break;
                case IccTypeSignature.ParametricCurve:
                    count += this.WriteParametricCurveTagDataEntry((IccParametricCurveTagDataEntry)entry);
                    break;
                case IccTypeSignature.ProfileSequenceDesc:
                    count += this.WriteProfileSequenceDescTagDataEntry((IccProfileSequenceDescTagDataEntry)entry);
                    break;
                case IccTypeSignature.ProfileSequenceIdentifier:
                    count += this.WriteProfileSequenceIdentifierTagDataEntry((IccProfileSequenceIdentifierTagDataEntry)entry);
                    break;
                case IccTypeSignature.ResponseCurveSet16:
                    count += this.WriteResponseCurveSet16TagDataEntry((IccResponseCurveSet16TagDataEntry)entry);
                    break;
                case IccTypeSignature.S15Fixed16Array:
                    count += this.WriteFix16ArrayTagDataEntry((IccFix16ArrayTagDataEntry)entry);
                    break;
                case IccTypeSignature.Signature:
                    count += this.WriteSignatureTagDataEntry((IccSignatureTagDataEntry)entry);
                    break;
                case IccTypeSignature.Text:
                    count += this.WriteTextTagDataEntry((IccTextTagDataEntry)entry);
                    break;
                case IccTypeSignature.U16Fixed16Array:
                    count += this.WriteUFix16ArrayTagDataEntry((IccUFix16ArrayTagDataEntry)entry);
                    break;
                case IccTypeSignature.UInt16Array:
                    count += this.WriteUInt16ArrayTagDataEntry((IccUInt16ArrayTagDataEntry)entry);
                    break;
                case IccTypeSignature.UInt32Array:
                    count += this.WriteUInt32ArrayTagDataEntry((IccUInt32ArrayTagDataEntry)entry);
                    break;
                case IccTypeSignature.UInt64Array:
                    count += this.WriteUInt64ArrayTagDataEntry((IccUInt64ArrayTagDataEntry)entry);
                    break;
                case IccTypeSignature.UInt8Array:
                    count += this.WriteUInt8ArrayTagDataEntry((IccUInt8ArrayTagDataEntry)entry);
                    break;
                case IccTypeSignature.ViewingConditions:
                    count += this.WriteViewingConditionsTagDataEntry((IccViewingConditionsTagDataEntry)entry);
                    break;
                case IccTypeSignature.Xyz:
                    count += this.WriteXyzTagDataEntry((IccXyzTagDataEntry)entry);
                    break;

                // V2 Types:
                case IccTypeSignature.TextDescription:
                    count += this.WriteTextDescriptionTagDataEntry((IccTextDescriptionTagDataEntry)entry);
                    break;
                case IccTypeSignature.CrdInfo:
                    count += this.WriteCrdInfoTagDataEntry((IccCrdInfoTagDataEntry)entry);
                    break;
                case IccTypeSignature.Screening:
                    count += this.WriteScreeningTagDataEntry((IccScreeningTagDataEntry)entry);
                    break;
                case IccTypeSignature.UcrBg:
                    count += this.WriteUcrBgTagDataEntry((IccUcrBgTagDataEntry)entry);
                    break;

                // Unsupported or unknown
                case IccTypeSignature.DeviceSettings:
                case IccTypeSignature.NamedColor:
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
        public int WriteUnknownTagDataEntry(IccUnknownTagDataEntry value) => this.WriteArray(value.Data);

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

            for (int i = 0; i < value.ColorantData.Length; i++)
            {
                ref IccColorantTableEntry colorant = ref value.ColorantData[i];

                count += this.WriteAsciiString(colorant.Name, 32, true);
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
                    count += this.WriteUInt16((ushort)Numerics.Clamp((value.CurveData[i] * ushort.MaxValue) + 0.5F, 0, ushort.MaxValue));
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
        public int WriteDateTimeTagDataEntry(IccDateTimeTagDataEntry value) => this.WriteDateTime(value.Value);

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
                count += this.WriteLut16(lut);
            }

            count += this.WriteClut16(value.ClutValues);

            foreach (IccLut lut in value.OutputValues)
            {
                count += this.WriteLut16(lut);
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
                count += this.WriteLut8(lut);
            }

            count += this.WriteClut8(value.ClutValues);

            foreach (IccLut lut in value.OutputValues)
            {
                count += this.WriteLut8(lut);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccLutAToBTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteLutAtoBTagDataEntry(IccLutAToBTagDataEntry value)
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
                count += this.WriteClut(value.ClutValues);
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
        public int WriteLutBtoATagDataEntry(IccLutBToATagDataEntry value)
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
                count += this.WriteClut(value.ClutValues);
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
                 + this.WriteXyzNumber(value.XyzBacking)
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

            // TODO: Investigate cost of Linq GroupBy
            IGrouping<string, IccLocalizedString>[] texts = value.Texts.GroupBy(t => t.Text).ToArray();

            var offset = new uint[texts.Length];
            var lengths = new int[texts.Length];

            for (int i = 0; i < texts.Length; i++)
            {
                offset[i] = (uint)(this.dataStream.Position - start);
                count += lengths[i] = this.WriteUnicodeString(texts[i].Key);
            }

            // Write position table
            long lpos = this.dataStream.Position;
            this.dataStream.Position = tpos;
            for (int i = 0; i < texts.Length; i++)
            {
                foreach (IccLocalizedString localizedString in texts[i])
                {
                    string cultureName = localizedString.Culture.Name;
                    if (string.IsNullOrEmpty(cultureName))
                    {
                        count += this.WriteAsciiString("xx", 2, false);
                        count += this.WriteAsciiString("\0\0", 2, false);
                    }
                    else if (cultureName.Contains("-"))
                    {
                        string[] code = cultureName.Split('-');
                        count += this.WriteAsciiString(code[0].ToLower(), 2, false);
                        count += this.WriteAsciiString(code[1].ToUpper(), 2, false);
                    }
                    else
                    {
                        count += this.WriteAsciiString(cultureName, 2, false);
                        count += this.WriteAsciiString("\0\0", 2, false);
                    }

                    count += this.WriteUInt32((uint)lengths[i]);
                    count += this.WriteUInt32(offset[i]);
                }
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

            var posTable = new IccPositionNumber[value.Data.Length];
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
            int count = this.WriteInt32(value.VendorFlags)
                      + this.WriteUInt32((uint)value.Colors.Length)
                      + this.WriteUInt32((uint)value.CoordinateCount)
                      + this.WriteAsciiString(value.Prefix, 32, true)
                      + this.WriteAsciiString(value.Suffix, 32, true);

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
        public int WriteParametricCurveTagDataEntry(IccParametricCurveTagDataEntry value) => this.WriteParametricCurve(value.Curve);

        /// <summary>
        /// Writes a <see cref="IccProfileSequenceDescTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteProfileSequenceDescTagDataEntry(IccProfileSequenceDescTagDataEntry value)
        {
            int count = this.WriteUInt32((uint)value.Descriptions.Length);

            for (int i = 0; i < value.Descriptions.Length; i++)
            {
                ref IccProfileDescription desc = ref value.Descriptions[i];

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
            var table = new IccPositionNumber[length];

            for (int i = 0; i < length; i++)
            {
                ref IccProfileSequenceIdentifier sequenceIdentifier = ref value.Data[i];

                uint offset = (uint)(this.dataStream.Position - start);
                int size = this.WriteProfileId(sequenceIdentifier.Id);
                size += this.WriteTagDataEntry(new IccMultiLocalizedUnicodeTagDataEntry(sequenceIdentifier.Description));
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

            var offset = new uint[value.Curves.Length];

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
        public int WriteSignatureTagDataEntry(IccSignatureTagDataEntry value) => this.WriteAsciiString(value.SignatureData, 4, false);

        /// <summary>
        /// Writes a <see cref="IccTextTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteTextTagDataEntry(IccTextTagDataEntry value) => this.WriteAsciiString(value.Text);

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
        public int WriteUInt16ArrayTagDataEntry(IccUInt16ArrayTagDataEntry value) => this.WriteArray(value.Data);

        /// <summary>
        /// Writes a <see cref="IccUInt32ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteUInt32ArrayTagDataEntry(IccUInt32ArrayTagDataEntry value) => this.WriteArray(value.Data);

        /// <summary>
        /// Writes a <see cref="IccUInt64ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteUInt64ArrayTagDataEntry(IccUInt64ArrayTagDataEntry value) => this.WriteArray(value.Data);

        /// <summary>
        /// Writes a <see cref="IccUInt8ArrayTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteUInt8ArrayTagDataEntry(IccUInt8ArrayTagDataEntry value) => this.WriteArray(value.Data);

        /// <summary>
        /// Writes a <see cref="IccViewingConditionsTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteViewingConditionsTagDataEntry(IccViewingConditionsTagDataEntry value)
        {
            return this.WriteXyzNumber(value.IlluminantXyz)
                 + this.WriteXyzNumber(value.SurroundXyz)
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
                count += this.WriteXyzNumber(value.Data[i]);
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

            if (value.Ascii is null)
            {
                count += this.WriteUInt32(0);
            }
            else
            {
                this.dataStream.Position += 4;
                count += size = this.WriteAsciiString(value.Ascii + '\0');
                this.dataStream.Position -= size + 4;
                count += this.WriteUInt32((uint)size);
                this.dataStream.Position += size;
            }

            if (value.Unicode is null)
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

            if (value.ScriptCode is null)
            {
                count += this.WriteUInt16(0);
                count += this.WriteByte(0);
                count += this.WriteEmpty(67);
            }
            else
            {
                this.dataStream.Position += 3;
                count += size = this.WriteAsciiString(value.ScriptCode, 67, true);
                this.dataStream.Position -= size + 3;
                count += this.WriteUInt16(value.ScriptCodeCode);
                count += this.WriteByte((byte)(value.ScriptCode.Length > 66 ? 67 : value.ScriptCode.Length + 1));
                this.dataStream.Position += size;
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccCrdInfoTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteCrdInfoTagDataEntry(IccCrdInfoTagDataEntry value)
        {
            int count = 0;
            WriteString(value.PostScriptProductName);
            WriteString(value.RenderingIntent0Crd);
            WriteString(value.RenderingIntent1Crd);
            WriteString(value.RenderingIntent2Crd);
            WriteString(value.RenderingIntent3Crd);

            return count;

            void WriteString(string text)
            {
                int textLength;
                if (string.IsNullOrEmpty(text))
                {
                    textLength = 0;
                }
                else
                {
                    textLength = text.Length + 1;    // + 1 for null terminator
                }

                count += this.WriteUInt32((uint)textLength);
                count += this.WriteAsciiString(text, textLength, true);
            }
        }

        /// <summary>
        /// Writes a <see cref="IccScreeningTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteScreeningTagDataEntry(IccScreeningTagDataEntry value)
        {
            int count = 0;

            count += this.WriteInt32((int)value.Flags);
            count += this.WriteUInt32((uint)value.Channels.Length);
            for (int i = 0; i < value.Channels.Length; i++)
            {
                count += this.WriteScreeningChannel(value.Channels[i]);
            }

            return count;
        }

        /// <summary>
        /// Writes a <see cref="IccUcrBgTagDataEntry"/>
        /// </summary>
        /// <param name="value">The entry to write</param>
        /// <returns>The number of bytes written</returns>
        public int WriteUcrBgTagDataEntry(IccUcrBgTagDataEntry value)
        {
            int count = 0;

            count += this.WriteUInt32((uint)value.UcrCurve.Length);
            for (int i = 0; i < value.UcrCurve.Length; i++)
            {
                count += this.WriteUInt16(value.UcrCurve[i]);
            }

            count += this.WriteUInt32((uint)value.BgCurve.Length);
            for (int i = 0; i < value.BgCurve.Length; i++)
            {
                count += this.WriteUInt16(value.BgCurve[i]);
            }

            count += this.WriteAsciiString(value.Description + '\0');

            return count;
        }
    }
}
