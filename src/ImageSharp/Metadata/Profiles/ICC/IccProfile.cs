// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Security.Cryptography;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Represents an ICC profile
/// </summary>
public sealed partial class IccProfile : IDeepCloneable<IccProfile>
{
    /// <summary>
    /// The exclusive maximum declared ICC profile size accepted during validation.
    /// </summary>
    internal const int MaxProfileSize = 50_000_000;

    /// <summary>
    /// The byte array to read the ICC profile from
    /// </summary>
    private readonly byte[] data;

    /// <summary>
    /// The backing file for the <see cref="Entries"/> property
    /// </summary>
    private IccTagDataEntry[] entries;

    /// <summary>
    /// ICC profile header
    /// </summary>
    private IccProfileHeader header;

    /// <summary>
    /// Initializes a new instance of the <see cref="IccProfile"/> class.
    /// </summary>
    public IccProfile()
        : this((byte[])null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IccProfile"/> class.
    /// </summary>
    /// <param name="data">The raw ICC profile data, or null to create a profile from entries.</param>
    /// <exception cref="ArgumentException">The supplied data does not contain a complete ICC header.</exception>
    public IccProfile(byte[] data)
    {
        // A null array creates an entry-based profile. Supplied bytes must contain the fixed header,
        // but parsing stays lazy so retaining a profile does not allocate header or tag objects.
        if (data != null)
        {
            IccReader.ValidateHeader(data);
        }

        this.data = data;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IccProfile"/> class.
    /// </summary>
    /// <param name="header">The profile header</param>
    /// <param name="entries">The actual profile data</param>
    internal IccProfile(IccProfileHeader header, IccTagDataEntry[] entries)
    {
        this.header = header ?? throw new ArgumentNullException(nameof(header));
        this.entries = entries ?? throw new ArgumentNullException(nameof(entries));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IccProfile"/> class
    /// by making a copy from another ICC profile.
    /// </summary>
    /// <param name="other">The other ICC profile, where the clone should be made from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>>
    private IccProfile(IccProfile other)
    {
        Guard.NotNull(other, nameof(other));

        this.data = other.ToByteArray();
        this.HasValidatedData = other.HasValidatedData;
        this.WasRecovered = other.WasRecovered;
        this.IsUnusableForDecoding = other.IsUnusableForDecoding;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the decoder recovered the profile.
    /// </summary>
    /// <remarks>The decoder uses recovery when profile bytes are missing or a tag range is invalid.</remarks>
    internal bool WasRecovered { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether decoding rejected this recovered profile.
    /// </summary>
    /// <remarks>
    /// This lets the decoder omit the profile from output metadata and skip it in later strips or frames.
    /// </remarks>
    internal bool IsUnusableForDecoding { get; set; }

    /// <summary>
    /// Gets or sets the profile header
    /// </summary>
    public IccProfileHeader Header
    {
        get
        {
            this.InitializeHeader();
            return this.header;
        }

        set => this.header = value;
    }

    /// <summary>
    /// Gets the actual profile data
    /// </summary>
    public IccTagDataEntry[] Entries
    {
        get
        {
            this.InitializeEntries();
            return this.entries;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether ImageSharp owns bytes already checked by the reader or produced by the writer.
    /// </summary>
    internal bool HasValidatedData { get; set; }

    /// <summary>
    /// Copies a valid profile for metadata. The copy owns its validated bytes.
    /// </summary>
    /// <returns>The owned copy, or null when the source profile is invalid.</returns>
    internal IccProfile CloneForMetadata()
    {
        if (!this.Validate())
        {
            return null;
        }

        return new IccProfile(this) { HasValidatedData = true };
    }

    /// <inheritdoc/>
    public IccProfile DeepClone() => new(this);

    /// <summary>
    /// Calculates the MD5 hash value of an ICC profile
    /// </summary>
    /// <param name="data">The data of which to calculate the hash value</param>
    /// <returns>The calculated hash</returns>
    public static IccProfileId CalculateHash(byte[] data)
    {
        IccReader.ValidateHeader(data);

        const int profileFlagPos = 44;
        const int renderingIntentPos = 64;
        const int profileIdPos = 84;

        // need to copy some values because they need to be zero for the hashing
        Span<byte> temp = stackalloc byte[24];
        data.AsSpan(profileFlagPos, 4).CopyTo(temp);
        data.AsSpan(renderingIntentPos, 4).CopyTo(temp[4..]);
        data.AsSpan(profileIdPos, 16).CopyTo(temp[8..]);

        try
        {
            // Zero out some values
            Array.Clear(data, profileFlagPos, 4);
            Array.Clear(data, renderingIntentPos, 4);
            Array.Clear(data, profileIdPos, 16);

            // Calculate hash
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
            byte[] hash = MD5.HashData(data);
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms

            // Read values from hash
            IccDataReader reader = new(hash);
            return reader.ReadProfileId();
        }
        finally
        {
            temp[..4].CopyTo(data.AsSpan(profileFlagPos));
            temp.Slice(4, 4).CopyTo(data.AsSpan(renderingIntentPos));
            temp.Slice(8, 16).CopyTo(data.AsSpan(profileIdPos));
        }
    }

    /// <summary>
    /// Selects the first valid ICC profile for encoding.
    /// </summary>
    /// <param name="preferred">The profile to check first.</param>
    /// <param name="fallback">The profile to check if the preferred profile is absent or invalid.</param>
    /// <returns>The validated profile, or null if neither profile is valid.</returns>
    internal static IccProfile SelectValidProfile(IccProfile preferred, IccProfile fallback)
    {
        if (preferred?.Validate() == true)
        {
            return preferred;
        }

        return !ReferenceEquals(preferred, fallback) && fallback?.Validate() == true ? fallback : null;
    }

    /// <summary>
    /// Checks that the ICC color space and rendering intent codes have defined values.
    /// </summary>
    /// <param name="dataColorSpace">The profile color space.</param>
    /// <param name="connectionColorSpace">The profile connection color space.</param>
    /// <param name="renderingIntent">The rendering intent.</param>
    /// <returns>Whether all three codes have defined values.</returns>
    internal static bool HasValidHeaderFields(
        IccColorSpaceType dataColorSpace,
        IccColorSpaceType connectionColorSpace,
        IccRenderingIntent renderingIntent)
        => Enum.IsDefined(dataColorSpace) && Enum.IsDefined(connectionColorSpace) && Enum.IsDefined(renderingIntent);

    /// <summary>
    /// Checks for signs of a corrupt profile.
    /// </summary>
    /// <remarks>This is not an absolute proof of validity but should weed out most corrupt data.</remarks>
    /// <returns>True if the profile is valid; False otherwise</returns>
    public bool CheckIsValid()
    {
        bool arrayValid = true;
        if (this.data != null)
        {
            arrayValid = this.data.Length >= this.Header.Size;
        }

        return arrayValid && this.HasValidHeader();
    }

    /// <summary>
    /// Checks that the profile contains tags and that its header and tag ranges are valid.
    /// </summary>
    /// <returns>Whether the profile can be used as image metadata.</returns>
    /// <remarks>Caller-owned arrays are checked each time because their contents can change.</remarks>
    internal bool Validate()
        => this.data == null
            ? this.entries?.Length > 0
            : this.HasValidatedData || IccReader.ValidateForMetadata(this.data);

    /// <summary>
    /// Checks the declared header fields independently of the available tag data.
    /// </summary>
    /// <returns>Whether the declared size is in range and the color spaces and rendering intent have known values.</returns>
    internal bool HasValidHeader()
        => this.Header.Size is >= 128 and < MaxProfileSize &&
            HasValidHeaderFields(this.Header.DataColorSpace, this.Header.ProfileConnectionSpace, this.Header.RenderingIntent);

    /// <summary>
    /// Converts this instance to a byte array.
    /// </summary>
    /// <returns>The <see cref="T:byte[]"/></returns>
    public byte[] ToByteArray()
    {
        if (this.data != null)
        {
            byte[] copy = new byte[this.data.Length];
            Buffer.BlockCopy(this.data, 0, copy, 0, copy.Length);
            return copy;
        }

        return IccWriter.Write(this);
    }

    /// <summary>
    /// Gets encoded profile bytes for consumers that only read them.
    /// </summary>
    /// <returns>The retained bytes, or a new serialization when the profile was constructed from entries.</returns>
    internal ReadOnlyMemory<byte> GetData()
    {
        // Decoded profiles own their encoded bytes. A read-only view keeps those bytes alive without copying them.
        // Entry-based profiles must be serialized on each request because callers can change their header and entries.
        return this.data ?? IccWriter.Write(this);
    }

    private void InitializeHeader()
    {
        if (this.header != null)
        {
            return;
        }

        if (this.data is null)
        {
            this.header = new IccProfileHeader();
            return;
        }

        this.header = IccReader.ReadHeader(this.data);
    }

    private void InitializeEntries()
    {
        if (this.entries != null)
        {
            return;
        }

        if (this.data is null)
        {
            this.entries = [];
            return;
        }

        this.entries = IccReader.ReadTagData(this.data);
    }
}
