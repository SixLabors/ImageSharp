// <copyright file="IccMultiLocalizedUnicodeTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;

    /// <summary>
    /// This tag structure contains a set of records each referencing
    /// a multilingual string associated with a profile.
    /// </summary>
    internal sealed class IccMultiLocalizedUnicodeTagDataEntry : IccTagDataEntry, IEquatable<IccMultiLocalizedUnicodeTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccMultiLocalizedUnicodeTagDataEntry"/> class.
        /// </summary>
        /// <param name="texts">Localized Text</param>
        public IccMultiLocalizedUnicodeTagDataEntry(IccLocalizedString[] texts)
            : this(texts, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccMultiLocalizedUnicodeTagDataEntry"/> class.
        /// </summary>
        /// <param name="texts">Localized Text</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccMultiLocalizedUnicodeTagDataEntry(IccLocalizedString[] texts, IccProfileTag tagSignature)
            : base(IccTypeSignature.MultiLocalizedUnicode, tagSignature)
        {
            Guard.NotNull(texts, nameof(texts));
            this.Texts = texts;
        }

        /// <summary>
        /// Gets the localized texts
        /// </summary>
        public IccLocalizedString[] Texts { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            var entry = other as IccMultiLocalizedUnicodeTagDataEntry;
            return entry != null && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccMultiLocalizedUnicodeTagDataEntry other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && this.Texts.SequenceEqual(other.Texts);
        }

        /// <inheritdoc/>
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

            return obj is IccMultiLocalizedUnicodeTagDataEntry && this.Equals((IccMultiLocalizedUnicodeTagDataEntry)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (this.Texts != null ? this.Texts.GetHashCode() : 0);
            }
        }
    }
}