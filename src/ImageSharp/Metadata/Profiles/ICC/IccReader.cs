// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Reads ICC profiles from complete or segmented data.
/// </summary>
internal sealed class IccReader
{
    private readonly int maxLength;
    private byte[]? profileData;
    private int written;

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
    /// Appends a complete ICC segment of known length.
    /// </summary>
    /// <param name="stream">The seekable stream positioned at the segment.</param>
    /// <param name="length">The declared segment length.</param>
    /// <returns>Whether the complete segment was read.</returns>
    public bool TryReadSegment(Stream stream, int length)
    {
        this.ValidateReadLength(length);
        return length == 0 || stream.TryAppendExactly(ref this.profileData, ref this.written, length, this.maxLength);
    }

    /// <summary>
    /// Appends up to the requested number of bytes from a stream whose length may be unknown.
    /// </summary>
    /// <param name="stream">The stream positioned at the profile data.</param>
    /// <param name="length">The maximum number of bytes to read.</param>
    /// <returns>The number of bytes appended.</returns>
    public int Read(Stream stream, int length)
    {
        this.ValidateReadLength(length);

        int totalRead = 0;
        Span<byte> next = stackalloc byte[1];
        while (totalRead < length)
        {
            if (this.profileData == null || this.written == this.profileData.Length)
            {
                // Check for EOF before growing. A profile that exactly fills the array can keep it.
                if (stream.Read(next) == 0)
                {
                    break;
                }

                // The smallest complete profile has a 128-byte header and a 4-byte tag count.
                int required = this.written + Math.Min(132, length - totalRead);
                this.profileData = StreamExtensions.EnsureReadBufferCapacity(this.profileData, this.written, required, this.maxLength);
                this.profileData[this.written++] = next[0];
                totalRead++;
                continue;
            }

            int count = Math.Min(this.profileData.Length - this.written, length - totalRead);
            int read = stream.Read(this.profileData.AsSpan(this.written, count));
            if (read == 0)
            {
                break;
            }

            this.written += read;
            totalRead += read;
        }

        return totalRead;
    }

    /// <summary>
    /// Creates a validated profile from the accumulated data without clearing the reader.
    /// </summary>
    /// <returns>The validated profile.</returns>
    public IccProfile ToProfile() => this.ToProfile(allowTruncated: false);

    /// <summary>
    /// Creates a profile from the accumulated data using the decoder's integrity policy.
    /// </summary>
    /// <param name="allowTruncated">Whether to recover a profile with missing bytes or invalid tag ranges.</param>
    /// <returns>The profile bounded by the available bytes.</returns>
    public IccProfile ToProfile(bool allowTruncated)
        => ToProfile(this.profileData.AsSpan(0, this.written), this.profileData, allowTruncated);

    /// <summary>
    /// Creates a validated profile from a complete, caller-owned byte array.
    /// </summary>
    /// <param name="data">The complete profile bytes. The caller must not change these bytes after this call.</param>
    /// <returns>The validated profile, retaining the array when it contains exactly the declared profile.</returns>
    /// <remarks>Trailing bytes are excluded from the retained profile. This requires a copy only when trailing bytes are present.</remarks>
    public static IccProfile ToProfile(byte[] data) => ToProfile(data, allowTruncated: false);

    /// <summary>
    /// Creates a profile from a complete or truncated array using the decoder's integrity policy.
    /// </summary>
    /// <param name="data">The owned profile bytes.</param>
    /// <param name="allowTruncated">Whether to recover a profile with missing bytes or invalid tag ranges.</param>
    /// <returns>The profile bounded by the available bytes.</returns>
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
    /// Checks that raw ICC data contains the complete fixed header without parsing it.
    /// </summary>
    /// <param name="data">The raw ICC profile data.</param>
    /// <exception cref="ArgumentNullException">The data is null.</exception>
    /// <exception cref="ArgumentException">The data does not contain a complete ICC header.</exception>
    public static void ValidateHeader(byte[] data)
    {
        Guard.NotNull(data, nameof(data));
        Guard.IsTrue(data.Length >= 128, nameof(data), "Data length must be at least 128 to be a valid profile header");
    }

    /// <summary>
    /// Reads an ICC profile header
    /// </summary>
    /// <param name="data">The raw ICC data</param>
    /// <returns>The read ICC profile header</returns>
    public static IccProfileHeader ReadHeader(byte[] data)
    {
        ValidateHeader(data);

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
        ValidateHeader(data);

        if (!IsTagTableValid(data, false, out _))
        {
            throw new InvalidIccProfileException("Invalid ICC profile tag table.");
        }

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

    /// <summary>
    /// Reads the fixed ICC header through the existing data reader.
    /// </summary>
    /// <param name="reader">The reader for the validated profile data.</param>
    /// <returns>The decoded profile header.</returns>
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

    /// <summary>
    /// Reads supported tags from the validated ICC tag table.
    /// </summary>
    /// <param name="reader">The reader for the validated profile data.</param>
    /// <returns>The tags that the existing data reader can decode.</returns>
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

        return [.. table];
    }

    /// <summary>
    /// Checks profile-length and tag-table boundaries before retaining profile bytes.
    /// </summary>
    /// <param name="data">The available profile bytes, which can include trailing data.</param>
    /// <param name="allowTruncated">Whether the declared profile may extend beyond the available bytes.</param>
    /// <param name="tagCount">The number of entries in the bounded tag table.</param>
    /// <returns>The usable profile length.</returns>
    private static int GetValidatedProfileLength(ReadOnlySpan<byte> data, bool allowTruncated, out uint tagCount)
    {
        if (!TryGetProfileLength(data, allowTruncated, out int length, out tagCount))
        {
            throw new InvalidIccProfileException("Invalid ICC profile.");
        }

        return length;
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
        int length = GetValidatedProfileLength(data, allowTruncated, out uint tagCount);

        // Retain an exact array. If the buffer has spare capacity or trailing data, copy only the profile bytes.
        return CreateProfile(ownedData != null && length == ownedData.Length ? ownedData : data[..length].ToArray(), allowTruncated, tagCount);
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
        return size < IccProfile.MaxProfileSize && (allowTruncated || size <= data.Length) &&
            IsTagTableValid(data[..length], true, out tagCount);
    }

    /// <summary>
    /// Creates a profile from validated length and table boundaries, then checks its parsed header.
    /// </summary>
    /// <param name="data">The owned array bounded by the profile extent.</param>
    /// <param name="allowTruncated">Whether to recover profiles with incomplete tags.</param>
    /// <param name="tagCount">The number of entries in the bounded tag table.</param>
    /// <returns>The profile with a valid header.</returns>
    private static IccProfile CreateProfile(byte[] data, bool allowTruncated, uint tagCount)
    {
        IccProfile profile = new(data);
        if (!profile.HasValidHeader())
        {
            throw new InvalidIccProfileException("Invalid ICC profile.");
        }

        if (profile.Header.Size > data.Length || !AreTagRangesValid(data, tagCount))
        {
            if (!allowTruncated)
            {
                throw new InvalidIccProfileException("An ICC tag is outside the profile data.");
            }

            // The decoder tries its own transform if it needs the remaining tags.
            profile.WasRecovered = true;
        }

        profile.HasValidatedData = true;
        return profile;
    }

    /// <summary>
    /// Checks that the count and complete fixed-size tag table fit in the payload.
    /// </summary>
    /// <param name="data">The raw profile bytes.</param>
    /// <param name="requireTags">Whether the table must contain at least one tag.</param>
    /// <param name="tagCount">The number of entries in the bounded tag table.</param>
    /// <returns>Whether the tag table is complete.</returns>
    private static bool IsTagTableValid(ReadOnlySpan<byte> data, bool requireTags, out uint tagCount)
    {
        // The tag count follows the 128-byte header. Each table entry is 12 bytes: signature, offset, and size.
        tagCount = 0;
        if (data.Length < 132)
        {
            return false;
        }

        // Divide the bytes remaining after the 132-byte prefix to bound the count without multiplication overflow.
        tagCount = BinaryPrimitives.ReadUInt32BigEndian(data[128..]);
        return (!requireTags || tagCount > 0) && tagCount <= (uint)((data.Length - 132) / 12);
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

    /// <summary>
    /// Checks that the request fits the remaining ICC buffer limit before consuming input.
    /// </summary>
    /// <param name="length">The requested byte count.</param>
    private void ValidateReadLength(int length)
    {
        if ((uint)length > (uint)(this.maxLength - this.written))
        {
            throw new InvalidIccProfileException("Invalid ICC profile.");
        }
    }
}
