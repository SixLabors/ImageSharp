// <copyright file="IccUcrBgTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;

    /// <summary>
    /// This type contains curves representing the under color removal and black generation
    /// and a text string which is a general description of the method used for the UCR and BG.
    /// </summary>
    internal sealed class IccUcrBgTagDataEntry : IccTagDataEntry, IEquatable<IccUcrBgTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccUcrBgTagDataEntry"/> class.
        /// </summary>
        /// <param name="ucrCurve">UCR (under color removal) curve values</param>
        /// <param name="bgCurve">BG (black generation) curve values</param>
        /// <param name="description">Description of the used UCR and BG method</param>
        public IccUcrBgTagDataEntry(ushort[] ucrCurve, ushort[] bgCurve, string description)
            : this(ucrCurve, bgCurve, description, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccUcrBgTagDataEntry"/> class.
        /// </summary>
        /// <param name="ucrCurve">UCR (under color removal) curve values</param>
        /// <param name="bgCurve">BG (black generation) curve values</param>
        /// <param name="description">Description of the used UCR and BG method</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccUcrBgTagDataEntry(ushort[] ucrCurve, ushort[] bgCurve, string description, IccProfileTag tagSignature)
            : base(IccTypeSignature.UcrBg, tagSignature)
        {
            Guard.NotNull(ucrCurve, nameof(ucrCurve));
            Guard.NotNull(bgCurve, nameof(bgCurve));
            Guard.NotNull(description, nameof(description));

            this.UcrCurve = ucrCurve;
            this.BgCurve = bgCurve;
            this.Description = description;
        }

        /// <summary>
        /// Gets the UCR (under color removal) curve values
        /// </summary>
        public ushort[] UcrCurve { get; }

        /// <summary>
        /// Gets the BG (black generation) curve values
        /// </summary>
        public ushort[] BgCurve { get; }

        /// <summary>
        /// Gets a description of the used UCR and BG method
        /// </summary>
        public string Description { get; }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            var entry = other as IccUcrBgTagDataEntry;
            return entry != null && this.Equals(entry);
        }

        /// <inheritdoc/>
        public bool Equals(IccUcrBgTagDataEntry other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other)
                && this.UcrCurve.SequenceEqual(other.UcrCurve)
                && this.BgCurve.SequenceEqual(other.BgCurve)
                && string.Equals(this.Description, other.Description);
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

            return obj is IccUcrBgTagDataEntry && this.Equals((IccUcrBgTagDataEntry)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.UcrCurve != null ? this.UcrCurve.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.BgCurve != null ? this.BgCurve.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (this.Description != null ? this.Description.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}