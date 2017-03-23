// <copyright file="IccProfile.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Collections.Generic;
#if !NETSTANDARD1_1
    using System;
    using System.Security.Cryptography;
#endif

    /// <summary>
    /// Represents an ICC profile
    /// </summary>
    internal class IccProfile
    {
        /// <summary>
        /// The byte array to read the ICC profile from
        /// </summary>
        private readonly byte[] data;

        /// <summary>
        /// The backing file for the <see cref="Entries"/> property
        /// </summary>
        private readonly List<IccTagDataEntry> entries;

        /// <summary>
        /// ICC profile header
        /// </summary>
        private readonly IccProfileHeader header;

        /// <summary>
        /// Initializes a new instance of the <see cref="IccProfile"/> class.
        /// </summary>
        public IccProfile()
        {
            this.data = null;
            this.entries = new List<IccTagDataEntry>();
            this.header = new IccProfileHeader();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccProfile"/> class.
        /// </summary>
        /// <param name="data">The raw ICC profile data</param>
        public IccProfile(byte[] data)
        {
            Guard.NotNull(data, nameof(data));

            this.data = data;
            IccReader reader = new IccReader();
            this.header = reader.ReadHeader(data);
            this.entries = new List<IccTagDataEntry>(reader.ReadTagData(data));
        }

        /// <summary>
        /// Gets the profile header
        /// </summary>
        public IccProfileHeader Header
        {
            get { return this.header; }
        }

        /// <summary>
        /// Gets the actual profile data
        /// </summary>
        public List<IccTagDataEntry> Entries
        {
            get { return this.entries; }
        }

#if !NETSTANDARD1_1

        /// <summary>
        /// Calculates the MD5 hash value of an ICC profile header
        /// </summary>
        /// <param name="data">The data of which to calculate the hash value</param>
        /// <returns>The calculated hash</returns>
        public static IccProfileId CalculateHash(byte[] data)
        {
            Guard.NotNull(data, nameof(data));
            Guard.IsTrue(data.Length < 128, nameof(data), "Data length must be at least 128 to be a valid profile header");

            byte[] header = new byte[128];
            Buffer.BlockCopy(data, 0, header, 0, 128);

            using (MD5 md5 = MD5.Create())
            {
                // Zero out some values
                Array.Clear(header, 44, 4);     // Profile flags
                Array.Clear(header, 64, 4);     // Rendering Intent
                Array.Clear(header, 84, 16);    // Profile ID

                // Calculate hash
                byte[] hash = md5.ComputeHash(data);

                // Read values from hash
                IccDataReader reader = new IccDataReader(hash);
                return reader.ReadProfileId();
            }
        }

#endif
    }
}
