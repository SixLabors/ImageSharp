// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Security.Cryptography;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
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
        public IccProfile DeepClone() => new IccProfile(this);

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
            var temp = new byte[24];
            Buffer.BlockCopy(data, profileFlagPos, temp, 0, 4);
            Buffer.BlockCopy(data, renderingIntentPos, temp, 4, 4);
            Buffer.BlockCopy(data, profileIdPos, temp, 8, 16);

            using (var md5 = MD5.Create())
            {
                try
                {
                    // Zero out some values
                    Array.Clear(data, profileFlagPos, 4);
                    Array.Clear(data, renderingIntentPos, 4);
                    Array.Clear(data, profileIdPos, 16);

                    // Calculate hash
                    byte[] hash = md5.ComputeHash(data);

                    // Read values from hash
                    var reader = new IccDataReader(hash);
                    return reader.ReadProfileId();
                }
                finally
                {
                    Buffer.BlockCopy(temp, 0, data, profileFlagPos, 4);
                    Buffer.BlockCopy(temp, 4, data, renderingIntentPos, 4);
                    Buffer.BlockCopy(temp, 8, data, profileIdPos, 16);
                }
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
                   Enum.IsDefined(typeof(IccColorSpaceType), this.Header.DataColorSpace) &&
                   Enum.IsDefined(typeof(IccColorSpaceType), this.Header.ProfileConnectionSpace) &&
                   Enum.IsDefined(typeof(IccRenderingIntent), this.Header.RenderingIntent) &&
                   this.Header.Size >= minSize &&
                   this.Header.Size < maxSize;
        }

        /// <summary>
        /// Converts this instance to a byte array.
        /// </summary>
        /// <returns>The <see cref="T:byte[]"/></returns>
        public byte[] ToByteArray()
        {
            if (this.data != null)
            {
                var copy = new byte[this.data.Length];
                Buffer.BlockCopy(this.data, 0, copy, 0, copy.Length);
                return copy;
            }
            else
            {
                var writer = new IccWriter();
                return writer.Write(this);
            }
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

            var reader = new IccReader();
            this.header = reader.ReadHeader(this.data);
        }

        private void InitializeEntries()
        {
            if (this.entries != null)
            {
                return;
            }

            if (this.data is null)
            {
                this.entries = Array.Empty<IccTagDataEntry>();
                return;
            }

            var reader = new IccReader();
            this.entries = reader.ReadTagData(this.data);
        }
    }
}
