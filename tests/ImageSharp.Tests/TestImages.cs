// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// ReSharper disable InconsistentNaming
// ReSharper disable MemberHidesStaticFromOuterClass
namespace SixLabors.ImageSharp.Tests;

/// <summary>
/// Class that contains all the relative test image paths in the TestImages/Formats directory.
/// Use with <see cref="WithFileAttribute"/>, <see cref="WithFileCollectionAttribute"/>.
/// </summary>
public static class TestImages
{
    public static class Png
    {
        public const string Transparency = "Png/transparency.png";
        public const string P1 = "Png/pl.png";
        public const string Pd = "Png/pd.png";
        public const string Blur = "Png/blur.png";
        public const string Indexed = "Png/indexed.png";
        public const string Splash = "Png/splash.png";
        public const string Cross = "Png/cross.png";
        public const string Powerpoint = "Png/pp.png";
        public const string SplashInterlaced = "Png/splash-interlaced.png";
        public const string Interlaced = "Png/interlaced.png";
        public const string Palette8Bpp = "Png/palette-8bpp.png";
        public const string Bpp1 = "Png/bpp1.png";
        public const string Gray4Bpp = "Png/gray_4bpp.png";
        public const string L16Bit = "Png/gray-16.png";
        public const string GrayA8Bit = "Png/gray-alpha-8.png";
        public const string GrayA8BitInterlaced = "Png/rollsroyce.png";
        public const string GrayAlpha1BitInterlaced = "Png/iftbbn0g01.png";
        public const string GrayAlpha2BitInterlaced = "Png/iftbbn0g02.png";
        public const string Gray4BitInterlaced = "Png/iftbbn0g04.png";
        public const string GrayAlpha16Bit = "Png/gray-alpha-16.png";
        public const string GrayTrns16BitInterlaced = "Png/gray-16-tRNS-interlaced.png";
        public const string Rgb24BppTrans = "Png/rgb-8-tRNS.png";
        public const string Rgb48Bpp = "Png/rgb-48bpp.png";
        public const string Rgb48BppInterlaced = "Png/rgb-48bpp-interlaced.png";
        public const string Rgb48BppTrans = "Png/rgb-16-tRNS.png";
        public const string Rgba64Bpp = "Png/rgb-16-alpha.png";
        public const string ColorsSaturationLightness = "Png/colors-saturation-lightness.png";
        public const string CalliphoraPartial = "Png/CalliphoraPartial.png";
        public const string CalliphoraPartialGrayscale = "Png/CalliphoraPartialGrayscale.png";
        public const string Bike = "Png/Bike.png";
        public const string BikeSmall = "Png/bike-small.png";
        public const string BikeGrayscale = "Png/BikeGrayscale.png";
        public const string SnakeGame = "Png/SnakeGame.png";
        public const string Icon = "Png/icon.png";
        public const string Kaboom = "Png/kaboom.png";
        public const string PDSrc = "Png/pd-source.png";
        public const string PDDest = "Png/pd-dest.png";
        public const string Gray1BitTrans = "Png/gray-1-trns.png";
        public const string Gray2BitTrans = "Png/gray-2-tRNS.png";
        public const string Gray4BitTrans = "Png/gray-4-tRNS.png";
        public const string L8BitTrans = "Png/gray-8-tRNS.png";
        public const string LowColorVariance = "Png/low-variance.png";
        public const string PngWithMetadata = "Png/PngWithMetaData.png";
        public const string InvalidTextData = "Png/InvalidTextData.png";
        public const string David = "Png/david.png";
        public const string TestPattern31x31 = "Png/testpattern31x31.png";
        public const string TestPattern31x31HalfTransparent = "Png/testpattern31x31-halftransparent.png";
        public const string XmpColorPalette = "Png/xmp-colorpalette.png";
        public const string AdamHeadsHlg = "Png/adamHeadsHLG.png";

        // Animated
        // https://philip.html5.org/tests/apng/tests.html
        public const string APng = "Png/animated/apng.png";
        public const string SplitIDatZeroLength = "Png/animated/4-split-idat-zero-length.png";
        public const string DisposeNone = "Png/animated/7-dispose-none.png";
        public const string DisposeBackground = "Png/animated/8-dispose-background.png";
        public const string DisposeBackgroundBeforeRegion = "Png/animated/14-dispose-background-before-region.png";
        public const string DisposeBackgroundRegion = "Png/animated/15-dispose-background-region.png";
        public const string DisposePreviousFirst = "Png/animated/12-dispose-prev-first.png";
        public const string BlendOverMultiple = "Png/animated/21-blend-over-multiple.png";
        public const string FrameOffset = "Png/animated/frame-offset.png";
        public const string DefaultNotAnimated = "Png/animated/default-not-animated.png";
        public const string Issue2666 = "Png/issues/Issue_2666.png";

        // Filtered test images from http://www.schaik.com/pngsuite/pngsuite_fil_png.html
        public const string Filter0 = "Png/filter0.png";
        public const string SubFilter3BytesPerPixel = "Png/filter1.png";
        public const string SubFilter4BytesPerPixel = "Png/SubFilter4Bpp.png";
        public const string UpFilter = "Png/filter2.png";
        public const string AverageFilter3BytesPerPixel = "Png/filter3.png";
        public const string AverageFilter4BytesPerPixel = "Png/AverageFilter4Bpp.png";
        public const string PaethFilter3BytesPerPixel = "Png/filter4.png";
        public const string PaethFilter4BytesPerPixel = "Png/PaethFilter4Bpp.png";

        // Paletted images also from http://www.schaik.com/pngsuite/pngsuite_fil_png.html
        public const string PalettedTwoColor = "Png/basn3p01.png";
        public const string PalettedFourColor = "Png/basn3p02.png";
        public const string PalettedSixteenColor = "Png/basn3p04.png";
        public const string Paletted256Colors = "Png/basn3p08.png";

        // Filter changing per scanline
        public const string FilterVar = "Png/filterVar.png";

        public const string VimImage1 = "Png/vim16x16_1.png";
        public const string VimImage2 = "Png/vim16x16_2.png";

        public const string VersioningImage1 = "Png/versioning-1_1.png";
        public const string VersioningImage2 = "Png/versioning-1_2.png";

        public const string Banner7Adam7InterlaceMode = "Png/banner7-adam.png";
        public const string Banner8Index = "Png/banner8-index.png";

        public const string Ratio1x4 = "Png/ratio-1x4.png";
        public const string Ratio4x1 = "Png/ratio-4x1.png";

        public const string Ducky = "Png/ducky.png";
        public const string Rainbow = "Png/rainbow.png";

        public const string Bradley01 = "Png/Bradley01.png";
        public const string Bradley02 = "Png/Bradley02.png";

        // Issue 1014: https://github.com/SixLabors/ImageSharp/issues/1014
        public const string Issue1014_1 = "Png/issues/Issue_1014_1.png";
        public const string Issue1014_2 = "Png/issues/Issue_1014_2.png";
        public const string Issue1014_3 = "Png/issues/Issue_1014_3.png";
        public const string Issue1014_4 = "Png/issues/Issue_1014_4.png";
        public const string Issue1014_5 = "Png/issues/Issue_1014_5.png";
        public const string Issue1014_6 = "Png/issues/Issue_1014_6.png";

        // Issue 1127: https://github.com/SixLabors/ImageSharp/issues/1127
        public const string Issue1127 = "Png/issues/Issue_1127.png";

        // Issue 1177: https://github.com/SixLabors/ImageSharp/issues/1177
        public const string Issue1177_1 = "Png/issues/Issue_1177_1.png";
        public const string Issue1177_2 = "Png/issues/Issue_1177_2.png";

        // Issue 935: https://github.com/SixLabors/ImageSharp/issues/935
        public const string Issue935 = "Png/issues/Issue_935.png";

        // Issue 1765: https://github.com/SixLabors/ImageSharp/issues/1765
        public const string Issue1765_Net6DeflateStreamRead = "Png/issues/Issue_1765_Net6DeflateStreamRead.png";

        // Discussion 1875: https://github.com/SixLabors/ImageSharp/discussions/1875
        public const string Issue1875 = "Png/raw-profile-type-exif.png";

        // Issue 2217: https://github.com/SixLabors/ImageSharp/issues/2217
        public const string Issue2217 = "Png/issues/Issue_2217_AdaptiveThresholdProcessor.png";

        // Issue 2209: https://github.com/SixLabors/ImageSharp/issues/2209
        public const string Issue2209IndexedWithTransparency = "Png/issues/Issue_2209.png";

        // Issue 2259: https://github.com/SixLabors/ImageSharp/issues/2469
        public const string Issue2259 = "Png/issues/Issue_2259.png";

        // Issue 2259: https://github.com/SixLabors/ImageSharp/issues/2469
        public const string Issue2469 = "Png/issues/issue_2469.png";

        // Issue 2447: https://github.com/SixLabors/ImageSharp/issues/2447
        public const string Issue2447 = "Png/issues/issue_2447.png";

        // Issue 2668: https://github.com/SixLabors/ImageSharp/issues/2668
        public const string Issue2668 = "Png/issues/Issue_2668.png";

        // Issue 2752: https://github.com/SixLabors/ImageSharp/issues/2752
        public const string Issue2752 = "Png/issues/Issue_2752.png";

        public static class Bad
        {
            public const string MissingDataChunk = "Png/xdtn0g01.png";
            public const string WrongCrcDataChunk = "Png/xcsn0g01.png";
            public const string CorruptedChunk = "Png/big-corrupted-chunk.png";
            public const string MissingPaletteChunk1 = "Png/missing_plte.png";
            public const string MissingPaletteChunk2 = "Png/missing_plte_2.png";
            public const string InvalidGammaChunk = "Png/length_gama.png";
            public const string Issue2589 = "Png/issues/Issue_2589.png";

            // Zlib errors.
            public const string ZlibOverflow = "Png/zlib-overflow.png";
            public const string ZlibOverflow2 = "Png/zlib-overflow2.png";
            public const string ZlibZtxtBadHeader = "Png/zlib-ztxt-bad-header.png";

            // Odd chunk lengths
            public const string ChunkLength1 = "Png/chunklength1.png";
            public const string ChunkLength2 = "Png/chunklength2.png";

            // Issue 1047: https://github.com/SixLabors/ImageSharp/issues/1047
            public const string Issue1047_BadEndChunk = "Png/issues/Issue_1047.png";

            // Issue 410: https://github.com/SixLabors/ImageSharp/issues/410
            public const string Issue410_MalformedApplePng = "Png/issues/Issue_410.png";

            // Bad bit depth.
            public const string BitDepthZero = "Png/xd0n2c08.png";
            public const string BitDepthThree = "Png/xd3n2c08.png";

            // Invalid color type.
            public const string ColorTypeOne = "Png/xc1n0g08.png";
            public const string ColorTypeNine = "Png/xc9n2c08.png";
            public const string FlagOfGermany0000016446 = "Png/issues/flag_of_germany-0000016446.png";

            public const string BadZTXT = "Png/issues/bad-ztxt.png";
            public const string BadZTXT2 = "Png/issues/bad-ztxt2.png";

            public const string Issue2714BadPalette = "Png/issues/Issue_2714.png";
        }
    }

    public static class Jpeg
    {
        public static class Progressive
        {
            public const string Fb = "Jpg/progressive/fb.jpg";
            public const string Progress = "Jpg/progressive/progress.jpg";
            public const string Festzug = "Jpg/progressive/Festzug.jpg";
            public const string Winter420_NonInterleaved = "Jpg/progressive/winter420_noninterleaved.jpg";

            public static class Bad
            {
                public const string BadEOF = "Jpg/progressive/BadEofProgressive.jpg";
                public const string ExifUndefType = "Jpg/progressive/ExifUndefType.jpg";
            }

            public static readonly string[] All = { Fb, Progress, Festzug };
        }

        public static class Baseline
        {
            public static class Bad
            {
                public const string BadEOF = "Jpg/baseline/badeof.jpg";
                public const string BadRST = "Jpg/baseline/badrst.jpg";
            }

            public const string Cmyk = "Jpg/baseline/cmyk.jpg";
            public const string Exif = "Jpg/baseline/exif.jpg";
            public const string Floorplan = "Jpg/baseline/Floorplan.jpg";
            public const string Calliphora = "Jpg/baseline/Calliphora.jpg";
            public const string Calliphora_EncodedStrings = "Jpg/baseline/Calliphora_encoded_strings.jpg";
            public const string Ycck = "Jpg/baseline/ycck.jpg";
            public const string Turtle420 = "Jpg/baseline/turtle.jpg";
            public const string GammaDalaiLamaGray = "Jpg/baseline/gamma_dalai_lama_gray.jpg";
            public const string Hiyamugi = "Jpg/baseline/Hiyamugi.jpg";
            public const string Snake = "Jpg/baseline/Snake.jpg";
            public const string Lake = "Jpg/baseline/Lake.jpg";
            public const string Jpeg400 = "Jpg/baseline/jpeg400jfif.jpg";
            public const string Jpeg420Exif = "Jpg/baseline/jpeg420exif.jpg";
            public const string Jpeg444 = "Jpg/baseline/jpeg444.jpg";
            public const string Jpeg420Small = "Jpg/baseline/jpeg420small.jpg";
            public const string JpegRgb = "Jpg/baseline/jpeg-rgb.jpg";
            public const string Jpeg410 = "Jpg/baseline/jpeg410.jpg";
            public const string Jpeg411 = "Jpg/baseline/jpeg411.jpg";
            public const string Jpeg422 = "Jpg/baseline/jpeg422.jpg";
            public const string Testorig420 = "Jpg/baseline/testorig.jpg";
            public const string MultiScanBaselineCMYK = "Jpg/baseline/MultiScanBaselineCMYK.jpg";
            public const string Ratio1x1 = "Jpg/baseline/ratio-1x1.jpg";
            public const string LowContrast = "Jpg/baseline/AsianCarvingLowContrast.jpg";
            public const string Testorig12bit = "Jpg/baseline/testorig12.jpg";
            public const string YcckSubsample1222 = "Jpg/baseline/ycck-subsample-1222.jpg";
            public const string Iptc = "Jpg/baseline/iptc.jpg";
            public const string App13WithEmptyIptc = "Jpg/baseline/iptc-psAPP13-wIPTCempty.jpg";
            public const string HistogramEqImage = "Jpg/baseline/640px-Unequalized_Hawkes_Bay_NZ.jpg";
            public const string ForestBridgeDifferentComponentsQuality = "Jpg/baseline/forest_bridge.jpg";
            public const string Lossless = "Jpg/baseline/lossless.jpg";
            public const string Winter444_Interleaved = "Jpg/baseline/winter444_interleaved.jpg";
            public const string Metadata = "Jpg/baseline/Metadata-test-file.jpg";
            public const string ExtendedXmp = "Jpg/baseline/extended-xmp.jpg";
            public const string GrayscaleSampling2x2 = "Jpg/baseline/grayscale_sampling22.jpg";

            // Jpeg's with arithmetic coding.
            public const string ArithmeticCoding01 = "Jpg/baseline/Calliphora_arithmetic.jpg";
            public const string ArithmeticCoding02 = "Jpg/baseline/arithmetic_coding.jpg";
            public const string ArithmeticCodingProgressive01 = "Jpg/progressive/arithmetic_progressive.jpg";
            public const string ArithmeticCodingProgressive02 = "Jpg/progressive/Calliphora-arithmetic-progressive-interleaved.jpg";
            public const string ArithmeticCodingGray = "Jpg/baseline/Calliphora-arithmetic-grayscale.jpg";
            public const string ArithmeticCodingInterleaved = "Jpg/baseline/Calliphora-arithmetic-interleaved.jpg";
            public const string ArithmeticCodingWithRestart = "Jpg/baseline/Calliphora-arithmetic-restart.jpg";

            public static readonly string[] All =
            {
                Cmyk, Ycck, Exif, Floorplan,
                Calliphora, Turtle420, GammaDalaiLamaGray,
                Hiyamugi, Jpeg400, Jpeg420Exif, Jpeg444,
                Ratio1x1, Testorig12bit, YcckSubsample1222
            };
        }

        public static class Issues
        {
            public const string CriticalEOF214 = "Jpg/issues/Issue214-CriticalEOF.jpg";
            public const string MissingFF00ProgressiveGirl159 = "Jpg/issues/Issue159-MissingFF00-Progressive-Girl.jpg";
            public const string MissingFF00ProgressiveBedroom159 = "Jpg/issues/Issue159-MissingFF00-Progressive-Bedroom.jpg";
            public const string BadCoeffsProgressive178 = "Jpg/issues/Issue178-BadCoeffsProgressive-Lemon.jpg";
            public const string BadZigZagProgressive385 = "Jpg/issues/Issue385-BadZigZag-Progressive.jpg";
            public const string MultiHuffmanBaseline394 = "Jpg/issues/Issue394-MultiHuffmanBaseline-Speakers.jpg";
            public const string NoEoiProgressive517 = "Jpg/issues/Issue517-No-EOI-Progressive.jpg";
            public const string BadRstProgressive518 = "Jpg/issues/Issue518-Bad-RST-Progressive.jpg";
            public const string InvalidCast520 = "Jpg/issues/Issue520-InvalidCast.jpg";
            public const string DhtHasWrongLength624 = "Jpg/issues/Issue624-DhtHasWrongLength-Progressive-N.jpg";
            public const string ExifDecodeOutOfRange694 = "Jpg/issues/Issue694-Decode-Exif-OutOfRange.jpg";
            public const string InvalidEOI695 = "Jpg/issues/Issue695-Invalid-EOI.jpg";
            public const string ExifResizeOutOfRange696 = "Jpg/issues/Issue696-Resize-Exif-OutOfRange.jpg";
            public const string InvalidAPP0721 = "Jpg/issues/Issue721-InvalidAPP0.jpg";
            public const string OrderedInterleavedProgressive723A = "Jpg/issues/Issue723-Ordered-Interleaved-Progressive-A.jpg";
            public const string OrderedInterleavedProgressive723B = "Jpg/issues/Issue723-Ordered-Interleaved-Progressive-B.jpg";
            public const string OrderedInterleavedProgressive723C = "Jpg/issues/Issue723-Ordered-Interleaved-Progressive-C.jpg";
            public const string ExifGetString750Transform = "Jpg/issues/issue750-exif-tranform.jpg";
            public const string ExifGetString750Load = "Jpg/issues/issue750-exif-load.jpg";
            public const string IncorrectQuality845 = "Jpg/issues/Issue845-Incorrect-Quality99.jpg";
            public const string IncorrectColorspace855 = "Jpg/issues/issue855-incorrect-colorspace.jpg";
            public const string IncorrectResize1006 = "Jpg/issues/issue1006-incorrect-resize.jpg";
            public const string ExifResize1049 = "Jpg/issues/issue1049-exif-resize.jpg";
            public const string BadSubSampling1076 = "Jpg/issues/issue-1076-invalid-subsampling.jpg";
            public const string IdentifyMultiFrame1211 = "Jpg/issues/issue-1221-identify-multi-frame.jpg";
            public const string WrongColorSpace = "Jpg/issues/Issue1732-WrongColorSpace.jpg";
            public const string MalformedUnsupportedComponentCount = "Jpg/issues/issue-1900-malformed-unsupported-255-components.jpg";
            public const string MultipleApp01932 = "Jpg/issues/issue-1932-app0-resolution.jpg";
            public const string InvalidIptcTag = "Jpg/issues/Issue1942InvalidIptcTag.jpg";
            public const string Issue2057App1Parsing = "Jpg/issues/Issue2057-App1Parsing.jpg";
            public const string ExifNullArrayTag = "Jpg/issues/issue-2056-exif-null-array.jpg";
            public const string ValidExifArgumentNullExceptionOnEncode = "Jpg/issues/Issue2087-exif-null-reference-on-encode.jpg";
            public const string Issue2133_DeduceColorSpace = "Jpg/issues/Issue2133.jpg";
            public const string Issue2136_ScanMarkerExtraneousBytes = "Jpg/issues/Issue2136-scan-segment-extraneous-bytes.jpg";
            public const string Issue2315_NotEnoughBytes = "Jpg/issues/issue-2315.jpg";
            public const string Issue2334_NotEnoughBytesA = "Jpg/issues/issue-2334-a.jpg";
            public const string Issue2334_NotEnoughBytesB = "Jpg/issues/issue-2334-b.jpg";
            public const string Issue2478_JFXX = "Jpg/issues/issue-2478-jfxx.jpg";
            public const string Issue2564 = "Jpg/issues/issue-2564.jpg";
            public const string HangBadScan = "Jpg/issues/Hang_C438A851.jpg";
            public const string Issue2517 = "Jpg/issues/issue2517-bad-d7.jpg";
            public const string Issue2638 = "Jpg/issues/Issue2638.jpg";
            public const string Issue2758 = "Jpg/issues/issue-2758.jpg";

            public static class Fuzz
            {
                public const string NullReferenceException797 = "Jpg/issues/fuzz/Issue797-NullReferenceException.jpg";
                public const string AccessViolationException798 = "Jpg/issues/fuzz/Issue798-AccessViolationException.jpg";
                public const string DivideByZeroException821 = "Jpg/issues/fuzz/Issue821-DivideByZeroException.jpg";
                public const string DivideByZeroException822 = "Jpg/issues/fuzz/Issue822-DivideByZeroException.jpg";
                public const string NullReferenceException823 = "Jpg/issues/fuzz/Issue823-NullReferenceException.jpg";
                public const string IndexOutOfRangeException824A = "Jpg/issues/fuzz/Issue824-IndexOutOfRangeException-A.jpg";
                public const string IndexOutOfRangeException824B = "Jpg/issues/fuzz/Issue824-IndexOutOfRangeException-B.jpg";
                public const string IndexOutOfRangeException824C = "Jpg/issues/fuzz/Issue824-IndexOutOfRangeException-C.jpg";
                public const string IndexOutOfRangeException824D = "Jpg/issues/fuzz/Issue824-IndexOutOfRangeException-D.jpg";
                public const string IndexOutOfRangeException824E = "Jpg/issues/fuzz/Issue824-IndexOutOfRangeException-E.jpg";
                public const string IndexOutOfRangeException824F = "Jpg/issues/fuzz/Issue824-IndexOutOfRangeException-F.jpg";
                public const string IndexOutOfRangeException824G = "Jpg/issues/fuzz/Issue824-IndexOutOfRangeException-G.jpg";
                public const string IndexOutOfRangeException824H = "Jpg/issues/fuzz/Issue824-IndexOutOfRangeException-H.jpg";
                public const string ArgumentOutOfRangeException825A = "Jpg/issues/fuzz/Issue825-ArgumentOutOfRangeException-A.jpg";
                public const string ArgumentOutOfRangeException825B = "Jpg/issues/fuzz/Issue825-ArgumentOutOfRangeException-B.jpg";
                public const string ArgumentOutOfRangeException825C = "Jpg/issues/fuzz/Issue825-ArgumentOutOfRangeException-C.jpg";
                public const string ArgumentOutOfRangeException825D = "Jpg/issues/fuzz/Issue825-ArgumentOutOfRangeException-D.jpg";
                public const string ArgumentException826A = "Jpg/issues/fuzz/Issue826-ArgumentException-A.jpg";
                public const string ArgumentException826B = "Jpg/issues/fuzz/Issue826-ArgumentException-B.jpg";
                public const string ArgumentException826C = "Jpg/issues/fuzz/Issue826-ArgumentException-C.jpg";
                public const string AccessViolationException827 = "Jpg/issues/fuzz/Issue827-AccessViolationException.jpg";
                public const string ExecutionEngineException839 = "Jpg/issues/fuzz/Issue839-ExecutionEngineException.jpg";
                public const string AccessViolationException922 = "Jpg/issues/fuzz/Issue922-AccessViolationException.jpg";
                public const string IndexOutOfRangeException1693A = "Jpg/issues/fuzz/Issue1693-IndexOutOfRangeException-A.jpg";
                public const string IndexOutOfRangeException1693B = "Jpg/issues/fuzz/Issue1693-IndexOutOfRangeException-B.jpg";
                public const string NullReferenceException2085 = "Jpg/issues/fuzz/Issue2085-NullReferenceException.jpg";
            }
        }

        public static readonly string[] All = Baseline.All.Concat(Progressive.All).ToArray();

        public static class BenchmarkSuite
        {
            public const string Jpeg400_SmallMonochrome = Baseline.Jpeg400;
            public const string Jpeg420Exif_MidSizeYCbCr = Baseline.Jpeg420Exif;
            public const string Lake_Small444YCbCr = Baseline.Lake;

            // A few large images from the "issues" set are actually very useful for benchmarking:
            public const string MissingFF00ProgressiveBedroom159_MidSize420YCbCr = Issues.MissingFF00ProgressiveBedroom159;
            public const string BadRstProgressive518_Large444YCbCr = Issues.BadRstProgressive518;
            public const string ExifGetString750Transform_Huge420YCbCr = Issues.ExifGetString750Transform;
        }
    }

    public static class Bmp
    {
        // Note: The inverted images have been generated by altering the BitmapInfoHeader using a hex editor.
        // As such, the expected pixel output will be the reverse of the unaltered equivalent images.
        public const string Car = "Bmp/Car.bmp";
        public const string F = "Bmp/F.bmp";
        public const string NegHeight = "Bmp/neg_height.bmp";
        public const string CoreHeader = "Bmp/BitmapCoreHeaderQR.bmp";
        public const string V5Header = "Bmp/BITMAPV5HEADER.bmp";
        public const string RLE24 = "Bmp/rgb24rle24.bmp";
        public const string RLE24Cut = "Bmp/rle24rlecut.bmp";
        public const string RLE24Delta = "Bmp/rle24rlecut.bmp";
        public const string RLE8 = "Bmp/RunLengthEncoded.bmp";
        public const string RLE8Cut = "Bmp/pal8rlecut.bmp";
        public const string RLE8Delta = "Bmp/pal8rletrns.bmp";
        public const string Rle8Delta320240 = "Bmp/rle8-delta-320x240.bmp";
        public const string Rle8Blank160120 = "Bmp/rle8-blank-160x120.bmp";
        public const string RLE8Inverted = "Bmp/RunLengthEncoded-inverted.bmp";
        public const string RLE4 = "Bmp/pal4rle.bmp";
        public const string RLE4Cut = "Bmp/pal4rlecut.bmp";
        public const string RLE4Delta = "Bmp/pal4rletrns.bmp";
        public const string Rle4Delta320240 = "Bmp/rle4-delta-320x240.bmp";
        public const string Bit1 = "Bmp/pal1.bmp";
        public const string Bit2 = "Bmp/pal2.bmp";
        public const string Bit2Color = "Bmp/pal2color.bmp";
        public const string Bit1Pal1 = "Bmp/pal1p1.bmp";
        public const string Bit4 = "Bmp/pal4.bmp";
        public const string Bit8 = "Bmp/test8.bmp";
        public const string Bit8Gs = "Bmp/pal8gs.bmp";
        public const string Bit8Inverted = "Bmp/test8-inverted.bmp";
        public const string Bit16 = "Bmp/test16.bmp";
        public const string Bit16Inverted = "Bmp/test16-inverted.bmp";
        public const string Bit32Rgb = "Bmp/rgb32.bmp";
        public const string Bit32Rgba = "Bmp/rgba32.bmp";
        public const string Rgb16 = "Bmp/rgb16.bmp";

        // Note: This format can be called OS/2 BMPv1, or Windows BMPv2
        public const string WinBmpv2 = "Bmp/pal8os2v1_winv2.bmp";

        public const string WinBmpv3 = "Bmp/rgb24.bmp";
        public const string WinBmpv4 = "Bmp/pal8v4.bmp";
        public const string WinBmpv5 = "Bmp/pal8v5.bmp";
        public const string Bit8Palette4 = "Bmp/pal8-0.bmp";
        public const string Os2v2Short = "Bmp/pal8os2v2-16.bmp";
        public const string Os2v2 = "Bmp/pal8os2v2.bmp";
        public const string Os2BitmapArray = "Bmp/ba-bm.bmp";
        public const string Os2BitmapArray9s = "Bmp/9S.bmp";
        public const string Os2BitmapArrayDiamond = "Bmp/DIAMOND.bmp";
        public const string Os2BitmapArrayMarble = "Bmp/GMARBLE.bmp";
        public const string Os2BitmapArraySkater = "Bmp/SKATER.bmp";
        public const string Os2BitmapArraySpade = "Bmp/SPADE.bmp";
        public const string Os2BitmapArraySunflower = "Bmp/SUNFLOW.bmp";
        public const string Os2BitmapArrayWarpd = "Bmp/WARPD.bmp";
        public const string Os2BitmapArrayPines = "Bmp/PINES.bmp";
        public const string LessThanFullSizedPalette = "Bmp/pal8os2sp.bmp";
        public const string Pal8Offset = "Bmp/pal8offs.bmp";
        public const string OversizedPalette = "Bmp/pal8oversizepal.bmp";
        public const string Rgb24LargePalette = "Bmp/rgb24largepal.bmp";
        public const string InvalidPaletteSize = "Bmp/invalidPaletteSize.bmp";
        public const string Rgb24jpeg = "Bmp/rgb24jpeg.bmp";
        public const string Rgb24png = "Bmp/rgb24png.bmp";
        public const string Rgba32v4 = "Bmp/rgba32v4.bmp";
        public const string IccProfile = "Bmp/BMP_v5_with_ICC_2.bmp";

        // Bitmap images with compression type BITFIELDS.
        public const string Rgb32bfdef = "Bmp/rgb32bfdef.bmp";
        public const string Rgb32bf = "Bmp/rgb32bf.bmp";
        public const string Rgb16bfdef = "Bmp/rgb16bfdef.bmp";
        public const string Rgb16565 = "Bmp/rgb16-565.bmp";
        public const string Rgb16565pal = "Bmp/rgb16-565pal.bmp";
        public const string Issue735 = "Bmp/issue735.bmp";
        public const string Rgba32bf56AdobeV3 = "Bmp/rgba32h56.bmp";
        public const string Rgb32h52AdobeV3 = "Bmp/rgb32h52.bmp";
        public const string Rgba321010102 = "Bmp/rgba32-1010102.bmp";
        public const string RgbaAlphaBitfields = "Bmp/rgba32abf.bmp";

        public const string Issue2696 = "Bmp/issue-2696.bmp";

        public const string BlackWhitePalletDataMatrix = "Bmp/bit1datamatrix.bmp";

        public static readonly string[] BitFields =
        {
              Rgb32bfdef,
              Rgb32bf,
              Rgb16565,
              Rgb16bfdef,
              Rgb16565pal,
              Issue735,
        };

        public static readonly string[] Miscellaneous =
        {
            Car,
            F,
            NegHeight
        };

        public static readonly string[] Benchmark =
        {
            Car,
            F,
            NegHeight,
            CoreHeader,
            V5Header,
            RLE4,
            RLE8,
            RLE8Inverted,
            Bit1,
            Bit1Pal1,
            Bit4,
            Bit8,
            Bit8Inverted,
            Bit16,
            Bit16Inverted,
            Bit32Rgb
        };
    }

    public static class Gif
    {
        public const string Rings = "Gif/rings.gif";
        public const string Giphy = "Gif/giphy.gif";
        public const string Cheers = "Gif/cheers.gif";
        public const string Receipt = "Gif/receipt.gif";
        public const string Trans = "Gif/trans.gif";
        public const string Kumin = "Gif/kumin.gif";
        public const string Leo = "Gif/leo.gif";
        public const string Ratio4x1 = "Gif/base_4x1.gif";
        public const string Ratio1x4 = "Gif/base_1x4.gif";
        public const string LargeComment = "Gif/large_comment.gif";
        public const string GlobalQuantizationTest = "Gif/GlobalQuantizationTest.gif";
        public const string MixedDisposal = "Gif/mixed-disposal.gif";
        public const string M4nb = "Gif/m4nb.gif";
        public const string Bit18RGBCube = "Gif/18-bit_RGB_Cube.gif";
        public const string Global256NoTrans = "Gif/global-256-no-trans.gif";

        // Test images from https://github.com/robert-ancell/pygif/tree/master/test-suite
        public const string ZeroSize = "Gif/image-zero-size.gif";
        public const string ZeroHeight = "Gif/image-zero-height.gif";
        public const string ZeroWidth = "Gif/image-zero-width.gif";
        public const string MaxWidth = "Gif/max-width.gif";
        public const string MaxHeight = "Gif/max-height.gif";

        public static class Issues
        {
            public const string BadAppExtLength = "Gif/issues/issue405_badappextlength252.gif";
            public const string BadAppExtLength_2 = "Gif/issues/issue405_badappextlength252-2.gif";
            public const string BadDescriptorWidth = "Gif/issues/issue403_baddescriptorwidth.gif";
            public const string BadMaxLzwBits = "Gif/issues/issue_2743.gif";
            public const string DeferredClearCode = "Gif/issues/bugzilla-55918.gif";
            public const string Issue1505 = "Gif/issues/issue1505_argumentoutofrange.png";
            public const string Issue1530 = "Gif/issues/issue1530.gif";
            public const string InvalidColorIndex = "Gif/issues/issue1668_invalidcolorindex.gif";
            public const string Issue1962NoColorTable = "Gif/issues/issue1962_tiniest_gif_1st.gif";
            public const string Issue2012EmptyXmp = "Gif/issues/issue2012_Stronghold-Crusader-Extreme-Cover.gif";
            public const string Issue2012BadMinCode = "Gif/issues/issue2012_drona1.gif";
            public const string Issue2288_A = "Gif/issues/issue_2288.gif";
            public const string Issue2288_B = "Gif/issues/issue_2288_2.gif";
            public const string Issue2288_C = "Gif/issues/issue_2288_3.gif";
            public const string Issue2288_D = "Gif/issues/issue_2288_4.gif";
            public const string Issue2450_A = "Gif/issues/issue_2450.gif";
            public const string Issue2450_B = "Gif/issues/issue_2450_2.gif";
            public const string Issue2198 = "Gif/issues/issue_2198.gif";
            public const string Issue2758 = "Gif/issues/issue_2758.gif";
        }

        public static readonly string[] Animated =
        {
            M4nb,
            Giphy,
            Cheers,
            Kumin,
            Leo,
            MixedDisposal,
            GlobalQuantizationTest,
            Issues.Issue2198,
            Issues.Issue2288_A,
            Issues.Issue2288_B,
            Issues.Issue2288_C,
            Issues.Issue2288_D,
            Issues.Issue2450_A,
            Issues.Issue2450_B,
            Issues.BadDescriptorWidth,
            Issues.Issue1530,
            Bit18RGBCube,
            Global256NoTrans
        };
    }

    public static class Tga
    {
        public const string Gray8BitTopLeft = "Tga/grayscale_UL.tga";
        public const string Gray8BitTopRight = "Tga/grayscale_UR.tga";
        public const string Gray8BitBottomLeft = "Tga/targa_8bit.tga";
        public const string Gray8BitBottomRight = "Tga/grayscale_LR.tga";

        public const string Gray8BitRleTopLeft = "Tga/grayscale_rle_UL.tga";
        public const string Gray8BitRleTopRight = "Tga/grayscale_rle_UR.tga";
        public const string Gray8BitRleBottomLeft = "Tga/targa_8bit_rle.tga";
        public const string Gray8BitRleBottomRight = "Tga/grayscale_rle_LR.tga";

        public const string Bit15 = "Tga/rgb15.tga";
        public const string Bit15Rle = "Tga/rgb15rle.tga";
        public const string Bit16BottomLeft = "Tga/targa_16bit.tga";
        public const string Bit16PalRle = "Tga/ccm8.tga";
        public const string Bit16RleBottomLeft = "Tga/targa_16bit_rle.tga";
        public const string Bit16PalBottomLeft = "Tga/targa_16bit_pal.tga";

        public const string Gray16BitTopLeft = "Tga/grayscale_a_UL.tga";
        public const string Gray16BitBottomLeft = "Tga/grayscale_a_LL.tga";
        public const string Gray16BitBottomRight = "Tga/grayscale_a_LR.tga";
        public const string Gray16BitTopRight = "Tga/grayscale_a_UR.tga";

        public const string Gray16BitRleTopLeft = "Tga/grayscale_a_rle_UL.tga";
        public const string Gray16BitRleBottomLeft = "Tga/grayscale_a_rle_LL.tga";
        public const string Gray16BitRleBottomRight = "Tga/grayscale_a_rle_LR.tga";
        public const string Gray16BitRleTopRight = "Tga/grayscale_a_rle_UR.tga";

        public const string Bit24TopLeft = "Tga/rgb24_top_left.tga";
        public const string Bit24BottomLeft = "Tga/targa_24bit.tga";
        public const string Bit24BottomRight = "Tga/rgb_LR.tga";
        public const string Bit24TopRight = "Tga/rgb_UR.tga";

        public const string Bit24RleTopLeft = "Tga/targa_24bit_rle_origin_topleft.tga";
        public const string Bit24RleBottomLeft = "Tga/targa_24bit_rle.tga";
        public const string Bit24RleTopRight = "Tga/rgb_rle_UR.tga";
        public const string Bit24RleBottomRight = "Tga/rgb_rle_LR.tga";

        public const string Bit24PalTopLeft = "Tga/targa_24bit_pal_origin_topleft.tga";
        public const string Bit24PalTopRight = "Tga/indexed_UR.tga";
        public const string Bit24PalBottomLeft = "Tga/targa_24bit_pal.tga";
        public const string Bit24PalBottomRight = "Tga/indexed_LR.tga";

        public const string Bit24PalRleTopLeft = "Tga/indexed_rle_UL.tga";
        public const string Bit24PalRleBottomLeft = "Tga/indexed_rle_LL.tga";
        public const string Bit24PalRleTopRight = "Tga/indexed_rle_UR.tga";
        public const string Bit24PalRleBottomRight = "Tga/indexed_rle_LR.tga";

        public const string Bit32TopLeft = "Tga/rgb_a_UL.tga";
        public const string Bit32BottomLeft = "Tga/targa_32bit.tga";
        public const string Bit32TopRight = "Tga/rgb_a_UR.tga";
        public const string Bit32BottomRight = "Tga/rgb_a_LR.tga";

        public const string Bit32PalTopLeft = "Tga/indexed_a_UL.tga";
        public const string Bit32PalBottomLeft = "Tga/indexed_a_LL.tga";
        public const string Bit32PalBottomRight = "Tga/indexed_a_LR.tga";
        public const string Bit32PalTopRight = "Tga/indexed_a_UR.tga";

        public const string Bit32RleTopLeft = "Tga/rgb_a_rle_UL.tga";
        public const string Bit32RleTopRight = "Tga/rgb_a_rle_UR.tga";
        public const string Bit32RleBottomRight = "Tga/rgb_a_rle_LR.tga";
        public const string Bit32RleBottomLeft = "Tga/targa_32bit_rle.tga";

        public const string Bit32PalRleTopLeft = "Tga/indexed_a_rle_UL.tga";
        public const string Bit32PalRleBottomLeft = "Tga/indexed_a_rle_LL.tga";
        public const string Bit32PalRleTopRight = "Tga/indexed_a_rle_UR.tga";
        public const string Bit32PalRleBottomRight = "Tga/indexed_a_rle_LR.tga";

        public const string NoAlphaBits16Bit = "Tga/16bit_noalphabits.tga";
        public const string NoAlphaBits16BitRle = "Tga/16bit_rle_noalphabits.tga";
        public const string NoAlphaBits32Bit = "Tga/32bit_no_alphabits.tga";
        public const string NoAlphaBits32BitRle = "Tga/32bit_rle_no_alphabits.tga";

        public const string Github_RLE_legacy = "Tga/Github_RLE_legacy.tga";
        public const string WhiteStripesPattern = "Tga/whitestripes.png";
    }

    public static class Webp
    {
        // Reference image as png
        public const string Peak = "Webp/peak.png";

        // Test pattern images for testing the encoder.
        public const string TestPatternOpaque = "Webp/testpattern_opaque.png";
        public const string TestPatternOpaqueSmall = "Webp/testpattern_opaque_small.png";
        public const string RgbTestPattern100x100 = "Webp/rgb_pattern_100x100.png";
        public const string RgbTestPattern80x80 = "Webp/rgb_pattern_80x80.png";
        public const string RgbTestPattern63x63 = "Webp/rgb_pattern_63x63.png";

        // Test image for encoding image with a palette.
        public const string Flag = "Webp/flag_of_germany.png";

        // Test images for converting rgb data to yuv.
        public const string Yuv = "Webp/yuv_test.png";

        public static class Lossless
        {
            public const string Animated = "Webp/leo_animated_lossless.webp";
            public const string Earth = "Webp/earth_lossless.webp";
            public const string Alpha = "Webp/lossless_alpha_small.webp";
            public const string WithExif = "Webp/exif_lossless.webp";
            public const string WithIccp = "Webp/lossless_with_iccp.webp";
            public const string NoTransform1 = "Webp/lossless_vec_1_0.webp";
            public const string NoTransform2 = "Webp/lossless_vec_2_0.webp";
            public const string GreenTransform1 = "Webp/lossless1.webp";
            public const string GreenTransform2 = "Webp/lossless2.webp";
            public const string GreenTransform3 = "Webp/lossless3.webp";
            public const string GreenTransform4 = "Webp/lossless_vec_1_4.webp";
            public const string GreenTransform5 = "Webp/lossless_vec_2_4.webp";
            public const string CrossColorTransform1 = "Webp/lossless_vec_1_8.webp";
            public const string CrossColorTransform2 = "Webp/lossless_vec_2_8.webp";
            public const string PredictorTransform1 = "Webp/lossless_vec_1_2.webp";
            public const string PredictorTransform2 = "Webp/lossless_vec_2_2.webp";
            public const string ColorIndexTransform1 = "Webp/lossless4.webp";
            public const string ColorIndexTransform2 = "Webp/lossless_vec_1_1.webp";
            public const string ColorIndexTransform3 = "Webp/lossless_vec_1_5.webp";
            public const string ColorIndexTransform4 = "Webp/lossless_vec_2_1.webp";
            public const string ColorIndexTransform5 = "Webp/lossless_vec_2_5.webp";
            public const string TwoTransforms1 = "Webp/lossless_vec_1_10.webp"; // cross_color, predictor
            public const string TwoTransforms2 = "Webp/lossless_vec_1_12.webp"; // cross_color, substract_green
            public const string TwoTransforms3 = "Webp/lossless_vec_1_13.webp"; // color_indexing, cross_color
            public const string TwoTransforms4 = "Webp/lossless_vec_1_3.webp"; // color_indexing, predictor
            public const string TwoTransforms5 = "Webp/lossless_vec_1_6.webp"; // substract_green, predictor
            public const string TwoTransforms6 = "Webp/lossless_vec_1_7.webp"; // color_indexing, predictor
            public const string TwoTransforms7 = "Webp/lossless_vec_1_9.webp"; // color_indexing, cross_color
            public const string TwoTransforms8 = "Webp/lossless_vec_2_10.webp"; // predictor, cross_color
            public const string TwoTransforms9 = "Webp/lossless_vec_2_12.webp"; // substract_green, cross_color
            public const string TwoTransforms10 = "Webp/lossless_vec_2_13.webp"; // color_indexing, cross_color
            public const string TwoTransforms11 = "Webp/lossless_vec_2_3.webp"; // color_indexing, predictor
            public const string TwoTransforms12 = "Webp/lossless_vec_2_6.webp"; // substract_green, predictor
            public const string TwoTransforms13 = "Webp/lossless_vec_2_9.webp"; // color_indexing, predictor

            // substract_green, predictor, cross_color
            public const string ThreeTransforms1 = "Webp/color_cache_bits_11.webp";

            // color_indexing, predictor, cross_color
            public const string ThreeTransforms2 = "Webp/lossless_vec_1_11.webp";

            // substract_green, predictor, cross_color
            public const string ThreeTransforms3 = "Webp/lossless_vec_1_14.webp";

            // color_indexing, predictor, cross_color
            public const string ThreeTransforms4 = "Webp/lossless_vec_1_15.webp";

            // color_indexing, predictor, cross_color
            public const string ThreeTransforms5 = "Webp/lossless_vec_2_11.webp";

            // substract_green, predictor, cross_color
            public const string ThreeTransforms6 = "Webp/lossless_vec_2_14.webp";

            // color_indexing, predictor, cross_color
            public const string ThreeTransforms7 = "Webp/lossless_vec_2_15.webp";

            // substract_green, predictor, cross_color
            public const string BikeThreeTransforms = "Webp/bike_lossless.webp";

            // Invalid / corrupted images
            // Below images have errors according to webpinfo. The error message webpinfo gives is "Truncated data detected when parsing RIFF payload."
            public const string LossLessCorruptImage1 = "Webp/lossless_big_random_alpha.webp"; // substract_green, predictor, cross_color.

            public const string LossLessCorruptImage2 = "Webp/lossless_vec_2_7.webp"; // color_indexing, predictor.

            public const string LossLessCorruptImage3 = "Webp/lossless_color_transform.webp"; // cross_color, predictor

            public const string LossLessCorruptImage4 = "Webp/near_lossless_75.webp"; // predictor, cross_color.
        }

        public static class Lossy
        {
            public const string AnimatedLandscape = "Webp/landscape.webp";
            public const string Earth = "Webp/earth_lossy.webp";
            public const string WithExif = "Webp/exif_lossy.webp";
            public const string WithExifNotEnoughData = "Webp/exif_lossy_not_enough_data.webp";
            public const string WithIccp = "Webp/lossy_with_iccp.webp";
            public const string WithXmp = "Webp/xmp_lossy.webp";
            public const string BikeSmall = "Webp/bike_lossy_small.webp";
            public const string Animated = "Webp/leo_animated_lossy.webp";
            public const string AnimatedIssue2528 = "Webp/issues/Issue2528.webp";

            // Lossy images without macroblock filtering.
            public const string BikeWithExif = "Webp/bike_lossy_with_exif.webp";
            public const string NoFilter01 = "Webp/vp80-01-intra-1400.webp";
            public const string NoFilter02 = "Webp/vp80-00-comprehensive-010.webp";
            public const string NoFilter03 = "Webp/vp80-00-comprehensive-005.webp";
            public const string NoFilter04 = "Webp/vp80-01-intra-1417.webp";
            public const string NoFilter05 = "Webp/vp80-02-inter-1402.webp";
            public const string NoFilter06 = "Webp/test.webp";

            // Lossy images with a simple filter.
            public const string SimpleFilter01 = "Webp/segment01.webp";
            public const string SimpleFilter02 = "Webp/segment02.webp";
            public const string SimpleFilter03 = "Webp/vp80-00-comprehensive-003.webp";
            public const string SimpleFilter04 = "Webp/vp80-00-comprehensive-007.webp";
            public const string SimpleFilter05 = "Webp/test-nostrong.webp";

            // Lossy images with a complex filter.
            public const string IccpComplexFilter = WithIccp;
            public const string VeryShort = "Webp/very_short.webp";
            public const string BikeComplexFilter = "Webp/bike_lossy_complex_filter.webp";
            public const string ComplexFilter01 = "Webp/vp80-02-inter-1418.webp";
            public const string ComplexFilter02 = "Webp/vp80-02-inter-1418.webp";
            public const string ComplexFilter03 = "Webp/vp80-00-comprehensive-002.webp";
            public const string ComplexFilter04 = "Webp/vp80-00-comprehensive-006.webp";
            public const string ComplexFilter05 = "Webp/vp80-00-comprehensive-009.webp";
            public const string ComplexFilter06 = "Webp/vp80-00-comprehensive-012.webp";
            public const string ComplexFilter07 = "Webp/vp80-00-comprehensive-015.webp";
            public const string ComplexFilter08 = "Webp/vp80-00-comprehensive-016.webp";
            public const string ComplexFilter09 = "Webp/vp80-00-comprehensive-017.webp";

            // Lossy with partitions.
            public const string Partitions01 = "Webp/vp80-04-partitions-1404.webp";
            public const string Partitions02 = "Webp/vp80-04-partitions-1405.webp";
            public const string Partitions03 = "Webp/vp80-04-partitions-1406.webp";

            // Lossy with segmentation.
            public const string SegmentationNoFilter01 = "Webp/vp80-03-segmentation-1401.webp";
            public const string SegmentationNoFilter02 = "Webp/vp80-03-segmentation-1403.webp";
            public const string SegmentationNoFilter03 = "Webp/vp80-03-segmentation-1407.webp";
            public const string SegmentationNoFilter04 = "Webp/vp80-03-segmentation-1408.webp";
            public const string SegmentationNoFilter05 = "Webp/vp80-03-segmentation-1409.webp";
            public const string SegmentationNoFilter06 = "Webp/vp80-03-segmentation-1410.webp";
            public const string SegmentationComplexFilter01 = "Webp/vp80-03-segmentation-1413.webp";
            public const string SegmentationComplexFilter02 = "Webp/vp80-03-segmentation-1425.webp";
            public const string SegmentationComplexFilter03 = "Webp/vp80-03-segmentation-1426.webp";
            public const string SegmentationComplexFilter04 = "Webp/vp80-03-segmentation-1427.webp";
            public const string SegmentationComplexFilter05 = "Webp/vp80-03-segmentation-1432.webp";

            // Lossy with sharpness level.
            public const string Sharpness01 = "Webp/vp80-05-sharpness-1428.webp";
            public const string Sharpness02 = "Webp/vp80-05-sharpness-1429.webp";
            public const string Sharpness03 = "Webp/vp80-05-sharpness-1430.webp";
            public const string Sharpness04 = "Webp/vp80-05-sharpness-1431.webp";
            public const string Sharpness05 = "Webp/vp80-05-sharpness-1433.webp";
            public const string Sharpness06 = "Webp/vp80-05-sharpness-1434.webp";

            // Very small images (all with complex filter).
            public const string Small01 = "Webp/small_13x1.webp";
            public const string Small02 = "Webp/small_1x1.webp";
            public const string Small03 = "Webp/small_1x13.webp";
            public const string Small04 = "Webp/small_31x13.webp";

            // Lossy images with a alpha channel.
            public const string Alpha1 = "Webp/lossy_alpha1.webp";
            public const string Alpha2 = "Webp/lossy_alpha2.webp";
            public const string Alpha3 = "Webp/alpha_color_cache.webp";
            public const string AlphaNoCompression = "Webp/alpha_no_compression.webp";
            public const string AlphaNoCompressionNoFilter = "Webp/alpha_filter_0_method_0.webp";
            public const string AlphaCompressedNoFilter = "Webp/alpha_filter_0_method_1.webp";
            public const string AlphaNoCompressionHorizontalFilter = "Webp/alpha_filter_1_method_0.webp";
            public const string AlphaCompressedHorizontalFilter = "Webp/alpha_filter_1_method_1.webp";
            public const string AlphaNoCompressionVerticalFilter = "Webp/alpha_filter_2_method_0.webp";
            public const string AlphaCompressedVerticalFilter = "Webp/alpha_filter_2_method_1.webp";
            public const string AlphaNoCompressionGradientFilter = "Webp/alpha_filter_3_method_0.webp";
            public const string AlphaCompressedGradientFilter = "Webp/alpha_filter_3_method_1.webp";
            public const string AlphaThinkingSmiley = "Webp/1602311202.webp";
            public const string AlphaSticker = "Webp/sticker.webp";

            // Issues
            public const string Issue1594 = "Webp/issues/Issue1594.webp";
            public const string Issue2243 = "Webp/issues/Issue2243.webp";
            public const string Issue2257 = "Webp/issues/Issue2257.webp";
            public const string Issue2670 = "Webp/issues/Issue2670.webp";
            public const string Issue2763 = "Webp/issues/Issue2763.png";
            public const string Issue2801 = "Webp/issues/Issue2801.webp";
            public const string Issue2866 = "Webp/issues/Issue2866.webp";
        }
    }

    public static class Tiff
    {
        public const string Benchmark_Path = "Tiff/Benchmarks/";
        public const string Benchmark_BwFax3 = "medium_bw_Fax3.tiff";
        public const string Benchmark_BwFax4 = "medium_bw_Fax4.tiff";
        public const string Benchmark_BwRle = "medium_bw_Rle.tiff";
        public const string Benchmark_GrayscaleUncompressed = "medium_grayscale_uncompressed.tiff";
        public const string Benchmark_PaletteUncompressed = "medium_palette_uncompressed.tiff";
        public const string Benchmark_RgbDeflate = "medium_rgb_deflate.tiff";
        public const string Benchmark_RgbLzw = "medium_rgb_lzw.tiff";
        public const string Benchmark_RgbPackbits = "medium_rgb_packbits.tiff";
        public const string Benchmark_RgbUncompressed = "medium_rgb_uncompressed.tiff";

        public const string Calliphora_GrayscaleUncompressed = "Tiff/Calliphora_grayscale_uncompressed.tiff";
        public const string Calliphora_GrayscaleDeflate_Predictor = "Tiff/Calliphora_gray_deflate_predictor.tiff";
        public const string Calliphora_GrayscaleLzw_Predictor = "Tiff/Calliphora_gray_lzw_predictor.tiff";
        public const string Calliphora_GrayscaleDeflate = "Tiff/Calliphora_gray_deflate.tiff";
        public const string Calliphora_GrayscaleUncompressed16Bit = "Tiff/Calliphora_grayscale_uncompressed_16bit.tiff";
        public const string Calliphora_GrayscaleDeflate_Predictor16Bit = "Tiff/Calliphora_gray_deflate_predictor_16bit.tiff";
        public const string Calliphora_GrayscaleLzw_Predictor16Bit = "Tiff/Calliphora_gray_lzw_predictor_16bit.tiff";
        public const string Calliphora_GrayscaleDeflate16Bit = "Tiff/Calliphora_gray_deflate_16bit.tiff";
        public const string Calliphora_RgbDeflate_Predictor = "Tiff/Calliphora_rgb_deflate_predictor.tiff";
        public const string Calliphora_RgbJpeg = "Tiff/Calliphora_rgb_jpeg.tiff";
        public const string Calliphora_PaletteUncompressed = "Tiff/Calliphora_palette_uncompressed.tiff";
        public const string Calliphora_RgbLzwPredictor = "Tiff/Calliphora_rgb_lzw_predictor.tiff";
        public const string Calliphora_RgbPaletteLzw = "Tiff/Calliphora_rgb_palette_lzw.tiff";
        public const string Calliphora_RgbPaletteLzw_Predictor = "Tiff/Calliphora_rgb_palette_lzw_predictor.tiff";
        public const string Calliphora_RgbPackbits = "Tiff/Calliphora_rgb_packbits.tiff";
        public const string Calliphora_RgbUncompressed = "Tiff/Calliphora_rgb_uncompressed.tiff";
        public const string Calliphora_Fax3Compressed = "Tiff/Calliphora_ccitt_fax3.tiff";
        public const string Fax3Uncompressed = "Tiff/ccitt_fax3_uncompressed.tiff";
        public const string Calliphora_Fax3Compressed_WithEolPadding = "Tiff/Calliphora_ccitt_fax3_with_eol_padding.tiff";
        public const string Calliphora_Fax4Compressed = "Tiff/Calliphora_ccitt_fax4.tiff";
        public const string Calliphora_HuffmanCompressed = "Tiff/Calliphora_huffman_rle.tiff";
        public const string Calliphora_BiColorUncompressed = "Tiff/Calliphora_bicolor_uncompressed.tiff";
        public const string Fax4Compressed = "Tiff/basi3p02_fax4.tiff";
        public const string Fax4Compressed2 = "Tiff/CCITTGroup4.tiff";
        public const string Fax4CompressedLowerOrderBitsFirst = "Tiff/basi3p02_fax4_lowerOrderBitsFirst.tiff";
        public const string WebpCompressed = "Tiff/webp_compressed.tiff";
        public const string Fax4CompressedMinIsBlack = "Tiff/CCITTGroup4_minisblack.tiff";
        public const string CcittFax3AllTermCodes = "Tiff/ccitt_fax3_all_terminating_codes.tiff";
        public const string CcittFax3AllMakeupCodes = "Tiff/ccitt_fax3_all_makeup_codes.tiff";
        public const string HuffmanRleAllTermCodes = "Tiff/huffman_rle_all_terminating_codes.tiff";
        public const string HuffmanRleAllMakeupCodes = "Tiff/huffman_rle_all_makeup_codes.tiff";
        public const string CcittFax3LowerOrderBitsFirst = "Tiff/basi3p02_fax3_lowerOrderBitsFirst.tiff";
        public const string HuffmanRleLowerOrderBitsFirst = "Tiff/basi3p02_huffman_rle_lowerOrderBitsFirst.tiff";

        // Test case for an issue, that the last bits in a row got ignored.
        public const string HuffmanRle_basi3p02 = "Tiff/basi3p02_huffman_rle.tiff";

        public const string GrayscaleDeflateMultistrip = "Tiff/grayscale_deflate_multistrip.tiff";
        public const string GrayscaleUncompressed = "Tiff/grayscale_uncompressed.tiff";
        public const string GrayscaleUncompressed16Bit = "Tiff/grayscale_uncompressed_16bit.tiff";
        public const string GrayscaleJpegCompressed = "Tiff/JpegCompressedGray.tiff";
        public const string PaletteDeflateMultistrip = "Tiff/palette_grayscale_deflate_multistrip.tiff";
        public const string PaletteUncompressed = "Tiff/palette_uncompressed.tiff";
        public const string RgbDeflate = "Tiff/rgb_deflate.tiff";
        public const string RgbDeflatePredictor = "Tiff/rgb_deflate_predictor.tiff";
        public const string RgbDeflateMultistrip = "Tiff/rgb_deflate_multistrip.tiff";
        public const string RgbJpegCompressed = "Tiff/rgb_jpegcompression.tiff";
        public const string RgbJpegCompressed2 = "Tiff/twain-rgb-jpeg-with-bogus-ycbcr-subsampling.tiff";
        public const string RgbOldJpegCompressed = "Tiff/OldJpegCompression.tiff";
        public const string RgbOldJpegCompressed2 = "Tiff/OldJpegCompression2.tiff";
        public const string RgbOldJpegCompressed3 = "Tiff/OldJpegCompression3.tiff";
        public const string RgbOldJpegCompressedGray = "Tiff/OldJpegCompressionGray.tiff";
        public const string YCbCrOldJpegCompressed = "Tiff/YCbCrOldJpegCompressed.tiff";
        public const string RgbWithStripsJpegCompressed = "Tiff/rgb_jpegcompressed_stripped.tiff";
        public const string RgbJpegCompressedNoJpegTable = "Tiff/rgb_jpegcompressed_nojpegtable.tiff";
        public const string RgbLzwPredictor = "Tiff/rgb_lzw_predictor.tiff";
        public const string RgbLzwNoPredictor = "Tiff/rgb_lzw_no_predictor.tiff";
        public const string RgbLzwNoPredictorMultistrip = "Tiff/rgb_lzw_noPredictor_multistrip.tiff";
        public const string RgbLzwNoPredictorMultistripMotorola = "Tiff/rgb_lzw_noPredictor_multistrip_Motorola.tiff";
        public const string RgbLzwNoPredictorSinglestripMotorola = "Tiff/rgb_lzw_noPredictor_singlestrip_Motorola.tiff";
        public const string RgbLzwMultistripPredictor = "Tiff/rgb_lzw_multistrip.tiff";
        public const string RgbPackbits = "Tiff/rgb_packbits.tiff";
        public const string RgbPackbitsMultistrip = "Tiff/rgb_packbits_multistrip.tiff";
        public const string RgbUncompressed = "Tiff/rgb_uncompressed.tiff";
        public const string RgbPalette = "Tiff/rgb_palette.tiff";
        public const string Rgb4BitPalette = "Tiff/bike_colorpalette_4bit.tiff";
        public const string RgbPaletteDeflate = "Tiff/rgb_palette_deflate.tiff";
        public const string FlowerRgbFloat323232 = "Tiff/flower-rgb-float32_msb.tiff";
        public const string FlowerRgbFloat323232LittleEndian = "Tiff/flower-rgb-float32_lsb.tiff";
        public const string FlowerRgb323232Contiguous = "Tiff/flower-rgb-contig-32.tiff";
        public const string FlowerRgb323232ContiguousLittleEndian = "Tiff/flower-rgb-contig-32_lsb.tiff";
        public const string FlowerRgb323232Planar = "Tiff/flower-rgb-planar-32.tiff";
        public const string FlowerRgb323232PlanarLittleEndian = "Tiff/flower-rgb-planar-32_lsb.tiff";
        public const string FlowerRgb323232PredictorBigEndian = "Tiff/flower-rgb-contig-32_msb_zip_predictor.tiff";
        public const string FlowerRgb323232PredictorLittleEndian = "Tiff/flower-rgb-contig-32_lsb_zip_predictor.tiff";
        public const string FlowerRgb242424Planar = "Tiff/flower-rgb-planar-24.tiff";
        public const string FlowerRgb242424PlanarLittleEndian = "Tiff/flower-rgb-planar-24_lsb.tiff";
        public const string FlowerRgb242424Contiguous = "Tiff/flower-rgb-contig-24.tiff";
        public const string FlowerRgb242424ContiguousLittleEndian = "Tiff/flower-rgb-contig-24_lsb.tiff";
        public const string FlowerRgb161616Contiguous = "Tiff/flower-rgb-contig-16.tiff";
        public const string FlowerRgb161616ContiguousLittleEndian = "Tiff/flower-rgb-contig-16_lsb.tiff";
        public const string FlowerRgb161616PredictorBigEndian = "Tiff/flower-rgb-contig-16_msb_zip_predictor.tiff";
        public const string FlowerRgb161616PredictorLittleEndian = "Tiff/flower-rgb-contig-16_lsb_zip_predictor.tiff";
        public const string FlowerRgb161616Planar = "Tiff/flower-rgb-planar-16.tiff";
        public const string FlowerRgb161616PlanarLittleEndian = "Tiff/flower-rgb-planar-16_lsb.tiff";
        public const string FlowerRgb141414Contiguous = "Tiff/flower-rgb-contig-14.tiff";
        public const string FlowerRgb141414Planar = "Tiff/flower-rgb-planar-14.tiff";
        public const string FlowerRgb121212Contiguous = "Tiff/flower-rgb-contig-12.tiff";
        public const string FlowerRgb101010Contiguous = "Tiff/flower-rgb-contig-10.tiff";
        public const string FlowerRgb101010Planar = "Tiff/flower-rgb-planar-10.tiff";
        public const string FlowerRgb888Contiguous = "Tiff/flower-rgb-contig-08.tiff";
        public const string FlowerRgb888Planar6Strips = "Tiff/flower-rgb-planar-08-6strips.tiff";
        public const string FlowerRgb888Planar15Strips = "Tiff/flower-rgb-planar-08-15strips.tiff";
        public const string FlowerYCbCr888Contiguous = "Tiff/flower-ycbcr-contig-08_h1v1.tiff";
        public const string FlowerYCbCr888Planar = "Tiff/flower-ycbcr-planar-08_h1v1.tiff";
        public const string FlowerYCbCr888Contiguoush2v1 = "Tiff/flower-ycbcr-contig-08_h2v1.tiff";
        public const string FlowerYCbCr888Contiguoush2v2 = "Tiff/flower-ycbcr-contig-08_h2v2.tiff";
        public const string FlowerYCbCr888Contiguoush4v4 = "Tiff/flower-ycbcr-contig-08_h4v4.tiff";
        public const string RgbYCbCr888Contiguoush2v2 = "Tiff/rgb-ycbcr-contig-08_h2v2.tiff";
        public const string RgbYCbCr888Contiguoush4v4 = "Tiff/rgb-ycbcr-contig-08_h4v4.tiff";
        public const string YCbCrJpegCompressed = "Tiff/ycbcr_jpegcompressed.tiff";
        public const string YCbCrJpegCompressed2 = "Tiff/ycbcr_jpegcompressed2.tiff";
        public const string FlowerRgb444Contiguous = "Tiff/flower-rgb-contig-04.tiff";
        public const string FlowerRgb444Planar = "Tiff/flower-rgb-planar-04.tiff";
        public const string FlowerRgb222Contiguous = "Tiff/flower-rgb-contig-02.tiff";
        public const string FlowerRgb222Planar = "Tiff/flower-rgb-planar-02.tiff";
        public const string Flower2BitGray = "Tiff/flower-minisblack-02.tiff";
        public const string Flower2BitPalette = "Tiff/flower-palette-02.tiff";
        public const string Flower4BitPalette = "Tiff/flower-palette-04.tiff";
        public const string Flower4BitPaletteGray = "Tiff/flower-minisblack-04.tiff";
        public const string FLowerRgb3Bit = "Tiff/flower-rgb-3bit.tiff";
        public const string FLowerRgb5Bit = "Tiff/flower-rgb-5bit.tiff";
        public const string FLowerRgb6Bit = "Tiff/flower-rgb-6bit.tiff";
        public const string Flower6BitGray = "Tiff/flower-minisblack-06.tiff";
        public const string Flower8BitGray = "Tiff/flower-minisblack-08.tiff";
        public const string Flower10BitGray = "Tiff/flower-minisblack-10.tiff";
        public const string Flower12BitGray = "Tiff/flower-minisblack-12.tiff";
        public const string Flower14BitGray = "Tiff/flower-minisblack-14.tiff";
        public const string Flower16BitGray = "Tiff/flower-minisblack-16.tiff";
        public const string Flower16BitGrayLittleEndian = "Tiff/flower-minisblack-16_lsb.tiff";
        public const string Flower16BitGrayMinIsWhiteLittleEndian = "Tiff/flower-miniswhite-16_lsb.tiff";
        public const string Flower16BitGrayPredictorBigEndian = "Tiff/flower-minisblack-16_msb_lzw_predictor.tiff";
        public const string Flower16BitGrayPredictorLittleEndian = "Tiff/flower-minisblack-16_lsb_lzw_predictor.tiff";
        public const string Flower16BitGrayMinIsWhiteBigEndian = "Tiff/flower-miniswhite-16_msb.tiff";
        public const string Flower24BitGray = "Tiff/flower-minisblack-24_msb.tiff";
        public const string Flower24BitGrayLittleEndian = "Tiff/flower-minisblack-24_lsb.tiff";
        public const string Flower32BitGray = "Tiff/flower-minisblack-32_msb.tiff";
        public const string Flower32BitGrayLittleEndian = "Tiff/flower-minisblack-32_lsb.tiff";
        public const string Flower32BitFloatGray = "Tiff/flower-minisblack-float32_msb.tiff";
        public const string Flower32BitFloatGrayLittleEndian = "Tiff/flower-minisblack-float32_lsb.tiff";
        public const string Flower32BitFloatGrayMinIsWhite = "Tiff/flower-miniswhite-float32_msb.tiff";
        public const string Flower32BitFloatGrayMinIsWhiteLittleEndian = "Tiff/flower-miniswhite-float32_lsb.tiff";
        public const string Flower32BitGrayMinIsWhite = "Tiff/flower-miniswhite-32_msb.tiff";
        public const string Flower32BitGrayMinIsWhiteLittleEndian = "Tiff/flower-miniswhite-32_lsb.tiff";
        public const string Flower32BitGrayPredictorBigEndian = "Tiff/flower-minisblack-32_msb_deflate_predictor.tiff";
        public const string Flower32BitGrayPredictorLittleEndian = "Tiff/flower-minisblack-32_lsb_deflate_predictor.tiff";

        // Tiled images.
        public const string Tiled = "Tiff/tiled.tiff";
        public const string QuadTile = "Tiff/quad-tile.tiff";
        public const string TiledChunky = "Tiff/rgb_uncompressed_tiled_chunky.tiff";
        public const string TiledPlanar = "Tiff/rgb_uncompressed_tiled_planar.tiff";

        // Images with alpha channel.
        public const string Rgba2BitUnassociatedAlpha = "Tiff/RgbaUnassociatedAlpha2bit.tiff";
        public const string Rgba3BitUnassociatedAlpha = "Tiff/RgbaUnassociatedAlpha3bit.tiff";
        public const string Rgba3BitAssociatedAlpha = "Tiff/RgbaAssociatedAlpha3bit.tiff";
        public const string Rgba4BitUnassociatedAlpha = "Tiff/RgbaUnassociatedAlpha4bit.tiff";
        public const string Rgba4BitAassociatedAlpha = "Tiff/RgbaAssociatedAlpha4bit.tiff";
        public const string Rgba5BitUnassociatedAlpha = "Tiff/RgbaUnassociatedAlpha5bit.tiff";
        public const string Rgba5BitAssociatedAlpha = "Tiff/RgbaAssociatedAlpha5bit.tiff";
        public const string Rgba6BitUnassociatedAlpha = "Tiff/RgbaUnassociatedAlpha6bit.tiff";
        public const string Rgba6BitAssociatedAlpha = "Tiff/RgbaAssociatedAlpha6bit.tiff";
        public const string Rgba8BitUnassociatedAlpha = "Tiff/RgbaUnassociatedAlpha8bit.tiff";
        public const string Rgba8BitAssociatedAlpha = "Tiff/RgbaAlpha8bit.tiff";
        public const string Rgba8BitUnassociatedAlphaWithPredictor = "Tiff/RgbaUnassociatedAlphaPredictor8bit.tiff";
        public const string Rgba8BitPlanarUnassociatedAlpha = "Tiff/RgbaUnassociatedAlphaPlanar8bit.tiff";
        public const string Rgba10BitUnassociatedAlphaBigEndian = "Tiff/RgbaUnassociatedAlpha10bit_msb.tiff";
        public const string Rgba10BitUnassociatedAlphaLittleEndian = "Tiff/RgbaUnassociatedAlpha10bit_lsb.tiff";
        public const string Rgba10BitAssociatedAlphaBigEndian = "Tiff/RgbaAssociatedAlpha10bit_msb.tiff";
        public const string Rgba10BitAssociatedAlphaLittleEndian = "Tiff/RgbaAssociatedAlpha10bit_lsb.tiff";
        public const string Rgba12BitUnassociatedAlphaBigEndian = "Tiff/RgbaUnassociatedAlpha12bit_msb.tiff";
        public const string Rgba12BitUnassociatedAlphaLittleEndian = "Tiff/RgbaUnassociatedAlpha12bit_lsb.tiff";
        public const string Rgba12BitAssociatedAlphaBigEndian = "Tiff/RgbaAssociatedAlpha12bit_msb.tiff";
        public const string Rgba12BitAssociatedAlphaLittleEndian = "Tiff/RgbaAssociatedAlpha12bit_lsb.tiff";
        public const string Rgba14BitUnassociatedAlphaBigEndian = "Tiff/RgbaUnassociatedAlpha14bit_msb.tiff";
        public const string Rgba14BitUnassociatedAlphaLittleEndian = "Tiff/RgbaUnassociatedAlpha14bit_lsb.tiff";
        public const string Rgba14BitAssociatedAlphaBigEndian = "Tiff/RgbaAssociatedAlpha14bit_msb.tiff";
        public const string Rgba14BitAssociatedAlphaLittleEndian = "Tiff/RgbaAssociatedAlpha14bit_lsb.tiff";
        public const string Rgba16BitUnassociatedAlphaBigEndian = "Tiff/RgbaUnassociatedAlpha16bit_msb.tiff";
        public const string Rgba16BitUnassociatedAlphaLittleEndian = "Tiff/RgbaUnassociatedAlpha16bit_lsb.tiff";
        public const string Rgba16BitAssociatedAlphaBigEndian = "Tiff/RgbaAssociatedAlpha16bit_msb.tiff";
        public const string Rgba16BitAssociatedAlphaLittleEndian = "Tiff/RgbaAssociatedAlpha16bit_lsb.tiff";
        public const string Rgba16BitUnassociatedAlphaBigEndianWithPredictor = "Tiff/RgbaUnassociatedAlphaPredictor16bit_msb.tiff";
        public const string Rgba16BitUnassociatedAlphaLittleEndianWithPredictor = "Tiff/RgbaUnassociatedAlphaPredictor16bit_lsb.tiff";
        public const string Rgba16BitPlanarUnassociatedAlphaBigEndian = "Tiff/RgbaUnassociatedAlphaPlanar16bit_msb.tiff";
        public const string Rgba16BitPlanarUnassociatedAlphaLittleEndian = "Tiff/RgbaUnassociatedAlphaPlanar16bit_lsb.tiff";
        public const string Rgba24BitUnassociatedAlphaBigEndian = "Tiff/RgbaUnassociatedAlpha24bit_msb.tiff";
        public const string Rgba24BitUnassociatedAlphaLittleEndian = "Tiff/RgbaUnassociatedAlpha24bit_lsb.tiff";
        public const string Rgba24BitPlanarUnassociatedAlphaBigEndian = "Tiff/RgbaUnassociatedAlphaPlanar24bit_msb.tiff";
        public const string Rgba24BitPlanarUnassociatedAlphaLittleEndian = "Tiff/RgbaUnassociatedAlphaPlanar24bit_lsb.tiff";
        public const string Rgba32BitUnassociatedAlphaBigEndian = "Tiff/RgbaUnassociatedAlpha32bit_msb.tiff";
        public const string Rgba32BitUnassociatedAlphaLittleEndian = "Tiff/RgbaUnassociatedAlpha32bit_lsb.tiff";
        public const string Rgba32BitUnassociatedAlphaBigEndianWithPredictor = "Tiff/RgbaUnassociatedAlphaPredictor32bit_msb.tiff";
        public const string Rgba32BitUnassociatedAlphaLittleEndianWithPredictor = "Tiff/RgbaUnassociatedAlphaPredictor32bit_lsb.tiff";
        public const string Rgba32BitPlanarUnassociatedAlphaBigEndian = "Tiff/RgbaUnassociatedAlphaPlanar32bit_msb.tiff";
        public const string Rgba32BitPlanarUnassociatedAlphaLittleEndian = "Tiff/RgbaUnassociatedAlphaPlanar32bit_lsb.tiff";

        // Cie Lab color space.
        public const string CieLab = "Tiff/CieLab.tiff";
        public const string CieLabPlanar = "Tiff/CieLabPlanar.tiff";
        public const string CieLabLzwPredictor = "Tiff/CieLab_lzwcompressed_predictor.tiff";

        public const string Cmyk = "Tiff/Cmyk.tiff";
        public const string Cmyk64BitDeflate = "Tiff/cmyk_deflate_64bit.tiff";
        public const string CmykLzwPredictor = "Tiff/Cmyk-lzw-predictor.tiff";

        public const string Issues1716Rgb161616BitLittleEndian = "Tiff/Issues/Issue1716.tiff";
        public const string Issues1891 = "Tiff/Issues/Issue1891.tiff";
        public const string Issues2123 = "Tiff/Issues/Issue2123.tiff";
        public const string Issues2149 = "Tiff/Issues/Group4CompressionWithStrips.tiff";
        public const string Issues2255 = "Tiff/Issues/Issue2255.png";
        public const string Issues2435 = "Tiff/Issues/Issue2435.tiff";
        public const string Issues2587 = "Tiff/Issues/Issue2587.tiff";
        public const string JpegCompressedGray0000539558 = "Tiff/Issues/JpegCompressedGray-0000539558.tiff";
        public const string Tiled0000023664 = "Tiff/Issues/tiled-0000023664.tiff";

        public const string SmallRgbDeflate = "Tiff/rgb_small_deflate.tiff";
        public const string SmallRgbLzw = "Tiff/rgb_small_lzw.tiff";

        public const string RgbUncompressedTiled = "Tiff/rgb_uncompressed_tiled.tiff";
        public const string MultiframeDifferentSizeTiled = "Tiff/multipage_ withPreview_differentSize_tiled.tiff";

        public const string MultiframeLzwPredictor = "Tiff/multipage_lzw.tiff";
        public const string MultiframeDeflateWithPreview = "Tiff/multipage_deflate_withPreview.tiff";
        public const string MultiframeDifferentSize = "Tiff/multipage_differentSize.tiff";
        public const string MultiframeDifferentVariants = "Tiff/multipage_differentVariants.tiff";
        public const string MultiFrameMipMap = "Tiff/SKC1H3.tiff";

        public const string LittleEndianByteOrder = "Tiff/little_endian.tiff";

        public const string Fax4_Motorola = "Tiff/moy.tiff";

        public const string SampleMetadata = "Tiff/metadata_sample.tiff";

        // Iptc data as long[] instead of byte[]
        public const string InvalidIptcData = "Tiff/7324fcaff3aad96f27899da51c1bb5d9.tiff";
        public const string IptcData = "Tiff/iptc.tiff";

        public static readonly string[] Multiframes = { MultiframeDeflateWithPreview, MultiframeLzwPredictor /*, MultiFrameDifferentSize, MultiframeDifferentSizeTiled, MultiFrameDifferentVariants,*/ };

        public static readonly string[] Metadata = { SampleMetadata };
    }

    public static class BigTiff
    {
        public const string Base = "Tiff/BigTiff/";

        public const string BigTIFF = Base + "BigTIFF.tif";
        public const string BigTIFFLong = Base + "BigTIFFLong.tif";
        public const string BigTIFFLong8 = Base + "BigTIFFLong8.tif";
        public const string BigTIFFLong8Tiles = Base + "BigTIFFLong8Tiles.tif";
        public const string BigTIFFMotorola = Base + "BigTIFFMotorola.tif";
        public const string BigTIFFMotorolaLongStrips = Base + "BigTIFFMotorolaLongStrips.tif";

        public const string BigTIFFSubIFD4 = Base + "BigTIFFSubIFD4.tif";
        public const string BigTIFFSubIFD8 = Base + "BigTIFFSubIFD8.tif";

        public const string Indexed4_Deflate = Base + "BigTIFF_Indexed4_Deflate.tif";
        public const string Indexed8_LZW = Base + "BigTIFF_Indexed8_LZW.tif";
        public const string MinIsBlack = Base + "BigTIFF_MinIsBlack.tif";
        public const string MinIsWhite = Base + "BigTIFF_MinIsWhite.tif";

        public const string Damaged_MinIsWhite_RLE = Base + "BigTIFF_MinIsWhite_RLE.tif";
        public const string Damaged_MinIsBlack_RLE = Base + "BigTIFF_MinIsBlack_RLE.tif";
    }

    public static class Pbm
    {
        public const string BlackAndWhitePlain = "Pbm/blackandwhite_plain.pbm";
        public const string BlackAndWhiteBinary = "Pbm/blackandwhite_binary.pbm";
        public const string GrayscaleBinary = "Pbm/rings.pgm";
        public const string GrayscaleBinaryWide = "Pbm/Gene-UP WebSocket RunImageMask.pgm";
        public const string GrayscalePlain = "Pbm/grayscale_plain.pgm";
        public const string GrayscalePlainNormalized = "Pbm/grayscale_plain_normalized.pgm";
        public const string GrayscalePlainMagick = "Pbm/grayscale_plain_magick.pgm";
        public const string RgbBinary = "Pbm/00000_00000.ppm";
        public const string RgbBinaryPrematureEof = "Pbm/00000_00000_premature_eof.ppm";
        public const string RgbPlain = "Pbm/rgb_plain.ppm";
        public const string RgbPlainNormalized = "Pbm/rgb_plain_normalized.ppm";
        public const string RgbPlainMagick = "Pbm/rgb_plain_magick.ppm";
        public const string Issue2477 = "Pbm/issue2477.pbm";
    }

    public static class Qoi
    {
        public const string Dice = "Qoi/dice.qoi";
        public const string EdgeCase = "Qoi/edgecase.qoi";
        public const string Kodim10 = "Qoi/kodim10.qoi";
        public const string Kodim23 = "Qoi/kodim23.qoi";
        public const string QoiLogo = "Qoi/qoi_logo.qoi";
        public const string TestCard = "Qoi/testcard.qoi";
        public const string TestCardRGBA = "Qoi/testcard_rgba.qoi";
        public const string Wikipedia008 = "Qoi/wikipedia_008.qoi";
    }
}
