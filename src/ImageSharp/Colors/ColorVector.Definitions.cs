// <copyright file="ColorVector.Definitions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// Unpacked pixel type containing four 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in red, green, blue, and alpha order.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public partial struct ColorVector
    {
        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #F0F8FF.
        /// </summary>
        public static readonly ColorVector AliceBlue = NamedColors<ColorVector>.AliceBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FAEBD7.
        /// </summary>
        public static readonly ColorVector AntiqueWhite = NamedColors<ColorVector>.AntiqueWhite;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #00FFFF.
        /// </summary>
        public static readonly ColorVector Aqua = NamedColors<ColorVector>.Aqua;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #7FFFD4.
        /// </summary>
        public static readonly ColorVector Aquamarine = NamedColors<ColorVector>.Aquamarine;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #F0FFFF.
        /// </summary>
        public static readonly ColorVector Azure = NamedColors<ColorVector>.Azure;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #F5F5DC.
        /// </summary>
        public static readonly ColorVector Beige = NamedColors<ColorVector>.Beige;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFE4C4.
        /// </summary>
        public static readonly ColorVector Bisque = NamedColors<ColorVector>.Bisque;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #000000.
        /// </summary>
        public static readonly ColorVector Black = NamedColors<ColorVector>.Black;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFEBCD.
        /// </summary>
        public static readonly ColorVector BlanchedAlmond = NamedColors<ColorVector>.BlanchedAlmond;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #0000FF.
        /// </summary>
        public static readonly ColorVector Blue = NamedColors<ColorVector>.Blue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #8A2BE2.
        /// </summary>
        public static readonly ColorVector BlueViolet = NamedColors<ColorVector>.BlueViolet;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #A52A2A.
        /// </summary>
        public static readonly ColorVector Brown = NamedColors<ColorVector>.Brown;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #DEB887.
        /// </summary>
        public static readonly ColorVector BurlyWood = NamedColors<ColorVector>.BurlyWood;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #5F9EA0.
        /// </summary>
        public static readonly ColorVector CadetBlue = NamedColors<ColorVector>.CadetBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #7FFF00.
        /// </summary>
        public static readonly ColorVector Chartreuse = NamedColors<ColorVector>.Chartreuse;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #D2691E.
        /// </summary>
        public static readonly ColorVector Chocolate = NamedColors<ColorVector>.Chocolate;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FF7F50.
        /// </summary>
        public static readonly ColorVector Coral = NamedColors<ColorVector>.Coral;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #6495ED.
        /// </summary>
        public static readonly ColorVector CornflowerBlue = NamedColors<ColorVector>.CornflowerBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFF8DC.
        /// </summary>
        public static readonly ColorVector Cornsilk = NamedColors<ColorVector>.Cornsilk;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #DC143C.
        /// </summary>
        public static readonly ColorVector Crimson = NamedColors<ColorVector>.Crimson;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #00FFFF.
        /// </summary>
        public static readonly ColorVector Cyan = NamedColors<ColorVector>.Cyan;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #00008B.
        /// </summary>
        public static readonly ColorVector DarkBlue = NamedColors<ColorVector>.DarkBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #008B8B.
        /// </summary>
        public static readonly ColorVector DarkCyan = NamedColors<ColorVector>.DarkCyan;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #B8860B.
        /// </summary>
        public static readonly ColorVector DarkGoldenrod = NamedColors<ColorVector>.DarkGoldenrod;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #A9A9A9.
        /// </summary>
        public static readonly ColorVector DarkGray = NamedColors<ColorVector>.DarkGray;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #006400.
        /// </summary>
        public static readonly ColorVector DarkGreen = NamedColors<ColorVector>.DarkGreen;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #BDB76B.
        /// </summary>
        public static readonly ColorVector DarkKhaki = NamedColors<ColorVector>.DarkKhaki;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #8B008B.
        /// </summary>
        public static readonly ColorVector DarkMagenta = NamedColors<ColorVector>.DarkMagenta;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #556B2F.
        /// </summary>
        public static readonly ColorVector DarkOliveGreen = NamedColors<ColorVector>.DarkOliveGreen;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FF8C00.
        /// </summary>
        public static readonly ColorVector DarkOrange = NamedColors<ColorVector>.DarkOrange;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #9932CC.
        /// </summary>
        public static readonly ColorVector DarkOrchid = NamedColors<ColorVector>.DarkOrchid;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #8B0000.
        /// </summary>
        public static readonly ColorVector DarkRed = NamedColors<ColorVector>.DarkRed;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #E9967A.
        /// </summary>
        public static readonly ColorVector DarkSalmon = NamedColors<ColorVector>.DarkSalmon;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #8FBC8B.
        /// </summary>
        public static readonly ColorVector DarkSeaGreen = NamedColors<ColorVector>.DarkSeaGreen;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #483D8B.
        /// </summary>
        public static readonly ColorVector DarkSlateBlue = NamedColors<ColorVector>.DarkSlateBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #2F4F4F.
        /// </summary>
        public static readonly ColorVector DarkSlateGray = NamedColors<ColorVector>.DarkSlateGray;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #00CED1.
        /// </summary>
        public static readonly ColorVector DarkTurquoise = NamedColors<ColorVector>.DarkTurquoise;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #9400D3.
        /// </summary>
        public static readonly ColorVector DarkViolet = NamedColors<ColorVector>.DarkViolet;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FF1493.
        /// </summary>
        public static readonly ColorVector DeepPink = NamedColors<ColorVector>.DeepPink;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #00BFFF.
        /// </summary>
        public static readonly ColorVector DeepSkyBlue = NamedColors<ColorVector>.DeepSkyBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #696969.
        /// </summary>
        public static readonly ColorVector DimGray = NamedColors<ColorVector>.DimGray;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #1E90FF.
        /// </summary>
        public static readonly ColorVector DodgerBlue = NamedColors<ColorVector>.DodgerBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #B22222.
        /// </summary>
        public static readonly ColorVector Firebrick = NamedColors<ColorVector>.Firebrick;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFFAF0.
        /// </summary>
        public static readonly ColorVector FloralWhite = NamedColors<ColorVector>.FloralWhite;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #228B22.
        /// </summary>
        public static readonly ColorVector ForestGreen = NamedColors<ColorVector>.ForestGreen;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FF00FF.
        /// </summary>
        public static readonly ColorVector Fuchsia = NamedColors<ColorVector>.Fuchsia;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #DCDCDC.
        /// </summary>
        public static readonly ColorVector Gainsboro = NamedColors<ColorVector>.Gainsboro;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #F8F8FF.
        /// </summary>
        public static readonly ColorVector GhostWhite = NamedColors<ColorVector>.GhostWhite;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFD700.
        /// </summary>
        public static readonly ColorVector Gold = NamedColors<ColorVector>.Gold;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #DAA520.
        /// </summary>
        public static readonly ColorVector Goldenrod = NamedColors<ColorVector>.Goldenrod;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #808080.
        /// </summary>
        public static readonly ColorVector Gray = NamedColors<ColorVector>.Gray;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #008000.
        /// </summary>
        public static readonly ColorVector Green = NamedColors<ColorVector>.Green;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #ADFF2F.
        /// </summary>
        public static readonly ColorVector GreenYellow = NamedColors<ColorVector>.GreenYellow;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #F0FFF0.
        /// </summary>
        public static readonly ColorVector Honeydew = NamedColors<ColorVector>.Honeydew;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FF69B4.
        /// </summary>
        public static readonly ColorVector HotPink = NamedColors<ColorVector>.HotPink;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #CD5C5C.
        /// </summary>
        public static readonly ColorVector IndianRed = NamedColors<ColorVector>.IndianRed;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #4B0082.
        /// </summary>
        public static readonly ColorVector Indigo = NamedColors<ColorVector>.Indigo;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFFFF0.
        /// </summary>
        public static readonly ColorVector Ivory = NamedColors<ColorVector>.Ivory;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #F0E68C.
        /// </summary>
        public static readonly ColorVector Khaki = NamedColors<ColorVector>.Khaki;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #E6E6FA.
        /// </summary>
        public static readonly ColorVector Lavender = NamedColors<ColorVector>.Lavender;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFF0F5.
        /// </summary>
        public static readonly ColorVector LavenderBlush = NamedColors<ColorVector>.LavenderBlush;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #7CFC00.
        /// </summary>
        public static readonly ColorVector LawnGreen = NamedColors<ColorVector>.LawnGreen;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFFACD.
        /// </summary>
        public static readonly ColorVector LemonChiffon = NamedColors<ColorVector>.LemonChiffon;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #ADD8E6.
        /// </summary>
        public static readonly ColorVector LightBlue = NamedColors<ColorVector>.LightBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #F08080.
        /// </summary>
        public static readonly ColorVector LightCoral = NamedColors<ColorVector>.LightCoral;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #E0FFFF.
        /// </summary>
        public static readonly ColorVector LightCyan = NamedColors<ColorVector>.LightCyan;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FAFAD2.
        /// </summary>
        public static readonly ColorVector LightGoldenrodYellow = NamedColors<ColorVector>.LightGoldenrodYellow;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #D3D3D3.
        /// </summary>
        public static readonly ColorVector LightGray = NamedColors<ColorVector>.LightGray;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #90EE90.
        /// </summary>
        public static readonly ColorVector LightGreen = NamedColors<ColorVector>.LightGreen;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFB6C1.
        /// </summary>
        public static readonly ColorVector LightPink = NamedColors<ColorVector>.LightPink;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFA07A.
        /// </summary>
        public static readonly ColorVector LightSalmon = NamedColors<ColorVector>.LightSalmon;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #20B2AA.
        /// </summary>
        public static readonly ColorVector LightSeaGreen = NamedColors<ColorVector>.LightSeaGreen;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #87CEFA.
        /// </summary>
        public static readonly ColorVector LightSkyBlue = NamedColors<ColorVector>.LightSkyBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #778899.
        /// </summary>
        public static readonly ColorVector LightSlateGray = NamedColors<ColorVector>.LightSlateGray;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #B0C4DE.
        /// </summary>
        public static readonly ColorVector LightSteelBlue = NamedColors<ColorVector>.LightSteelBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFFFE0.
        /// </summary>
        public static readonly ColorVector LightYellow = NamedColors<ColorVector>.LightYellow;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #00FF00.
        /// </summary>
        public static readonly ColorVector Lime = NamedColors<ColorVector>.Lime;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #32CD32.
        /// </summary>
        public static readonly ColorVector LimeGreen = NamedColors<ColorVector>.LimeGreen;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FAF0E6.
        /// </summary>
        public static readonly ColorVector Linen = NamedColors<ColorVector>.Linen;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FF00FF.
        /// </summary>
        public static readonly ColorVector Magenta = NamedColors<ColorVector>.Magenta;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #800000.
        /// </summary>
        public static readonly ColorVector Maroon = NamedColors<ColorVector>.Maroon;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #66CDAA.
        /// </summary>
        public static readonly ColorVector MediumAquamarine = NamedColors<ColorVector>.MediumAquamarine;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #0000CD.
        /// </summary>
        public static readonly ColorVector MediumBlue = NamedColors<ColorVector>.MediumBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #BA55D3.
        /// </summary>
        public static readonly ColorVector MediumOrchid = NamedColors<ColorVector>.MediumOrchid;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #9370DB.
        /// </summary>
        public static readonly ColorVector MediumPurple = NamedColors<ColorVector>.MediumPurple;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #3CB371.
        /// </summary>
        public static readonly ColorVector MediumSeaGreen = NamedColors<ColorVector>.MediumSeaGreen;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #7B68EE.
        /// </summary>
        public static readonly ColorVector MediumSlateBlue = NamedColors<ColorVector>.MediumSlateBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #00FA9A.
        /// </summary>
        public static readonly ColorVector MediumSpringGreen = NamedColors<ColorVector>.MediumSpringGreen;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #48D1CC.
        /// </summary>
        public static readonly ColorVector MediumTurquoise = NamedColors<ColorVector>.MediumTurquoise;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #C71585.
        /// </summary>
        public static readonly ColorVector MediumVioletRed = NamedColors<ColorVector>.MediumVioletRed;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #191970.
        /// </summary>
        public static readonly ColorVector MidnightBlue = NamedColors<ColorVector>.MidnightBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #F5FFFA.
        /// </summary>
        public static readonly ColorVector MintCream = NamedColors<ColorVector>.MintCream;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFE4E1.
        /// </summary>
        public static readonly ColorVector MistyRose = NamedColors<ColorVector>.MistyRose;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFE4B5.
        /// </summary>
        public static readonly ColorVector Moccasin = NamedColors<ColorVector>.Moccasin;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFDEAD.
        /// </summary>
        public static readonly ColorVector NavajoWhite = NamedColors<ColorVector>.NavajoWhite;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #000080.
        /// </summary>
        public static readonly ColorVector Navy = NamedColors<ColorVector>.Navy;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FDF5E6.
        /// </summary>
        public static readonly ColorVector OldLace = NamedColors<ColorVector>.OldLace;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #808000.
        /// </summary>
        public static readonly ColorVector Olive = NamedColors<ColorVector>.Olive;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #6B8E23.
        /// </summary>
        public static readonly ColorVector OliveDrab = NamedColors<ColorVector>.OliveDrab;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFA500.
        /// </summary>
        public static readonly ColorVector Orange = NamedColors<ColorVector>.Orange;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FF4500.
        /// </summary>
        public static readonly ColorVector OrangeRed = NamedColors<ColorVector>.OrangeRed;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #DA70D6.
        /// </summary>
        public static readonly ColorVector Orchid = NamedColors<ColorVector>.Orchid;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #EEE8AA.
        /// </summary>
        public static readonly ColorVector PaleGoldenrod = NamedColors<ColorVector>.PaleGoldenrod;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #98FB98.
        /// </summary>
        public static readonly ColorVector PaleGreen = NamedColors<ColorVector>.PaleGreen;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #AFEEEE.
        /// </summary>
        public static readonly ColorVector PaleTurquoise = NamedColors<ColorVector>.PaleTurquoise;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #DB7093.
        /// </summary>
        public static readonly ColorVector PaleVioletRed = NamedColors<ColorVector>.PaleVioletRed;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFEFD5.
        /// </summary>
        public static readonly ColorVector PapayaWhip = NamedColors<ColorVector>.PapayaWhip;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFDAB9.
        /// </summary>
        public static readonly ColorVector PeachPuff = NamedColors<ColorVector>.PeachPuff;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #CD853F.
        /// </summary>
        public static readonly ColorVector Peru = NamedColors<ColorVector>.Peru;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFC0CB.
        /// </summary>
        public static readonly ColorVector Pink = NamedColors<ColorVector>.Pink;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #DDA0DD.
        /// </summary>
        public static readonly ColorVector Plum = NamedColors<ColorVector>.Plum;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #B0E0E6.
        /// </summary>
        public static readonly ColorVector PowderBlue = NamedColors<ColorVector>.PowderBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #800080.
        /// </summary>
        public static readonly ColorVector Purple = NamedColors<ColorVector>.Purple;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #663399.
        /// </summary>
        public static readonly ColorVector RebeccaPurple = NamedColors<ColorVector>.RebeccaPurple;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FF0000.
        /// </summary>
        public static readonly ColorVector Red = NamedColors<ColorVector>.Red;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #BC8F8F.
        /// </summary>
        public static readonly ColorVector RosyBrown = NamedColors<ColorVector>.RosyBrown;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #4169E1.
        /// </summary>
        public static readonly ColorVector RoyalBlue = NamedColors<ColorVector>.RoyalBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #8B4513.
        /// </summary>
        public static readonly ColorVector SaddleBrown = NamedColors<ColorVector>.SaddleBrown;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FA8072.
        /// </summary>
        public static readonly ColorVector Salmon = NamedColors<ColorVector>.Salmon;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #F4A460.
        /// </summary>
        public static readonly ColorVector SandyBrown = NamedColors<ColorVector>.SandyBrown;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #2E8B57.
        /// </summary>
        public static readonly ColorVector SeaGreen = NamedColors<ColorVector>.SeaGreen;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFF5EE.
        /// </summary>
        public static readonly ColorVector SeaShell = NamedColors<ColorVector>.SeaShell;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #A0522D.
        /// </summary>
        public static readonly ColorVector Sienna = NamedColors<ColorVector>.Sienna;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #C0C0C0.
        /// </summary>
        public static readonly ColorVector Silver = NamedColors<ColorVector>.Silver;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #87CEEB.
        /// </summary>
        public static readonly ColorVector SkyBlue = NamedColors<ColorVector>.SkyBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #6A5ACD.
        /// </summary>
        public static readonly ColorVector SlateBlue = NamedColors<ColorVector>.SlateBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #708090.
        /// </summary>
        public static readonly ColorVector SlateGray = NamedColors<ColorVector>.SlateGray;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFFAFA.
        /// </summary>
        public static readonly ColorVector Snow = NamedColors<ColorVector>.Snow;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #00FF7F.
        /// </summary>
        public static readonly ColorVector SpringGreen = NamedColors<ColorVector>.SpringGreen;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #4682B4.
        /// </summary>
        public static readonly ColorVector SteelBlue = NamedColors<ColorVector>.SteelBlue;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #D2B48C.
        /// </summary>
        public static readonly ColorVector Tan = NamedColors<ColorVector>.Tan;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #008080.
        /// </summary>
        public static readonly ColorVector Teal = NamedColors<ColorVector>.Teal;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #D8BFD8.
        /// </summary>
        public static readonly ColorVector Thistle = NamedColors<ColorVector>.Thistle;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FF6347.
        /// </summary>
        public static readonly ColorVector Tomato = NamedColors<ColorVector>.Tomato;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFFFFF.
        /// </summary>
        public static readonly ColorVector Transparent = NamedColors<ColorVector>.Transparent;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #40E0D0.
        /// </summary>
        public static readonly ColorVector Turquoise = NamedColors<ColorVector>.Turquoise;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #EE82EE.
        /// </summary>
        public static readonly ColorVector Violet = NamedColors<ColorVector>.Violet;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #F5DEB3.
        /// </summary>
        public static readonly ColorVector Wheat = NamedColors<ColorVector>.Wheat;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFFFFF.
        /// </summary>
        public static readonly ColorVector White = NamedColors<ColorVector>.White;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #F5F5F5.
        /// </summary>
        public static readonly ColorVector WhiteSmoke = NamedColors<ColorVector>.WhiteSmoke;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #FFFF00.
        /// </summary>
        public static readonly ColorVector Yellow = NamedColors<ColorVector>.Yellow;

        /// <summary>
        /// Represents a <see cref="ColorVector"/> matching the W3C definition that has an hex value of #9ACD32.
        /// </summary>
        public static readonly ColorVector YellowGreen = NamedColors<ColorVector>.YellowGreen;
    }
}