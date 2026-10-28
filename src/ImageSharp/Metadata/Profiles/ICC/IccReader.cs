// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Reads ICC profiles from complete or segmented data.
/// </summary>
internal sealed class IccReader
{
    // The smallest complete profile has a 128-byte header and a 4-byte tag count.
    private readonly ArrayBufferWriter<byte> profileData = new(132);
    private readonly int maxLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="IccReader"/> class with the maximum ICC profile length.
    /// </summary>
    public IccReader()
        : this(IccProfile.MaxProfileSize - 1)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IccReader"/> class with a maximum retained profile length.
    /// </summary>
    /// <param name="maxLength">The maximum number of profile bytes to retain.</param>
    public IccReader(int maxLength)
    {
        Guard.MustBeGreaterThanOrEqualTo(maxLength, 0, nameof(maxLength));
        this.maxLength = Math.Min(maxLength, IccProfile.MaxProfileSize - 1);
    }

    /// <summary>
    /// Appends up to the requested number of bytes, returning the count actually read.
    /// </summary>
    /// <param name="stream">The stream positioned at the segment payload.</param>
    /// <param name="length">The segment payload length.</param>
    /// <returns>The number of bytes appended.</returns>
    public int Read(Stream stream, int length)
    {
        if ((uint)length > (uint)(this.maxLength - this.profileData.WrittenCount))
        {
            // Bound the accumulated length before growing storage for this segment.
            throw new InvalidIccProfileException("Invalid ICC profile.");
        }

        int totalRead = 0;
        while (totalRead < length)
        {
            Span<byte> destination = this.profileData.GetSpan(1);
            int read = stream.Read(destination[..Math.Min(destination.Length, length - totalRead)]);
            if (read == 0)
            {
                break;
            }

            this.profileData.Advance(read);
            totalRead += read;
        }

        return totalRead;
    }

    /// <summary>
    /// Creates a validated profile from the accumulated data without clearing the reader.
    /// </summary>
    /// <returns>The validated profile.</returns>
    public IccProfile ToProfile()
    {
        ReadOnlySpan<byte> data = this.profileData.WrittenSpan;

        // Even a profile with no tags requires the 128-byte ICC header and the 4-byte tag count.
        if (data.Length < 132)
        {
            throw new InvalidIccProfileException("Invalid ICC profile.");
        }

        uint declaredSize = BinaryPrimitives.ReadUInt32BigEndian(data);

        // The declared size must also include the header and tag count and fit within the bytes read.
        if (declaredSize < 132 || declaredSize > data.Length || declaredSize >= IccProfile.MaxProfileSize ||
            !IsTagTableValid(data[..(int)declaredSize]))
        {
            throw new InvalidIccProfileException("Invalid ICC profile.");
        }

        // Retain only the declared profile bytes so lazy tag parsing uses the same boundary as validation.
        IccProfile profile = new(data[..(int)declaredSize].ToArray());
        if (!profile.CheckIsValid())
        {
            throw new InvalidIccProfileException("Invalid ICC profile.");
        }

        return profile;
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

        if (!IsTagTableValid(data))
        {
            throw new InvalidIccProfileException("Invalid ICC profile tag table.");
        }

        IccDataReader reader = new(data);
        return ReadTagData(reader);
    }

    internal static IccProfileHeader ReadHeader(IccDataReader reader)
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

    internal static IccTagDataEntry[] ReadTagData(IccDataReader reader)
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

        return [.. entries];
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
        for (int i = 0; i < tagCount; i++)
        {
            uint tagSignature = reader.ReadUInt32();
            uint tagOffset = reader.ReadUInt32();
            uint tagSize = reader.ReadUInt32();

            // Exclude entries that have nonsense values and could cause exceptions further on
            if (tagSize >= 8 && tagOffset <= dataLength && tagSize <= dataLength - tagOffset)
            {
                table.Add(new IccTagTableEntry((IccProfileTag)tagSignature, tagOffset, tagSize));
            }
        }

        return [.. table];
    }

    /// <summary>
    /// Checks that the count and complete fixed-size tag table fit in the payload.
    /// </summary>
    /// <param name="data">The raw profile bytes.</param>
    /// <returns>Whether the tag table is complete.</returns>
    private static bool IsTagTableValid(ReadOnlySpan<byte> data)
    {
        // The tag count follows the 128-byte header. Each table entry is 12 bytes: signature, offset, and size.
        // Divide the bytes remaining after the 132-byte prefix to bound the count without multiplication overflow.
        return data.Length >= 132 && BinaryPrimitives.ReadUInt32BigEndian(data[128..]) <= (uint)((data.Length - 132) / 12);
    }
}
