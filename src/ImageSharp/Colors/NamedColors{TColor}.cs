// <copyright file="NamedColors{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// A set of named colors mapped to the provided Color space.
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    public static class NamedColors<TColor>
        where TColor : struct, IPixel<TColor>
    {
        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F0F8FF.
        /// </summary>
        public static readonly TColor AliceBlue = ColorBuilder<TColor>.FromRGBA(240, 248, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FAEBD7.
        /// </summary>
        public static readonly TColor AntiqueWhite = ColorBuilder<TColor>.FromRGBA(250, 235, 215, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FFFF.
        /// </summary>
        public static readonly TColor Aqua = ColorBuilder<TColor>.FromRGBA(0, 255, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #7FFFD4.
        /// </summary>
        public static readonly TColor Aquamarine = ColorBuilder<TColor>.FromRGBA(127, 255, 212, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F0FFFF.
        /// </summary>
        public static readonly TColor Azure = ColorBuilder<TColor>.FromRGBA(240, 255, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F5F5DC.
        /// </summary>
        public static readonly TColor Beige = ColorBuilder<TColor>.FromRGBA(245, 245, 220, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFE4C4.
        /// </summary>
        public static readonly TColor Bisque = ColorBuilder<TColor>.FromRGBA(255, 228, 196, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #000000.
        /// </summary>
        public static readonly TColor Black = ColorBuilder<TColor>.FromRGBA(0, 0, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFEBCD.
        /// </summary>
        public static readonly TColor BlanchedAlmond = ColorBuilder<TColor>.FromRGBA(255, 235, 205, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #0000FF.
        /// </summary>
        public static readonly TColor Blue = ColorBuilder<TColor>.FromRGBA(0, 0, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8A2BE2.
        /// </summary>
        public static readonly TColor BlueViolet = ColorBuilder<TColor>.FromRGBA(138, 43, 226, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #A52A2A.
        /// </summary>
        public static readonly TColor Brown = ColorBuilder<TColor>.FromRGBA(165, 42, 42, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DEB887.
        /// </summary>
        public static readonly TColor BurlyWood = ColorBuilder<TColor>.FromRGBA(222, 184, 135, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #5F9EA0.
        /// </summary>
        public static readonly TColor CadetBlue = ColorBuilder<TColor>.FromRGBA(95, 158, 160, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #7FFF00.
        /// </summary>
        public static readonly TColor Chartreuse = ColorBuilder<TColor>.FromRGBA(127, 255, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #D2691E.
        /// </summary>
        public static readonly TColor Chocolate = ColorBuilder<TColor>.FromRGBA(210, 105, 30, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF7F50.
        /// </summary>
        public static readonly TColor Coral = ColorBuilder<TColor>.FromRGBA(255, 127, 80, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #6495ED.
        /// </summary>
        public static readonly TColor CornflowerBlue = ColorBuilder<TColor>.FromRGBA(100, 149, 237, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFF8DC.
        /// </summary>
        public static readonly TColor Cornsilk = ColorBuilder<TColor>.FromRGBA(255, 248, 220, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DC143C.
        /// </summary>
        public static readonly TColor Crimson = ColorBuilder<TColor>.FromRGBA(220, 20, 60, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FFFF.
        /// </summary>
        public static readonly TColor Cyan = ColorBuilder<TColor>.FromRGBA(0, 255, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00008B.
        /// </summary>
        public static readonly TColor DarkBlue = ColorBuilder<TColor>.FromRGBA(0, 0, 139, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #008B8B.
        /// </summary>
        public static readonly TColor DarkCyan = ColorBuilder<TColor>.FromRGBA(0, 139, 139, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #B8860B.
        /// </summary>
        public static readonly TColor DarkGoldenrod = ColorBuilder<TColor>.FromRGBA(184, 134, 11, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #A9A9A9.
        /// </summary>
        public static readonly TColor DarkGray = ColorBuilder<TColor>.FromRGBA(169, 169, 169, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #006400.
        /// </summary>
        public static readonly TColor DarkGreen = ColorBuilder<TColor>.FromRGBA(0, 100, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #BDB76B.
        /// </summary>
        public static readonly TColor DarkKhaki = ColorBuilder<TColor>.FromRGBA(189, 183, 107, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8B008B.
        /// </summary>
        public static readonly TColor DarkMagenta = ColorBuilder<TColor>.FromRGBA(139, 0, 139, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #556B2F.
        /// </summary>
        public static readonly TColor DarkOliveGreen = ColorBuilder<TColor>.FromRGBA(85, 107, 47, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF8C00.
        /// </summary>
        public static readonly TColor DarkOrange = ColorBuilder<TColor>.FromRGBA(255, 140, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #9932CC.
        /// </summary>
        public static readonly TColor DarkOrchid = ColorBuilder<TColor>.FromRGBA(153, 50, 204, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8B0000.
        /// </summary>
        public static readonly TColor DarkRed = ColorBuilder<TColor>.FromRGBA(139, 0, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #E9967A.
        /// </summary>
        public static readonly TColor DarkSalmon = ColorBuilder<TColor>.FromRGBA(233, 150, 122, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8FBC8B.
        /// </summary>
        public static readonly TColor DarkSeaGreen = ColorBuilder<TColor>.FromRGBA(143, 188, 139, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #483D8B.
        /// </summary>
        public static readonly TColor DarkSlateBlue = ColorBuilder<TColor>.FromRGBA(72, 61, 139, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #2F4F4F.
        /// </summary>
        public static readonly TColor DarkSlateGray = ColorBuilder<TColor>.FromRGBA(47, 79, 79, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00CED1.
        /// </summary>
        public static readonly TColor DarkTurquoise = ColorBuilder<TColor>.FromRGBA(0, 206, 209, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #9400D3.
        /// </summary>
        public static readonly TColor DarkViolet = ColorBuilder<TColor>.FromRGBA(148, 0, 211, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF1493.
        /// </summary>
        public static readonly TColor DeepPink = ColorBuilder<TColor>.FromRGBA(255, 20, 147, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00BFFF.
        /// </summary>
        public static readonly TColor DeepSkyBlue = ColorBuilder<TColor>.FromRGBA(0, 191, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #696969.
        /// </summary>
        public static readonly TColor DimGray = ColorBuilder<TColor>.FromRGBA(105, 105, 105, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #1E90FF.
        /// </summary>
        public static readonly TColor DodgerBlue = ColorBuilder<TColor>.FromRGBA(30, 144, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #B22222.
        /// </summary>
        public static readonly TColor Firebrick = ColorBuilder<TColor>.FromRGBA(178, 34, 34, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFAF0.
        /// </summary>
        public static readonly TColor FloralWhite = ColorBuilder<TColor>.FromRGBA(255, 250, 240, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #228B22.
        /// </summary>
        public static readonly TColor ForestGreen = ColorBuilder<TColor>.FromRGBA(34, 139, 34, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF00FF.
        /// </summary>
        public static readonly TColor Fuchsia = ColorBuilder<TColor>.FromRGBA(255, 0, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DCDCDC.
        /// </summary>
        public static readonly TColor Gainsboro = ColorBuilder<TColor>.FromRGBA(220, 220, 220, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F8F8FF.
        /// </summary>
        public static readonly TColor GhostWhite = ColorBuilder<TColor>.FromRGBA(248, 248, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFD700.
        /// </summary>
        public static readonly TColor Gold = ColorBuilder<TColor>.FromRGBA(255, 215, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DAA520.
        /// </summary>
        public static readonly TColor Goldenrod = ColorBuilder<TColor>.FromRGBA(218, 165, 32, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #808080.
        /// </summary>
        public static readonly TColor Gray = ColorBuilder<TColor>.FromRGBA(128, 128, 128, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #008000.
        /// </summary>
        public static readonly TColor Green = ColorBuilder<TColor>.FromRGBA(0, 128, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #ADFF2F.
        /// </summary>
        public static readonly TColor GreenYellow = ColorBuilder<TColor>.FromRGBA(173, 255, 47, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F0FFF0.
        /// </summary>
        public static readonly TColor Honeydew = ColorBuilder<TColor>.FromRGBA(240, 255, 240, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF69B4.
        /// </summary>
        public static readonly TColor HotPink = ColorBuilder<TColor>.FromRGBA(255, 105, 180, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #CD5C5C.
        /// </summary>
        public static readonly TColor IndianRed = ColorBuilder<TColor>.FromRGBA(205, 92, 92, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #4B0082.
        /// </summary>
        public static readonly TColor Indigo = ColorBuilder<TColor>.FromRGBA(75, 0, 130, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFFF0.
        /// </summary>
        public static readonly TColor Ivory = ColorBuilder<TColor>.FromRGBA(255, 255, 240, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F0E68C.
        /// </summary>
        public static readonly TColor Khaki = ColorBuilder<TColor>.FromRGBA(240, 230, 140, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #E6E6FA.
        /// </summary>
        public static readonly TColor Lavender = ColorBuilder<TColor>.FromRGBA(230, 230, 250, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFF0F5.
        /// </summary>
        public static readonly TColor LavenderBlush = ColorBuilder<TColor>.FromRGBA(255, 240, 245, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #7CFC00.
        /// </summary>
        public static readonly TColor LawnGreen = ColorBuilder<TColor>.FromRGBA(124, 252, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFACD.
        /// </summary>
        public static readonly TColor LemonChiffon = ColorBuilder<TColor>.FromRGBA(255, 250, 205, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #ADD8E6.
        /// </summary>
        public static readonly TColor LightBlue = ColorBuilder<TColor>.FromRGBA(173, 216, 230, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F08080.
        /// </summary>
        public static readonly TColor LightCoral = ColorBuilder<TColor>.FromRGBA(240, 128, 128, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #E0FFFF.
        /// </summary>
        public static readonly TColor LightCyan = ColorBuilder<TColor>.FromRGBA(224, 255, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FAFAD2.
        /// </summary>
        public static readonly TColor LightGoldenrodYellow = ColorBuilder<TColor>.FromRGBA(250, 250, 210, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #D3D3D3.
        /// </summary>
        public static readonly TColor LightGray = ColorBuilder<TColor>.FromRGBA(211, 211, 211, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #90EE90.
        /// </summary>
        public static readonly TColor LightGreen = ColorBuilder<TColor>.FromRGBA(144, 238, 144, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFB6C1.
        /// </summary>
        public static readonly TColor LightPink = ColorBuilder<TColor>.FromRGBA(255, 182, 193, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFA07A.
        /// </summary>
        public static readonly TColor LightSalmon = ColorBuilder<TColor>.FromRGBA(255, 160, 122, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #20B2AA.
        /// </summary>
        public static readonly TColor LightSeaGreen = ColorBuilder<TColor>.FromRGBA(32, 178, 170, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #87CEFA.
        /// </summary>
        public static readonly TColor LightSkyBlue = ColorBuilder<TColor>.FromRGBA(135, 206, 250, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #778899.
        /// </summary>
        public static readonly TColor LightSlateGray = ColorBuilder<TColor>.FromRGBA(119, 136, 153, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #B0C4DE.
        /// </summary>
        public static readonly TColor LightSteelBlue = ColorBuilder<TColor>.FromRGBA(176, 196, 222, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFFE0.
        /// </summary>
        public static readonly TColor LightYellow = ColorBuilder<TColor>.FromRGBA(255, 255, 224, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FF00.
        /// </summary>
        public static readonly TColor Lime = ColorBuilder<TColor>.FromRGBA(0, 255, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #32CD32.
        /// </summary>
        public static readonly TColor LimeGreen = ColorBuilder<TColor>.FromRGBA(50, 205, 50, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FAF0E6.
        /// </summary>
        public static readonly TColor Linen = ColorBuilder<TColor>.FromRGBA(250, 240, 230, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF00FF.
        /// </summary>
        public static readonly TColor Magenta = ColorBuilder<TColor>.FromRGBA(255, 0, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #800000.
        /// </summary>
        public static readonly TColor Maroon = ColorBuilder<TColor>.FromRGBA(128, 0, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #66CDAA.
        /// </summary>
        public static readonly TColor MediumAquamarine = ColorBuilder<TColor>.FromRGBA(102, 205, 170, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #0000CD.
        /// </summary>
        public static readonly TColor MediumBlue = ColorBuilder<TColor>.FromRGBA(0, 0, 205, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #BA55D3.
        /// </summary>
        public static readonly TColor MediumOrchid = ColorBuilder<TColor>.FromRGBA(186, 85, 211, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #9370DB.
        /// </summary>
        public static readonly TColor MediumPurple = ColorBuilder<TColor>.FromRGBA(147, 112, 219, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #3CB371.
        /// </summary>
        public static readonly TColor MediumSeaGreen = ColorBuilder<TColor>.FromRGBA(60, 179, 113, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #7B68EE.
        /// </summary>
        public static readonly TColor MediumSlateBlue = ColorBuilder<TColor>.FromRGBA(123, 104, 238, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FA9A.
        /// </summary>
        public static readonly TColor MediumSpringGreen = ColorBuilder<TColor>.FromRGBA(0, 250, 154, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #48D1CC.
        /// </summary>
        public static readonly TColor MediumTurquoise = ColorBuilder<TColor>.FromRGBA(72, 209, 204, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #C71585.
        /// </summary>
        public static readonly TColor MediumVioletRed = ColorBuilder<TColor>.FromRGBA(199, 21, 133, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #191970.
        /// </summary>
        public static readonly TColor MidnightBlue = ColorBuilder<TColor>.FromRGBA(25, 25, 112, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F5FFFA.
        /// </summary>
        public static readonly TColor MintCream = ColorBuilder<TColor>.FromRGBA(245, 255, 250, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFE4E1.
        /// </summary>
        public static readonly TColor MistyRose = ColorBuilder<TColor>.FromRGBA(255, 228, 225, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFE4B5.
        /// </summary>
        public static readonly TColor Moccasin = ColorBuilder<TColor>.FromRGBA(255, 228, 181, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFDEAD.
        /// </summary>
        public static readonly TColor NavajoWhite = ColorBuilder<TColor>.FromRGBA(255, 222, 173, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #000080.
        /// </summary>
        public static readonly TColor Navy = ColorBuilder<TColor>.FromRGBA(0, 0, 128, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FDF5E6.
        /// </summary>
        public static readonly TColor OldLace = ColorBuilder<TColor>.FromRGBA(253, 245, 230, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #808000.
        /// </summary>
        public static readonly TColor Olive = ColorBuilder<TColor>.FromRGBA(128, 128, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #6B8E23.
        /// </summary>
        public static readonly TColor OliveDrab = ColorBuilder<TColor>.FromRGBA(107, 142, 35, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFA500.
        /// </summary>
        public static readonly TColor Orange = ColorBuilder<TColor>.FromRGBA(255, 165, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF4500.
        /// </summary>
        public static readonly TColor OrangeRed = ColorBuilder<TColor>.FromRGBA(255, 69, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DA70D6.
        /// </summary>
        public static readonly TColor Orchid = ColorBuilder<TColor>.FromRGBA(218, 112, 214, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #EEE8AA.
        /// </summary>
        public static readonly TColor PaleGoldenrod = ColorBuilder<TColor>.FromRGBA(238, 232, 170, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #98FB98.
        /// </summary>
        public static readonly TColor PaleGreen = ColorBuilder<TColor>.FromRGBA(152, 251, 152, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #AFEEEE.
        /// </summary>
        public static readonly TColor PaleTurquoise = ColorBuilder<TColor>.FromRGBA(175, 238, 238, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DB7093.
        /// </summary>
        public static readonly TColor PaleVioletRed = ColorBuilder<TColor>.FromRGBA(219, 112, 147, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFEFD5.
        /// </summary>
        public static readonly TColor PapayaWhip = ColorBuilder<TColor>.FromRGBA(255, 239, 213, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFDAB9.
        /// </summary>
        public static readonly TColor PeachPuff = ColorBuilder<TColor>.FromRGBA(255, 218, 185, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #CD853F.
        /// </summary>
        public static readonly TColor Peru = ColorBuilder<TColor>.FromRGBA(205, 133, 63, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFC0CB.
        /// </summary>
        public static readonly TColor Pink = ColorBuilder<TColor>.FromRGBA(255, 192, 203, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DDA0DD.
        /// </summary>
        public static readonly TColor Plum = ColorBuilder<TColor>.FromRGBA(221, 160, 221, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #B0E0E6.
        /// </summary>
        public static readonly TColor PowderBlue = ColorBuilder<TColor>.FromRGBA(176, 224, 230, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #800080.
        /// </summary>
        public static readonly TColor Purple = ColorBuilder<TColor>.FromRGBA(128, 0, 128, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #663399.
        /// </summary>
        public static readonly TColor RebeccaPurple = ColorBuilder<TColor>.FromRGBA(102, 51, 153, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF0000.
        /// </summary>
        public static readonly TColor Red = ColorBuilder<TColor>.FromRGBA(255, 0, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #BC8F8F.
        /// </summary>
        public static readonly TColor RosyBrown = ColorBuilder<TColor>.FromRGBA(188, 143, 143, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #4169E1.
        /// </summary>
        public static readonly TColor RoyalBlue = ColorBuilder<TColor>.FromRGBA(65, 105, 225, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8B4513.
        /// </summary>
        public static readonly TColor SaddleBrown = ColorBuilder<TColor>.FromRGBA(139, 69, 19, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FA8072.
        /// </summary>
        public static readonly TColor Salmon = ColorBuilder<TColor>.FromRGBA(250, 128, 114, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F4A460.
        /// </summary>
        public static readonly TColor SandyBrown = ColorBuilder<TColor>.FromRGBA(244, 164, 96, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #2E8B57.
        /// </summary>
        public static readonly TColor SeaGreen = ColorBuilder<TColor>.FromRGBA(46, 139, 87, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFF5EE.
        /// </summary>
        public static readonly TColor SeaShell = ColorBuilder<TColor>.FromRGBA(255, 245, 238, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #A0522D.
        /// </summary>
        public static readonly TColor Sienna = ColorBuilder<TColor>.FromRGBA(160, 82, 45, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #C0C0C0.
        /// </summary>
        public static readonly TColor Silver = ColorBuilder<TColor>.FromRGBA(192, 192, 192, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #87CEEB.
        /// </summary>
        public static readonly TColor SkyBlue = ColorBuilder<TColor>.FromRGBA(135, 206, 235, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #6A5ACD.
        /// </summary>
        public static readonly TColor SlateBlue = ColorBuilder<TColor>.FromRGBA(106, 90, 205, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #708090.
        /// </summary>
        public static readonly TColor SlateGray = ColorBuilder<TColor>.FromRGBA(112, 128, 144, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFAFA.
        /// </summary>
        public static readonly TColor Snow = ColorBuilder<TColor>.FromRGBA(255, 250, 250, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FF7F.
        /// </summary>
        public static readonly TColor SpringGreen = ColorBuilder<TColor>.FromRGBA(0, 255, 127, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #4682B4.
        /// </summary>
        public static readonly TColor SteelBlue = ColorBuilder<TColor>.FromRGBA(70, 130, 180, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #D2B48C.
        /// </summary>
        public static readonly TColor Tan = ColorBuilder<TColor>.FromRGBA(210, 180, 140, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #008080.
        /// </summary>
        public static readonly TColor Teal = ColorBuilder<TColor>.FromRGBA(0, 128, 128, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #D8BFD8.
        /// </summary>
        public static readonly TColor Thistle = ColorBuilder<TColor>.FromRGBA(216, 191, 216, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF6347.
        /// </summary>
        public static readonly TColor Tomato = ColorBuilder<TColor>.FromRGBA(255, 99, 71, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFFFF.
        /// </summary>
        public static readonly TColor Transparent = ColorBuilder<TColor>.FromRGBA(255, 255, 255, 0);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #40E0D0.
        /// </summary>
        public static readonly TColor Turquoise = ColorBuilder<TColor>.FromRGBA(64, 224, 208, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #EE82EE.
        /// </summary>
        public static readonly TColor Violet = ColorBuilder<TColor>.FromRGBA(238, 130, 238, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F5DEB3.
        /// </summary>
        public static readonly TColor Wheat = ColorBuilder<TColor>.FromRGBA(245, 222, 179, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFFFF.
        /// </summary>
        public static readonly TColor White = ColorBuilder<TColor>.FromRGBA(255, 255, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F5F5F5.
        /// </summary>
        public static readonly TColor WhiteSmoke = ColorBuilder<TColor>.FromRGBA(245, 245, 245, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFF00.
        /// </summary>
        public static readonly TColor Yellow = ColorBuilder<TColor>.FromRGBA(255, 255, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #9ACD32.
        /// </summary>
        public static readonly TColor YellowGreen = ColorBuilder<TColor>.FromRGBA(154, 205, 50, 255);
    }
}