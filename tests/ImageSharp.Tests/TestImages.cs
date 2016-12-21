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
        private static readonly string FormatsDirectory = GetFormatsDirectory();

        private static string GetFormatsDirectory()
        {
          // Here for code coverage tests.
          string directory = "TestImages/Formats/";
          if (Directory.Exists(directory))
          {
              return directory;
          }
          return "../../../../TestImages/Formats/";
        }

        public static class Png
        {
            private static readonly string folder = FormatsDirectory + "Png/";

            public static TestFile P1 => new TestFile(folder + "pl.png");
            public static TestFile Pd => new TestFile(folder + "pd.png");
            public static TestFile Blur => new TestFile(folder + "blur.png");
            public static TestFile Indexed => new TestFile(folder + "indexed.png");
            public static TestFile Splash => new TestFile(folder + "splash.png");

            public static TestFile SplashInterlaced => new TestFile(folder + "splash-interlaced.png");

            public static TestFile Interlaced => new TestFile(folder + "interlaced.png");

            // filtered test images from http://www.schaik.com/pngsuite/pngsuite_fil_png.html
            public static TestFile Filter0 => new TestFile(folder + "filter0.png");
            public static TestFile Filter1 => new TestFile(folder + "filter1.png");
            public static TestFile Filter2 => new TestFile(folder + "filter2.png");
            public static TestFile Filter3 => new TestFile(folder + "filter3.png");
            public static TestFile Filter4 => new TestFile(folder + "filter4.png");

            // filter changing per scanline
            public static TestFile FilterVar => new TestFile(folder + "filterVar.png");
        }

        public static class Jpeg
        {
            private static readonly string folder = FormatsDirectory + "Jpg/";

            public static TestFile Cmyk => new TestFile(folder + "cmyk.jpg");
            public static TestFile Exif => new TestFile(folder + "exif.jpg");
            public static TestFile Floorplan => new TestFile(folder + "Floorplan.jpg");
            public static TestFile Calliphora => new TestFile(folder + "Calliphora.jpg");
            public static TestFile Ycck => new TestFile(folder + "ycck.jpg");
            public static TestFile Turtle => new TestFile(folder + "turtle.jpg");
            public static TestFile Fb => new TestFile(folder + "fb.jpg");
            public static TestFile Progress => new TestFile(folder + "progress.jpg");
            public static TestFile GammaDalaiLamaGray => new TestFile(folder + "gamma_dalai_lama_gray.jpg");

            public static TestFile Festzug => new TestFile(folder + "Festzug.jpg");
            public static TestFile Hiyamugi => new TestFile(folder + "Hiyamugi.jpg");

            public static TestFile Jpeg400 => new TestFile(folder + "baseline/jpeg400jfif.jpg");
            public static TestFile Jpeg420 => new TestFile(folder + "baseline/jpeg420exif.jpg");
            public static TestFile Jpeg422 => new TestFile(folder + "baseline/jpeg422jfif.jpg");
            public static TestFile Jpeg444 => new TestFile(folder + "baseline/jpeg444.jpg");
            

            public static readonly TestFile[] All = {
                Cmyk, Exif, Floorplan, Calliphora, Turtle, Fb, Progress, GammaDalaiLamaGray,
                Festzug, Hiyamugi,
                Jpeg400, Jpeg420, Jpeg444,
            };
        }

        public static class Bmp
        {
            private static readonly string folder = FormatsDirectory + "Bmp/";

            public static TestFile Car => new TestFile(folder + "Car.bmp");

            public static TestFile F => new TestFile(folder + "F.bmp");

            public static TestFile NegHeight => new TestFile(folder + "neg_height.bmp");

            public static readonly TestFile[] All = {
                Car, F, NegHeight
            };
        }

        public static class Gif
        {
            private static readonly string folder = FormatsDirectory + "Gif/";

            public static TestFile Rings => new TestFile(folder + "rings.gif");
            public static TestFile Giphy => new TestFile(folder + "giphy.gif");

            public static TestFile Cheers => new TestFile(folder + "cheers.gif");
        }
    }
}
