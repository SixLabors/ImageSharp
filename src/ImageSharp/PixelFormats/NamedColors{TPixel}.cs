// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// A set of named colors mapped to the provided color space.
    /// </summary>
    /// <typeparam name="TPixel">The type of the color.</typeparam>
    public static class NamedColors<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Thread-safe backing field for the constant palettes.
        /// </summary>
        private static readonly Lazy<TPixel[]> WebSafePaletteLazy = new Lazy<TPixel[]>(GetWebSafePalette, true);
        private static readonly Lazy<TPixel[]> WernerPaletteLazy = new Lazy<TPixel[]>(GetWernerPalette, true);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #F0F8FF.
        /// </summary>
        public static readonly TPixel AliceBlue = ColorBuilder<TPixel>.FromRgba(240, 248, 255, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FAEBD7.
        /// </summary>
        public static readonly TPixel AntiqueWhite = ColorBuilder<TPixel>.FromRgba(250, 235, 215, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #00FFFF.
        /// </summary>
        public static readonly TPixel Aqua = ColorBuilder<TPixel>.FromRgba(0, 255, 255, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #7FFFD4.
        /// </summary>
        public static readonly TPixel Aquamarine = ColorBuilder<TPixel>.FromRgba(127, 255, 212, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #F0FFFF.
        /// </summary>
        public static readonly TPixel Azure = ColorBuilder<TPixel>.FromRgba(240, 255, 255, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #F5F5DC.
        /// </summary>
        public static readonly TPixel Beige = ColorBuilder<TPixel>.FromRgba(245, 245, 220, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFE4C4.
        /// </summary>
        public static readonly TPixel Bisque = ColorBuilder<TPixel>.FromRgba(255, 228, 196, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #000000.
        /// </summary>
        public static readonly TPixel Black = ColorBuilder<TPixel>.FromRgba(0, 0, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFEBCD.
        /// </summary>
        public static readonly TPixel BlanchedAlmond = ColorBuilder<TPixel>.FromRgba(255, 235, 205, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #0000FF.
        /// </summary>
        public static readonly TPixel Blue = ColorBuilder<TPixel>.FromRgba(0, 0, 255, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #8A2BE2.
        /// </summary>
        public static readonly TPixel BlueViolet = ColorBuilder<TPixel>.FromRgba(138, 43, 226, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #A52A2A.
        /// </summary>
        public static readonly TPixel Brown = ColorBuilder<TPixel>.FromRgba(165, 42, 42, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #DEB887.
        /// </summary>
        public static readonly TPixel BurlyWood = ColorBuilder<TPixel>.FromRgba(222, 184, 135, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #5F9EA0.
        /// </summary>
        public static readonly TPixel CadetBlue = ColorBuilder<TPixel>.FromRgba(95, 158, 160, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #7FFF00.
        /// </summary>
        public static readonly TPixel Chartreuse = ColorBuilder<TPixel>.FromRgba(127, 255, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #D2691E.
        /// </summary>
        public static readonly TPixel Chocolate = ColorBuilder<TPixel>.FromRgba(210, 105, 30, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FF7F50.
        /// </summary>
        public static readonly TPixel Coral = ColorBuilder<TPixel>.FromRgba(255, 127, 80, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #6495ED.
        /// </summary>
        public static readonly TPixel CornflowerBlue = ColorBuilder<TPixel>.FromRgba(100, 149, 237, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFF8DC.
        /// </summary>
        public static readonly TPixel Cornsilk = ColorBuilder<TPixel>.FromRgba(255, 248, 220, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #DC143C.
        /// </summary>
        public static readonly TPixel Crimson = ColorBuilder<TPixel>.FromRgba(220, 20, 60, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #00FFFF.
        /// </summary>
        public static readonly TPixel Cyan = ColorBuilder<TPixel>.FromRgba(0, 255, 255, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #00008B.
        /// </summary>
        public static readonly TPixel DarkBlue = ColorBuilder<TPixel>.FromRgba(0, 0, 139, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #008B8B.
        /// </summary>
        public static readonly TPixel DarkCyan = ColorBuilder<TPixel>.FromRgba(0, 139, 139, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #B8860B.
        /// </summary>
        public static readonly TPixel DarkGoldenrod = ColorBuilder<TPixel>.FromRgba(184, 134, 11, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #A9A9A9.
        /// </summary>
        public static readonly TPixel DarkGray = ColorBuilder<TPixel>.FromRgba(169, 169, 169, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #006400.
        /// </summary>
        public static readonly TPixel DarkGreen = ColorBuilder<TPixel>.FromRgba(0, 100, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #BDB76B.
        /// </summary>
        public static readonly TPixel DarkKhaki = ColorBuilder<TPixel>.FromRgba(189, 183, 107, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #8B008B.
        /// </summary>
        public static readonly TPixel DarkMagenta = ColorBuilder<TPixel>.FromRgba(139, 0, 139, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #556B2F.
        /// </summary>
        public static readonly TPixel DarkOliveGreen = ColorBuilder<TPixel>.FromRgba(85, 107, 47, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FF8C00.
        /// </summary>
        public static readonly TPixel DarkOrange = ColorBuilder<TPixel>.FromRgba(255, 140, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #9932CC.
        /// </summary>
        public static readonly TPixel DarkOrchid = ColorBuilder<TPixel>.FromRgba(153, 50, 204, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #8B0000.
        /// </summary>
        public static readonly TPixel DarkRed = ColorBuilder<TPixel>.FromRgba(139, 0, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #E9967A.
        /// </summary>
        public static readonly TPixel DarkSalmon = ColorBuilder<TPixel>.FromRgba(233, 150, 122, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #8FBC8B.
        /// </summary>
        public static readonly TPixel DarkSeaGreen = ColorBuilder<TPixel>.FromRgba(143, 188, 139, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #483D8B.
        /// </summary>
        public static readonly TPixel DarkSlateBlue = ColorBuilder<TPixel>.FromRgba(72, 61, 139, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #2F4F4F.
        /// </summary>
        public static readonly TPixel DarkSlateGray = ColorBuilder<TPixel>.FromRgba(47, 79, 79, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #00CED1.
        /// </summary>
        public static readonly TPixel DarkTurquoise = ColorBuilder<TPixel>.FromRgba(0, 206, 209, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #9400D3.
        /// </summary>
        public static readonly TPixel DarkViolet = ColorBuilder<TPixel>.FromRgba(148, 0, 211, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FF1493.
        /// </summary>
        public static readonly TPixel DeepPink = ColorBuilder<TPixel>.FromRgba(255, 20, 147, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #00BFFF.
        /// </summary>
        public static readonly TPixel DeepSkyBlue = ColorBuilder<TPixel>.FromRgba(0, 191, 255, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #696969.
        /// </summary>
        public static readonly TPixel DimGray = ColorBuilder<TPixel>.FromRgba(105, 105, 105, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #1E90FF.
        /// </summary>
        public static readonly TPixel DodgerBlue = ColorBuilder<TPixel>.FromRgba(30, 144, 255, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #B22222.
        /// </summary>
        public static readonly TPixel Firebrick = ColorBuilder<TPixel>.FromRgba(178, 34, 34, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFFAF0.
        /// </summary>
        public static readonly TPixel FloralWhite = ColorBuilder<TPixel>.FromRgba(255, 250, 240, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #228B22.
        /// </summary>
        public static readonly TPixel ForestGreen = ColorBuilder<TPixel>.FromRgba(34, 139, 34, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FF00FF.
        /// </summary>
        public static readonly TPixel Fuchsia = ColorBuilder<TPixel>.FromRgba(255, 0, 255, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #DCDCDC.
        /// </summary>
        public static readonly TPixel Gainsboro = ColorBuilder<TPixel>.FromRgba(220, 220, 220, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #F8F8FF.
        /// </summary>
        public static readonly TPixel GhostWhite = ColorBuilder<TPixel>.FromRgba(248, 248, 255, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFD700.
        /// </summary>
        public static readonly TPixel Gold = ColorBuilder<TPixel>.FromRgba(255, 215, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #DAA520.
        /// </summary>
        public static readonly TPixel Goldenrod = ColorBuilder<TPixel>.FromRgba(218, 165, 32, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #808080.
        /// </summary>
        public static readonly TPixel Gray = ColorBuilder<TPixel>.FromRgba(128, 128, 128, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #008000.
        /// </summary>
        public static readonly TPixel Green = ColorBuilder<TPixel>.FromRgba(0, 128, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #ADFF2F.
        /// </summary>
        public static readonly TPixel GreenYellow = ColorBuilder<TPixel>.FromRgba(173, 255, 47, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #F0FFF0.
        /// </summary>
        public static readonly TPixel Honeydew = ColorBuilder<TPixel>.FromRgba(240, 255, 240, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FF69B4.
        /// </summary>
        public static readonly TPixel HotPink = ColorBuilder<TPixel>.FromRgba(255, 105, 180, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #CD5C5C.
        /// </summary>
        public static readonly TPixel IndianRed = ColorBuilder<TPixel>.FromRgba(205, 92, 92, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #4B0082.
        /// </summary>
        public static readonly TPixel Indigo = ColorBuilder<TPixel>.FromRgba(75, 0, 130, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFFFF0.
        /// </summary>
        public static readonly TPixel Ivory = ColorBuilder<TPixel>.FromRgba(255, 255, 240, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #F0E68C.
        /// </summary>
        public static readonly TPixel Khaki = ColorBuilder<TPixel>.FromRgba(240, 230, 140, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #E6E6FA.
        /// </summary>
        public static readonly TPixel Lavender = ColorBuilder<TPixel>.FromRgba(230, 230, 250, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFF0F5.
        /// </summary>
        public static readonly TPixel LavenderBlush = ColorBuilder<TPixel>.FromRgba(255, 240, 245, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #7CFC00.
        /// </summary>
        public static readonly TPixel LawnGreen = ColorBuilder<TPixel>.FromRgba(124, 252, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFFACD.
        /// </summary>
        public static readonly TPixel LemonChiffon = ColorBuilder<TPixel>.FromRgba(255, 250, 205, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #ADD8E6.
        /// </summary>
        public static readonly TPixel LightBlue = ColorBuilder<TPixel>.FromRgba(173, 216, 230, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #F08080.
        /// </summary>
        public static readonly TPixel LightCoral = ColorBuilder<TPixel>.FromRgba(240, 128, 128, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #E0FFFF.
        /// </summary>
        public static readonly TPixel LightCyan = ColorBuilder<TPixel>.FromRgba(224, 255, 255, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FAFAD2.
        /// </summary>
        public static readonly TPixel LightGoldenrodYellow = ColorBuilder<TPixel>.FromRgba(250, 250, 210, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #D3D3D3.
        /// </summary>
        public static readonly TPixel LightGray = ColorBuilder<TPixel>.FromRgba(211, 211, 211, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #90EE90.
        /// </summary>
        public static readonly TPixel LightGreen = ColorBuilder<TPixel>.FromRgba(144, 238, 144, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFB6C1.
        /// </summary>
        public static readonly TPixel LightPink = ColorBuilder<TPixel>.FromRgba(255, 182, 193, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFA07A.
        /// </summary>
        public static readonly TPixel LightSalmon = ColorBuilder<TPixel>.FromRgba(255, 160, 122, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #20B2AA.
        /// </summary>
        public static readonly TPixel LightSeaGreen = ColorBuilder<TPixel>.FromRgba(32, 178, 170, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #87CEFA.
        /// </summary>
        public static readonly TPixel LightSkyBlue = ColorBuilder<TPixel>.FromRgba(135, 206, 250, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #778899.
        /// </summary>
        public static readonly TPixel LightSlateGray = ColorBuilder<TPixel>.FromRgba(119, 136, 153, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #B0C4DE.
        /// </summary>
        public static readonly TPixel LightSteelBlue = ColorBuilder<TPixel>.FromRgba(176, 196, 222, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFFFE0.
        /// </summary>
        public static readonly TPixel LightYellow = ColorBuilder<TPixel>.FromRgba(255, 255, 224, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #00FF00.
        /// </summary>
        public static readonly TPixel Lime = ColorBuilder<TPixel>.FromRgba(0, 255, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #32CD32.
        /// </summary>
        public static readonly TPixel LimeGreen = ColorBuilder<TPixel>.FromRgba(50, 205, 50, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FAF0E6.
        /// </summary>
        public static readonly TPixel Linen = ColorBuilder<TPixel>.FromRgba(250, 240, 230, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FF00FF.
        /// </summary>
        public static readonly TPixel Magenta = ColorBuilder<TPixel>.FromRgba(255, 0, 255, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #800000.
        /// </summary>
        public static readonly TPixel Maroon = ColorBuilder<TPixel>.FromRgba(128, 0, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #66CDAA.
        /// </summary>
        public static readonly TPixel MediumAquamarine = ColorBuilder<TPixel>.FromRgba(102, 205, 170, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #0000CD.
        /// </summary>
        public static readonly TPixel MediumBlue = ColorBuilder<TPixel>.FromRgba(0, 0, 205, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #BA55D3.
        /// </summary>
        public static readonly TPixel MediumOrchid = ColorBuilder<TPixel>.FromRgba(186, 85, 211, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #9370DB.
        /// </summary>
        public static readonly TPixel MediumPurple = ColorBuilder<TPixel>.FromRgba(147, 112, 219, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #3CB371.
        /// </summary>
        public static readonly TPixel MediumSeaGreen = ColorBuilder<TPixel>.FromRgba(60, 179, 113, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #7B68EE.
        /// </summary>
        public static readonly TPixel MediumSlateBlue = ColorBuilder<TPixel>.FromRgba(123, 104, 238, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #00FA9A.
        /// </summary>
        public static readonly TPixel MediumSpringGreen = ColorBuilder<TPixel>.FromRgba(0, 250, 154, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #48D1CC.
        /// </summary>
        public static readonly TPixel MediumTurquoise = ColorBuilder<TPixel>.FromRgba(72, 209, 204, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #C71585.
        /// </summary>
        public static readonly TPixel MediumVioletRed = ColorBuilder<TPixel>.FromRgba(199, 21, 133, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #191970.
        /// </summary>
        public static readonly TPixel MidnightBlue = ColorBuilder<TPixel>.FromRgba(25, 25, 112, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #F5FFFA.
        /// </summary>
        public static readonly TPixel MintCream = ColorBuilder<TPixel>.FromRgba(245, 255, 250, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFE4E1.
        /// </summary>
        public static readonly TPixel MistyRose = ColorBuilder<TPixel>.FromRgba(255, 228, 225, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFE4B5.
        /// </summary>
        public static readonly TPixel Moccasin = ColorBuilder<TPixel>.FromRgba(255, 228, 181, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFDEAD.
        /// </summary>
        public static readonly TPixel NavajoWhite = ColorBuilder<TPixel>.FromRgba(255, 222, 173, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #000080.
        /// </summary>
        public static readonly TPixel Navy = ColorBuilder<TPixel>.FromRgba(0, 0, 128, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FDF5E6.
        /// </summary>
        public static readonly TPixel OldLace = ColorBuilder<TPixel>.FromRgba(253, 245, 230, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #808000.
        /// </summary>
        public static readonly TPixel Olive = ColorBuilder<TPixel>.FromRgba(128, 128, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #6B8E23.
        /// </summary>
        public static readonly TPixel OliveDrab = ColorBuilder<TPixel>.FromRgba(107, 142, 35, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFA500.
        /// </summary>
        public static readonly TPixel Orange = ColorBuilder<TPixel>.FromRgba(255, 165, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FF4500.
        /// </summary>
        public static readonly TPixel OrangeRed = ColorBuilder<TPixel>.FromRgba(255, 69, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #DA70D6.
        /// </summary>
        public static readonly TPixel Orchid = ColorBuilder<TPixel>.FromRgba(218, 112, 214, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #EEE8AA.
        /// </summary>
        public static readonly TPixel PaleGoldenrod = ColorBuilder<TPixel>.FromRgba(238, 232, 170, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #98FB98.
        /// </summary>
        public static readonly TPixel PaleGreen = ColorBuilder<TPixel>.FromRgba(152, 251, 152, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #AFEEEE.
        /// </summary>
        public static readonly TPixel PaleTurquoise = ColorBuilder<TPixel>.FromRgba(175, 238, 238, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #DB7093.
        /// </summary>
        public static readonly TPixel PaleVioletRed = ColorBuilder<TPixel>.FromRgba(219, 112, 147, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFEFD5.
        /// </summary>
        public static readonly TPixel PapayaWhip = ColorBuilder<TPixel>.FromRgba(255, 239, 213, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFDAB9.
        /// </summary>
        public static readonly TPixel PeachPuff = ColorBuilder<TPixel>.FromRgba(255, 218, 185, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #CD853F.
        /// </summary>
        public static readonly TPixel Peru = ColorBuilder<TPixel>.FromRgba(205, 133, 63, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFC0CB.
        /// </summary>
        public static readonly TPixel Pink = ColorBuilder<TPixel>.FromRgba(255, 192, 203, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #DDA0DD.
        /// </summary>
        public static readonly TPixel Plum = ColorBuilder<TPixel>.FromRgba(221, 160, 221, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #B0E0E6.
        /// </summary>
        public static readonly TPixel PowderBlue = ColorBuilder<TPixel>.FromRgba(176, 224, 230, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #800080.
        /// </summary>
        public static readonly TPixel Purple = ColorBuilder<TPixel>.FromRgba(128, 0, 128, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #663399.
        /// </summary>
        public static readonly TPixel RebeccaPurple = ColorBuilder<TPixel>.FromRgba(102, 51, 153, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FF0000.
        /// </summary>
        public static readonly TPixel Red = ColorBuilder<TPixel>.FromRgba(255, 0, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #BC8F8F.
        /// </summary>
        public static readonly TPixel RosyBrown = ColorBuilder<TPixel>.FromRgba(188, 143, 143, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #4169E1.
        /// </summary>
        public static readonly TPixel RoyalBlue = ColorBuilder<TPixel>.FromRgba(65, 105, 225, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #8B4513.
        /// </summary>
        public static readonly TPixel SaddleBrown = ColorBuilder<TPixel>.FromRgba(139, 69, 19, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FA8072.
        /// </summary>
        public static readonly TPixel Salmon = ColorBuilder<TPixel>.FromRgba(250, 128, 114, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #F4A460.
        /// </summary>
        public static readonly TPixel SandyBrown = ColorBuilder<TPixel>.FromRgba(244, 164, 96, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #2E8B57.
        /// </summary>
        public static readonly TPixel SeaGreen = ColorBuilder<TPixel>.FromRgba(46, 139, 87, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFF5EE.
        /// </summary>
        public static readonly TPixel SeaShell = ColorBuilder<TPixel>.FromRgba(255, 245, 238, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #A0522D.
        /// </summary>
        public static readonly TPixel Sienna = ColorBuilder<TPixel>.FromRgba(160, 82, 45, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #C0C0C0.
        /// </summary>
        public static readonly TPixel Silver = ColorBuilder<TPixel>.FromRgba(192, 192, 192, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #87CEEB.
        /// </summary>
        public static readonly TPixel SkyBlue = ColorBuilder<TPixel>.FromRgba(135, 206, 235, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #6A5ACD.
        /// </summary>
        public static readonly TPixel SlateBlue = ColorBuilder<TPixel>.FromRgba(106, 90, 205, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #708090.
        /// </summary>
        public static readonly TPixel SlateGray = ColorBuilder<TPixel>.FromRgba(112, 128, 144, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFFAFA.
        /// </summary>
        public static readonly TPixel Snow = ColorBuilder<TPixel>.FromRgba(255, 250, 250, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #00FF7F.
        /// </summary>
        public static readonly TPixel SpringGreen = ColorBuilder<TPixel>.FromRgba(0, 255, 127, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #4682B4.
        /// </summary>
        public static readonly TPixel SteelBlue = ColorBuilder<TPixel>.FromRgba(70, 130, 180, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #D2B48C.
        /// </summary>
        public static readonly TPixel Tan = ColorBuilder<TPixel>.FromRgba(210, 180, 140, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #008080.
        /// </summary>
        public static readonly TPixel Teal = ColorBuilder<TPixel>.FromRgba(0, 128, 128, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #D8BFD8.
        /// </summary>
        public static readonly TPixel Thistle = ColorBuilder<TPixel>.FromRgba(216, 191, 216, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FF6347.
        /// </summary>
        public static readonly TPixel Tomato = ColorBuilder<TPixel>.FromRgba(255, 99, 71, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFFFFF.
        /// </summary>
        public static readonly TPixel Transparent = ColorBuilder<TPixel>.FromRgba(255, 255, 255, 0);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #40E0D0.
        /// </summary>
        public static readonly TPixel Turquoise = ColorBuilder<TPixel>.FromRgba(64, 224, 208, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #EE82EE.
        /// </summary>
        public static readonly TPixel Violet = ColorBuilder<TPixel>.FromRgba(238, 130, 238, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #F5DEB3.
        /// </summary>
        public static readonly TPixel Wheat = ColorBuilder<TPixel>.FromRgba(245, 222, 179, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFFFFF.
        /// </summary>
        public static readonly TPixel White = ColorBuilder<TPixel>.FromRgba(255, 255, 255, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #F5F5F5.
        /// </summary>
        public static readonly TPixel WhiteSmoke = ColorBuilder<TPixel>.FromRgba(245, 245, 245, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #FFFF00.
        /// </summary>
        public static readonly TPixel Yellow = ColorBuilder<TPixel>.FromRgba(255, 255, 0, 255);

        /// <summary>
        /// Represents a <see paramref="TPixel"/> matching the W3C definition that has an hex value of #9ACD32.
        /// </summary>
        public static readonly TPixel YellowGreen = ColorBuilder<TPixel>.FromRgba(154, 205, 50, 255);

        /// <summary>
        /// Gets a <see cref="T:TPixel[]"/> collection of web safe, colors as defined in the CSS Color Module Level 4.
        /// </summary>
        public static TPixel[] WebSafePalette => WebSafePaletteLazy.Value;

        /// <summary>
        /// Gets a <see cref="T:TPixel[]"/> collection of colors as defined in the original second edition of Werner’s Nomenclature of Colours 1821.
        /// The hex codes were collected and defined by Nicholas Rougeux <see href="https://www.c82.net/werner"/>
        /// </summary>
        public static TPixel[] WernerPalette => WernerPaletteLazy.Value;

        private static TPixel[] GetWebSafePalette() => GetPalette(ColorConstants.WebSafeColors);

        private static TPixel[] GetWernerPalette() => GetPalette(ColorConstants.WernerColors);

        private static TPixel[] GetPalette(Rgba32[] palette)
        {
            var converted = new TPixel[palette.Length];

            Span<byte> constantsBytes = MemoryMarshal.Cast<Rgba32, byte>(palette.AsSpan());
            PixelOperations<TPixel>.Instance.FromRgba32Bytes(
                Configuration.Default,
                constantsBytes,
                converted,
                palette.Length);

            return converted;
        }
    }
}