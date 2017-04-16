// <copyright file="Color32Constants.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provides useful color definitions.
    /// </summary>
    public static class Color32Constants
    {
        /// <summary>
        /// Provides a lazy, one time method of returning the colors.
        /// </summary>
        private static readonly Lazy<Color32[]> SafeColors = new Lazy<Color32[]>(GetWebSafeColors);

        /// <summary>
        /// Gets a collection of named, web safe, colors as defined in the CSS Color Module Level 4.
        /// </summary>
        public static Color32[] WebSafeColors => SafeColors.Value;

        /// <summary>
        /// Returns an array of web safe colors.
        /// </summary>
        /// <returns>The <see cref="T:Color[]"/></returns>
        private static Color32[] GetWebSafeColors()
        {
            return new List<Color32>
            {
                Color32.AliceBlue,
                Color32.AntiqueWhite,
                Color32.Aqua,
                Color32.Aquamarine,
                Color32.Azure,
                Color32.Beige,
                Color32.Bisque,
                Color32.Black,
                Color32.BlanchedAlmond,
                Color32.Blue,
                Color32.BlueViolet,
                Color32.Brown,
                Color32.BurlyWood,
                Color32.CadetBlue,
                Color32.Chartreuse,
                Color32.Chocolate,
                Color32.Coral,
                Color32.CornflowerBlue,
                Color32.Cornsilk,
                Color32.Crimson,
                Color32.Cyan,
                Color32.DarkBlue,
                Color32.DarkCyan,
                Color32.DarkGoldenrod,
                Color32.DarkGray,
                Color32.DarkGreen,
                Color32.DarkKhaki,
                Color32.DarkMagenta,
                Color32.DarkOliveGreen,
                Color32.DarkOrange,
                Color32.DarkOrchid,
                Color32.DarkRed,
                Color32.DarkSalmon,
                Color32.DarkSeaGreen,
                Color32.DarkSlateBlue,
                Color32.DarkSlateGray,
                Color32.DarkTurquoise,
                Color32.DarkViolet,
                Color32.DeepPink,
                Color32.DeepSkyBlue,
                Color32.DimGray,
                Color32.DodgerBlue,
                Color32.Firebrick,
                Color32.FloralWhite,
                Color32.ForestGreen,
                Color32.Fuchsia,
                Color32.Gainsboro,
                Color32.GhostWhite,
                Color32.Gold,
                Color32.Goldenrod,
                Color32.Gray,
                Color32.Green,
                Color32.GreenYellow,
                Color32.Honeydew,
                Color32.HotPink,
                Color32.IndianRed,
                Color32.Indigo,
                Color32.Ivory,
                Color32.Khaki,
                Color32.Lavender,
                Color32.LavenderBlush,
                Color32.LawnGreen,
                Color32.LemonChiffon,
                Color32.LightBlue,
                Color32.LightCoral,
                Color32.LightCyan,
                Color32.LightGoldenrodYellow,
                Color32.LightGray,
                Color32.LightGreen,
                Color32.LightPink,
                Color32.LightSalmon,
                Color32.LightSeaGreen,
                Color32.LightSkyBlue,
                Color32.LightSlateGray,
                Color32.LightSteelBlue,
                Color32.LightYellow,
                Color32.Lime,
                Color32.LimeGreen,
                Color32.Linen,
                Color32.Magenta,
                Color32.Maroon,
                Color32.MediumAquamarine,
                Color32.MediumBlue,
                Color32.MediumOrchid,
                Color32.MediumPurple,
                Color32.MediumSeaGreen,
                Color32.MediumSlateBlue,
                Color32.MediumSpringGreen,
                Color32.MediumTurquoise,
                Color32.MediumVioletRed,
                Color32.MidnightBlue,
                Color32.MintCream,
                Color32.MistyRose,
                Color32.Moccasin,
                Color32.NavajoWhite,
                Color32.Navy,
                Color32.OldLace,
                Color32.Olive,
                Color32.OliveDrab,
                Color32.Orange,
                Color32.OrangeRed,
                Color32.Orchid,
                Color32.PaleGoldenrod,
                Color32.PaleGreen,
                Color32.PaleTurquoise,
                Color32.PaleVioletRed,
                Color32.PapayaWhip,
                Color32.PeachPuff,
                Color32.Peru,
                Color32.Pink,
                Color32.Plum,
                Color32.PowderBlue,
                Color32.Purple,
                Color32.RebeccaPurple,
                Color32.Red,
                Color32.RosyBrown,
                Color32.RoyalBlue,
                Color32.SaddleBrown,
                Color32.Salmon,
                Color32.SandyBrown,
                Color32.SeaGreen,
                Color32.SeaShell,
                Color32.Sienna,
                Color32.Silver,
                Color32.SkyBlue,
                Color32.SlateBlue,
                Color32.SlateGray,
                Color32.Snow,
                Color32.SpringGreen,
                Color32.SteelBlue,
                Color32.Tan,
                Color32.Teal,
                Color32.Thistle,
                Color32.Tomato,
                Color32.Transparent,
                Color32.Turquoise,
                Color32.Violet,
                Color32.Wheat,
                Color32.White,
                Color32.WhiteSmoke,
                Color32.Yellow,
                Color32.YellowGreen
            }.ToArray();
        }
    }
}
