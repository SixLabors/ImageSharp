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
            private static readonly string folder = "../../TestImages/Formats/Png/";

            public static string P1 => folder + "pl.png";
            public static string Pd => folder + "pd.png";
            public static string Blur => folder + "blur.png";
            public static string Indexed => folder + "indexed.png";
            public static string Splash => folder + "splash.png";
        }

        public static class Jpeg
        {
            private const string Folder = "../../TestImages/Formats/Jpg/";
            public const string Cmyk = Folder + "cmyk.jpg";
            public const string Exif = Folder + "exif.jpg";
            public const string Floorplan = Folder + "Floorplan.jpeg";
            public const string Calliphora = Folder + "Calliphora.jpg";
            public const string Turtle = Folder + "turtle.jpg";
            public const string Fb = Folder + "fb.jpg";
            public const string Progress = Folder + "progress.jpg";
            public const string GammaDalaiLamaGray = Folder + "gamma_dalai_lama_gray.jpg";

            public static readonly string[] All = new[]
            {
                Cmyk, Exif, Floorplan, Calliphora, Turtle, Fb, Progress, GammaDalaiLamaGray
            };
        }

        public static class Bmp
        {
            private static readonly string folder = "../../TestImages/Formats/Bmp/";

            public static string Car => folder + "Car.bmp";

            public static string F => folder + "F.bmp";

            public static string NegHeight => folder + "neg_height.bmp";
        }

        public static class Gif
        {
            private static readonly string folder = "../../TestImages/Formats/Gif/";

            public static string Rings => folder + "rings.gif";
            public static string Giphy => folder + "giphy.gif";
        }
    }
}
