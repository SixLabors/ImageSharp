// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Security.Cryptography;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Represents an ICC profile
/// </summary>
public sealed class IccProfile : IDeepCloneable<IccProfile>
{
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
    /// <param name="data">The raw ICC profile data</param>
    public IccProfile(byte[] data) => this.data = data;

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
    }

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

    /// <inheritdoc/>
    public IccProfile DeepClone() => new(this);

    /// <summary>
    /// Calculates the MD5 hash value of an ICC profile
    /// </summary>
    /// <param name="data">The data of which to calculate the hash value</param>
    /// <returns>The calculated hash</returns>
    public static IccProfileId CalculateHash(byte[] data)
    {
        Guard.NotNull(data, nameof(data));
        Guard.IsTrue(data.Length >= 128, nameof(data), "Data length must be at least 128 to be a valid profile header");

        const int profileFlagPos = 44;
        const int renderingIntentPos = 64;
        const int profileIdPos = 84;

        // need to copy some values because they need to be zero for the hashing
        Span<byte> temp = stackalloc byte[24];
        data.AsSpan(profileFlagPos, 4).CopyTo(temp);
        data.AsSpan(renderingIntentPos, 4).CopyTo(temp.Slice(4));
        data.AsSpan(profileIdPos, 16).CopyTo(temp.Slice(8));

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
            temp.Slice(0, 4).CopyTo(data.AsSpan(profileFlagPos));
            temp.Slice(4, 4).CopyTo(data.AsSpan(renderingIntentPos));
            temp.Slice(8, 16).CopyTo(data.AsSpan(profileIdPos));
        }
    }

    /// <summary>
    /// Checks for signs of a corrupt profile.
    /// </summary>
    /// <remarks>This is not an absolute proof of validity but should weed out most corrupt data.</remarks>
    /// <returns>True if the profile is valid; False otherwise</returns>
    public bool CheckIsValid()
    {
        const int minSize = 128;
        const int maxSize = 50_000_000; // it's unlikely there is a profile bigger than 50MB

        bool arrayValid = true;
        if (this.data != null)
        {
            arrayValid = this.data.Length >= minSize &&
                         this.data.Length >= this.Header.Size;
        }

        return arrayValid &&
               Enum.IsDefined(this.Header.DataColorSpace) &&
               Enum.IsDefined(this.Header.ProfileConnectionSpace) &&
               Enum.IsDefined(this.Header.RenderingIntent) &&
               this.Header.Size is >= minSize and < maxSize;
    }

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

    private void InitializeHeader()
    {
        if (this.header != null)
        {
            return;
        }

        if (this.data is null)
        {
            this.header = new();
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
