// <copyright file="FileTestBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// ReSharper disable MemberHidesStaticFromOuterClass
namespace ImageSharp.Tests
{
    using System.Linq;

    /// <summary>
    /// Class that contains all the relative test image paths in the TestImages/Formats directory.
    /// Use with <see cref="WithFileAttribute"/>, <see cref="WithFileCollectionAttribute"/> or <see cref="FileTestBase"/>.
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
            public const string Cross = "Png/cross.png";
            public const string Powerpoint = "Png/pp.png";
            public const string SplashInterlaced = "Png/splash-interlaced.png";
            public const string Interlaced = "Png/interlaced.png";
            public const string Rgb48Bpp = "Png/rgb-48bpp.png";
            public const string Rgb48BppInterlaced = "Png/rgb-48bpp-interlaced.png";

            // Filtered test images from http://www.schaik.com/pngsuite/pngsuite_fil_png.html
            public const string Filter0 = "Png/filter0.png";
            public const string Filter1 = "Png/filter1.png";
            public const string Filter2 = "Png/filter2.png";
            public const string Filter3 = "Png/filter3.png";
            public const string Filter4 = "Png/filter4.png";

            // Filter changing per scanline
            public const string FilterVar = "Png/filterVar.png";

            public const string VimImage1 = "Png/vim16x16_1.png";
            public const string VimImage2 = "Png/vim16x16_2.png";

            public const string VersioningImage1 = "Png/versioning-1_1.png";
            public const string VersioningImage2 = "Png/versioning-1_2.png";

            public static class Bad
            {
                // Odd chunk lengths
                public const string ChunkLength1 = "Png/chunklength1.png";
                public const string ChunkLength2 = "Png/chunklength2.png";
            }

            public static readonly string[] All =
            {
                P1, Pd, Blur, Splash, Cross,
                Powerpoint, SplashInterlaced, Interlaced,
                Filter0, Filter1, Filter2, Filter3, Filter4,
                FilterVar, VimImage1, VimImage2, VersioningImage1,
                VersioningImage2
            };
        }

        public static class Jpeg
        {
            public static class Progressive
            {
                public const string Fb = "Jpg/progressive/fb.jpg";
                public const string Progress = "Jpg/progressive/progress.jpg";
                public const string Festzug = "Jpg/progressive/Festzug.jpg";

                public static class Bad
                {
                    public const string BadEOF = "Jpg/progressive/BadEofProgressive.jpg";
                }

                public static readonly string[] All = { Fb, Progress, Festzug };
            }

            public static class Baseline
            {
                public static class Bad
                {
                    public const string MissingEOF = "Jpg/baseline/badeof.jpg";
                    public const string ExifUndefType = "Jpg/baseline/ExifUndefType.jpg";
                }

                public const string Cmyk = "Jpg/baseline/cmyk.jpg";
                public const string Exif = "Jpg/baseline/exif.jpg";
                public const string Floorplan = "Jpg/baseline/Floorplan.jpg";
                public const string Calliphora = "Jpg/baseline/Calliphora.jpg";
                public const string Ycck = "Jpg/baseline/ycck.jpg";
                public const string Turtle = "Jpg/baseline/turtle.jpg";
                public const string GammaDalaiLamaGray = "Jpg/baseline/gamma_dalai_lama_gray.jpg";
                public const string Hiyamugi = "Jpg/baseline/Hiyamugi.jpg";
                public const string Snake = "Jpg/baseline/Snake.jpg";
                public const string Lake = "Jpg/baseline/Lake.jpg";
                public const string Jpeg400 = "Jpg/baseline/jpeg400jfif.jpg";
                public const string Jpeg420 = "Jpg/baseline/jpeg420exif.jpg";
                public const string Jpeg444 = "Jpg/baseline/jpeg444.jpg";
                public const string Testimgorig = "Jpg/baseline/testorig.jpg";

                public static readonly string[] All =
                    {
                        Cmyk, Ycck, Exif, Floorplan,
                        Calliphora, Turtle, GammaDalaiLamaGray,
                        Hiyamugi, Jpeg400, Jpeg420, Jpeg444,
                    };
            }

            public static readonly string[] All = Baseline.All.Concat(Progressive.All).ToArray();
        }

        public static class Bmp
        {
            public const string Car = "Bmp/Car.bmp";
            public const string F = "Bmp/F.bmp";
            public const string NegHeight = "Bmp/neg_height.bmp";
            public const string CoreHeader = "Bmp/BitmapCoreHeaderQR.bmp";
            public const string V5Header = "Bmp/BITMAPV5HEADER.bmp";
            
            public static readonly string[] All = { Car, F, NegHeight, CoreHeader, V5Header };
        }

        public static class Gif
        {
            public const string Rings = "Gif/rings.gif";
            public const string Giphy = "Gif/giphy.gif";
            public const string Cheers = "Gif/cheers.gif";
            public const string Trans = "Gif/trans.gif";

            public static readonly string[] All = { Rings, Giphy, Cheers, Trans };
        }
    }
}
