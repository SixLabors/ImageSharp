// <copyright file="IccTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// The data of an ICC tag entry
    /// </summary>
    public abstract class IccTagDataEntry : IEquatable<IccTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccTagDataEntry"/> class.
        /// TagSignature will be <see cref="IccProfileTag.Unknown"/>
        /// </summary>
        /// <param name="signature">Type Signature</param>
        protected IccTagDataEntry(IccTypeSignature signature)
            : this(signature, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccTagDataEntry"/> class.
        /// </summary>
        /// <param name="signature">Type Signature</param>
        /// <param name="tagSignature">Tag Signature</param>
        protected IccTagDataEntry(IccTypeSignature signature, IccProfileTag tagSignature)
        {
            this.Signature = signature;
            this.TagSignature = tagSignature;
        }

        /// <summary>
        /// Gets the type Signature
        /// </summary>
        public IccTypeSignature Signature { get; }

        /// <summary>
        /// Gets or sets the tag Signature
        /// </summary>
        public IccProfileTag TagSignature { get; set; }

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

            var entry = obj as IccTagDataEntry;
            return entry != null && this.Equals(entry);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (int)this.Signature * 397;
            }
        }

        /// <inheritdoc/>
        public virtual bool Equals(IccTagDataEntry other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Signature == other.Signature;
        }
    }
}