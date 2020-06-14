// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Numerics;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Provides methods to read ICC data types
    /// </summary>
    internal sealed partial class IccDataReader
    {
        /// <summary>
        /// Reads a tag data entry
        /// </summary>
        /// <param name="info">The table entry with reading information</param>
        /// <returns>the tag data entry</returns>
        public IccTagDataEntry ReadTagDataEntry(IccTagTableEntry info)
        {
            this.currentIndex = (int)info.Offset;
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
                    return this.ReadLutAtoBTagDataEntry();
                case IccTypeSignature.LutBToA:
                    return this.ReadLutBtoATagDataEntry();
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
                    return this.ReadViewingConditionsTagDataEntry();
                case IccTypeSignature.Xyz:
                    return this.ReadXyzTagDataEntry(info.DataSize);

                // V2 Types:
                case IccTypeSignature.TextDescription:
                    return this.ReadTextDescriptionTagDataEntry();
                case IccTypeSignature.CrdInfo:
                    return this.ReadCrdInfoTagDataEntry();
                case IccTypeSignature.Screening:
                    return this.ReadScreeningTagDataEntry();
                case IccTypeSignature.UcrBg:
                    return this.ReadUcrBgTagDataEntry(info.DataSize);

                // Unsupported or unknown
                case IccTypeSignature.DeviceSettings:
                case IccTypeSignature.NamedColor:
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
            var type = (IccTypeSignature)this.ReadUInt32();
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
            var colorant = (IccColorantEncoding)this.ReadUInt16();

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
                    values[i] = new double[] { this.ReadUFix16(), this.ReadUFix16() };
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
            var cdata = new IccColorantTableEntry[colorantCount];
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

            if (pointCount == 1)
            {
                return new IccCurveTagDataEntry(this.ReadUFix8());
            }

            float[] cdata = new float[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                cdata[i] = this.ReadUInt16() / 65535f;
            }

            return new IccCurveTagDataEntry(cdata);

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
            var inValues = new IccLut[inChCount];
            byte[] gridPointCount = new byte[inChCount];
            for (int i = 0; i < inChCount; i++)
            {
                inValues[i] = this.ReadLut16(inTableCount);
                gridPointCount[i] = clutPointCount;
            }

            // CLUT
            IccClut clut = this.ReadClut16(inChCount, outChCount, gridPointCount);

            // Output LUT
            var outValues = new IccLut[outChCount];
            for (int i = 0; i < outChCount; i++)
            {
                outValues[i] = this.ReadLut16(outTableCount);
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
            var inValues = new IccLut[inChCount];
            byte[] gridPointCount = new byte[inChCount];
            for (int i = 0; i < inChCount; i++)
            {
                inValues[i] = this.ReadLut8();
                gridPointCount[i] = clutPointCount;
            }

            // CLUT
            IccClut clut = this.ReadClut8(inChCount, outChCount, gridPointCount);

            // Output LUT
            var outValues = new IccLut[outChCount];
            for (int i = 0; i < outChCount; i++)
            {
                outValues[i] = this.ReadLut8();
            }

            return new IccLut8TagDataEntry(matrix, inValues, clut, outValues);
        }

        /// <summary>
        /// Reads a <see cref="IccLutAToBTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccLutAToBTagDataEntry ReadLutAtoBTagDataEntry()
        {
            int start = this.currentIndex - 8; // 8 is the tag header size

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
                this.currentIndex = (int)bCurveOffset + start;
                bCurve = this.ReadCurves(outChCount);
            }

            if (mCurveOffset != 0)
            {
                this.currentIndex = (int)mCurveOffset + start;
                mCurve = this.ReadCurves(outChCount);
            }

            if (aCurveOffset != 0)
            {
                this.currentIndex = (int)aCurveOffset + start;
                aCurve = this.ReadCurves(inChCount);
            }

            if (clutOffset != 0)
            {
                this.currentIndex = (int)clutOffset + start;
                clut = this.ReadClut(inChCount, outChCount, false);
            }

            if (matrixOffset != 0)
            {
                this.currentIndex = (int)matrixOffset + start;
                matrix3x3 = this.ReadMatrix(3, 3, false);
                matrix3x1 = this.ReadMatrix(3, false);
            }

            return new IccLutAToBTagDataEntry(bCurve, matrix3x3, matrix3x1, mCurve, clut, aCurve);
        }

        /// <summary>
        /// Reads a <see cref="IccLutBToATagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccLutBToATagDataEntry ReadLutBtoATagDataEntry()
        {
            int start = this.currentIndex - 8; // 8 is the tag header size

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
                this.currentIndex = (int)bCurveOffset + start;
                bCurve = this.ReadCurves(inChCount);
            }

            if (mCurveOffset != 0)
            {
                this.currentIndex = (int)mCurveOffset + start;
                mCurve = this.ReadCurves(inChCount);
            }

            if (aCurveOffset != 0)
            {
                this.currentIndex = (int)aCurveOffset + start;
                aCurve = this.ReadCurves(outChCount);
            }

            if (clutOffset != 0)
            {
                this.currentIndex = (int)clutOffset + start;
                clut = this.ReadClut(inChCount, outChCount, false);
            }

            if (matrixOffset != 0)
            {
                this.currentIndex = (int)matrixOffset + start;
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
            int start = this.currentIndex - 8; // 8 is the tag header size
            uint recordCount = this.ReadUInt32();

            this.ReadUInt32();  // Record size (always 12)
            var text = new IccLocalizedString[recordCount];

            var culture = new CultureInfo[recordCount];
            uint[] length = new uint[recordCount];
            uint[] offset = new uint[recordCount];

            for (int i = 0; i < recordCount; i++)
            {
                string languageCode = this.ReadAsciiString(2);
                string countryCode = this.ReadAsciiString(2);

                culture[i] = ReadCulture(languageCode, countryCode);
                length[i] = this.ReadUInt32();
                offset[i] = this.ReadUInt32();
            }

            for (int i = 0; i < recordCount; i++)
            {
                this.currentIndex = (int)(start + offset[i]);
                text[i] = new IccLocalizedString(culture[i], this.ReadUnicodeString((int)length[i]));
            }

            return new IccMultiLocalizedUnicodeTagDataEntry(text);

            CultureInfo ReadCulture(string language, string country)
            {
                if (string.IsNullOrWhiteSpace(language))
                {
                    return CultureInfo.InvariantCulture;
                }
                else if (string.IsNullOrWhiteSpace(country))
                {
                    try
                    {
                        return new CultureInfo(language);
                    }
                    catch (CultureNotFoundException)
                    {
                        return CultureInfo.InvariantCulture;
                    }
                }
                else
                {
                    try
                    {
                        return new CultureInfo($"{language}-{country}");
                    }
                    catch (CultureNotFoundException)
                    {
                        return ReadCulture(language, null);
                    }
                }
            }
        }

        /// <summary>
        /// Reads a <see cref="IccMultiProcessElementsTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccMultiProcessElementsTagDataEntry ReadMultiProcessElementsTagDataEntry()
        {
            int start = this.currentIndex - 8;

            this.ReadUInt16();
            this.ReadUInt16();
            uint elementCount = this.ReadUInt32();

            var positionTable = new IccPositionNumber[elementCount];
            for (int i = 0; i < elementCount; i++)
            {
                positionTable[i] = this.ReadPositionNumber();
            }

            var elements = new IccMultiProcessElement[elementCount];
            for (int i = 0; i < elementCount; i++)
            {
                this.currentIndex = (int)positionTable[i].Offset + start;
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
            int vendorFlag = this.ReadInt32();
            uint colorCount = this.ReadUInt32();
            uint coordCount = this.ReadUInt32();
            string prefix = this.ReadAsciiString(32);
            string suffix = this.ReadAsciiString(32);

            var colors = new IccNamedColor[colorCount];
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
            var description = new IccProfileDescription[count];
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
            int start = this.currentIndex - 8; // 8 is the tag header size
            uint count = this.ReadUInt32();
            var table = new IccPositionNumber[count];
            for (int i = 0; i < count; i++)
            {
                table[i] = this.ReadPositionNumber();
            }

            var entries = new IccProfileSequenceIdentifier[count];
            for (int i = 0; i < count; i++)
            {
                this.currentIndex = (int)(start + table[i].Offset);
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
            int start = this.currentIndex - 8; // 8 is the tag header size
            ushort channelCount = this.ReadUInt16();
            ushort measurementCount = this.ReadUInt16();

            uint[] offset = new uint[measurementCount];
            for (int i = 0; i < measurementCount; i++)
            {
                offset[i] = this.ReadUInt32();
            }

            var curves = new IccResponseCurve[measurementCount];
            for (int i = 0; i < measurementCount; i++)
            {
                this.currentIndex = (int)(start + offset[i]);
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
        /// <returns>The read entry</returns>
        public IccViewingConditionsTagDataEntry ReadViewingConditionsTagDataEntry()
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
            var arrayData = new Vector3[count];
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
            string unicodeValue, scriptcodeValue;
            string asciiValue = unicodeValue = scriptcodeValue = null;

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

        /// <summary>
        /// Reads a <see cref="IccTextDescriptionTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccCrdInfoTagDataEntry ReadCrdInfoTagDataEntry()
        {
            uint productNameCount = this.ReadUInt32();
            string productName = this.ReadAsciiString((int)productNameCount);

            uint crd0Count = this.ReadUInt32();
            string crd0Name = this.ReadAsciiString((int)crd0Count);

            uint crd1Count = this.ReadUInt32();
            string crd1Name = this.ReadAsciiString((int)crd1Count);

            uint crd2Count = this.ReadUInt32();
            string crd2Name = this.ReadAsciiString((int)crd2Count);

            uint crd3Count = this.ReadUInt32();
            string crd3Name = this.ReadAsciiString((int)crd3Count);

            return new IccCrdInfoTagDataEntry(productName, crd0Name, crd1Name, crd2Name, crd3Name);
        }

        /// <summary>
        /// Reads a <see cref="IccScreeningTagDataEntry"/>
        /// </summary>
        /// <returns>The read entry</returns>
        public IccScreeningTagDataEntry ReadScreeningTagDataEntry()
        {
            var flags = (IccScreeningFlag)this.ReadInt32();
            uint channelCount = this.ReadUInt32();
            var channels = new IccScreeningChannel[channelCount];
            for (int i = 0; i < channels.Length; i++)
            {
                channels[i] = this.ReadScreeningChannel();
            }

            return new IccScreeningTagDataEntry(flags, channels);
        }

        /// <summary>
        /// Reads a <see cref="IccUcrBgTagDataEntry"/>
        /// </summary>
        /// <param name="size">The size of the entry in bytes</param>
        /// <returns>The read entry</returns>
        public IccUcrBgTagDataEntry ReadUcrBgTagDataEntry(uint size)
        {
            uint ucrCount = this.ReadUInt32();
            ushort[] ucrCurve = new ushort[ucrCount];
            for (int i = 0; i < ucrCurve.Length; i++)
            {
                ucrCurve[i] = this.ReadUInt16();
            }

            uint bgCount = this.ReadUInt32();
            ushort[] bgCurve = new ushort[bgCount];
            for (int i = 0; i < bgCurve.Length; i++)
            {
                bgCurve[i] = this.ReadUInt16();
            }

            // ((ucr length + bg length) * UInt16 size) + (ucrCount + bgCount)
            uint dataSize = ((ucrCount + bgCount) * 2) + 8;
            int descriptionLength = (int)(size - 8 - dataSize);   // 8 is the tag header size
            string description = this.ReadAsciiString(descriptionLength);

            return new IccUcrBgTagDataEntry(ucrCurve, bgCurve, description);
        }
    }
}
