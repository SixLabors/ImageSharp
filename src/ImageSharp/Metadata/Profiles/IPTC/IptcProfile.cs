// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace SixLabors.ImageSharp.Metadata.Profiles.Iptc
{
    /// <summary>
    /// Represents an IPTC profile providing access to the collection of values.
    /// </summary>
    public sealed class IptcProfile : IDeepCloneable<IptcProfile>
    {
        private Collection<IptcValue> values;

        private const byte IptcTagMarkerByte = 0x1c;

        private const uint MaxStandardDataTagSize = 0x7FFF;

        /// <summary>
        /// Initializes a new instance of the <see cref="IptcProfile"/> class.
        /// </summary>
        public IptcProfile()
            : this((byte[])null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IptcProfile"/> class.
        /// </summary>
        /// <param name="data">The byte array to read the iptc profile from.</param>
        public IptcProfile(byte[] data)
        {
            this.Data = data;
            this.Initialize();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IptcProfile"/> class
        /// by making a copy from another IPTC profile.
        /// </summary>
        /// <param name="other">The other IPTC profile, from which the clone should be made from.</param>
        private IptcProfile(IptcProfile other)
        {
            Guard.NotNull(other, nameof(other));

            if (other.values != null)
            {
                this.values = new Collection<IptcValue>();

                foreach (IptcValue value in other.Values)
                {
                    this.values.Add(value.DeepClone());
                }
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
        public byte[] Data { get; private set; }

        /// <summary>
        /// Gets the values of this iptc profile.
        /// </summary>
        public IEnumerable<IptcValue> Values
        {
            get
            {
                this.Initialize();
                return this.values;
            }
        }

        /// <inheritdoc/>
        public IptcProfile DeepClone() => new(this);

        /// <summary>
        /// Returns all values with the specified tag.
        /// </summary>
        /// <param name="tag">The tag of the iptc value.</param>
        /// <returns>The values found with the specified tag.</returns>
        public List<IptcValue> GetValues(IptcTag tag)
        {
            var iptcValues = new List<IptcValue>();
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
            this.Initialize();

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
            this.Initialize();

            bool removed = false;
            for (int i = this.values.Count - 1; i >= 0; i--)
            {
                if (this.values[i].Tag == tag && this.values[i].Value.Equals(value))
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
        /// Makes sure the datetime is formatted according to the iptc specification.
        /// <example>
        /// A date will be formatted as CCYYMMDD, e.g. "19890317" for 17 March 1989.
        /// A time value will be formatted as HHMMSS±HHMM, e.g. "090000+0200" for 9 o'clock Berlin time,
        /// two hours ahead of UTC.
        /// </example>
        /// </summary>
        /// <param name="tag">The tag of the iptc value.</param>
        /// <param name="dateTimeOffset">The datetime.</param>
        public void SetDateTimeValue(IptcTag tag, DateTimeOffset dateTimeOffset)
        {
            if (!tag.IsDate() && !tag.IsTime())
            {
                throw new ArgumentException("iptc tag is not a time or date type");
            }

            string formattedDate = tag.IsDate()
                ? dateTimeOffset.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture)
                : dateTimeOffset.ToString("HHmmsszzzz", System.Globalization.CultureInfo.InvariantCulture)
                    .Replace(":", string.Empty);

            this.SetValue(tag, Encoding.UTF8, formattedDate);
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
        /// Updates the data of the profile.
        /// </summary>
        public void UpdateData()
        {
            int length = 0;
            foreach (IptcValue value in this.Values)
            {
                length += value.Length + 5;
            }

            this.Data = new byte[length];

            int i = 0;
            foreach (IptcValue value in this.Values)
            {
                // Standard DataSet Tag
                // +-----------+----------------+---------------------------------------------------------------------------------+
                // | Octet Pos | Name           | Description                                                                     |
                // +==========-+================+=================================================================================+
                // | 1         | Tag Marker     | Is the tag marker that initiates the start of a DataSet 0x1c.                   |
                // +-----------+----------------+---------------------------------------------------------------------------------+
                // | 2         | Record Number  | Octet 2 is the binary representation of the record number. Note that the        |
                // |           |                | envelope record number is always 1, and that the application records are        |
                // |           |                | numbered 2 through 6, the pre-object descriptor record is 7, the object record  |
                // |           |                | is 8, and the post - object descriptor record is 9.                             |
                // +-----------+----------------+---------------------------------------------------------------------------------+
                // | 3         | DataSet Number | Octet 3 is the binary representation of the DataSet number.                     |
                // +-----------+----------------+---------------------------------------------------------------------------------+
                // | 4 and 5   | Data Field     | Octets 4 and 5, taken together, are the binary count of the number of octets in |
                // |           | Octet Count    | the following data field(32767 or fewer octets). Note that the value of bit 7 of|
                // |           |                | octet 4(most significant bit) always will be 0.                                 |
                // +-----------+----------------+---------------------------------------------------------------------------------+
                this.Data[i++] = IptcTagMarkerByte;
                this.Data[i++] = 2;
                this.Data[i++] = (byte)value.Tag;
                this.Data[i++] = (byte)(value.Length >> 8);
                this.Data[i++] = (byte)value.Length;
                if (value.Length > 0)
                {
                    Buffer.BlockCopy(value.ToByteArray(), 0, this.Data, i, value.Length);
                    i += value.Length;
                }
            }
        }

        private void Initialize()
        {
            if (this.values != null)
            {
                return;
            }

            this.values = new Collection<IptcValue>();

            if (this.Data == null || this.Data[0] != IptcTagMarkerByte)
            {
                return;
            }

            int offset = 0;
            while (offset < this.Data.Length - 4)
            {
                bool isValidTagMarker = this.Data[offset++] == IptcTagMarkerByte;
                byte recordNumber = this.Data[offset++];
                bool isValidRecordNumber = recordNumber is >= 1 and <= 9;
                var tag = (IptcTag)this.Data[offset++];
                bool isValidEntry = isValidTagMarker && isValidRecordNumber;

                uint byteCount = BinaryPrimitives.ReadUInt16BigEndian(this.Data.AsSpan(offset, 2));
                offset += 2;
                if (byteCount > MaxStandardDataTagSize)
                {
                    // Extended data set tag's are not supported.
                    break;
                }

                if (isValidEntry && byteCount > 0 && (offset <= this.Data.Length - byteCount))
                {
                    var iptcData = new byte[byteCount];
                    Buffer.BlockCopy(this.Data, offset, iptcData, 0, (int)byteCount);
                    this.values.Add(new IptcValue(tag, iptcData, false));
                }

                offset += (int)byteCount;
            }
        }
    }
}
