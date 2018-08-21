// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;

namespace SixLabors.ImageSharp.MetaData.Profiles.Icc
{
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

        /// <summary>
        /// Performs an explicit conversion from <see cref="IccTextDescriptionTagDataEntry"/>
        /// to <see cref="IccMultiLocalizedUnicodeTagDataEntry"/>.
        /// </summary>
        /// <param name="textEntry">The entry to convert</param>
        /// <returns>The converted entry</returns>
        public static explicit operator IccMultiLocalizedUnicodeTagDataEntry(IccTextDescriptionTagDataEntry textEntry)
        {
            if (textEntry is null)
            {
                return null;
            }

            IccLocalizedString localString;
            if (!string.IsNullOrEmpty(textEntry.Unicode))
            {
                CultureInfo culture = GetCulture(textEntry.UnicodeLanguageCode);
                localString = culture != null
                    ? new IccLocalizedString(culture, textEntry.Unicode)
                    : new IccLocalizedString(textEntry.Unicode);
            }
            else if (!string.IsNullOrEmpty(textEntry.Ascii))
            {
                localString = new IccLocalizedString(textEntry.Ascii);
            }
            else if (!string.IsNullOrEmpty(textEntry.ScriptCode))
            {
                localString = new IccLocalizedString(textEntry.ScriptCode);
            }
            else
            {
                localString = new IccLocalizedString(string.Empty);
            }

            return new IccMultiLocalizedUnicodeTagDataEntry(new[] { localString }, textEntry.TagSignature);

            CultureInfo GetCulture(uint value)
            {
                if (value == 0)
                {
                    return null;
                }

                byte p1 = (byte)(value >> 24);
                byte p2 = (byte)(value >> 16);
                byte p3 = (byte)(value >> 8);
                byte p4 = (byte)value;

                // Check if the values are [a-z]{2}[A-Z]{2}
                if (p1 >= 0x61 && p1 <= 0x7A
                    && p2 >= 0x61 && p2 <= 0x7A
                    && p3 >= 0x41 && p3 <= 0x5A
                    && p4 >= 0x41 && p4 <= 0x5A)
                {
                    string culture = new string(new[] { (char)p1, (char)p2, '-', (char)p3, (char)p4 });
                    return new CultureInfo(culture);
                }

                return null;
            }
        }

        /// <inheritdoc/>
        public override bool Equals(IccTagDataEntry other)
        {
            return other is IccTextDescriptionTagDataEntry entry && this.Equals(entry);
        }

        /// <inheritdoc />
        public bool Equals(IccTextDescriptionTagDataEntry other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other)
                && string.Equals(this.Ascii, other.Ascii)
                && string.Equals(this.Unicode, other.Unicode)
                && string.Equals(this.ScriptCode, other.ScriptCode)
                && this.UnicodeLanguageCode == other.UnicodeLanguageCode
                && this.ScriptCodeCode == other.ScriptCodeCode;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is IccTextDescriptionTagDataEntry other && this.Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (this.Ascii?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (this.Unicode?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (this.ScriptCode?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (int)this.UnicodeLanguageCode;
                hashCode = (hashCode * 397) ^ this.ScriptCodeCode.GetHashCode();
                return hashCode;
            }
        }
    }
}