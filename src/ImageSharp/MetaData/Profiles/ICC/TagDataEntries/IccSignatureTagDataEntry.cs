// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
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
            this.SignatureData = signatureData ?? throw new ArgumentNullException(nameof(signatureData));
        }

        /// <summary>
        /// Gets the signature data
        /// </summary>
        public string SignatureData { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            return other is IccSignatureTagDataEntry entry && this.Equals(entry);
        }

        /// <inheritdoc />
        public bool Equals(IccSignatureTagDataEntry other)
        {
            if (other is null)
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
            return obj is IccSignatureTagDataEntry other && this.Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (this.SignatureData?.GetHashCode() ?? 0);
            }
        }
    }
}
