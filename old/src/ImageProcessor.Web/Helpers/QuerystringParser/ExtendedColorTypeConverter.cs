// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ExtendedColorTypeConverter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The extended color type converter allows conversion of system and web colors.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Helpers
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.Drawing;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The extended color type converter allows conversion of system and web colors.
    /// </summary>
    public class ExtendedColorTypeConverter : ColorConverter
    {
        /// <summary>
        /// The web color regex.
        /// </summary>
        private static readonly Regex HexColorRegex = new Regex("([0-9a-fA-F]{3}){1,2}", RegexOptions.Compiled);

        /// <summary>
        /// The number color regex.
        /// </summary>
        private static readonly Regex NumberRegex = new Regex(@"\d+", RegexOptions.Compiled);

        /// <summary>
        /// The html system color table map.
        /// </summary>
        private static Hashtable htmlSystemColorTable;

        /// <summary>
        /// Converts the given object to the type of this converter, using the specified context and culture 
        /// information.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Object"/> that represents the converted value.
        /// </returns>
        /// <param name="context">
        /// An <see cref="T:System.ComponentModel.ITypeDescriptorContext"/> that provides a format context. 
        /// </param>
        /// <param name="culture">
        /// The <see cref="T:System.Globalization.CultureInfo"/> to use as the current culture. 
        /// </param>
        /// <param name="value">The <see cref="T:System.Object"/> to convert. </param>
        /// <exception cref="T:System.NotSupportedException">The conversion cannot be performed.</exception>
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string s = value as string;
            if (s != null)
            {
                string colorText = s.Trim();
                Color c = Color.Empty;

                // Empty color 
                if (string.IsNullOrEmpty(colorText))
                {
                    return c;
                }

                // Special case. HTML requires LightGrey, but System.Drawing.KnownColor has LightGray 
                if (colorText.Equals("LightGrey", StringComparison.OrdinalIgnoreCase))
                {
                    return Color.LightGray;
                }

                // Handle a,r,g,b
                char separator = culture.TextInfo.ListSeparator[0];
                if (colorText.Contains(separator.ToString()))
                {
                    string[] components = colorText.Split(separator);

                    bool convert = true;
                    foreach (string component in components)
                    {
                        if (!NumberRegex.IsMatch(component))
                        {
                            convert = false;
                        }
                    }

                    if (convert)
                    {
                        if (components.Length == 4)
                        {
                            return Color.FromArgb(
                                    Convert.ToInt32(components[3]),
                                    Convert.ToInt32(components[0]),
                                    Convert.ToInt32(components[1]),
                                    Convert.ToInt32(components[2]));
                        }

                        return Color.FromArgb(
                            Convert.ToInt32(components[0]),
                            Convert.ToInt32(components[1]),
                            Convert.ToInt32(components[2]));
                    }
                }

                // Hex based color values.
                char hash = colorText[0];
                if (hash == '#' || HexColorRegex.IsMatch(colorText))
                {
                    if (hash != '#')
                    {
                        colorText = "#" + colorText;
                    }

                    if (colorText.Length == 7)
                    {
                        return Color.FromArgb(
                            Convert.ToInt32(colorText.Substring(1, 2), 16),
                            Convert.ToInt32(colorText.Substring(3, 2), 16),
                            Convert.ToInt32(colorText.Substring(5, 2), 16));
                    }

                    // Length is 4
                    string r = char.ToString(colorText[1]);
                    string g = char.ToString(colorText[2]);
                    string b = char.ToString(colorText[3]);

                    return Color.FromArgb(
                        Convert.ToInt32(r + r, 16),
                        Convert.ToInt32(g + g, 16),
                        Convert.ToInt32(b + b, 16));
                }

                // System color
                if (htmlSystemColorTable == null)
                {
                    InitializeHtmlSystemColorTable();
                }

                if (htmlSystemColorTable != null)
                {
                    object o = htmlSystemColorTable[colorText];
                    if (o != null)
                    {
                        return (Color)o;
                    }
                }
            }

            // ColorConverter handles all named and KnownColors
            return base.ConvertFrom(context, culture, value);
        }

        /// <summary>
        /// Converts the given value object to the specified type, using the specified context and culture 
        /// information.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Object"/> that represents the converted value.
        /// </returns>
        /// <param name="context">
        /// An <see cref="T:System.ComponentModel.ITypeDescriptorContext"/> that provides a format context. 
        /// </param>
        /// <param name="culture">
        /// A <see cref="T:System.Globalization.CultureInfo"/>. If null is passed, the current culture is assumed. 
        /// </param>
        /// <param name="value">The <see cref="T:System.Object"/> to convert. </param>
        /// <param name="destinationType">
        /// The <see cref="T:System.Type"/> to convert the <paramref name="value"/> parameter to. 
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// The <paramref name="destinationType"/> parameter is null. 
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">The conversion cannot be performed. 
        /// </exception>
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == null)
            {
                throw new ArgumentNullException("destinationType");
            }

            if (destinationType == typeof(string))
            {
                if (value != null)
                {
                    Color color = (Color)value;

                    if (color == Color.Empty)
                    {
                        return string.Empty;
                    }

                    if (color.IsKnownColor == false)
                    {
                        // In the Web scenario, colors should be formatted in #RRGGBB notation 
                        StringBuilder sb = new StringBuilder("#", 7);
                        sb.Append(color.R.ToString("X2", CultureInfo.InvariantCulture));
                        sb.Append(color.G.ToString("X2", CultureInfo.InvariantCulture));
                        sb.Append(color.B.ToString("X2", CultureInfo.InvariantCulture));
                        return sb.ToString();
                    }
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        /// <summary>
        /// Initializes color table mapping system colors to known colors.
        /// </summary>
        private static void InitializeHtmlSystemColorTable()
        {
            Hashtable hashTable = new Hashtable(StringComparer.OrdinalIgnoreCase);
            hashTable["activeborder"] = Color.FromKnownColor(KnownColor.ActiveBorder);
            hashTable["activecaption"] = Color.FromKnownColor(KnownColor.ActiveCaption);
            hashTable["appworkspace"] = Color.FromKnownColor(KnownColor.AppWorkspace);
            hashTable["background"] = Color.FromKnownColor(KnownColor.Desktop);
            hashTable["buttonface"] = Color.FromKnownColor(KnownColor.Control);
            hashTable["buttonhighlight"] = Color.FromKnownColor(KnownColor.ControlLightLight);
            hashTable["buttonshadow"] = Color.FromKnownColor(KnownColor.ControlDark);
            hashTable["buttontext"] = Color.FromKnownColor(KnownColor.ControlText);
            hashTable["captiontext"] = Color.FromKnownColor(KnownColor.ActiveCaptionText);
            hashTable["graytext"] = Color.FromKnownColor(KnownColor.GrayText);
            hashTable["highlight"] = Color.FromKnownColor(KnownColor.Highlight);
            hashTable["highlighttext"] = Color.FromKnownColor(KnownColor.HighlightText);
            hashTable["inactiveborder"] = Color.FromKnownColor(KnownColor.InactiveBorder);
            hashTable["inactivecaption"] = Color.FromKnownColor(KnownColor.InactiveCaption);
            hashTable["inactivecaptiontext"] = Color.FromKnownColor(KnownColor.InactiveCaptionText);
            hashTable["infobackground"] = Color.FromKnownColor(KnownColor.Info);
            hashTable["infotext"] = Color.FromKnownColor(KnownColor.InfoText);
            hashTable["menu"] = Color.FromKnownColor(KnownColor.Menu);
            hashTable["menutext"] = Color.FromKnownColor(KnownColor.MenuText);
            hashTable["scrollbar"] = Color.FromKnownColor(KnownColor.ScrollBar);
            hashTable["threeddarkshadow"] = Color.FromKnownColor(KnownColor.ControlDarkDark);
            hashTable["threedface"] = Color.FromKnownColor(KnownColor.Control);
            hashTable["threedhighlight"] = Color.FromKnownColor(KnownColor.ControlLight);
            hashTable["threedlightshadow"] = Color.FromKnownColor(KnownColor.ControlLightLight);
            hashTable["window"] = Color.FromKnownColor(KnownColor.Window);
            hashTable["windowframe"] = Color.FromKnownColor(KnownColor.WindowFrame);
            hashTable["windowtext"] = Color.FromKnownColor(KnownColor.WindowText);
            htmlSystemColorTable = hashTable;
        }
    }
}
