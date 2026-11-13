// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Globalization;
using System.Numerics;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Provides methods to read ICC data types.
/// </summary>
internal sealed partial class IccDataReader
{
    /// <summary>
    /// Reads a tag data entry.
    /// </summary>
    /// <param name="info">The table entry with reading information.</param>
    /// <returns>The tag data entry.</returns>
    public IccTagDataEntry ReadTagDataEntry(IccTagTableEntry info)
        => this.ReadBounded(0, info.Offset, info.DataSize, static (reader, size) => reader.ReadTagData(size));

    /// <summary>
    /// Reads a tag or an embedded element within its declared byte range.
    /// </summary>
    /// <typeparam name="T">The decoded value type.</typeparam>
    /// <param name="origin">The base position of the offset.</param>
    /// <param name="offset">The byte offset from the base position.</param>
    /// <param name="size">The number of bytes in the range.</param>
    /// <param name="read">The parser for the bounded range.</param>
    /// <returns>The decoded value.</returns>
    private T ReadBounded<T>(int origin, uint offset, uint size, Func<IccDataReader, uint, T> read)
    {
        long start = (long)origin + offset;
        if (start < this.tagStart || start > this.tagEnd || size > this.tagEnd - start)
        {
            throw new InvalidIccProfileException("ICC data extends beyond the containing tag.");
        }

        int previousStart = this.tagStart;
        int previousEnd = this.tagEnd;
        this.currentIndex = this.tagStart = (int)start;
        this.tagEnd = this.tagStart + (int)size;
        try
        {
            return read(this, size);
        }
        finally
        {
            this.tagStart = previousStart;
            this.tagEnd = previousEnd;
        }
    }

    /// <summary>
    /// Reads the tag at the current position.
    /// </summary>
    /// <param name="size">The tag size in bytes, including its header.</param>
    /// <returns>The decoded tag.</returns>
    private IccTagDataEntry ReadTagData(uint size)
    {
        return this.ReadTagDataEntryHeader() switch
        {
            IccTypeSignature.Chromaticity => this.ReadChromaticityTagDataEntry(),
            IccTypeSignature.ColorantOrder => this.ReadColorantOrderTagDataEntry(),
            IccTypeSignature.ColorantTable => this.ReadColorantTableTagDataEntry(),
            IccTypeSignature.Curve => this.ReadCurveTagDataEntry(),
            IccTypeSignature.Data => this.ReadDataTagDataEntry(size),
            IccTypeSignature.DateTime => this.ReadDateTimeTagDataEntry(),
            IccTypeSignature.Lut16 => this.ReadLut16TagDataEntry(),
            IccTypeSignature.Lut8 => this.ReadLut8TagDataEntry(),
            IccTypeSignature.LutAToB => this.ReadLutAtoBTagDataEntry(),
            IccTypeSignature.LutBToA => this.ReadLutBtoATagDataEntry(),
            IccTypeSignature.Measurement => this.ReadMeasurementTagDataEntry(),
            IccTypeSignature.MultiLocalizedUnicode => this.ReadMultiLocalizedUnicodeTagDataEntry(),
            IccTypeSignature.MultiProcessElements => this.ReadMultiProcessElementsTagDataEntry(),
            IccTypeSignature.NamedColor2 => this.ReadNamedColor2TagDataEntry(),
            IccTypeSignature.ParametricCurve => this.ReadParametricCurveTagDataEntry(),
            IccTypeSignature.ProfileSequenceDesc => this.ReadProfileSequenceDescTagDataEntry(),
            IccTypeSignature.ProfileSequenceIdentifier => this.ReadProfileSequenceIdentifierTagDataEntry(),
            IccTypeSignature.ResponseCurveSet16 => this.ReadResponseCurveSet16TagDataEntry(),
            IccTypeSignature.S15Fixed16Array => this.ReadFix16ArrayTagDataEntry(size),
            IccTypeSignature.Signature => this.ReadSignatureTagDataEntry(),
            IccTypeSignature.Text => this.ReadTextTagDataEntry(size),
            IccTypeSignature.U16Fixed16Array => this.ReadUFix16ArrayTagDataEntry(size),
            IccTypeSignature.UInt16Array => this.ReadUInt16ArrayTagDataEntry(size),
            IccTypeSignature.UInt32Array => this.ReadUInt32ArrayTagDataEntry(size),
            IccTypeSignature.UInt64Array => this.ReadUInt64ArrayTagDataEntry(size),
            IccTypeSignature.UInt8Array => this.ReadUInt8ArrayTagDataEntry(size),
            IccTypeSignature.ViewingConditions => this.ReadViewingConditionsTagDataEntry(),
            IccTypeSignature.Xyz => this.ReadXyzTagDataEntry(size),

            // V2 Types:
            IccTypeSignature.TextDescription => this.ReadTextDescriptionTagDataEntry(),
            IccTypeSignature.CrdInfo => this.ReadCrdInfoTagDataEntry(),
            IccTypeSignature.Screening => this.ReadScreeningTagDataEntry(),
            IccTypeSignature.UcrBg => this.ReadUcrBgTagDataEntry(size),

            // Unsupported or unknown
            _ => this.ReadUnknownTagDataEntry(size),
        };
    }

    /// <summary>
    /// Reads the header of a <see cref="IccTagDataEntry"/>
    /// </summary>
    /// <returns>The read signature.</returns>
    public IccTypeSignature ReadTagDataEntryHeader()
    {
        IccTypeSignature type = (IccTypeSignature)this.ReadUInt32();
        this.AddIndex(4); // 4 bytes are not used
        return type;
    }

    /// <summary>
    /// Reads the header of a <see cref="IccTagDataEntry"/> and checks if it's the expected value
    /// </summary>
    /// <param name="expected">The expected value to check against.</param>
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
    /// <param name="size">The size of the entry in bytes.</param>
    /// <returns>The read entry.</returns>
    public IccUnknownTagDataEntry ReadUnknownTagDataEntry(uint size)
    {
        int count = (int)size - 8;  // 8 is the tag header size
        return new IccUnknownTagDataEntry(this.ReadBytes(count));
    }

    /// <summary>
    /// Reads a <see cref="IccChromaticityTagDataEntry"/>
    /// </summary>
    /// <returns>The read entry.</returns>
    public IccChromaticityTagDataEntry ReadChromaticityTagDataEntry()
    {
        ushort channelCount = this.ReadUInt16();
        IccColorantEncoding colorant = (IccColorantEncoding)this.ReadUInt16();

        if (Enum.IsDefined(colorant) && colorant != IccColorantEncoding.Unknown)
        {
            // The type is known and so are the values (they are constant)
            // channelCount should always be 3 but it doesn't really matter if it's not
            return new IccChromaticityTagDataEntry(colorant);
        }
        else
        {
            // Each channel stores two 32-bit fixed-point coordinates.
            this.EnsureAvailable(channelCount, 2 * sizeof(uint));
            double[][] values = new double[channelCount][];
            for (int i = 0; i < channelCount; i++)
            {
                values[i] = [this.ReadUFix16(), this.ReadUFix16()];
            }

            return new IccChromaticityTagDataEntry(values);
        }
    }

    /// <summary>
    /// Reads a <see cref="IccColorantOrderTagDataEntry"/>
    /// </summary>
    /// <returns>The read entry.</returns>
    public IccColorantOrderTagDataEntry ReadColorantOrderTagDataEntry()
    {
        uint colorantCount = this.ReadUInt32();
        byte[] number = this.ReadBytes((int)colorantCount);
        return new IccColorantOrderTagDataEntry(number);
    }

    /// <summary>
    /// Reads a <see cref="IccColorantTableTagDataEntry"/>
    /// </summary>
    /// <returns>The read entry.</returns>
    public IccColorantTableTagDataEntry ReadColorantTableTagDataEntry()
    {
        uint colorantCount = this.ReadUInt32();
        this.EnsureAvailable(colorantCount, 38);
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
    /// <returns>The read entry.</returns>
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

        this.EnsureAvailable(pointCount, sizeof(ushort));
        float[] cdata = new float[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            cdata[i] = this.ReadUInt16() / 65535f;
        }

        return new IccCurveTagDataEntry(cdata);

        // TODO: For PCSXYZ input, map 1+(32 767/32 768) to 1,0.
        // For PCSXYZ output, map 1,0 to 1+(32 767/32 768).
    }

    /// <summary>
    /// Reads a <see cref="IccDataTagDataEntry"/>
    /// </summary>
    /// <param name="size">The size of the entry in bytes.</param>
    /// <returns>The read entry</returns>
    public IccDataTagDataEntry ReadDataTagDataEntry(uint size)
    {
        this.AddIndex(3);   // first 3 bytes are zero
        byte b = this.data[this.AddIndex(1)];

        // last bit of 4th byte is either 0 = ASCII or 1 = binary
        bool ascii = GetBit(b, 7);
        int length = (int)size - 12;
        byte[] cdata = this.ReadBytes(length);

        return new IccDataTagDataEntry(cdata, ascii);
    }

    /// <summary>
    /// Reads a <see cref="IccDateTimeTagDataEntry"/>
    /// </summary>
    /// <returns>The read entry.</returns>
    public IccDateTimeTagDataEntry ReadDateTimeTagDataEntry() => new(this.ReadDateTime());

    /// <summary>
    /// Reads a <see cref="IccLut16TagDataEntry"/>
    /// </summary>
    /// <returns>The read entry.</returns>
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
            inValues[i] = this.ReadLut16(inTableCount);
            gridPointCount[i] = clutPointCount;
        }

        // CLUT
        IccClut clut = this.ReadClut16(inChCount, outChCount, gridPointCount);

        // Output LUT
        IccLut[] outValues = new IccLut[outChCount];
        for (int i = 0; i < outChCount; i++)
        {
            outValues[i] = this.ReadLut16(outTableCount);
        }

        return new IccLut16TagDataEntry(matrix, inValues, clut, outValues);
    }

    /// <summary>
    /// Reads a <see cref="IccLut8TagDataEntry"/>
    /// </summary>
    /// <returns>The read entry.</returns>
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
            inValues[i] = this.ReadLut8();
            gridPointCount[i] = clutPointCount;
        }

        // CLUT
        IccClut clut = this.ReadClut8(inChCount, outChCount, gridPointCount);

        // Output LUT
        IccLut[] outValues = new IccLut[outChCount];
        for (int i = 0; i < outChCount; i++)
        {
            outValues[i] = this.ReadLut8();
        }

        return new IccLut8TagDataEntry(matrix, inValues, clut, outValues);
    }

    /// <summary>
    /// Reads a <see cref="IccLutAToBTagDataEntry"/>
    /// </summary>
    /// <returns>The read entry.</returns>
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
            this.Seek(start, bCurveOffset);
            bCurve = this.ReadCurves(outChCount);
        }

        if (mCurveOffset != 0)
        {
            this.Seek(start, mCurveOffset);
            mCurve = this.ReadCurves(outChCount);
        }

        if (aCurveOffset != 0)
        {
            this.Seek(start, aCurveOffset);
            aCurve = this.ReadCurves(inChCount);
        }

        if (clutOffset != 0)
        {
            this.Seek(start, clutOffset);
            clut = this.ReadClut(inChCount, outChCount, false);
        }

        if (matrixOffset != 0)
        {
            this.Seek(start, matrixOffset);
            matrix3x3 = this.ReadMatrix(3, 3, false);
            matrix3x1 = this.ReadMatrix(3, false);
        }

        return new IccLutAToBTagDataEntry(bCurve, matrix3x3, matrix3x1, mCurve, clut, aCurve);
    }

    /// <summary>
    /// Reads a <see cref="IccLutBToATagDataEntry"/>
    /// </summary>
    /// <returns>The read entry.</returns>
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
            this.Seek(start, bCurveOffset);
            bCurve = this.ReadCurves(inChCount);
        }

        if (mCurveOffset != 0)
        {
            this.Seek(start, mCurveOffset);
            mCurve = this.ReadCurves(inChCount);
        }

        if (aCurveOffset != 0)
        {
            this.Seek(start, aCurveOffset);
            aCurve = this.ReadCurves(outChCount);
        }

        if (clutOffset != 0)
        {
            this.Seek(start, clutOffset);
            clut = this.ReadClut(inChCount, outChCount, false);
        }

        if (matrixOffset != 0)
        {
            this.Seek(start, matrixOffset);
            matrix3x3 = this.ReadMatrix(3, 3, false);
            matrix3x1 = this.ReadMatrix(3, false);
        }

        return new IccLutBToATagDataEntry(bCurve, matrix3x3, matrix3x1, mCurve, clut, aCurve);
    }

    /// <summary>
    /// Reads a <see cref="IccMeasurementTagDataEntry"/>
    /// </summary>
    /// <returns>The read entry.</returns>
    public IccMeasurementTagDataEntry ReadMeasurementTagDataEntry() => new(
            observer: (IccStandardObserver)this.ReadUInt32(),
            xyzBacking: this.ReadXyzNumber(),
            geometry: (IccMeasurementGeometry)this.ReadUInt32(),
            flare: this.ReadUFix16(),
            illuminant: (IccStandardIlluminant)this.ReadUInt32());

    /// <summary>
    /// Reads a <see cref="IccMultiLocalizedUnicodeTagDataEntry"/>
    /// </summary>
    /// <returns>The read entry.</returns>
    public IccMultiLocalizedUnicodeTagDataEntry ReadMultiLocalizedUnicodeTagDataEntry()
    {
        int start = this.currentIndex - 8; // 8 is the tag header size
        uint recordCount = this.ReadUInt32();

        this.ReadUInt32();  // Record size (always 12)
        this.EnsureAvailable(recordCount, 12);
        IccLocalizedString[] text = new IccLocalizedString[recordCount];

        CultureInfo[] culture = new CultureInfo[recordCount];
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

        int textStart = this.currentIndex;
        int remainingTextBytes = this.tagEnd - textStart;
        Dictionary<(uint Offset, uint Length), string> decoded = recordCount > 1 ? new() : null;
        for (int i = 0; i < recordCount; i++)
        {
            this.Seek(start, offset[i]);
            uint byteLength = length[i];
            if (byteLength != 0 && this.currentIndex < textStart)
            {
                throw new InvalidIccProfileException("ICC localized text overlaps its record table.");
            }

            // The ICC writer shares the same encoded string across cultures. Decode each range once.
            (uint Offset, uint Length) range = (offset[i], byteLength);
            string value;
            if (decoded != null && decoded.TryGetValue(range, out value))
            {
                this.AddIndex((int)byteLength);
            }
            else
            {
                if (byteLength > (uint)remainingTextBytes)
                {
                    throw new InvalidIccProfileException("ICC localized strings exceed the available text data.");
                }

                value = this.ReadUnicodeString((int)byteLength);
                remainingTextBytes -= (int)byteLength;
                decoded?.Add(range, value);
            }

            text[i] = new IccLocalizedString(culture[i], value);
        }

        return new IccMultiLocalizedUnicodeTagDataEntry(text);

        static CultureInfo ReadCulture(string language, string country)
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
    /// <returns>The read entry.</returns>
    public IccMultiProcessElementsTagDataEntry ReadMultiProcessElementsTagDataEntry()
    {
        int start = this.currentIndex - 8;

        this.ReadUInt16();
        this.ReadUInt16();
        uint elementCount = this.ReadUInt32();

        this.EnsureAvailable(elementCount, 8);
        IccPositionNumber[] positionTable = new IccPositionNumber[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            positionTable[i] = this.ReadPositionNumber();
        }

        IccMultiProcessElement[] elements = new IccMultiProcessElement[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            elements[i] = this.ReadBounded(
                start,
                positionTable[i].Offset,
                positionTable[i].Size,
                static (reader, _) => reader.ReadMultiProcessElement());
        }

        return new IccMultiProcessElementsTagDataEntry(elements);
    }

    /// <summary>
    /// Reads a <see cref="IccNamedColor2TagDataEntry"/>
    /// </summary>
    /// <returns>The read entry.</returns>
    public IccNamedColor2TagDataEntry ReadNamedColor2TagDataEntry()
    {
        int vendorFlag = this.ReadInt32();
        uint colorCount = this.ReadUInt32();
        uint coordCount = this.ReadUInt32();
        string prefix = this.ReadAsciiString(32);
        string suffix = this.ReadAsciiString(32);

        this.EnsureAvailable(colorCount, 38 + (2UL * coordCount));
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
    public IccParametricCurveTagDataEntry ReadParametricCurveTagDataEntry() => new(this.ReadParametricCurve());

    /// <summary>
    /// Reads a <see cref="IccProfileSequenceDescTagDataEntry"/>
    /// </summary>
    /// <returns>The read entry.</returns>
    public IccProfileSequenceDescTagDataEntry ReadProfileSequenceDescTagDataEntry()
    {
        uint count = this.ReadUInt32();
        this.EnsureAvailable(count, 36);
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
    /// <returns>The read entry.</returns>
    public IccProfileSequenceIdentifierTagDataEntry ReadProfileSequenceIdentifierTagDataEntry()
    {
        int start = this.currentIndex - 8; // 8 is the tag header size
        uint count = this.ReadUInt32();
        this.EnsureAvailable(count, 8);
        IccPositionNumber[] table = new IccPositionNumber[count];
        for (int i = 0; i < count; i++)
        {
            table[i] = this.ReadPositionNumber();
        }

        IccProfileSequenceIdentifier[] entries = new IccProfileSequenceIdentifier[count];
        for (int i = 0; i < count; i++)
        {
            entries[i] = this.ReadBounded(
                start,
                table[i].Offset,
                table[i].Size,
                static (reader, _) =>
                {
                    IccProfileId id = reader.ReadProfileId();
                    reader.ReadCheckTagDataEntryHeader(IccTypeSignature.MultiLocalizedUnicode);
                    IccMultiLocalizedUnicodeTagDataEntry description = reader.ReadMultiLocalizedUnicodeTagDataEntry();
                    return new IccProfileSequenceIdentifier(id, description.Texts);
                });
        }

        return new IccProfileSequenceIdentifierTagDataEntry(entries);
    }

    /// <summary>
    /// Reads a <see cref="IccResponseCurveSet16TagDataEntry"/>
    /// </summary>
    /// <returns>The read entry.</returns>
    public IccResponseCurveSet16TagDataEntry ReadResponseCurveSet16TagDataEntry()
    {
        int start = this.currentIndex - 8; // 8 is the tag header size
        ushort channelCount = this.ReadUInt16();
        ushort measurementCount = this.ReadUInt16();

        this.EnsureAvailable(measurementCount, sizeof(uint));
        uint[] offset = new uint[measurementCount];
        for (int i = 0; i < measurementCount; i++)
        {
            offset[i] = this.ReadUInt32();
        }

        IccResponseCurve[] curves = new IccResponseCurve[measurementCount];
        for (int i = 0; i < measurementCount; i++)
        {
            this.Seek(start, offset[i]);
            curves[i] = this.ReadResponseCurve(channelCount);
        }

        return new IccResponseCurveSet16TagDataEntry(curves);
    }

    /// <summary>
    /// Reads a <see cref="IccFix16ArrayTagDataEntry"/>
    /// </summary>
    /// <param name="size">The size of the entry in bytes.</param>
    /// <returns>The read entry.</returns>
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
    /// <returns>The read entry.</returns>
    public IccSignatureTagDataEntry ReadSignatureTagDataEntry() => new(this.ReadAsciiString(4));

    /// <summary>
    /// Reads a <see cref="IccTextTagDataEntry"/>
    /// </summary>
    /// <param name="size">The size of the entry in bytes.</param>
    /// <returns>The read entry.</returns>
    public IccTextTagDataEntry ReadTextTagDataEntry(uint size) => new(this.ReadAsciiString((int)size - 8)); // 8 is the tag header size

    /// <summary>
    /// Reads a <see cref="IccUFix16ArrayTagDataEntry"/>
    /// </summary>
    /// <param name="size">The size of the entry in bytes.</param>
    /// <returns>The read entry.</returns>
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
    /// <param name="size">The size of the entry in bytes.</param>
    /// <returns>The read entry.</returns>
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
    /// <param name="size">The size of the entry in bytes.</param>
    /// <returns>The read entry.</returns>
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
    /// <param name="size">The size of the entry in bytes.</param>
    /// <returns>The read entry.</returns>
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
    /// <param name="size">The size of the entry in bytes.</param>
    /// <returns>The read entry.</returns>
    public IccUInt8ArrayTagDataEntry ReadUInt8ArrayTagDataEntry(uint size)
    {
        int count = (int)size - 8; // 8 is the tag header size
        byte[] adata = this.ReadBytes(count);

        return new IccUInt8ArrayTagDataEntry(adata);
    }

    /// <summary>
    /// Reads a <see cref="IccViewingConditionsTagDataEntry"/>
    /// </summary>
    /// <returns>The read entry.</returns>
    public IccViewingConditionsTagDataEntry ReadViewingConditionsTagDataEntry() => new(
            illuminantXyz: this.ReadXyzNumber(),
            surroundXyz: this.ReadXyzNumber(),
            illuminant: (IccStandardIlluminant)this.ReadUInt32());

    /// <summary>
    /// Reads a <see cref="IccXyzTagDataEntry"/>
    /// </summary>
    /// <param name="size">The size of the entry in bytes.</param>
    /// <returns>The read entry.</returns>
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
    /// <returns>The read entry.</returns>
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
        uint unicodeCount = this.ReadUInt32();
        if (unicodeCount > 0)
        {
            this.EnsureAvailable(unicodeCount, sizeof(char));
            unicodeValue = this.ReadUnicodeString(((int)unicodeCount - 1) * sizeof(char));
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
    /// <returns>The read entry.</returns>
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
    /// <returns>The read entry.</returns>
    public IccScreeningTagDataEntry ReadScreeningTagDataEntry()
    {
        IccScreeningFlag flags = (IccScreeningFlag)this.ReadInt32();
        uint channelCount = this.ReadUInt32();
        this.EnsureAvailable(channelCount, 12);
        IccScreeningChannel[] channels = new IccScreeningChannel[channelCount];
        for (int i = 0; i < channels.Length; i++)
        {
            channels[i] = this.ReadScreeningChannel();
        }

        return new IccScreeningTagDataEntry(flags, channels);
    }

    /// <summary>
    /// Reads a <see cref="IccUcrBgTagDataEntry"/>
    /// </summary>
    /// <param name="size">The size of the entry in bytes.</param>
    /// <returns>The read entry</returns>
    public IccUcrBgTagDataEntry ReadUcrBgTagDataEntry(uint size)
    {
        uint ucrCount = this.ReadUInt32();
        this.EnsureAvailable(ucrCount, sizeof(ushort));
        ushort[] ucrCurve = new ushort[ucrCount];
        for (int i = 0; i < ucrCurve.Length; i++)
        {
            ucrCurve[i] = this.ReadUInt16();
        }

        uint bgCount = this.ReadUInt32();
        this.EnsureAvailable(bgCount, sizeof(ushort));
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
