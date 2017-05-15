// <copyright file="ColorConstants.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provides useful color definitions.
    /// </summary>
    public static class ColorConstants
    {
        /// <summary>
        /// Provides a lazy, one time method of returning the colors.
        /// </summary>
        private static readonly Lazy<Rgba32[]> SafeColors = new Lazy<Rgba32[]>(GetWebSafeColors);

        /// <summary>
        /// Gets a collection of named, web safe, colors as defined in the CSS Color Module Level 4.
        /// </summary>
        public static Rgba32[] WebSafeColors => SafeColors.Value;

        /// <summary>
        /// Returns an array of web safe colors.
        /// </summary>
        /// <returns>The <see cref="T:Color[]"/></returns>
        private static Rgba32[] GetWebSafeColors()
        {
            return new List<Rgba32>
            {
                Rgba32.AliceBlue,
                Rgba32.AntiqueWhite,
                Rgba32.Aqua,
                Rgba32.Aquamarine,
                Rgba32.Azure,
                Rgba32.Beige,
                Rgba32.Bisque,
                Rgba32.Black,
                Rgba32.BlanchedAlmond,
                Rgba32.Blue,
                Rgba32.BlueViolet,
                Rgba32.Brown,
                Rgba32.BurlyWood,
                Rgba32.CadetBlue,
                Rgba32.Chartreuse,
                Rgba32.Chocolate,
                Rgba32.Coral,
                Rgba32.CornflowerBlue,
                Rgba32.Cornsilk,
                Rgba32.Crimson,
                Rgba32.Cyan,
                Rgba32.DarkBlue,
                Rgba32.DarkCyan,
                Rgba32.DarkGoldenrod,
                Rgba32.DarkGray,
                Rgba32.DarkGreen,
                Rgba32.DarkKhaki,
                Rgba32.DarkMagenta,
                Rgba32.DarkOliveGreen,
                Rgba32.DarkOrange,
                Rgba32.DarkOrchid,
                Rgba32.DarkRed,
                Rgba32.DarkSalmon,
                Rgba32.DarkSeaGreen,
                Rgba32.DarkSlateBlue,
                Rgba32.DarkSlateGray,
                Rgba32.DarkTurquoise,
                Rgba32.DarkViolet,
                Rgba32.DeepPink,
                Rgba32.DeepSkyBlue,
                Rgba32.DimGray,
                Rgba32.DodgerBlue,
                Rgba32.Firebrick,
                Rgba32.FloralWhite,
                Rgba32.ForestGreen,
                Rgba32.Fuchsia,
                Rgba32.Gainsboro,
                Rgba32.GhostWhite,
                Rgba32.Gold,
                Rgba32.Goldenrod,
                Rgba32.Gray,
                Rgba32.Green,
                Rgba32.GreenYellow,
                Rgba32.Honeydew,
                Rgba32.HotPink,
                Rgba32.IndianRed,
                Rgba32.Indigo,
                Rgba32.Ivory,
                Rgba32.Khaki,
                Rgba32.Lavender,
                Rgba32.LavenderBlush,
                Rgba32.LawnGreen,
                Rgba32.LemonChiffon,
                Rgba32.LightBlue,
                Rgba32.LightCoral,
                Rgba32.LightCyan,
                Rgba32.LightGoldenrodYellow,
                Rgba32.LightGray,
                Rgba32.LightGreen,
                Rgba32.LightPink,
                Rgba32.LightSalmon,
                Rgba32.LightSeaGreen,
                Rgba32.LightSkyBlue,
                Rgba32.LightSlateGray,
                Rgba32.LightSteelBlue,
                Rgba32.LightYellow,
                Rgba32.Lime,
                Rgba32.LimeGreen,
                Rgba32.Linen,
                Rgba32.Magenta,
                Rgba32.Maroon,
                Rgba32.MediumAquamarine,
                Rgba32.MediumBlue,
                Rgba32.MediumOrchid,
                Rgba32.MediumPurple,
                Rgba32.MediumSeaGreen,
                Rgba32.MediumSlateBlue,
                Rgba32.MediumSpringGreen,
                Rgba32.MediumTurquoise,
                Rgba32.MediumVioletRed,
                Rgba32.MidnightBlue,
                Rgba32.MintCream,
                Rgba32.MistyRose,
                Rgba32.Moccasin,
                Rgba32.NavajoWhite,
                Rgba32.Navy,
                Rgba32.OldLace,
                Rgba32.Olive,
                Rgba32.OliveDrab,
                Rgba32.Orange,
                Rgba32.OrangeRed,
                Rgba32.Orchid,
                Rgba32.PaleGoldenrod,
                Rgba32.PaleGreen,
                Rgba32.PaleTurquoise,
                Rgba32.PaleVioletRed,
                Rgba32.PapayaWhip,
                Rgba32.PeachPuff,
                Rgba32.Peru,
                Rgba32.Pink,
                Rgba32.Plum,
                Rgba32.PowderBlue,
                Rgba32.Purple,
                Rgba32.RebeccaPurple,
                Rgba32.Red,
                Rgba32.RosyBrown,
                Rgba32.RoyalBlue,
                Rgba32.SaddleBrown,
                Rgba32.Salmon,
                Rgba32.SandyBrown,
                Rgba32.SeaGreen,
                Rgba32.SeaShell,
                Rgba32.Sienna,
                Rgba32.Silver,
                Rgba32.SkyBlue,
                Rgba32.SlateBlue,
                Rgba32.SlateGray,
                Rgba32.Snow,
                Rgba32.SpringGreen,
                Rgba32.SteelBlue,
                Rgba32.Tan,
                Rgba32.Teal,
                Rgba32.Thistle,
                Rgba32.Tomato,
                Rgba32.Transparent,
                Rgba32.Turquoise,
                Rgba32.Violet,
                Rgba32.Wheat,
                Rgba32.White,
                Rgba32.WhiteSmoke,
                Rgba32.Yellow,
                Rgba32.YellowGreen
            }.ToArray();
        }
    }
}
