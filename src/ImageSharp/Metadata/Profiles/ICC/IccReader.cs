// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Reads and parses ICC data from a byte array
/// </summary>
internal sealed class IccReader
{
    /// <summary>
    /// Reads an ICC profile
    /// </summary>
    /// <param name="data">The raw ICC data</param>
    /// <returns>The read ICC profile</returns>
    public static IccProfile Read(byte[] data)
    {
        Guard.NotNull(data, nameof(data));
        Guard.IsTrue(data.Length >= 128, nameof(data), "Data length must be at least 128 to be a valid ICC profile");

        IccDataReader reader = new(data);
        IccProfileHeader header = ReadHeader(reader);
        IccTagDataEntry[] tagData = ReadTagData(reader);

        return new(header, tagData);
    }

    /// <summary>
    /// Reads an ICC profile header
    /// </summary>
    /// <param name="data">The raw ICC data</param>
    /// <returns>The read ICC profile header</returns>
    public static IccProfileHeader ReadHeader(byte[] data)
    {
        Guard.NotNull(data, nameof(data));
        Guard.IsTrue(data.Length >= 128, nameof(data), "Data length must be at least 128 to be a valid profile header");

        IccDataReader reader = new(data);
        return ReadHeader(reader);
    }

    /// <summary>
    /// Reads the ICC profile tag data
    /// </summary>
    /// <param name="data">The raw ICC data</param>
    /// <returns>The read ICC profile tag data</returns>
    public static IccTagDataEntry[] ReadTagData(byte[] data)
    {
        Guard.NotNull(data, nameof(data));
        Guard.IsTrue(data.Length >= 128, nameof(data), "Data length must be at least 128 to be a valid ICC profile");

        IccDataReader reader = new(data);
        return ReadTagData(reader);
    }

    private static IccProfileHeader ReadHeader(IccDataReader reader)
    {
        reader.SetIndex(0);

        return new()
        {
            Size = reader.ReadUInt32(),
            CmmType = reader.ReadAsciiString(4),
            Version = reader.ReadVersionNumber(),
            Class = (IccProfileClass)reader.ReadUInt32(),
            DataColorSpace = (IccColorSpaceType)reader.ReadUInt32(),
            ProfileConnectionSpace = (IccColorSpaceType)reader.ReadUInt32(),
            CreationDate = reader.ReadDateTime(),
            FileSignature = reader.ReadAsciiString(4),
            PrimaryPlatformSignature = (IccPrimaryPlatformType)reader.ReadUInt32(),
            Flags = (IccProfileFlag)reader.ReadInt32(),
            DeviceManufacturer = reader.ReadUInt32(),
            DeviceModel = reader.ReadUInt32(),
            DeviceAttributes = (IccDeviceAttribute)reader.ReadInt64(),
            RenderingIntent = (IccRenderingIntent)reader.ReadUInt32(),
            PcsIlluminant = reader.ReadXyzNumber(),
            CreatorSignature = reader.ReadAsciiString(4),
            Id = reader.ReadProfileId(),
        };
    }

    private static IccTagDataEntry[] ReadTagData(IccDataReader reader)
    {
        IccTagTableEntry[] tagTable = ReadTagTable(reader);
        List<IccTagDataEntry> entries = new(tagTable.Length);
        Dictionary<uint, IccTagDataEntry> store = new();

        foreach (IccTagTableEntry tag in tagTable)
        {
            IccTagDataEntry entry;
            if (store.TryGetValue(tag.Offset, out IccTagDataEntry? value))
            {
                entry = value;
            }
            else
            {
                try
                {
                    entry = reader.ReadTagDataEntry(tag);
                }
                catch
                {
                    // Ignore tags that could not be read
                    continue;
                }

                store.Add(tag.Offset, entry);
            }

            entry.TagSignature = tag.Signature;
            entries.Add(entry);
        }

        return entries.ToArray();
    }

    private static IccTagTableEntry[] ReadTagTable(IccDataReader reader)
    {
        reader.SetIndex(128);   // An ICC header is 128 bytes long

        uint tagCount = reader.ReadUInt32();

        // Prevent creating huge arrays because of corrupt profiles.
        // A normal profile usually has 5-15 entries
        if (tagCount > 100)
        {
            return Array.Empty<IccTagTableEntry>();
        }

        List<IccTagTableEntry> table = new((int)tagCount);
        for (int i = 0; i < tagCount; i++)
        {
            uint tagSignature = reader.ReadUInt32();
            uint tagOffset = reader.ReadUInt32();
            uint tagSize = reader.ReadUInt32();

            // Exclude entries that have nonsense values and could cause exceptions further on
            if (tagOffset < reader.DataLength && tagSize < reader.DataLength - 128)
            {
                table.Add(new((IccProfileTag)tagSignature, tagOffset, tagSize));
            }
        }

        return table.ToArray();
    }
}
