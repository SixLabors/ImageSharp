// <copyright file="ColorDefinitions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
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
        public static readonly Color AliceBlue = NamedColors<Color>.AliceBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FAEBD7.
        /// </summary>
        public static readonly Color AntiqueWhite = NamedColors<Color>.AntiqueWhite;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FFFF.
        /// </summary>
        public static readonly Color Aqua = NamedColors<Color>.Aqua;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #7FFFD4.
        /// </summary>
        public static readonly Color Aquamarine = NamedColors<Color>.Aquamarine;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F0FFFF.
        /// </summary>
        public static readonly Color Azure = NamedColors<Color>.Azure;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F5F5DC.
        /// </summary>
        public static readonly Color Beige = NamedColors<Color>.Beige;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFE4C4.
        /// </summary>
        public static readonly Color Bisque = NamedColors<Color>.Bisque;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #000000.
        /// </summary>
        public static readonly Color Black = NamedColors<Color>.Black;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFEBCD.
        /// </summary>
        public static readonly Color BlanchedAlmond = NamedColors<Color>.BlanchedAlmond;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #0000FF.
        /// </summary>
        public static readonly Color Blue = NamedColors<Color>.Blue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8A2BE2.
        /// </summary>
        public static readonly Color BlueViolet = NamedColors<Color>.BlueViolet;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #A52A2A.
        /// </summary>
        public static readonly Color Brown = NamedColors<Color>.Brown;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DEB887.
        /// </summary>
        public static readonly Color BurlyWood = NamedColors<Color>.BurlyWood;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #5F9EA0.
        /// </summary>
        public static readonly Color CadetBlue = NamedColors<Color>.CadetBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #7FFF00.
        /// </summary>
        public static readonly Color Chartreuse = NamedColors<Color>.Chartreuse;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #D2691E.
        /// </summary>
        public static readonly Color Chocolate = NamedColors<Color>.Chocolate;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF7F50.
        /// </summary>
        public static readonly Color Coral = NamedColors<Color>.Coral;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #6495ED.
        /// </summary>
        public static readonly Color CornflowerBlue = NamedColors<Color>.CornflowerBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFF8DC.
        /// </summary>
        public static readonly Color Cornsilk = NamedColors<Color>.Cornsilk;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DC143C.
        /// </summary>
        public static readonly Color Crimson = NamedColors<Color>.Crimson;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FFFF.
        /// </summary>
        public static readonly Color Cyan = NamedColors<Color>.Cyan;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00008B.
        /// </summary>
        public static readonly Color DarkBlue = NamedColors<Color>.DarkBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #008B8B.
        /// </summary>
        public static readonly Color DarkCyan = NamedColors<Color>.DarkCyan;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #B8860B.
        /// </summary>
        public static readonly Color DarkGoldenrod = NamedColors<Color>.DarkGoldenrod;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #A9A9A9.
        /// </summary>
        public static readonly Color DarkGray = NamedColors<Color>.DarkGray;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #006400.
        /// </summary>
        public static readonly Color DarkGreen = NamedColors<Color>.DarkGreen;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #BDB76B.
        /// </summary>
        public static readonly Color DarkKhaki = NamedColors<Color>.DarkKhaki;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8B008B.
        /// </summary>
        public static readonly Color DarkMagenta = NamedColors<Color>.DarkMagenta;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #556B2F.
        /// </summary>
        public static readonly Color DarkOliveGreen = NamedColors<Color>.DarkOliveGreen;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF8C00.
        /// </summary>
        public static readonly Color DarkOrange = NamedColors<Color>.DarkOrange;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #9932CC.
        /// </summary>
        public static readonly Color DarkOrchid = NamedColors<Color>.DarkOrchid;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8B0000.
        /// </summary>
        public static readonly Color DarkRed = NamedColors<Color>.DarkRed;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #E9967A.
        /// </summary>
        public static readonly Color DarkSalmon = NamedColors<Color>.DarkSalmon;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8FBC8B.
        /// </summary>
        public static readonly Color DarkSeaGreen = NamedColors<Color>.DarkSeaGreen;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #483D8B.
        /// </summary>
        public static readonly Color DarkSlateBlue = NamedColors<Color>.DarkSlateBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #2F4F4F.
        /// </summary>
        public static readonly Color DarkSlateGray = NamedColors<Color>.DarkSlateGray;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00CED1.
        /// </summary>
        public static readonly Color DarkTurquoise = NamedColors<Color>.DarkTurquoise;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #9400D3.
        /// </summary>
        public static readonly Color DarkViolet = NamedColors<Color>.DarkViolet;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF1493.
        /// </summary>
        public static readonly Color DeepPink = NamedColors<Color>.DeepPink;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00BFFF.
        /// </summary>
        public static readonly Color DeepSkyBlue = NamedColors<Color>.DeepSkyBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #696969.
        /// </summary>
        public static readonly Color DimGray = NamedColors<Color>.DimGray;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #1E90FF.
        /// </summary>
        public static readonly Color DodgerBlue = NamedColors<Color>.DodgerBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #B22222.
        /// </summary>
        public static readonly Color Firebrick = NamedColors<Color>.Firebrick;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFAF0.
        /// </summary>
        public static readonly Color FloralWhite = NamedColors<Color>.FloralWhite;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #228B22.
        /// </summary>
        public static readonly Color ForestGreen = NamedColors<Color>.ForestGreen;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF00FF.
        /// </summary>
        public static readonly Color Fuchsia = NamedColors<Color>.Fuchsia;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DCDCDC.
        /// </summary>
        public static readonly Color Gainsboro = NamedColors<Color>.Gainsboro;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F8F8FF.
        /// </summary>
        public static readonly Color GhostWhite = NamedColors<Color>.GhostWhite;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFD700.
        /// </summary>
        public static readonly Color Gold = NamedColors<Color>.Gold;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DAA520.
        /// </summary>
        public static readonly Color Goldenrod = NamedColors<Color>.Goldenrod;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #808080.
        /// </summary>
        public static readonly Color Gray = NamedColors<Color>.Gray;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #008000.
        /// </summary>
        public static readonly Color Green = NamedColors<Color>.Green;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #ADFF2F.
        /// </summary>
        public static readonly Color GreenYellow = NamedColors<Color>.GreenYellow;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F0FFF0.
        /// </summary>
        public static readonly Color Honeydew = NamedColors<Color>.Honeydew;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF69B4.
        /// </summary>
        public static readonly Color HotPink = NamedColors<Color>.HotPink;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #CD5C5C.
        /// </summary>
        public static readonly Color IndianRed = NamedColors<Color>.IndianRed;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #4B0082.
        /// </summary>
        public static readonly Color Indigo = NamedColors<Color>.Indigo;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFFF0.
        /// </summary>
        public static readonly Color Ivory = NamedColors<Color>.Ivory;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F0E68C.
        /// </summary>
        public static readonly Color Khaki = NamedColors<Color>.Khaki;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #E6E6FA.
        /// </summary>
        public static readonly Color Lavender = NamedColors<Color>.Lavender;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFF0F5.
        /// </summary>
        public static readonly Color LavenderBlush = NamedColors<Color>.LavenderBlush;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #7CFC00.
        /// </summary>
        public static readonly Color LawnGreen = NamedColors<Color>.LawnGreen;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFACD.
        /// </summary>
        public static readonly Color LemonChiffon = NamedColors<Color>.LemonChiffon;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #ADD8E6.
        /// </summary>
        public static readonly Color LightBlue = NamedColors<Color>.LightBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F08080.
        /// </summary>
        public static readonly Color LightCoral = NamedColors<Color>.LightCoral;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #E0FFFF.
        /// </summary>
        public static readonly Color LightCyan = NamedColors<Color>.LightCyan;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FAFAD2.
        /// </summary>
        public static readonly Color LightGoldenrodYellow = NamedColors<Color>.LightGoldenrodYellow;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #D3D3D3.
        /// </summary>
        public static readonly Color LightGray = NamedColors<Color>.LightGray;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #90EE90.
        /// </summary>
        public static readonly Color LightGreen = NamedColors<Color>.LightGreen;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFB6C1.
        /// </summary>
        public static readonly Color LightPink = NamedColors<Color>.LightPink;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFA07A.
        /// </summary>
        public static readonly Color LightSalmon = NamedColors<Color>.LightSalmon;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #20B2AA.
        /// </summary>
        public static readonly Color LightSeaGreen = NamedColors<Color>.LightSeaGreen;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #87CEFA.
        /// </summary>
        public static readonly Color LightSkyBlue = NamedColors<Color>.LightSkyBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #778899.
        /// </summary>
        public static readonly Color LightSlateGray = NamedColors<Color>.LightSlateGray;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #B0C4DE.
        /// </summary>
        public static readonly Color LightSteelBlue = NamedColors<Color>.LightSteelBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFFE0.
        /// </summary>
        public static readonly Color LightYellow = NamedColors<Color>.LightYellow;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FF00.
        /// </summary>
        public static readonly Color Lime = NamedColors<Color>.Lime;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #32CD32.
        /// </summary>
        public static readonly Color LimeGreen = NamedColors<Color>.LimeGreen;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FAF0E6.
        /// </summary>
        public static readonly Color Linen = NamedColors<Color>.Linen;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF00FF.
        /// </summary>
        public static readonly Color Magenta = NamedColors<Color>.Magenta;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #800000.
        /// </summary>
        public static readonly Color Maroon = NamedColors<Color>.Maroon;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #66CDAA.
        /// </summary>
        public static readonly Color MediumAquamarine = NamedColors<Color>.MediumAquamarine;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #0000CD.
        /// </summary>
        public static readonly Color MediumBlue = NamedColors<Color>.MediumBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #BA55D3.
        /// </summary>
        public static readonly Color MediumOrchid = NamedColors<Color>.MediumOrchid;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #9370DB.
        /// </summary>
        public static readonly Color MediumPurple = NamedColors<Color>.MediumPurple;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #3CB371.
        /// </summary>
        public static readonly Color MediumSeaGreen = NamedColors<Color>.MediumSeaGreen;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #7B68EE.
        /// </summary>
        public static readonly Color MediumSlateBlue = NamedColors<Color>.MediumSlateBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FA9A.
        /// </summary>
        public static readonly Color MediumSpringGreen = NamedColors<Color>.MediumSpringGreen;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #48D1CC.
        /// </summary>
        public static readonly Color MediumTurquoise = NamedColors<Color>.MediumTurquoise;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #C71585.
        /// </summary>
        public static readonly Color MediumVioletRed = NamedColors<Color>.MediumVioletRed;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #191970.
        /// </summary>
        public static readonly Color MidnightBlue = NamedColors<Color>.MidnightBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F5FFFA.
        /// </summary>
        public static readonly Color MintCream = NamedColors<Color>.MintCream;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFE4E1.
        /// </summary>
        public static readonly Color MistyRose = NamedColors<Color>.MistyRose;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFE4B5.
        /// </summary>
        public static readonly Color Moccasin = NamedColors<Color>.Moccasin;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFDEAD.
        /// </summary>
        public static readonly Color NavajoWhite = NamedColors<Color>.NavajoWhite;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #000080.
        /// </summary>
        public static readonly Color Navy = NamedColors<Color>.Navy;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FDF5E6.
        /// </summary>
        public static readonly Color OldLace = NamedColors<Color>.OldLace;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #808000.
        /// </summary>
        public static readonly Color Olive = NamedColors<Color>.Olive;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #6B8E23.
        /// </summary>
        public static readonly Color OliveDrab = NamedColors<Color>.OliveDrab;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFA500.
        /// </summary>
        public static readonly Color Orange = NamedColors<Color>.Orange;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF4500.
        /// </summary>
        public static readonly Color OrangeRed = NamedColors<Color>.OrangeRed;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DA70D6.
        /// </summary>
        public static readonly Color Orchid = NamedColors<Color>.Orchid;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #EEE8AA.
        /// </summary>
        public static readonly Color PaleGoldenrod = NamedColors<Color>.PaleGoldenrod;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #98FB98.
        /// </summary>
        public static readonly Color PaleGreen = NamedColors<Color>.PaleGreen;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #AFEEEE.
        /// </summary>
        public static readonly Color PaleTurquoise = NamedColors<Color>.PaleTurquoise;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DB7093.
        /// </summary>
        public static readonly Color PaleVioletRed = NamedColors<Color>.PaleVioletRed;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFEFD5.
        /// </summary>
        public static readonly Color PapayaWhip = NamedColors<Color>.PapayaWhip;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFDAB9.
        /// </summary>
        public static readonly Color PeachPuff = NamedColors<Color>.PeachPuff;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #CD853F.
        /// </summary>
        public static readonly Color Peru = NamedColors<Color>.Peru;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFC0CB.
        /// </summary>
        public static readonly Color Pink = NamedColors<Color>.Pink;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #DDA0DD.
        /// </summary>
        public static readonly Color Plum = NamedColors<Color>.Plum;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #B0E0E6.
        /// </summary>
        public static readonly Color PowderBlue = NamedColors<Color>.PowderBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #800080.
        /// </summary>
        public static readonly Color Purple = NamedColors<Color>.Purple;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #663399.
        /// </summary>
        public static readonly Color RebeccaPurple = NamedColors<Color>.RebeccaPurple;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF0000.
        /// </summary>
        public static readonly Color Red = NamedColors<Color>.Red;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #BC8F8F.
        /// </summary>
        public static readonly Color RosyBrown = NamedColors<Color>.RosyBrown;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #4169E1.
        /// </summary>
        public static readonly Color RoyalBlue = NamedColors<Color>.RoyalBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #8B4513.
        /// </summary>
        public static readonly Color SaddleBrown = NamedColors<Color>.SaddleBrown;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FA8072.
        /// </summary>
        public static readonly Color Salmon = NamedColors<Color>.Salmon;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F4A460.
        /// </summary>
        public static readonly Color SandyBrown = NamedColors<Color>.SandyBrown;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #2E8B57.
        /// </summary>
        public static readonly Color SeaGreen = NamedColors<Color>.SeaGreen;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFF5EE.
        /// </summary>
        public static readonly Color SeaShell = NamedColors<Color>.SeaShell;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #A0522D.
        /// </summary>
        public static readonly Color Sienna = NamedColors<Color>.Sienna;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #C0C0C0.
        /// </summary>
        public static readonly Color Silver = NamedColors<Color>.Silver;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #87CEEB.
        /// </summary>
        public static readonly Color SkyBlue = NamedColors<Color>.SkyBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #6A5ACD.
        /// </summary>
        public static readonly Color SlateBlue = NamedColors<Color>.SlateBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #708090.
        /// </summary>
        public static readonly Color SlateGray = NamedColors<Color>.SlateGray;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFAFA.
        /// </summary>
        public static readonly Color Snow = NamedColors<Color>.Snow;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #00FF7F.
        /// </summary>
        public static readonly Color SpringGreen = NamedColors<Color>.SpringGreen;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #4682B4.
        /// </summary>
        public static readonly Color SteelBlue = NamedColors<Color>.SteelBlue;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #D2B48C.
        /// </summary>
        public static readonly Color Tan = NamedColors<Color>.Tan;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #008080.
        /// </summary>
        public static readonly Color Teal = NamedColors<Color>.Teal;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #D8BFD8.
        /// </summary>
        public static readonly Color Thistle = NamedColors<Color>.Thistle;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FF6347.
        /// </summary>
        public static readonly Color Tomato = NamedColors<Color>.Tomato;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFFFF.
        /// </summary>
        public static readonly Color Transparent = NamedColors<Color>.Transparent;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #40E0D0.
        /// </summary>
        public static readonly Color Turquoise = NamedColors<Color>.Turquoise;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #EE82EE.
        /// </summary>
        public static readonly Color Violet = NamedColors<Color>.Violet;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F5DEB3.
        /// </summary>
        public static readonly Color Wheat = NamedColors<Color>.Wheat;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFFFF.
        /// </summary>
        public static readonly Color White = NamedColors<Color>.White;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #F5F5F5.
        /// </summary>
        public static readonly Color WhiteSmoke = NamedColors<Color>.WhiteSmoke;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #FFFF00.
        /// </summary>
        public static readonly Color Yellow = NamedColors<Color>.Yellow;

        /// <summary>
        /// Represents a <see cref="Color"/> matching the W3C definition that has an hex value of #9ACD32.
        /// </summary>
        public static readonly Color YellowGreen = NamedColors<Color>.YellowGreen;
    }
}