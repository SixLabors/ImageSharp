// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public partial class JpegDecoderTests
    {
        public static string[] BaselineTestJpegs =
            {
                TestImages.Jpeg.Baseline.Calliphora,
                TestImages.Jpeg.Baseline.Cmyk,
                TestImages.Jpeg.Baseline.Ycck,
                TestImages.Jpeg.Baseline.Jpeg400,
                TestImages.Jpeg.Baseline.Testorig420,

                // BUG: The following image has a high difference compared to the expected output:
                // TestImages.Jpeg.Baseline.Jpeg420Small,

                TestImages.Jpeg.Baseline.Jpeg444,
                TestImages.Jpeg.Baseline.Bad.BadEOF,
                TestImages.Jpeg.Baseline.MultiScanBaselineCMYK,
                TestImages.Jpeg.Baseline.YcckSubsample1222,
                TestImages.Jpeg.Baseline.Bad.BadRST,
                TestImages.Jpeg.Issues.MultiHuffmanBaseline394,
                TestImages.Jpeg.Issues.ExifDecodeOutOfRange694,
                TestImages.Jpeg.Issues.InvalidEOI695,
                TestImages.Jpeg.Issues.ExifResizeOutOfRange696,
                TestImages.Jpeg.Issues.InvalidAPP0721,
                TestImages.Jpeg.Issues.ExifGetString750Load,
                TestImages.Jpeg.Issues.ExifGetString750Transform,

                // High depth images
                TestImages.Jpeg.Baseline.Testorig12bit,
            };

        public static string[] ProgressiveTestJpegs =
            {
                TestImages.Jpeg.Progressive.Fb,
                TestImages.Jpeg.Progressive.Progress,
                TestImages.Jpeg.Progressive.Festzug,
                TestImages.Jpeg.Progressive.Bad.BadEOF,
                TestImages.Jpeg.Issues.BadCoeffsProgressive178,
                TestImages.Jpeg.Issues.MissingFF00ProgressiveGirl159,
                TestImages.Jpeg.Issues.MissingFF00ProgressiveBedroom159,
                TestImages.Jpeg.Issues.BadZigZagProgressive385,
                TestImages.Jpeg.Progressive.Bad.ExifUndefType,
                TestImages.Jpeg.Issues.NoEoiProgressive517,
                TestImages.Jpeg.Issues.BadRstProgressive518,
                TestImages.Jpeg.Issues.DhtHasWrongLength624,
                TestImages.Jpeg.Issues.OrderedInterleavedProgressive723A,
                TestImages.Jpeg.Issues.OrderedInterleavedProgressive723B,
                TestImages.Jpeg.Issues.OrderedInterleavedProgressive723C
            };

        public static string[] UnrecoverableTestJpegs = {

            TestImages.Jpeg.Issues.CriticalEOF214,
            TestImages.Jpeg.Issues.Fuzz.NullReferenceException797,
            // TestImages.Jpeg.Issues.Fuzz.AccessViolationException798,
            TestImages.Jpeg.Issues.Fuzz.DivideByZeroException821,
            TestImages.Jpeg.Issues.Fuzz.DivideByZeroException822,
            TestImages.Jpeg.Issues.Fuzz.NullReferenceException823,
            TestImages.Jpeg.Issues.Fuzz.IndexOutOfRangeException824A,
            TestImages.Jpeg.Issues.Fuzz.IndexOutOfRangeException824B,
            TestImages.Jpeg.Issues.Fuzz.IndexOutOfRangeException824C,
            TestImages.Jpeg.Issues.Fuzz.IndexOutOfRangeException824D,
            TestImages.Jpeg.Issues.Fuzz.IndexOutOfRangeException824E,
            TestImages.Jpeg.Issues.Fuzz.IndexOutOfRangeException824F
        };

        private static readonly Dictionary<string, float> CustomToleranceValues =
            new Dictionary<string, float>
            {
                // Baseline:
                [TestImages.Jpeg.Baseline.Calliphora] = 0.00002f / 100,
                [TestImages.Jpeg.Baseline.Bad.BadEOF] = 0.38f / 100,
                [TestImages.Jpeg.Baseline.Testorig420] = 0.38f / 100,
                [TestImages.Jpeg.Baseline.Bad.BadRST] = 0.0589f / 100,

                // Progressive:
                [TestImages.Jpeg.Issues.MissingFF00ProgressiveGirl159] = 0.34f / 100,
                [TestImages.Jpeg.Issues.BadCoeffsProgressive178] = 0.38f / 100,
                [TestImages.Jpeg.Progressive.Bad.BadEOF] = 0.3f / 100,
                [TestImages.Jpeg.Progressive.Festzug] = 0.02f / 100,
                [TestImages.Jpeg.Progressive.Fb] = 0.16f / 100,
                [TestImages.Jpeg.Progressive.Progress] = 0.31f / 100,
                [TestImages.Jpeg.Issues.BadZigZagProgressive385] = 0.23f / 100,
                [TestImages.Jpeg.Progressive.Bad.ExifUndefType] = 0.011f / 100,
            };
    }
}