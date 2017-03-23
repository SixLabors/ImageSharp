// <copyright file="IccDataTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Linq;
    using System.Text;

    /// <summary>
    /// The dataType is a simple data structure that contains
    /// either 7-bit ASCII or binary data, i.e. textType data or transparent bytes.
    /// </summary>
    internal sealed class IccDataTagDataEntry : IccTagDataEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccDataTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The raw data</param>
        public IccDataTagDataEntry(byte[] data)
            : this(data, false, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccDataTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The raw data</param>
        /// <param name="isAscii">True if the given data is 7bit ASCII encoded text</param>
        public IccDataTagDataEntry(byte[] data, bool isAscii)
            : this(data, isAscii, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccDataTagDataEntry"/> class.
        /// </summary>
        /// <param name="data">The raw data</param>
        /// <param name="isAscii">True if the given data is 7bit ASCII encoded text</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccDataTagDataEntry(byte[] data, bool isAscii, IccProfileTag tagSignature)
            : base(IccTypeSignature.Data, tagSignature)
        {
            Guard.NotNull(data, nameof(data));
            this.Data = data;
            this.IsAscii = isAscii;
        }

        /// <summary>
        /// Gets the raw Data
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Gets a value indicating whether the <see cref="Data"/> represents 7bit ASCII encoded text
        /// </summary>
        public bool IsAscii { get; }

        /// <summary>
        /// Gets the <see cref="Data"/> decoded as 7bit ASCII.
        /// If <see cref="IsAscii"/> is false, returns null
        /// </summary>
        public string AsciiString
        {
            get
            {
                if (this.IsAscii)
                {
                    // Encoding.ASCII is missing in netstandard1.1, use UTF8 instead because it's compatible with ASCII
                    return Encoding.UTF8.GetString(this.Data, 0, this.Data.Length);
                }
                else
                {
                    return null;
                }
            }
        }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccDataTagDataEntry entry)
            {
                return this.IsAscii == entry.IsAscii
                    && this.Data.SequenceEqual(entry.Data);
            }

            return false;
        }
    }
}
