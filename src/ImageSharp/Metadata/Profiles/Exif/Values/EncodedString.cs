// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <summary>
    /// The EXIF encoded string structure.
    /// </summary>
    public readonly struct EncodedString : IEquatable<EncodedString>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EncodedString" /> struct.
        /// </summary>
        /// <param name="text">The text value.</param>
        public EncodedString(string text)
          : this(EncodedStringCode.Unicode, text)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EncodedString" /> struct.
        /// </summary>
        /// <param name="code">The character code.</param>
        /// <param name="text">The text value.</param>
        public EncodedString(EncodedStringCode code, string text)
        {
            this.Text = text;
            this.Code = code;
        }

        /// <summary>
        /// Gets the character ode.
        /// </summary>
        public EncodedStringCode Code { get; }

        /// <summary>
        /// Gets the text.
        /// </summary>
        public string Text { get; }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is EncodedString other && this.Equals(other);

        /// <inheritdoc/>
        public bool Equals(EncodedString other)
        {
            return this.Text == other.Text && this.Code == other.Code;
        }

        /// <inheritdoc/>
        public override int GetHashCode() =>
            HashCode.Combine(this.Text, this.Code);

        /// <inheritdoc/>
        public override string ToString() => this.Text;
    }
}
