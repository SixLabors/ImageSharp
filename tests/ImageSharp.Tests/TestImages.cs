// <copyright file="FileTestBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    /// <summary>
    /// Class that contains all the test images.
    /// </summary>
    public static class TestImages
    {
        public static class Png
        {
            public const string P1 = "Png/pl.png";
            public const string Pd = "Png/pd.png";
            public const string Blur = "Png/blur.png";
            public const string Indexed = "Png/indexed.png";
            public const string Splash = "Png/splash.png";
            public const string Powerpoint = "Png/pp.png";

            public const string SplashInterlaced = "Png/splash-interlaced.png";

            public const string Interlaced = "Png/interlaced.png";

            // filtered test images from http://www.schaik.com/pngsuite/pngsuite_fil_png.html
            public const string Filter0 = "Png/filter0.png";
            public const string Filter1 = "Png/filter1.png";
            public const string Filter2 = "Png/filter2.png";
            public const string Filter3 = "Png/filter3.png";
            public const string Filter4 = "Png/filter4.png";

            // filter changing per scanline
            public const string FilterVar = "Png/filterVar.png";
        }

        public static class Jpeg
        {
            public const string Cmyk =  "Jpg/cmyk.jpg";
            public const string Exif = "Jpg/exif.jpg";
            public const string Floorplan = "Jpg/Floorplan.jpg";
            public const string Calliphora = "Jpg/Calliphora.jpg";
            public const string Ycck = "Jpg/ycck.jpg";
            public const string Turtle = "Jpg/turtle.jpg";
            public const string Fb = "Jpg/fb.jpg";
            public const string Progress ="Jpg/progress.jpg";
            public const string GammaDalaiLamaGray = "Jpg/gamma_dalai_lama_gray.jpg";

            public const string Festzug = "Jpg/Festzug.jpg";
            public const string Hiyamugi = "Jpg/Hiyamugi.jpg";

            public const string Snake = "Jpg/Snake.jpg";
            public const string Lake = "Jpg/Lake.jpg";

            public const string Jpeg400 = "Jpg/baseline/jpeg400jfif.jpg";
            public const string Jpeg420 = "Jpg/baseline/jpeg420exif.jpg";
            public const string Jpeg422 = "Jpg/baseline/jpeg422jfif.jpg";
            public const string Jpeg444 = "Jpg/baseline/jpeg444.jpg";

            public static readonly string[] All = {
                Cmyk, Ycck, Exif, Floorplan, Calliphora, Turtle, Fb, Progress, GammaDalaiLamaGray,
                Festzug, Hiyamugi,
                Jpeg400, Jpeg420, Jpeg444,
            };
        }

        public static class Bmp
        {
            public const string Car = "Bmp/Car.bmp";

            public const string F = "Bmp/F.bmp";

            public const string NegHeight = "Bmp/neg_height.bmp";

            public static readonly string[] All = {
                Car, F, NegHeight
            };
        }

        public static class Gif
        {
            public const string Rings = "Gif/rings.gif";
            public const string Giphy = "Gif/giphy.gif";

            public const string Cheers = "Gif/cheers.gif";
        }
    }
}
