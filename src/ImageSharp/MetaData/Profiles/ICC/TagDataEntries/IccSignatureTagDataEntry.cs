// <copyright file="IccSignatureTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// Typically this type is used for registered tags that can
    /// be displayed on many development systems as a sequence of four characters.
    /// </summary>
    internal sealed class IccSignatureTagDataEntry : IccTagDataEntry, IEquatable<IccSignatureTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccSignatureTagDataEntry"/> class.
        /// </summary>
        /// <param name="signatureData">The Signature</param>
        public IccSignatureTagDataEntry(string signatureData)
            : this(signatureData, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccSignatureTagDataEntry"/> class.
        /// </summary>
        /// <param name="signatureData">The Signature</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccSignatureTagDataEntry(string signatureData, IccProfileTag tagSignature)
            : base(IccTypeSignature.Signature, tagSignature)
        {
            Guard.NotNull(signatureData, nameof(signatureData));
            this.SignatureData = signatureData;
        }

        /// <summary>
        /// Gets the Signature
        /// </summary>
        public string SignatureData { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            var entry = other as IccSignatureTagDataEntry;
            return entry != null && this.Equals(entry);
        }

        /// <inheritdoc />
        public bool Equals(IccSignatureTagDataEntry other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && string.Equals(this.SignatureData, other.SignatureData);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is IccSignatureTagDataEntry && this.Equals((IccSignatureTagDataEntry)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (this.SignatureData != null ? this.SignatureData.GetHashCode() : 0);
            }
        }
    }
}
