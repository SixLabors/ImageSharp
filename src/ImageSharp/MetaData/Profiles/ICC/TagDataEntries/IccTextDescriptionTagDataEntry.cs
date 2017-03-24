// <copyright file="IccTextDescriptionTagDataEntry.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// The TextDescriptionType contains three types of text description.
    /// </summary>
    internal sealed class IccTextDescriptionTagDataEntry : IccTagDataEntry, IEquatable<IccTextDescriptionTagDataEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccTextDescriptionTagDataEntry"/> class.
        /// </summary>
        /// <param name="ascii">ASCII text</param>
        /// <param name="unicode">Unicode text</param>
        /// <param name="scriptCode">ScriptCode text</param>
        /// <param name="unicodeLanguageCode">Unicode Language-Code</param>
        /// <param name="scriptCodeCode">ScriptCode Code</param>
        public IccTextDescriptionTagDataEntry(string ascii, string unicode, string scriptCode, uint unicodeLanguageCode, ushort scriptCodeCode)
            : this(ascii, unicode, scriptCode, unicodeLanguageCode, scriptCodeCode, IccProfileTag.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccTextDescriptionTagDataEntry"/> class.
        /// </summary>
        /// <param name="ascii">ASCII text</param>
        /// <param name="unicode">Unicode text</param>
        /// <param name="scriptCode">ScriptCode text</param>
        /// <param name="unicodeLanguageCode">Unicode Language-Code</param>
        /// <param name="scriptCodeCode">ScriptCode Code</param>
        /// <param name="tagSignature">Tag Signature</param>
        public IccTextDescriptionTagDataEntry(string ascii, string unicode, string scriptCode, uint unicodeLanguageCode, ushort scriptCodeCode, IccProfileTag tagSignature)
            : base(IccTypeSignature.TextDescription, tagSignature)
        {
            this.Ascii = ascii;
            this.Unicode = unicode;
            this.ScriptCode = scriptCode;
            this.UnicodeLanguageCode = unicodeLanguageCode;
            this.ScriptCodeCode = scriptCodeCode;
        }

        /// <summary>
        /// Gets the ASCII text
        /// </summary>
        public string Ascii { get; }

        /// <summary>
        /// Gets the Unicode text
        /// </summary>
        public string Unicode { get; }

        /// <summary>
        /// Gets the ScriptCode text
        /// </summary>
        public string ScriptCode { get; }

        /// <summary>
        /// Gets the Unicode Language-Code
        /// </summary>
        public uint UnicodeLanguageCode { get; }

        /// <summary>
        /// Gets the ScriptCode Code
        /// </summary>
        public ushort ScriptCodeCode { get; }

        /// <inheritdoc />
        public override bool Equals(IccTagDataEntry other)
        {
            if (base.Equals(other) && other is IccTextDescriptionTagDataEntry entry)
            {
                return this.Ascii == entry.Ascii
                    && this.Unicode == entry.Unicode
                    && this.ScriptCode == entry.ScriptCode
                    && this.UnicodeLanguageCode == entry.UnicodeLanguageCode
                    && this.ScriptCodeCode == entry.ScriptCodeCode;
            }

            return false;
        }

        /// <inheritdoc />
        public bool Equals(IccTextDescriptionTagDataEntry other)
        {
            return this.Equals((IccTagDataEntry)other);
        }
    }
}
