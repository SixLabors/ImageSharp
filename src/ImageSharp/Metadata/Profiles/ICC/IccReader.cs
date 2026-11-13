// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Reads and parses ICC data from a byte array
/// </summary>
internal sealed class IccReader
{
    private byte[]? profileData;
    private int written;

    /// <summary>
    /// Appends a complete ICC segment.
    /// </summary>
    /// <param name="stream">The seekable stream positioned at the segment.</param>
    /// <param name="length">The segment length.</param>
    /// <returns>Whether the complete segment was read.</returns>
    public bool TryReadSegment(Stream stream, int length)
        => length == 0 || stream.TryAppendExactly(ref this.profileData, ref this.written, length, IccProfile.MaxProfileSize - 1);

    /// <summary>
    /// Reads the assembled profile using the same bounds as a complete packet.
    /// </summary>
    /// <param name="allowTruncated">Whether to recover an incomplete profile.</param>
    /// <returns>The assembled ICC profile.</returns>
    public IccProfile ToProfile(bool allowTruncated)
        => ToProfile(this.profileData.AsSpan(0, this.written), this.profileData, allowTruncated);

    /// <summary>
    /// Reads a complete ICC payload before applying the shared profile checks.
    /// </summary>
    /// <param name="stream">The input positioned at the profile.</param>
    /// <param name="length">The declared payload length.</param>
    /// <param name="allowTruncated">Whether to recover an incomplete profile.</param>
    /// <param name="profile">The ICC profile, or null if the profile was ignored.</param>
    /// <returns>Whether the complete payload was read.</returns>
    public static bool TryReadProfile(Stream stream, long length, bool allowTruncated, out IccProfile? profile)
    {
        profile = null;
        if (length >= IccProfile.MaxProfileSize || !stream.TryReadExactly(length, out byte[]? data))
        {
            return false;
        }

        profile = ToProfile(data, allowTruncated);
        return true;
    }

    /// <summary>
    /// Creates a profile from decoded image metadata if the header and tag table are complete.
    /// </summary>
    /// <param name="data">The profile bytes supplied by the decoder.</param>
    /// <param name="allowTruncated">Whether to recover a profile with missing bytes or invalid tag ranges.</param>
    /// <returns>The profile bounded by the available bytes.</returns>
    /// <exception cref="InvalidIccProfileException">The profile is invalid.</exception>
    public static IccProfile ToProfile(byte[] data, bool allowTruncated)
        => ToProfile(data, data, allowTruncated);

    /// <summary>
    /// Validates borrowed bytes and copies the profile into an array it can keep.
    /// </summary>
    /// <param name="data">The profile bytes owned by the caller.</param>
    /// <param name="allowTruncated">Whether to recover an incomplete profile.</param>
    /// <returns>The profile bounded by the available bytes.</returns>
    public static IccProfile ReadProfile(ReadOnlySpan<byte> data, bool allowTruncated)
        => ToProfile(data, null, allowTruncated);

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

        return new IccProfile(header, tagData);
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

    /// <summary>
    /// Checks the header and tag ranges of a profile supplied to a metadata setter.
    /// </summary>
    /// <param name="data">The caller-supplied ICC bytes.</param>
    /// <returns>Whether the full declared profile and every tag range fit.</returns>
    public static bool ValidateForMetadata(ReadOnlySpan<byte> data)
    {
        if (!TryGetProfileLength(data, false, out int length, out uint tagCount))
        {
            return false;
        }

        // The ICC header stores the two color space codes at bytes 16 and 20 and the rendering intent at byte 64.
        // Read these fields from the same bytes as the tag table without allocating a parsed header.
        return IccProfile.HasValidHeaderFields(
            (IccColorSpaceType)BinaryPrimitives.ReadUInt32BigEndian(data[16..]),
            (IccColorSpaceType)BinaryPrimitives.ReadUInt32BigEndian(data[20..]),
            (IccRenderingIntent)BinaryPrimitives.ReadUInt32BigEndian(data[64..])) &&
            AreTagRangesValid(data[..length], tagCount);
    }

    private static IccProfileHeader ReadHeader(IccDataReader reader)
    {
        reader.SetIndex(0);

        return new IccProfileHeader
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

        foreach (IccTagTableEntry tag in tagTable)
        {
            IccTagDataEntry entry;

            try
            {
                entry = reader.ReadTagDataEntry(tag);
            }
            catch
            {
                // Ignore tags that could not be read
                continue;
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
            return [];
        }

        List<IccTagTableEntry> table = new((int)tagCount);
        uint dataLength = (uint)reader.DataLength;
        uint tagDataStart = 132 + (12 * tagCount);
        for (int i = 0; i < tagCount; i++)
        {
            uint tagSignature = reader.ReadUInt32();
            uint tagOffset = reader.ReadUInt32();
            uint tagSize = reader.ReadUInt32();

            // Exclude entries that have nonsense values and could cause exceptions further on
            if (IsTagRangeValid(tagOffset, tagSize, tagDataStart, dataLength))
            {
                table.Add(new IccTagTableEntry((IccProfileTag)tagSignature, tagOffset, tagSize));
            }
        }

        return table.ToArray();
    }

    /// <summary>
    /// Validates the bytes read and retains the array when it matches the profile length.
    /// </summary>
    /// <param name="data">The bytes available to validate.</param>
    /// <param name="ownedData">The array the profile can retain, or null when the bytes are borrowed.</param>
    /// <param name="allowTruncated">Whether to recover an incomplete profile.</param>
    /// <returns>The profile bounded by the available bytes.</returns>
    private static IccProfile ToProfile(ReadOnlySpan<byte> data, byte[]? ownedData, bool allowTruncated)
    {
        if (!TryGetProfileLength(data, allowTruncated, out int length, out uint tagCount))
        {
            throw new InvalidIccProfileException("Invalid ICC profile.");
        }

        IccProfile profile = new(ownedData != null && length == ownedData.Length ? ownedData : data[..length].ToArray());
        if (!profile.HasValidHeader())
        {
            throw new InvalidIccProfileException("Invalid ICC profile.");
        }

        if (profile.Header.Size > length || !AreTagRangesValid(data[..length], tagCount))
        {
            if (!allowTruncated)
            {
                throw new InvalidIccProfileException("An ICC tag is outside the profile data.");
            }

            // A decoder that converts pixels tries the transform from the remaining tags.
            profile.WasRecovered = true;
        }

        profile.HasValidatedData = true;
        return profile;
    }

    /// <summary>
    /// Checks that the header and tag table fit in the declared and available bytes.
    /// </summary>
    /// <param name="data">The available ICC bytes.</param>
    /// <param name="allowTruncated">Whether the profile may end before its declared size.</param>
    /// <param name="length">The usable profile length in bytes.</param>
    /// <param name="tagCount">The number of entries in the bounded tag table.</param>
    /// <returns>Whether the complete tag table fits.</returns>
    private static bool TryGetProfileLength(ReadOnlySpan<byte> data, bool allowTruncated, out int length, out uint tagCount)
    {
        // The 4-byte tag count follows the 128-byte ICC header.
        length = 0;
        tagCount = 0;
        if (data.Length < 132)
        {
            return false;
        }

        uint size = BinaryPrimitives.ReadUInt32BigEndian(data);
        length = (int)Math.Min(size, (uint)data.Length);

        // Each tag table entry uses 12 bytes. Divide the available bytes before comparing the count.
        tagCount = BinaryPrimitives.ReadUInt32BigEndian(data[128..]);
        return size < IccProfile.MaxProfileSize && (allowTruncated || size <= data.Length) &&
            length >= 132 && tagCount > 0 && tagCount <= (uint)((length - 132) / 12);
    }

    /// <summary>
    /// Checks that a tag starts after the directory and fits inside the available profile.
    /// </summary>
    /// <param name="offset">The tag offset from the start of the profile.</param>
    /// <param name="size">The tag size, including its type header.</param>
    /// <param name="tagDataStart">The first byte after the directory.</param>
    /// <param name="length">The bounded profile length.</param>
    /// <returns>Whether the tag range can be read.</returns>
    private static bool IsTagRangeValid(uint offset, uint size, uint tagDataStart, uint length)
        => size >= 8 && offset >= tagDataStart && offset <= length && size <= length - offset;

    /// <summary>
    /// Checks the directory entries after the complete directory has been bounded.
    /// </summary>
    /// <param name="data">The bounded profile bytes.</param>
    /// <param name="tagCount">The number of entries in the bounded tag table.</param>
    /// <returns>Whether every tag range fits inside the profile.</returns>
    private static bool AreTagRangesValid(ReadOnlySpan<byte> data, uint tagCount)
    {
        int tagDataStart = 132 + ((int)tagCount * 12);
        for (int entry = 132; entry < tagDataStart; entry += 12)
        {
            uint offset = BinaryPrimitives.ReadUInt32BigEndian(data[(entry + 4)..]);
            uint size = BinaryPrimitives.ReadUInt32BigEndian(data[(entry + 8)..]);
            if (!IsTagRangeValid(offset, size, (uint)tagDataStart, (uint)data.Length))
            {
                return false;
            }
        }

        return true;
    }
}
