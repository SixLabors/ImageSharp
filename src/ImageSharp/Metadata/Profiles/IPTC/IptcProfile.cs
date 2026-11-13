// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using SixLabors.ImageSharp.Metadata.Profiles.IPTC;

namespace SixLabors.ImageSharp.Metadata.Profiles.Iptc;

/// <summary>
/// Represents an IPTC profile providing access to the collection of values.
/// </summary>
public sealed class IptcProfile : IDeepCloneable<IptcProfile>
{
    private readonly Collection<IptcValue> values = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="IptcProfile"/> class.
    /// </summary>
    public IptcProfile()
        : this((byte[]?)null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IptcProfile"/> class.
    /// </summary>
    /// <param name="data">The byte array to read the iptc profile from.</param>
    public IptcProfile(byte[]? data)
    {
        this.Data = data;
        IptcReader.ReadRecords(data, this.values);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IptcProfile"/> class
    /// by making a copy from another IPTC profile.
    /// </summary>
    /// <param name="other">The other IPTC profile, from which the clone should be made from.</param>
    private IptcProfile(IptcProfile other)
    {
        Guard.NotNull(other, nameof(other));

        foreach (IptcValue value in other.Values)
        {
            this.values.Add(value.DeepClone());
        }

        if (other.Data != null)
        {
            this.Data = new byte[other.Data.Length];
            other.Data.AsSpan().CopyTo(this.Data);
        }
    }

    /// <summary>
    /// Gets the byte data of the IPTC profile.
    /// </summary>
    public byte[]? Data { get; private set; }

    /// <summary>
    /// Gets the values of this iptc profile.
    /// </summary>
    public IEnumerable<IptcValue> Values => this.values;

    /// <inheritdoc/>
    public IptcProfile DeepClone() => new(this);

    /// <summary>
    /// Returns all values with the specified tag.
    /// </summary>
    /// <param name="tag">The tag of the iptc value.</param>
    /// <returns>The values found with the specified tag.</returns>
    public List<IptcValue> GetValues(IptcTag tag)
    {
        List<IptcValue> iptcValues = [];
        foreach (IptcValue iptcValue in this.Values)
        {
            if (iptcValue.Tag == tag)
            {
                iptcValues.Add(iptcValue);
            }
        }

        return iptcValues;
    }

    /// <summary>
    /// Removes all values with the specified tag.
    /// </summary>
    /// <param name="tag">The tag of the iptc value to remove.</param>
    /// <returns>True when the value was found and removed.</returns>
    public bool RemoveValue(IptcTag tag)
    {
        bool removed = false;
        for (int i = this.values.Count - 1; i >= 0; i--)
        {
            if (this.values[i].Tag == tag)
            {
                this.values.RemoveAt(i);
                removed = true;
            }
        }

        return removed;
    }

    /// <summary>
    /// Removes values with the specified tag and value.
    /// </summary>
    /// <param name="tag">The tag of the iptc value to remove.</param>
    /// <param name="value">The value of the iptc item to remove.</param>
    /// <returns>True when the value was found and removed.</returns>
    public bool RemoveValue(IptcTag tag, string value)
    {
        bool removed = false;
        for (int i = this.values.Count - 1; i >= 0; i--)
        {
            if (this.values[i].Tag == tag && this.values[i].Value.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                this.values.RemoveAt(i);
                removed = true;
            }
        }

        return removed;
    }

    /// <summary>
    /// Changes the encoding for all the values.
    /// </summary>
    /// <param name="encoding">The encoding to use when storing the bytes.</param>
    public void SetEncoding(Encoding encoding)
    {
        Guard.NotNull(encoding, nameof(encoding));

        foreach (IptcValue value in this.Values)
        {
            value.Encoding = encoding;
        }
    }

    /// <summary>
    /// Sets the value for the specified tag.
    /// </summary>
    /// <param name="tag">The tag of the iptc value.</param>
    /// <param name="encoding">The encoding to use when storing the bytes.</param>
    /// <param name="value">The value.</param>
    /// <param name="strict">
    /// Indicates if length restrictions from the specification should be followed strictly.
    /// Defaults to true.
    /// </param>
    public void SetValue(IptcTag tag, Encoding encoding, string value, bool strict = true)
    {
        Guard.NotNull(encoding, nameof(encoding));
        Guard.NotNull(value, nameof(value));

        if (!tag.IsRepeatable())
        {
            foreach (IptcValue iptcValue in this.Values)
            {
                if (iptcValue.Tag == tag)
                {
                    iptcValue.Strict = strict;
                    iptcValue.Encoding = encoding;
                    iptcValue.Value = value;
                    return;
                }
            }
        }

        this.values.Add(new IptcValue(tag, encoding, value, strict));
    }

    /// <summary>
    /// Sets the value of the specified tag.
    /// </summary>
    /// <param name="tag">The tag of the iptc value.</param>
    /// <param name="value">The value.</param>
    /// <param name="strict">
    /// Indicates if length restrictions from the specification should be followed strictly.
    /// Defaults to true.
    /// </param>
    public void SetValue(IptcTag tag, string value, bool strict = true) => this.SetValue(tag, Encoding.UTF8, value, strict);

    /// <summary>
    /// Makes sure the datetime is formatted according to the iptc specification.
    /// <example>
    /// A date will be formatted as CCYYMMDD, e.g. "19890317" for 17 March 1989.
    /// A time value will be formatted as HHMMSS±HHMM, e.g. "090000+0200" for 9 o'clock Berlin time,
    /// two hours ahead of UTC.
    /// </example>
    /// </summary>
    /// <param name="tag">The tag of the iptc value.</param>
    /// <param name="dateTimeOffset">The datetime.</param>
    /// <exception cref="ArgumentException">Iptc tag is not a time or date type.</exception>
    public void SetDateTimeValue(IptcTag tag, DateTimeOffset dateTimeOffset)
    {
        if (!tag.IsDate() && !tag.IsTime())
        {
            throw new ArgumentException("Iptc tag is not a time or date type.");
        }

        string formattedDate = tag.IsDate()
            ? dateTimeOffset.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
            : dateTimeOffset.ToString("HHmmsszzzz", CultureInfo.InvariantCulture)
                .Replace(":", string.Empty);

        this.SetValue(tag, Encoding.UTF8, formattedDate);
    }

    /// <summary>
    /// Checks that the profile contains at least one record.
    /// </summary>
    /// <returns>Whether the profile contains IPTC data.</returns>
    internal bool Validate() => this.values.Count > 0;

    /// <summary>
    /// Updates the data of the profile.
    /// Adds the UTF-8 character set declaration when any value uses UTF-8.
    /// </summary>
    public void UpdateData() => this.Data = this.Validate() ? IptcWriter.WriteRecords(this.values) : Array.Empty<byte>();
}
