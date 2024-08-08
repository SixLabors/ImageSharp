// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.PixelFormats;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <summary>
/// Represents an EXIF profile providing access to the collection of values.
/// </summary>
public sealed class ExifProfile : IDeepCloneable<ExifProfile>
{
    /// <summary>
    /// The byte array to read the EXIF profile from.
    /// </summary>
    private readonly byte[]? data;

    /// <summary>
    /// The collection of EXIF values
    /// </summary>
    private List<IExifValue>? values;

    /// <summary>
    /// The thumbnail offset position in the byte stream
    /// </summary>
    private int thumbnailOffset;

    /// <summary>
    /// The thumbnail length in the byte stream
    /// </summary>
    private int thumbnailLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExifProfile"/> class.
    /// </summary>
    public ExifProfile()
        : this((byte[]?)null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExifProfile"/> class.
    /// </summary>
    /// <param name="data">The byte array to read the EXIF profile from.</param>
    public ExifProfile(byte[]? data)
    {
        this.Parts = ExifParts.All;
        this.data = data;
        this.InvalidTags = Array.Empty<ExifTag>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExifProfile" /> class.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <param name="invalidTags">The invalid tags.</param>
    internal ExifProfile(List<IExifValue> values, IReadOnlyList<ExifTag> invalidTags)
    {
        this.Parts = ExifParts.All;
        this.values = values;
        this.InvalidTags = invalidTags;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExifProfile"/> class
    /// by making a copy from another EXIF profile.
    /// </summary>
    /// <param name="other">The other EXIF profile, where the clone should be made from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>>
    private ExifProfile(ExifProfile other)
    {
        Guard.NotNull(other, nameof(other));

        this.Parts = other.Parts;
        this.thumbnailLength = other.thumbnailLength;
        this.thumbnailOffset = other.thumbnailOffset;

        this.InvalidTags = other.InvalidTags.Count > 0
            ? new List<ExifTag>(other.InvalidTags)
            : Array.Empty<ExifTag>();

        if (other.values != null)
        {
            this.values = new List<IExifValue>(other.Values.Count);

            foreach (IExifValue value in other.Values)
            {
                this.values.Add(value.DeepClone());
            }
        }

        if (other.data != null)
        {
            this.data = new byte[other.data.Length];
            other.data.AsSpan().CopyTo(this.data);
        }
    }

    /// <summary>
    /// Gets or sets which parts will be written when the profile is added to an image.
    /// </summary>
    public ExifParts Parts { get; set; }

    /// <summary>
    /// Gets the tags that where found but contained an invalid value.
    /// </summary>
    public IReadOnlyList<ExifTag> InvalidTags { get; private set; }

    /// <summary>
    /// Gets the values of this EXIF profile.
    /// </summary>
    [MemberNotNull(nameof(values))]
    public IReadOnlyList<IExifValue> Values
    {
        get
        {
            this.InitializeValues();
            return this.values;
        }
    }

    /// <summary>
    /// Returns the thumbnail in the EXIF profile when available.
    /// </summary>
    /// <param name="image">The thumbnail</param>
    /// <returns>
    /// True, if there is a thumbnail otherwise false.
    /// </returns>
    public bool TryCreateThumbnail([NotNullWhen(true)] out Image? image)
    {
        if (this.TryCreateThumbnail(out Image<Rgba32>? innerimage))
        {
            image = innerimage;
            return true;
        }

        image = null;
        return false;
    }

    /// <summary>
    /// Returns the thumbnail in the EXIF profile when available.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The thumbnail.</param>
    /// <returns>True, if there is a thumbnail otherwise false.</returns>
    public bool TryCreateThumbnail<TPixel>([NotNullWhen(true)] out Image<TPixel>? image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        this.InitializeValues();
        image = null;
        if (this.thumbnailOffset == 0 || this.thumbnailLength == 0)
        {
            return false;
        }

        if (this.data is null || this.data.Length < (this.thumbnailOffset + this.thumbnailLength))
        {
            return false;
        }

        using (MemoryStream memStream = new(this.data, this.thumbnailOffset, this.thumbnailLength))
        {
            image = Image.Load<TPixel>(memStream);
            return true;
        }
    }

    /// <summary>
    /// Returns the value with the specified tag.
    /// </summary>
    /// <param name="tag">The tag of the exif value.</param>
    /// <param name="exifValue">The value with the specified tag.</param>
    /// <returns>True when found, otherwise false</returns>
    /// <typeparam name="TValueType">The data type of the tag.</typeparam>
    public bool TryGetValue<TValueType>(ExifTag<TValueType> tag, [NotNullWhen(true)] out IExifValue<TValueType>? exifValue)
    {
        IExifValue? value = this.GetValueInternal(tag);

        if (value is null)
        {
            exifValue = null;
            return false;
        }

        exifValue = (IExifValue<TValueType>)value;
        return true;
    }

    /// <summary>
    /// Removes the value with the specified tag.
    /// </summary>
    /// <param name="tag">The tag of the EXIF value.</param>
    /// <returns>
    /// True, if the value was removed, otherwise false.
    /// </returns>
    public bool RemoveValue(ExifTag tag)
    {
        this.InitializeValues();

        for (int i = 0; i < this.values.Count; i++)
        {
            if (this.values[i].Tag == tag)
            {
                this.values.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sets the value of the specified tag.
    /// </summary>
    /// <param name="tag">The tag of the exif value.</param>
    /// <param name="value">The value.</param>
    /// <typeparam name="TValueType">The data type of the tag.</typeparam>
    public void SetValue<TValueType>(ExifTag<TValueType> tag, TValueType value)
        => this.SetValueInternal(tag, value);

    /// <summary>
    /// Converts this instance to a byte array.
    /// </summary>
    /// <returns>The <see cref="T:byte[]"/></returns>
    public byte[]? ToByteArray()
    {
        if (this.values is null)
        {
            return this.data;
        }

        if (this.values.Count == 0)
        {
            return Array.Empty<byte>();
        }

        ExifWriter writer = new(this.values, this.Parts);
        return writer.GetData();
    }

    /// <inheritdoc/>
    public ExifProfile DeepClone() => new(this);

    /// <summary>
    /// Returns the value with the specified tag.
    /// </summary>
    /// <param name="tag">The tag of the exif value.</param>
    /// <returns>The value with the specified tag.</returns>
    internal IExifValue? GetValueInternal(ExifTag tag)
    {
        foreach (IExifValue exifValue in this.Values)
        {
            if (exifValue.Tag == tag)
            {
                return exifValue;
            }
        }

        return null;
    }

    /// <summary>
    /// Sets the value of the specified tag.
    /// </summary>
    /// <param name="tag">The tag of the exif value.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="NotSupportedException">Newly created value is null.</exception>
    internal void SetValueInternal(ExifTag tag, object? value)
    {
        foreach (IExifValue exifValue in this.Values)
        {
            if (exifValue.Tag == tag)
            {
                exifValue.TrySetValue(value);
                return;
            }
        }

        ExifValue? newExifValue = ExifValues.Create(tag);
        if (newExifValue is null)
        {
            throw new NotSupportedException($"Newly created value for tag {tag} is null.");
        }

        newExifValue.TrySetValue(value);
        this.values.Add(newExifValue);
    }

    /// <summary>
    /// Synchronizes the profiles with the specified metadata.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    internal void Sync(ImageMetadata metadata)
    {
        this.SyncResolution(ExifTag.XResolution, metadata.HorizontalResolution);
        this.SyncResolution(ExifTag.YResolution, metadata.VerticalResolution);
    }

    internal void SyncDimensions(int width, int height)
    {
        if (this.TryGetValue(ExifTag.PixelXDimension, out _))
        {
            this.SetValue(ExifTag.PixelXDimension, width);
        }

        if (this.TryGetValue(ExifTag.PixelYDimension, out _))
        {
            this.SetValue(ExifTag.PixelYDimension, height);
        }
    }

    /// <summary>
    /// Synchronizes the profiles with the specified metadata.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
#pragma warning disable CA1822, RCS1163, IDE0060
    internal void Sync(ImageFrameMetadata metadata)
#pragma warning restore IDE0060, RCS1163, CA1822
    {
        // Nothing to do ....YET.
    }

    private void SyncResolution(ExifTag<Rational> tag, double resolution)
    {
        if (!this.TryGetValue(tag, out IExifValue<Rational>? value))
        {
            return;
        }

        if (value.IsArray || value.DataType != ExifDataType.Rational)
        {
            this.RemoveValue(value.Tag);
        }

        Rational newResolution = new(resolution, false);
        this.SetValue(tag, newResolution);
    }

    [MemberNotNull(nameof(values))]
    private void InitializeValues()
    {
        if (this.values != null)
        {
            return;
        }

        if (this.data is null)
        {
            this.values = new List<IExifValue>();
            return;
        }

        ExifReader reader = new(this.data);

        this.values = reader.ReadValues();

        this.InvalidTags = reader.InvalidTags.Count > 0
            ? new List<ExifTag>(reader.InvalidTags)
            : Array.Empty<ExifTag>();

        this.thumbnailOffset = (int)reader.ThumbnailOffset;
        this.thumbnailLength = (int)reader.ThumbnailLength;
    }
}
