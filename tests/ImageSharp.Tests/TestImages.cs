// <copyright file="FileTestBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    /// <summary>
    /// Class that contains all the test images.
    /// </summary>
    public static class TestImages
    {
        public static class Png
        {
            private static readonly string folder = "TestImages/Formats/Png/";

            public static string P1 => folder + "pl.png";
            public static string Pd => folder + "pd.png";
            public static string Blur => folder + "blur.png";
            public static string Indexed => folder + "indexed.png";
            public static string Splash => folder + "splash.png";

            public static string SplashInterlaced => folder + "splash-interlaced.png";

            public static string Interlaced => folder + "interlaced.png";

            // filtered test images from http://www.schaik.com/pngsuite/pngsuite_fil_png.html
            public static string Filter0 => folder + "filter0.png";
            public static string Filter1 => folder + "filter1.png";
            public static string Filter2 => folder + "filter2.png";
            public static string Filter3 => folder + "filter3.png";
            public static string Filter4 => folder + "filter4.png";
            // filter changing per scanline     
            public static string FilterVar => folder + "filterVar.png";
        }

        public static class Jpeg
        {
            private static readonly string folder = "TestImages/Formats/Jpg/";
            public static string Cmyk => folder + "cmyk.jpg";
            public static string Exif => folder + "exif.jpg";
            public static string Floorplan => folder + "Floorplan.jpg";
            public static string Calliphora => folder + "Calliphora.jpg";
            public static string Turtle => folder + "turtle.jpg";
            public static string Fb => folder + "fb.jpg";
            public static string Progress => folder + "progress.jpg";
            public static string GammaDalaiLamaGray => folder + "gamma_dalai_lama_gray.jpg";

            public static string Festzug => folder + "Festzug.jpg";
            public static string Hiyamugi => folder + "Hiyamugi.jpg";

            public static string Jpeg400 => folder + "baseline/jpeg400jfif.jpg";
            public static string Jpeg420 => folder + "baseline/jpeg420exif.jpg";
            public static string Jpeg422 => folder + "baseline/jpeg422jfif.jpg";
            public static string Jpeg444 => folder + "baseline/jpeg444.jpg";
            

            public static readonly string[] All = {
                Cmyk, Exif, Floorplan, Calliphora, Turtle, Fb, Progress, GammaDalaiLamaGray,
                Festzug, Hiyamugi,
                Jpeg400, Jpeg420, Jpeg444,
            };
        }

        public static class Bmp
        {
            private const string Folder = "TestImages/Formats/Bmp/";

            public const string Car = Folder + "Car.bmp";

            public const string F = Folder + "F.bmp";

            // ReSharper disable once InconsistentNaming
            public const string RGB3x3 = Folder + "RGB3x3.bmp";

            public const string NegHeight = Folder + "neg_height.bmp";

            public static readonly string[] All = {
                Car, F, NegHeight, RGB3x3
            };
        }

        public static class Gif
        {
            private static readonly string folder = "TestImages/Formats/Gif/";

            public static string Rings => folder + "rings.gif";
            public static string Giphy => folder + "giphy.gif";
        }
    }
}
