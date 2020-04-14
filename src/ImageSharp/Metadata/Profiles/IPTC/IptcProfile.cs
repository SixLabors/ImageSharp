// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace SixLabors.ImageSharp.Metadata.Profiles.Iptc
{
    /// <summary>
    /// Class that can be used to access an Iptc profile.
    /// </summary>
    /// <remarks>This source code is from the Magick.Net project:
    /// https://github.com/dlemstra/Magick.NET/tree/master/src/Magick.NET/Shared/Profiles/Iptc/IptcProfile.cs
    /// </remarks>
    public sealed class IptcProfile
    {
        private Collection<IptcValue> values;

        private byte[] data;

        /// <summary>
        /// Initializes a new instance of the <see cref="IptcProfile"/> class.
        /// </summary>
        public IptcProfile()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IptcProfile"/> class.
        /// </summary>
        /// <param name="data">The byte array to read the iptc profile from.</param>
        public IptcProfile(byte[] data)
        {
            this.data = data;
        }

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

        /// <summary>
        /// Returns the value with the specified tag.
        /// </summary>
        /// <param name="tag">The tag of the iptc value.</param>
        /// <returns>The value with the specified tag.</returns>
        public IptcValue GetValue(IptcTag tag)
        {
            foreach (IptcValue iptcValue in this.Values)
            {
                if (iptcValue.Tag == tag)
                {
                    return iptcValue;
                }
            }

            return null;
        }

        /// <summary>
        /// Removes the value with the specified tag.
        /// </summary>
        /// <param name="tag">The tag of the iptc value.</param>
        /// <returns>True when the value was fount and removed.</returns>
        public bool RemoveValue(IptcTag tag)
        {
            this.Initialize();

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
        /// Sets the value of the specified tag.
        /// </summary>
        /// <param name="tag">The tag of the iptc value.</param>
        /// <param name="encoding">The encoding to use when storing the bytes.</param>
        /// <param name="value">The value.</param>
        public void SetValue(IptcTag tag, Encoding encoding, string value)
        {
            Guard.NotNull(encoding, nameof(encoding));

            foreach (IptcValue iptcValue in this.Values)
            {
                if (iptcValue.Tag == tag)
                {
                    iptcValue.Encoding = encoding;
                    iptcValue.Value = value;
                    return;
                }
            }

            this.values.Add(new IptcValue(tag, encoding, value));
        }

        /// <summary>
        /// Sets the value of the specified tag.
        /// </summary>
        /// <param name="tag">The tag of the iptc value.</param>
        /// <param name="value">The value.</param>
        public void SetValue(IptcTag tag, string value) => this.SetValue(tag, Encoding.UTF8, value);

        /// <summary>
        /// Updates the data of the profile.
        /// </summary>
        public void UpdateData()
        {
            var length = 0;
            foreach (IptcValue value in this.Values)
            {
                length += value.Length + 5;
            }

            this.data = new byte[length];

            int i = 0;
            foreach (IptcValue value in this.Values)
            {
                this.data[i++] = 28;
                this.data[i++] = 2;
                this.data[i++] = (byte)value.Tag;
                this.data[i++] = (byte)(value.Length >> 8);
                this.data[i++] = (byte)value.Length;
                if (value.Length > 0)
                {
                    Buffer.BlockCopy(value.ToByteArray(), 0, this.data, i, value.Length);
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

            if (this.data == null || this.data[0] != 0x1c)
            {
                return;
            }

            int i = 0;
            while (i + 4 < this.data.Length)
            {
                if (this.data[i++] != 28)
                {
                    continue;
                }

                i++;

                var tag = (IptcTag)this.data[i++];

                int count = BinaryPrimitives.ReadInt16BigEndian(this.data.AsSpan(i, 2));
                i += 2;

                var iptcData = new byte[count];
                if ((count > 0) && (i + count <= this.data.Length))
                {
                    Buffer.BlockCopy(this.data, i, iptcData, 0, count);
                    this.values.Add(new IptcValue(tag, iptcData));
                }

                i += count;
            }
        }
    }
}
