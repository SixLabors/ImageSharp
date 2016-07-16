// <copyright file="ColorDefinitions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// Packed vector type containing four 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in red, green, blue, and alpha order.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public partial struct Color
    {
        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F0F8FF.
        /// </summary>
        public static readonly Color AliceBlue = new Color(240, 248, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FAEBD7.
        /// </summary>
        public static readonly Color AntiqueWhite = new Color(250, 235, 215, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FFFF.
        /// </summary>
        public static readonly Color Aqua = new Color(0, 255, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #7FFFD4.
        /// </summary>
        public static readonly Color Aquamarine = new Color(127, 255, 212, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F0FFFF.
        /// </summary>
        public static readonly Color Azure = new Color(240, 255, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F5F5DC.
        /// </summary>
        public static readonly Color Beige = new Color(245, 245, 220, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFE4C4.
        /// </summary>
        public static readonly Color Bisque = new Color(255, 228, 196, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #000000.
        /// </summary>
        public static readonly Color Black = new Color(0, 0, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFEBCD.
        /// </summary>
        public static readonly Color BlanchedAlmond = new Color(255, 235, 205, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #0000FF.
        /// </summary>
        public static readonly Color Blue = new Color(0, 0, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8A2BE2.
        /// </summary>
        public static readonly Color BlueViolet = new Color(138, 43, 226, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #A52A2A.
        /// </summary>
        public static readonly Color Brown = new Color(165, 42, 42, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DEB887.
        /// </summary>
        public static readonly Color BurlyWood = new Color(222, 184, 135, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #5F9EA0.
        /// </summary>
        public static readonly Color CadetBlue = new Color(95, 158, 160, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #7FFF00.
        /// </summary>
        public static readonly Color Chartreuse = new Color(127, 255, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #D2691E.
        /// </summary>
        public static readonly Color Chocolate = new Color(210, 105, 30, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF7F50.
        /// </summary>
        public static readonly Color Coral = new Color(255, 127, 80, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #6495ED.
        /// </summary>
        public static readonly Color CornflowerBlue = new Color(100, 149, 237, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFF8DC.
        /// </summary>
        public static readonly Color Cornsilk = new Color(255, 248, 220, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DC143C.
        /// </summary>
        public static readonly Color Crimson = new Color(220, 20, 60, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FFFF.
        /// </summary>
        public static readonly Color Cyan = new Color(0, 255, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00008B.
        /// </summary>
        public static readonly Color DarkBlue = new Color(0, 0, 139, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #008B8B.
        /// </summary>
        public static readonly Color DarkCyan = new Color(0, 139, 139, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #B8860B.
        /// </summary>
        public static readonly Color DarkGoldenrod = new Color(184, 134, 11, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #A9A9A9.
        /// </summary>
        public static readonly Color DarkGray = new Color(169, 169, 169, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #006400.
        /// </summary>
        public static readonly Color DarkGreen = new Color(0, 100, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #BDB76B.
        /// </summary>
        public static readonly Color DarkKhaki = new Color(189, 183, 107, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8B008B.
        /// </summary>
        public static readonly Color DarkMagenta = new Color(139, 0, 139, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #556B2F.
        /// </summary>
        public static readonly Color DarkOliveGreen = new Color(85, 107, 47, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF8C00.
        /// </summary>
        public static readonly Color DarkOrange = new Color(255, 140, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #9932CC.
        /// </summary>
        public static readonly Color DarkOrchid = new Color(153, 50, 204, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8B0000.
        /// </summary>
        public static readonly Color DarkRed = new Color(139, 0, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #E9967A.
        /// </summary>
        public static readonly Color DarkSalmon = new Color(233, 150, 122, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8FBC8B.
        /// </summary>
        public static readonly Color DarkSeaGreen = new Color(143, 188, 139, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #483D8B.
        /// </summary>
        public static readonly Color DarkSlateBlue = new Color(72, 61, 139, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #2F4F4F.
        /// </summary>
        public static readonly Color DarkSlateGray = new Color(47, 79, 79, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00CED1.
        /// </summary>
        public static readonly Color DarkTurquoise = new Color(0, 206, 209, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #9400D3.
        /// </summary>
        public static readonly Color DarkViolet = new Color(148, 0, 211, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF1493.
        /// </summary>
        public static readonly Color DeepPink = new Color(255, 20, 147, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00BFFF.
        /// </summary>
        public static readonly Color DeepSkyBlue = new Color(0, 191, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #696969.
        /// </summary>
        public static readonly Color DimGray = new Color(105, 105, 105, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #1E90FF.
        /// </summary>
        public static readonly Color DodgerBlue = new Color(30, 144, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #B22222.
        /// </summary>
        public static readonly Color Firebrick = new Color(178, 34, 34, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFAF0.
        /// </summary>
        public static readonly Color FloralWhite = new Color(255, 250, 240, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #228B22.
        /// </summary>
        public static readonly Color ForestGreen = new Color(34, 139, 34, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF00FF.
        /// </summary>
        public static readonly Color Fuchsia = new Color(255, 0, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DCDCDC.
        /// </summary>
        public static readonly Color Gainsboro = new Color(220, 220, 220, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F8F8FF.
        /// </summary>
        public static readonly Color GhostWhite = new Color(248, 248, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFD700.
        /// </summary>
        public static readonly Color Gold = new Color(255, 215, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DAA520.
        /// </summary>
        public static readonly Color Goldenrod = new Color(218, 165, 32, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #808080.
        /// </summary>
        public static readonly Color Gray = new Color(128, 128, 128, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #008000.
        /// </summary>
        public static readonly Color Green = new Color(0, 128, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #ADFF2F.
        /// </summary>
        public static readonly Color GreenYellow = new Color(173, 255, 47, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F0FFF0.
        /// </summary>
        public static readonly Color Honeydew = new Color(240, 255, 240, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF69B4.
        /// </summary>
        public static readonly Color HotPink = new Color(255, 105, 180, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #CD5C5C.
        /// </summary>
        public static readonly Color IndianRed = new Color(205, 92, 92, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #4B0082.
        /// </summary>
        public static readonly Color Indigo = new Color(75, 0, 130, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFFF0.
        /// </summary>
        public static readonly Color Ivory = new Color(255, 255, 240, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F0E68C.
        /// </summary>
        public static readonly Color Khaki = new Color(240, 230, 140, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #E6E6FA.
        /// </summary>
        public static readonly Color Lavender = new Color(230, 230, 250, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFF0F5.
        /// </summary>
        public static readonly Color LavenderBlush = new Color(255, 240, 245, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #7CFC00.
        /// </summary>
        public static readonly Color LawnGreen = new Color(124, 252, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFACD.
        /// </summary>
        public static readonly Color LemonChiffon = new Color(255, 250, 205, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #ADD8E6.
        /// </summary>
        public static readonly Color LightBlue = new Color(173, 216, 230, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F08080.
        /// </summary>
        public static readonly Color LightCoral = new Color(240, 128, 128, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #E0FFFF.
        /// </summary>
        public static readonly Color LightCyan = new Color(224, 255, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FAFAD2.
        /// </summary>
        public static readonly Color LightGoldenrodYellow = new Color(250, 250, 210, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #D3D3D3.
        /// </summary>
        public static readonly Color LightGray = new Color(211, 211, 211, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #90EE90.
        /// </summary>
        public static readonly Color LightGreen = new Color(144, 238, 144, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFB6C1.
        /// </summary>
        public static readonly Color LightPink = new Color(255, 182, 193, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFA07A.
        /// </summary>
        public static readonly Color LightSalmon = new Color(255, 160, 122, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #20B2AA.
        /// </summary>
        public static readonly Color LightSeaGreen = new Color(32, 178, 170, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #87CEFA.
        /// </summary>
        public static readonly Color LightSkyBlue = new Color(135, 206, 250, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #778899.
        /// </summary>
        public static readonly Color LightSlateGray = new Color(119, 136, 153, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #B0C4DE.
        /// </summary>
        public static readonly Color LightSteelBlue = new Color(176, 196, 222, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFFE0.
        /// </summary>
        public static readonly Color LightYellow = new Color(255, 255, 224, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FF00.
        /// </summary>
        public static readonly Color Lime = new Color(0, 255, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #32CD32.
        /// </summary>
        public static readonly Color LimeGreen = new Color(50, 205, 50, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FAF0E6.
        /// </summary>
        public static readonly Color Linen = new Color(250, 240, 230, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF00FF.
        /// </summary>
        public static readonly Color Magenta = new Color(255, 0, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #800000.
        /// </summary>
        public static readonly Color Maroon = new Color(128, 0, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #66CDAA.
        /// </summary>
        public static readonly Color MediumAquamarine = new Color(102, 205, 170, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #0000CD.
        /// </summary>
        public static readonly Color MediumBlue = new Color(0, 0, 205, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #BA55D3.
        /// </summary>
        public static readonly Color MediumOrchid = new Color(186, 85, 211, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #9370DB.
        /// </summary>
        public static readonly Color MediumPurple = new Color(147, 112, 219, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #3CB371.
        /// </summary>
        public static readonly Color MediumSeaGreen = new Color(60, 179, 113, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #7B68EE.
        /// </summary>
        public static readonly Color MediumSlateBlue = new Color(123, 104, 238, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FA9A.
        /// </summary>
        public static readonly Color MediumSpringGreen = new Color(0, 250, 154, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #48D1CC.
        /// </summary>
        public static readonly Color MediumTurquoise = new Color(72, 209, 204, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #C71585.
        /// </summary>
        public static readonly Color MediumVioletRed = new Color(199, 21, 133, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #191970.
        /// </summary>
        public static readonly Color MidnightBlue = new Color(25, 25, 112, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F5FFFA.
        /// </summary>
        public static readonly Color MintCream = new Color(245, 255, 250, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFE4E1.
        /// </summary>
        public static readonly Color MistyRose = new Color(255, 228, 225, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFE4B5.
        /// </summary>
        public static readonly Color Moccasin = new Color(255, 228, 181, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFDEAD.
        /// </summary>
        public static readonly Color NavajoWhite = new Color(255, 222, 173, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #000080.
        /// </summary>
        public static readonly Color Navy = new Color(0, 0, 128, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FDF5E6.
        /// </summary>
        public static readonly Color OldLace = new Color(253, 245, 230, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #808000.
        /// </summary>
        public static readonly Color Olive = new Color(128, 128, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #6B8E23.
        /// </summary>
        public static readonly Color OliveDrab = new Color(107, 142, 35, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFA500.
        /// </summary>
        public static readonly Color Orange = new Color(255, 165, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF4500.
        /// </summary>
        public static readonly Color OrangeRed = new Color(255, 69, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DA70D6.
        /// </summary>
        public static readonly Color Orchid = new Color(218, 112, 214, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #EEE8AA.
        /// </summary>
        public static readonly Color PaleGoldenrod = new Color(238, 232, 170, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #98FB98.
        /// </summary>
        public static readonly Color PaleGreen = new Color(152, 251, 152, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #AFEEEE.
        /// </summary>
        public static readonly Color PaleTurquoise = new Color(175, 238, 238, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DB7093.
        /// </summary>
        public static readonly Color PaleVioletRed = new Color(219, 112, 147, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFEFD5.
        /// </summary>
        public static readonly Color PapayaWhip = new Color(255, 239, 213, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFDAB9.
        /// </summary>
        public static readonly Color PeachPuff = new Color(255, 218, 185, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #CD853F.
        /// </summary>
        public static readonly Color Peru = new Color(205, 133, 63, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFC0CB.
        /// </summary>
        public static readonly Color Pink = new Color(255, 192, 203, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DDA0DD.
        /// </summary>
        public static readonly Color Plum = new Color(221, 160, 221, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #B0E0E6.
        /// </summary>
        public static readonly Color PowderBlue = new Color(176, 224, 230, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #800080.
        /// </summary>
        public static readonly Color Purple = new Color(128, 0, 128, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #0.
        /// </summary>
        public static readonly Color RebeccaPurple = new Color(102, 51, 153, 255);


        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF0000.
        /// </summary>
        public static readonly Color Red = new Color(255, 0, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #BC8F8F.
        /// </summary>
        public static readonly Color RosyBrown = new Color(188, 143, 143, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #4169E1.
        /// </summary>
        public static readonly Color RoyalBlue = new Color(65, 105, 225, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8B4513.
        /// </summary>
        public static readonly Color SaddleBrown = new Color(139, 69, 19, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FA8072.
        /// </summary>
        public static readonly Color Salmon = new Color(250, 128, 114, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F4A460.
        /// </summary>
        public static readonly Color SandyBrown = new Color(244, 164, 96, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #2E8B57.
        /// </summary>
        public static readonly Color SeaGreen = new Color(46, 139, 87, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFF5EE.
        /// </summary>
        public static readonly Color SeaShell = new Color(255, 245, 238, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #A0522D.
        /// </summary>
        public static readonly Color Sienna = new Color(160, 82, 45, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #C0C0C0.
        /// </summary>
        public static readonly Color Silver = new Color(192, 192, 192, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #87CEEB.
        /// </summary>
        public static readonly Color SkyBlue = new Color(135, 206, 235, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #6A5ACD.
        /// </summary>
        public static readonly Color SlateBlue = new Color(106, 90, 205, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #708090.
        /// </summary>
        public static readonly Color SlateGray = new Color(112, 128, 144, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFAFA.
        /// </summary>
        public static readonly Color Snow = new Color(255, 250, 250, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FF7F.
        /// </summary>
        public static readonly Color SpringGreen = new Color(0, 255, 127, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #4682B4.
        /// </summary>
        public static readonly Color SteelBlue = new Color(70, 130, 180, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #D2B48C.
        /// </summary>
        public static readonly Color Tan = new Color(210, 180, 140, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #008080.
        /// </summary>
        public static readonly Color Teal = new Color(0, 128, 128, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #D8BFD8.
        /// </summary>
        public static readonly Color Thistle = new Color(216, 191, 216, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF6347.
        /// </summary>
        public static readonly Color Tomato = new Color(255, 99, 71, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFFFF.
        /// </summary>
        public static readonly Color Transparent = new Color(255, 255, 255, 0);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #40E0D0.
        /// </summary>
        public static readonly Color Turquoise = new Color(64, 224, 208, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #EE82EE.
        /// </summary>
        public static readonly Color Violet = new Color(238, 130, 238, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F5DEB3.
        /// </summary>
        public static readonly Color Wheat = new Color(245, 222, 179, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFFFF.
        /// </summary>
        public static readonly Color White = new Color(255, 255, 255, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F5F5F5.
        /// </summary>
        public static readonly Color WhiteSmoke = new Color(245, 245, 245, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFF00.
        /// </summary>
        public static readonly Color Yellow = new Color(255, 255, 0, 255);

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #9ACD32.
        /// </summary>
        public static readonly Color YellowGreen = new Color(154, 205, 50, 255);
    }
}