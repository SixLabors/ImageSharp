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

            public static string P1 { get { return folder + "pl.png"; } }
            public static string Pd { get { return folder + "pd.png"; } }
            public static string Blur { get { return folder + "blur.png"; } }
            public static string Indexed { get { return folder + "indexed.png"; } }
            public static string Splash { get { return folder + "splash.png"; } }
        }

        public static class Jpg
        {
            private static readonly string folder = "TestImages/Formats/Jpg/";
            public static string Cmyk { get { return folder + "cmyk.jpg"; } }
            public static string Exif { get { return folder + "exif.jpeg"; } }
            public static string Floorplan { get { return folder + "Floorplan.jpeg"; } }
            public static string Calliphora { get { return folder + "Calliphora.jpg"; } }
            public static string Turtle { get { return folder + "turtle.jpg"; } }
            public static string Fb { get { return folder + "fb.jpg"; } }
            public static string Progress { get { return folder + "progress.jpg"; } }
            public static string Gamma_dalai_lama_gray { get { return folder + "gamma_dalai_lama_gray.jpg"; } }
        }

        public static class Bmp
        {
            private static readonly string folder = "TestImages/Formats/Bmp/";

            public static string Car { get { return folder + "Car.bmp"; } }

            public static string F { get { return folder + "F.bmp"; } }

            public static string Neg_height { get { return folder + "neg_height.bmp"; } }
        }

        public static class Gif
        {
            private static readonly string folder = "TestImages/Formats/Gif/";

            public static string Rings { get { return folder + "rings.gif"; } }
            public static string Giphy { get { return folder + "giphy.gif"; } }
        }
    }
}
