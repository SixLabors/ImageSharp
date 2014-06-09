// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ParameterParserUtilities.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to correctly parse querystring parameters.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Helpers
{
    using System.Drawing;
    using System.Globalization;
    using System.Text.RegularExpressions;
    using ImageProcessor.Core.Common.Extensions;

    /// <summary>
    /// Encapsulates methods to correctly parse querystring parameters.
    /// </summary>
    public static class ParameterParserUtilities
    {
        /// <summary>
        /// The regular expression to search strings for colors.
        /// </summary>
        private static readonly Regex ColorRegex = new Regex(@"(bgcolor|color)(=|-)(\d+,\d+,\d+,\d+|([0-9a-fA-F]{3}){1,2})", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for angles.
        /// </summary>
        private static readonly Regex AngleRegex = new Regex(@"(rotate|angle)(=|-)(?:3[0-5][0-9]|[12][0-9]{2}|[1-9][0-9]?)", RegexOptions.Compiled);

        /// <summary>
        /// Returns the correct <see cref="T:System.Int32"/> containing the angle for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Int32"/> containing the angle for the given string.
        /// </returns>
        public static int ParseAngle(string input)
        {
            foreach (Match match in AngleRegex.Matches(input))
            {
                // Split on angle
                int angle;
                string value = match.Value.Split(new[] { '=', '-' })[1];
                int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out angle);
                return angle;
            }

            // No rotate - matches the RotateLayer default.
            return 0;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.Color"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.Color"/>
        /// </returns>
        public static Color ParseColor(string input)
        {
            foreach (Match match in ColorRegex.Matches(input))
            {
                string value = match.Value.Split(new[] { '=', '-' })[1];

                if (value.Contains(","))
                {
                    int[] split = value.ToPositiveIntegerArray();
                    byte red = split[0].ToByte();
                    byte green = split[1].ToByte();
                    byte blue = split[2].ToByte();
                    byte alpha = split[3].ToByte();

                    return Color.FromArgb(alpha, red, green, blue);
                }

                // Split on color-hex
                return ColorTranslator.FromHtml("#" + value);
            }

            return Color.Transparent;
        }
    }
}
