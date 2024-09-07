// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Provides methods to write ICC data types
/// </summary>
internal sealed partial class IccDataWriter
{
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
    public int WriteVersionNumber(in IccVersion value)
    {
        int major = Numerics.Clamp(value.Major, 0, byte.MaxValue);
        int minor = Numerics.Clamp(value.Minor, 0, 15);
        int bugfix = Numerics.Clamp(value.Patch, 0, 15);

        int version = (major << 24) | (minor << 20) | (bugfix << 16);
        return this.WriteInt32(version);
    }

    /// <summary>
    /// Writes an XYZ number
    /// </summary>
    /// <param name="value">The value to write</param>
    /// <returns>the number of bytes written</returns>
    public int WriteXyzNumber(Vector3 value)
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
    public int WriteProfileId(in IccProfileId value)
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
    public int WritePositionNumber(in IccPositionNumber value)
    {
        return this.WriteUInt32(value.Offset)
             + this.WriteUInt32(value.Size);
    }

    /// <summary>
    /// Writes a response number
    /// </summary>
    /// <param name="value">The value to write</param>
    /// <returns>the number of bytes written</returns>
    public int WriteResponseNumber(in IccResponseNumber value)
    {
        return this.WriteUInt16(value.DeviceCode)
             + this.WriteFix16(value.MeasurementValue);
    }

    /// <summary>
    /// Writes a named color
    /// </summary>
    /// <param name="value">The value to write</param>
    /// <returns>the number of bytes written</returns>
    public int WriteNamedColor(in IccNamedColor value)
    {
        return this.WriteAsciiString(value.Name, 32, true)
             + this.WriteArray(value.PcsCoordinates)
             + this.WriteArray(value.DeviceCoordinates);
    }

    /// <summary>
    /// Writes a profile description
    /// </summary>
    /// <param name="value">The value to write</param>
    /// <returns>the number of bytes written</returns>
    public int WriteProfileDescription(in IccProfileDescription value)
    {
        return this.WriteUInt32(value.DeviceManufacturer)
             + this.WriteUInt32(value.DeviceModel)
             + this.WriteInt64((long)value.DeviceAttributes)
             + this.WriteUInt32((uint)value.TechnologyInformation)
             + this.WriteTagDataEntryHeader(IccTypeSignature.MultiLocalizedUnicode)
             + this.WriteMultiLocalizedUnicodeTagDataEntry(new(value.DeviceManufacturerInfo))
             + this.WriteTagDataEntryHeader(IccTypeSignature.MultiLocalizedUnicode)
             + this.WriteMultiLocalizedUnicodeTagDataEntry(new(value.DeviceModelInfo));
    }

    /// <summary>
    /// Writes a screening channel
    /// </summary>
    /// <param name="value">The value to write</param>
    /// <returns>the number of bytes written</returns>
    public int WriteScreeningChannel(in IccScreeningChannel value)
    {
        return this.WriteFix16(value.Frequency)
             + this.WriteFix16(value.Angle)
             + this.WriteInt32((int)value.SpotShape);
    }
}
