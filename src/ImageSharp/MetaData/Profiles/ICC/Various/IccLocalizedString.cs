// <copyright file="IccLocalizedString.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Globalization;

    /// <summary>
    /// A string with a specific locale
    /// </summary>
    internal sealed class IccLocalizedString : IEquatable<IccLocalizedString>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccLocalizedString"/> class.
        /// The culture will be <see cref="CultureInfo.CurrentCulture"/>
        /// </summary>
        /// <param name="text">The text value of this string</param>
        public IccLocalizedString(string text)
            : this(CultureInfo.CurrentCulture, text)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IccLocalizedString"/> class.
        /// The culture will be <see cref="CultureInfo.CurrentCulture"/>
        /// </summary>
        /// <param name="culture">The culture of this string</param>
        /// <param name="text">The text value of this string</param>
        public IccLocalizedString(CultureInfo culture, string text)
        {
            Guard.NotNull(culture, nameof(culture));
            Guard.NotNull(text, nameof(text));

            this.Culture = culture;
            this.Text = text;
        }

        /// <summary>
        /// Gets the actual text value
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the culture of the text
        /// </summary>
        public CultureInfo Culture { get; }

        /// <inheritdoc />
        public bool Equals(IccLocalizedString other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Culture.Equals(other.Culture)
                && this.Text == other.Text;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{this.Culture.Name}: {this.Text}";
        }
    }
}
