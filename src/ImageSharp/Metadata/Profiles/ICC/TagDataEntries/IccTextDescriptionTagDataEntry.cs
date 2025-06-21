// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Globalization;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

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
                ? new(culture, textEntry.Unicode)
                : new IccLocalizedString(textEntry.Unicode);
        }
        else if (!string.IsNullOrEmpty(textEntry.Ascii))
        {
            localString = new(textEntry.Ascii);
        }
        else if (!string.IsNullOrEmpty(textEntry.ScriptCode))
        {
            localString = new(textEntry.ScriptCode);
        }
        else
        {
            localString = new(string.Empty);
        }

        return new(new[] { localString }, textEntry.TagSignature);

        static CultureInfo GetCulture(uint value)
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
                string culture = new(new[] { (char)p1, (char)p2, '-', (char)p3, (char)p4 });
                return new(culture);
            }

            return null;
        }
    }

    /// <inheritdoc/>
    public override bool Equals(IccTagDataEntry other)
        => other is IccTextDescriptionTagDataEntry entry && this.Equals(entry);

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
            && string.Equals(this.Ascii, other.Ascii, StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.Unicode, other.Unicode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.ScriptCode, other.ScriptCode, StringComparison.OrdinalIgnoreCase)
            && this.UnicodeLanguageCode == other.UnicodeLanguageCode
            && this.ScriptCodeCode == other.ScriptCodeCode;
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
        => obj is IccTextDescriptionTagDataEntry other && this.Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(
            this.Signature,
            this.Ascii,
            this.Unicode,
            this.ScriptCode,
            this.UnicodeLanguageCode,
            this.ScriptCodeCode);
}
