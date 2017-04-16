// <copyright file="Color32Definitions.cs" company="James Jackson-South">
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
    public partial struct Color32
    {
        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #F0F8FF.
        /// </summary>
        public static readonly Color32 AliceBlue = NamedColors<Color32>.AliceBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FAEBD7.
        /// </summary>
        public static readonly Color32 AntiqueWhite = NamedColors<Color32>.AntiqueWhite;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #00FFFF.
        /// </summary>
        public static readonly Color32 Aqua = NamedColors<Color32>.Aqua;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #7FFFD4.
        /// </summary>
        public static readonly Color32 Aquamarine = NamedColors<Color32>.Aquamarine;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #F0FFFF.
        /// </summary>
        public static readonly Color32 Azure = NamedColors<Color32>.Azure;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #F5F5DC.
        /// </summary>
        public static readonly Color32 Beige = NamedColors<Color32>.Beige;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFE4C4.
        /// </summary>
        public static readonly Color32 Bisque = NamedColors<Color32>.Bisque;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #000000.
        /// </summary>
        public static readonly Color32 Black = NamedColors<Color32>.Black;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFEBCD.
        /// </summary>
        public static readonly Color32 BlanchedAlmond = NamedColors<Color32>.BlanchedAlmond;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #0000FF.
        /// </summary>
        public static readonly Color32 Blue = NamedColors<Color32>.Blue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #8A2BE2.
        /// </summary>
        public static readonly Color32 BlueViolet = NamedColors<Color32>.BlueViolet;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #A52A2A.
        /// </summary>
        public static readonly Color32 Brown = NamedColors<Color32>.Brown;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #DEB887.
        /// </summary>
        public static readonly Color32 BurlyWood = NamedColors<Color32>.BurlyWood;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #5F9EA0.
        /// </summary>
        public static readonly Color32 CadetBlue = NamedColors<Color32>.CadetBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #7FFF00.
        /// </summary>
        public static readonly Color32 Chartreuse = NamedColors<Color32>.Chartreuse;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #D2691E.
        /// </summary>
        public static readonly Color32 Chocolate = NamedColors<Color32>.Chocolate;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FF7F50.
        /// </summary>
        public static readonly Color32 Coral = NamedColors<Color32>.Coral;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #6495ED.
        /// </summary>
        public static readonly Color32 CornflowerBlue = NamedColors<Color32>.CornflowerBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFF8DC.
        /// </summary>
        public static readonly Color32 Cornsilk = NamedColors<Color32>.Cornsilk;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #DC143C.
        /// </summary>
        public static readonly Color32 Crimson = NamedColors<Color32>.Crimson;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #00FFFF.
        /// </summary>
        public static readonly Color32 Cyan = NamedColors<Color32>.Cyan;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #00008B.
        /// </summary>
        public static readonly Color32 DarkBlue = NamedColors<Color32>.DarkBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #008B8B.
        /// </summary>
        public static readonly Color32 DarkCyan = NamedColors<Color32>.DarkCyan;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #B8860B.
        /// </summary>
        public static readonly Color32 DarkGoldenrod = NamedColors<Color32>.DarkGoldenrod;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #A9A9A9.
        /// </summary>
        public static readonly Color32 DarkGray = NamedColors<Color32>.DarkGray;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #006400.
        /// </summary>
        public static readonly Color32 DarkGreen = NamedColors<Color32>.DarkGreen;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #BDB76B.
        /// </summary>
        public static readonly Color32 DarkKhaki = NamedColors<Color32>.DarkKhaki;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #8B008B.
        /// </summary>
        public static readonly Color32 DarkMagenta = NamedColors<Color32>.DarkMagenta;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #556B2F.
        /// </summary>
        public static readonly Color32 DarkOliveGreen = NamedColors<Color32>.DarkOliveGreen;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FF8C00.
        /// </summary>
        public static readonly Color32 DarkOrange = NamedColors<Color32>.DarkOrange;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #9932CC.
        /// </summary>
        public static readonly Color32 DarkOrchid = NamedColors<Color32>.DarkOrchid;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #8B0000.
        /// </summary>
        public static readonly Color32 DarkRed = NamedColors<Color32>.DarkRed;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #E9967A.
        /// </summary>
        public static readonly Color32 DarkSalmon = NamedColors<Color32>.DarkSalmon;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #8FBC8B.
        /// </summary>
        public static readonly Color32 DarkSeaGreen = NamedColors<Color32>.DarkSeaGreen;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #483D8B.
        /// </summary>
        public static readonly Color32 DarkSlateBlue = NamedColors<Color32>.DarkSlateBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #2F4F4F.
        /// </summary>
        public static readonly Color32 DarkSlateGray = NamedColors<Color32>.DarkSlateGray;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #00CED1.
        /// </summary>
        public static readonly Color32 DarkTurquoise = NamedColors<Color32>.DarkTurquoise;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #9400D3.
        /// </summary>
        public static readonly Color32 DarkViolet = NamedColors<Color32>.DarkViolet;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FF1493.
        /// </summary>
        public static readonly Color32 DeepPink = NamedColors<Color32>.DeepPink;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #00BFFF.
        /// </summary>
        public static readonly Color32 DeepSkyBlue = NamedColors<Color32>.DeepSkyBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #696969.
        /// </summary>
        public static readonly Color32 DimGray = NamedColors<Color32>.DimGray;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #1E90FF.
        /// </summary>
        public static readonly Color32 DodgerBlue = NamedColors<Color32>.DodgerBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #B22222.
        /// </summary>
        public static readonly Color32 Firebrick = NamedColors<Color32>.Firebrick;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFFAF0.
        /// </summary>
        public static readonly Color32 FloralWhite = NamedColors<Color32>.FloralWhite;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #228B22.
        /// </summary>
        public static readonly Color32 ForestGreen = NamedColors<Color32>.ForestGreen;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FF00FF.
        /// </summary>
        public static readonly Color32 Fuchsia = NamedColors<Color32>.Fuchsia;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #DCDCDC.
        /// </summary>
        public static readonly Color32 Gainsboro = NamedColors<Color32>.Gainsboro;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #F8F8FF.
        /// </summary>
        public static readonly Color32 GhostWhite = NamedColors<Color32>.GhostWhite;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFD700.
        /// </summary>
        public static readonly Color32 Gold = NamedColors<Color32>.Gold;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #DAA520.
        /// </summary>
        public static readonly Color32 Goldenrod = NamedColors<Color32>.Goldenrod;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #808080.
        /// </summary>
        public static readonly Color32 Gray = NamedColors<Color32>.Gray;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #008000.
        /// </summary>
        public static readonly Color32 Green = NamedColors<Color32>.Green;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #ADFF2F.
        /// </summary>
        public static readonly Color32 GreenYellow = NamedColors<Color32>.GreenYellow;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #F0FFF0.
        /// </summary>
        public static readonly Color32 Honeydew = NamedColors<Color32>.Honeydew;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FF69B4.
        /// </summary>
        public static readonly Color32 HotPink = NamedColors<Color32>.HotPink;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #CD5C5C.
        /// </summary>
        public static readonly Color32 IndianRed = NamedColors<Color32>.IndianRed;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #4B0082.
        /// </summary>
        public static readonly Color32 Indigo = NamedColors<Color32>.Indigo;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFFFF0.
        /// </summary>
        public static readonly Color32 Ivory = NamedColors<Color32>.Ivory;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #F0E68C.
        /// </summary>
        public static readonly Color32 Khaki = NamedColors<Color32>.Khaki;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #E6E6FA.
        /// </summary>
        public static readonly Color32 Lavender = NamedColors<Color32>.Lavender;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFF0F5.
        /// </summary>
        public static readonly Color32 LavenderBlush = NamedColors<Color32>.LavenderBlush;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #7CFC00.
        /// </summary>
        public static readonly Color32 LawnGreen = NamedColors<Color32>.LawnGreen;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFFACD.
        /// </summary>
        public static readonly Color32 LemonChiffon = NamedColors<Color32>.LemonChiffon;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #ADD8E6.
        /// </summary>
        public static readonly Color32 LightBlue = NamedColors<Color32>.LightBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #F08080.
        /// </summary>
        public static readonly Color32 LightCoral = NamedColors<Color32>.LightCoral;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #E0FFFF.
        /// </summary>
        public static readonly Color32 LightCyan = NamedColors<Color32>.LightCyan;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FAFAD2.
        /// </summary>
        public static readonly Color32 LightGoldenrodYellow = NamedColors<Color32>.LightGoldenrodYellow;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #D3D3D3.
        /// </summary>
        public static readonly Color32 LightGray = NamedColors<Color32>.LightGray;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #90EE90.
        /// </summary>
        public static readonly Color32 LightGreen = NamedColors<Color32>.LightGreen;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFB6C1.
        /// </summary>
        public static readonly Color32 LightPink = NamedColors<Color32>.LightPink;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFA07A.
        /// </summary>
        public static readonly Color32 LightSalmon = NamedColors<Color32>.LightSalmon;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #20B2AA.
        /// </summary>
        public static readonly Color32 LightSeaGreen = NamedColors<Color32>.LightSeaGreen;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #87CEFA.
        /// </summary>
        public static readonly Color32 LightSkyBlue = NamedColors<Color32>.LightSkyBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #778899.
        /// </summary>
        public static readonly Color32 LightSlateGray = NamedColors<Color32>.LightSlateGray;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #B0C4DE.
        /// </summary>
        public static readonly Color32 LightSteelBlue = NamedColors<Color32>.LightSteelBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFFFE0.
        /// </summary>
        public static readonly Color32 LightYellow = NamedColors<Color32>.LightYellow;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #00FF00.
        /// </summary>
        public static readonly Color32 Lime = NamedColors<Color32>.Lime;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #32CD32.
        /// </summary>
        public static readonly Color32 LimeGreen = NamedColors<Color32>.LimeGreen;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FAF0E6.
        /// </summary>
        public static readonly Color32 Linen = NamedColors<Color32>.Linen;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FF00FF.
        /// </summary>
        public static readonly Color32 Magenta = NamedColors<Color32>.Magenta;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #800000.
        /// </summary>
        public static readonly Color32 Maroon = NamedColors<Color32>.Maroon;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #66CDAA.
        /// </summary>
        public static readonly Color32 MediumAquamarine = NamedColors<Color32>.MediumAquamarine;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #0000CD.
        /// </summary>
        public static readonly Color32 MediumBlue = NamedColors<Color32>.MediumBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #BA55D3.
        /// </summary>
        public static readonly Color32 MediumOrchid = NamedColors<Color32>.MediumOrchid;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #9370DB.
        /// </summary>
        public static readonly Color32 MediumPurple = NamedColors<Color32>.MediumPurple;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #3CB371.
        /// </summary>
        public static readonly Color32 MediumSeaGreen = NamedColors<Color32>.MediumSeaGreen;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #7B68EE.
        /// </summary>
        public static readonly Color32 MediumSlateBlue = NamedColors<Color32>.MediumSlateBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #00FA9A.
        /// </summary>
        public static readonly Color32 MediumSpringGreen = NamedColors<Color32>.MediumSpringGreen;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #48D1CC.
        /// </summary>
        public static readonly Color32 MediumTurquoise = NamedColors<Color32>.MediumTurquoise;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #C71585.
        /// </summary>
        public static readonly Color32 MediumVioletRed = NamedColors<Color32>.MediumVioletRed;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #191970.
        /// </summary>
        public static readonly Color32 MidnightBlue = NamedColors<Color32>.MidnightBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #F5FFFA.
        /// </summary>
        public static readonly Color32 MintCream = NamedColors<Color32>.MintCream;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFE4E1.
        /// </summary>
        public static readonly Color32 MistyRose = NamedColors<Color32>.MistyRose;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFE4B5.
        /// </summary>
        public static readonly Color32 Moccasin = NamedColors<Color32>.Moccasin;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFDEAD.
        /// </summary>
        public static readonly Color32 NavajoWhite = NamedColors<Color32>.NavajoWhite;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #000080.
        /// </summary>
        public static readonly Color32 Navy = NamedColors<Color32>.Navy;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FDF5E6.
        /// </summary>
        public static readonly Color32 OldLace = NamedColors<Color32>.OldLace;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #808000.
        /// </summary>
        public static readonly Color32 Olive = NamedColors<Color32>.Olive;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #6B8E23.
        /// </summary>
        public static readonly Color32 OliveDrab = NamedColors<Color32>.OliveDrab;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFA500.
        /// </summary>
        public static readonly Color32 Orange = NamedColors<Color32>.Orange;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FF4500.
        /// </summary>
        public static readonly Color32 OrangeRed = NamedColors<Color32>.OrangeRed;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #DA70D6.
        /// </summary>
        public static readonly Color32 Orchid = NamedColors<Color32>.Orchid;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #EEE8AA.
        /// </summary>
        public static readonly Color32 PaleGoldenrod = NamedColors<Color32>.PaleGoldenrod;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #98FB98.
        /// </summary>
        public static readonly Color32 PaleGreen = NamedColors<Color32>.PaleGreen;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #AFEEEE.
        /// </summary>
        public static readonly Color32 PaleTurquoise = NamedColors<Color32>.PaleTurquoise;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #DB7093.
        /// </summary>
        public static readonly Color32 PaleVioletRed = NamedColors<Color32>.PaleVioletRed;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFEFD5.
        /// </summary>
        public static readonly Color32 PapayaWhip = NamedColors<Color32>.PapayaWhip;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFDAB9.
        /// </summary>
        public static readonly Color32 PeachPuff = NamedColors<Color32>.PeachPuff;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #CD853F.
        /// </summary>
        public static readonly Color32 Peru = NamedColors<Color32>.Peru;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFC0CB.
        /// </summary>
        public static readonly Color32 Pink = NamedColors<Color32>.Pink;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #DDA0DD.
        /// </summary>
        public static readonly Color32 Plum = NamedColors<Color32>.Plum;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #B0E0E6.
        /// </summary>
        public static readonly Color32 PowderBlue = NamedColors<Color32>.PowderBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #800080.
        /// </summary>
        public static readonly Color32 Purple = NamedColors<Color32>.Purple;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #663399.
        /// </summary>
        public static readonly Color32 RebeccaPurple = NamedColors<Color32>.RebeccaPurple;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FF0000.
        /// </summary>
        public static readonly Color32 Red = NamedColors<Color32>.Red;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #BC8F8F.
        /// </summary>
        public static readonly Color32 RosyBrown = NamedColors<Color32>.RosyBrown;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #4169E1.
        /// </summary>
        public static readonly Color32 RoyalBlue = NamedColors<Color32>.RoyalBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #8B4513.
        /// </summary>
        public static readonly Color32 SaddleBrown = NamedColors<Color32>.SaddleBrown;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FA8072.
        /// </summary>
        public static readonly Color32 Salmon = NamedColors<Color32>.Salmon;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #F4A460.
        /// </summary>
        public static readonly Color32 SandyBrown = NamedColors<Color32>.SandyBrown;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #2E8B57.
        /// </summary>
        public static readonly Color32 SeaGreen = NamedColors<Color32>.SeaGreen;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFF5EE.
        /// </summary>
        public static readonly Color32 SeaShell = NamedColors<Color32>.SeaShell;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #A0522D.
        /// </summary>
        public static readonly Color32 Sienna = NamedColors<Color32>.Sienna;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #C0C0C0.
        /// </summary>
        public static readonly Color32 Silver = NamedColors<Color32>.Silver;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #87CEEB.
        /// </summary>
        public static readonly Color32 SkyBlue = NamedColors<Color32>.SkyBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #6A5ACD.
        /// </summary>
        public static readonly Color32 SlateBlue = NamedColors<Color32>.SlateBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #708090.
        /// </summary>
        public static readonly Color32 SlateGray = NamedColors<Color32>.SlateGray;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFFAFA.
        /// </summary>
        public static readonly Color32 Snow = NamedColors<Color32>.Snow;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #00FF7F.
        /// </summary>
        public static readonly Color32 SpringGreen = NamedColors<Color32>.SpringGreen;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #4682B4.
        /// </summary>
        public static readonly Color32 SteelBlue = NamedColors<Color32>.SteelBlue;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #D2B48C.
        /// </summary>
        public static readonly Color32 Tan = NamedColors<Color32>.Tan;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #008080.
        /// </summary>
        public static readonly Color32 Teal = NamedColors<Color32>.Teal;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #D8BFD8.
        /// </summary>
        public static readonly Color32 Thistle = NamedColors<Color32>.Thistle;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FF6347.
        /// </summary>
        public static readonly Color32 Tomato = NamedColors<Color32>.Tomato;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFFFFF.
        /// </summary>
        public static readonly Color32 Transparent = NamedColors<Color32>.Transparent;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #40E0D0.
        /// </summary>
        public static readonly Color32 Turquoise = NamedColors<Color32>.Turquoise;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #EE82EE.
        /// </summary>
        public static readonly Color32 Violet = NamedColors<Color32>.Violet;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #F5DEB3.
        /// </summary>
        public static readonly Color32 Wheat = NamedColors<Color32>.Wheat;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFFFFF.
        /// </summary>
        public static readonly Color32 White = NamedColors<Color32>.White;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #F5F5F5.
        /// </summary>
        public static readonly Color32 WhiteSmoke = NamedColors<Color32>.WhiteSmoke;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #FFFF00.
        /// </summary>
        public static readonly Color32 Yellow = NamedColors<Color32>.Yellow;

        /// <summary>
        /// Represents a <see cref="Color32"/> matching the W3C definition that has an hex value of #9ACD32.
        /// </summary>
        public static readonly Color32 YellowGreen = NamedColors<Color32>.YellowGreen;
    }
}