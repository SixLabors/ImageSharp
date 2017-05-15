// <copyright file="IccTextTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// This is a simple text structure that contains a text string.
    /// </summary>
    internal sealed class IccTextTagDataEntry : IccTagDataEntry, IEquatable<IccTextTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccTextTagDataEntry"/> class.
        /// </summary>
        /// <param name="text">The Text</param>
        public IccTextTagDataEntry(string text)
            : this(text, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccTextTagDataEntry"/> class.
        /// </summary>
        /// <param name="text">The Text</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccTextTagDataEntry(string text, IccProfileTag tagSignature)
            : base(IccTypeSignature.Text, tagSignature)
        {
            Guard.NotNull(text, nameof(text));
            this.Text = text;
        }

        /// <summary>
        /// Gets the Text
        /// </summary>
        public string Text { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccTextTagDataEntry entry)
            {
                return this.Text == entry.Text;
            }

            return false;
        }

        /// <inheritdoc />
        public bool Equals(IccTextTagDataEntry other)
        {
            return this.Equals((IccTagDataEntry)other);
        }
    }
}
