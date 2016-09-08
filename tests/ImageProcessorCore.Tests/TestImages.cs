// <copyright file="FileTestBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
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
        }

        public static class Jpg
        {
            private static readonly string folder = "TestImages/Formats/Jpg/";
            public static string Cmyk => folder + "cmyk.jpg";
            public static string Exif => folder + "exif.jpeg";
            public static string Floorplan => folder + "Floorplan.jpeg";
            public static string Calliphora => folder + "Calliphora.jpg";
            public static string Turtle => folder + "turtle.jpg";
            public static string Fb => folder + "fb.jpg";
            public static string Progress => folder + "progress.jpg";
            public static string GammaDalaiLamaGray => folder + "gamma_dalai_lama_gray.jpg";
        }

        public static class Bmp
        {
            private static readonly string folder = "TestImages/Formats/Bmp/";

            public static string Car => folder + "Car.bmp";

            public static string F => folder + "F.bmp";

            public static string NegHeight => folder + "neg_height.bmp";
        }

        public static class Gif
        {
            private static readonly string folder = "TestImages/Formats/Gif/";

            public static string Rings => folder + "rings.gif";
            public static string Giphy => folder + "giphy.gif";
        }
    }
}
