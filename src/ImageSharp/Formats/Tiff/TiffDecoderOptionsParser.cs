// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// The decoder options parser.
    /// </summary>
    internal static class TiffDecoderOptionsParser
    {
        private const TiffPredictor DefaultPredictor = TiffPredictor.None;

        private const TiffPlanarConfiguration DefaultPlanarConfiguration = TiffPlanarConfiguration.Chunky;

        /// <summary>
        /// Determines the TIFF compression and color types, and reads any associated parameters.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="exifProfile">The exif profile of the frame to decode.</param>
        /// <param name="entries">The IFD entries container to read the image format information for.</param>
        public static void VerifyAndParse(this TiffDecoderCore options, ExifProfile exifProfile, TiffFrameMetadata entries)
        {
            if (exifProfile.GetValue(ExifTag.TileOffsets)?.Value != null)
            {
                TiffThrowHelper.ThrowNotSupported("Tiled images are not supported.");
            }

            if (exifProfile.GetValue(ExifTag.ExtraSamples)?.Value != null)
            {
                TiffThrowHelper.ThrowNotSupported("ExtraSamples is not supported.");
            }

            TiffFillOrder fillOrder = (TiffFillOrder?)exifProfile.GetValue(ExifTag.FillOrder)?.Value ?? TiffFillOrder.MostSignificantBitFirst;
            if (fillOrder != TiffFillOrder.MostSignificantBitFirst)
            {
                TiffThrowHelper.ThrowNotSupported("The lower-order bits of the byte FillOrder is not supported.");
            }

            TiffPredictor predictor = (TiffPredictor?)exifProfile.GetValue(ExifTag.Predictor)?.Value ?? DefaultPredictor;
            if (predictor == TiffPredictor.FloatingPoint)
            {
                TiffThrowHelper.ThrowNotSupported("TIFF images with FloatingPoint horizontal predictor are not supported.");
            }

            TiffSampleFormat[] sampleFormat = exifProfile.GetValue(ExifTag.SampleFormat)?.Value?.Select(a => (TiffSampleFormat)a).ToArray();
            if (sampleFormat != null)
            {
                foreach (TiffSampleFormat format in sampleFormat)
                {
                    if (format != TiffSampleFormat.UnsignedInteger)
                    {
                        TiffThrowHelper.ThrowNotSupported("ImageSharp only supports the UnsignedInteger SampleFormat.");
                    }
                }
            }

            if (exifProfile.GetValue(ExifTag.StripRowCounts)?.Value != null)
            {
                TiffThrowHelper.ThrowNotSupported("Variable-sized strips are not supported.");
            }

            VerifyRequiredFieldsArePresent(exifProfile);

            options.PlanarConfiguration = (TiffPlanarConfiguration?)exifProfile.GetValue(ExifTag.PlanarConfiguration)?.Value ?? DefaultPlanarConfiguration;
            options.Predictor = predictor;
            options.PhotometricInterpretation = exifProfile.GetValue(ExifTag.PhotometricInterpretation) != null ?
                (TiffPhotometricInterpretation)exifProfile.GetValue(ExifTag.PhotometricInterpretation).Value : TiffPhotometricInterpretation.WhiteIsZero;
            options.BitsPerPixel = entries.BitsPerPixel != null ? (int)entries.BitsPerPixel.Value : (int)TiffBitsPerPixel.Bit24;
            options.BitsPerSample = GetBitsPerSample(entries.BitsPerPixel);

            ParseColorType(options, exifProfile);
            ParseCompression(options, exifProfile);
        }

        private static void VerifyRequiredFieldsArePresent(ExifProfile exifProfile)
        {
            if (exifProfile.GetValue(ExifTag.StripOffsets) == null)
            {
                TiffThrowHelper.ThrowImageFormatException("StripOffsets are missing and are required for decoding the TIFF image!");
            }

            if (exifProfile.GetValue(ExifTag.StripByteCounts) == null)
            {
                TiffThrowHelper.ThrowImageFormatException("StripByteCounts are missing and are required for decoding the TIFF image!");
            }

            if (exifProfile.GetValue(ExifTag.BitsPerSample) == null)
            {
                TiffThrowHelper.ThrowNotSupported("The TIFF BitsPerSample entry is missing which is required to decode the image!");
            }
        }

        private static void ParseColorType(this TiffDecoderCore options, ExifProfile exifProfile)
        {
            switch (options.PhotometricInterpretation)
            {
                case TiffPhotometricInterpretation.WhiteIsZero:
                {
                    if (options.BitsPerSample.Bits().Length != 1)
                    {
                        TiffThrowHelper.ThrowNotSupported("The number of samples in the TIFF BitsPerSample entry is not supported.");
                    }

                    switch (options.BitsPerSample)
                    {
                        case TiffBitsPerSample.Bit8:
                        {
                            options.ColorType = TiffColorType.WhiteIsZero8;
                            break;
                        }

                        case TiffBitsPerSample.Bit4:
                        {
                            options.ColorType = TiffColorType.WhiteIsZero4;
                            break;
                        }

                        case TiffBitsPerSample.Bit1:
                        {
                            options.ColorType = TiffColorType.WhiteIsZero1;
                            break;
                        }

                        default:
                        {
                            options.ColorType = TiffColorType.WhiteIsZero;
                            break;
                        }
                    }

                    break;
                }

                case TiffPhotometricInterpretation.BlackIsZero:
                {
                    if (options.BitsPerSample.Bits().Length != 1)
                    {
                        TiffThrowHelper.ThrowNotSupported("The number of samples in the TIFF BitsPerSample entry is not supported.");
                    }

                    switch (options.BitsPerSample)
                    {
                        case TiffBitsPerSample.Bit8:
                        {
                            options.ColorType = TiffColorType.BlackIsZero8;
                            break;
                        }

                        case TiffBitsPerSample.Bit4:
                        {
                            options.ColorType = TiffColorType.BlackIsZero4;
                            break;
                        }

                        case TiffBitsPerSample.Bit1:
                        {
                            options.ColorType = TiffColorType.BlackIsZero1;
                            break;
                        }

                        default:
                        {
                            options.ColorType = TiffColorType.BlackIsZero;
                            break;
                        }
                    }

                    break;
                }

                case TiffPhotometricInterpretation.Rgb:
                {
                    if (options.BitsPerSample.Bits().Length != 3)
                    {
                        TiffThrowHelper.ThrowNotSupported("The number of samples in the TIFF BitsPerSample entry is not supported.");
                    }

                    if (options.PlanarConfiguration == TiffPlanarConfiguration.Chunky)
                    {
                        options.ColorType = options.BitsPerSample == TiffBitsPerSample.Bit24 ? TiffColorType.Rgb888 : TiffColorType.Rgb;
                    }
                    else
                    {
                        options.ColorType = TiffColorType.RgbPlanar;
                    }

                    break;
                }

                case TiffPhotometricInterpretation.PaletteColor:
                {
                    options.ColorMap = exifProfile.GetValue(ExifTag.ColorMap)?.Value;
                    if (options.ColorMap != null)
                    {
                        if (options.BitsPerSample.Bits().Length != 1)
                        {
                            TiffThrowHelper.ThrowNotSupported("The number of samples in the TIFF BitsPerSample entry is not supported.");
                        }

                        options.ColorType = TiffColorType.PaletteColor;
                    }
                    else
                    {
                        TiffThrowHelper.ThrowNotSupported("The TIFF ColorMap entry is missing for a palette color image.");
                    }

                    break;
                }

                default:
                {
                    TiffThrowHelper.ThrowNotSupported($"The specified TIFF photometric interpretation is not supported: {options.PhotometricInterpretation}");
                }

                break;
            }
        }

        private static void ParseCompression(this TiffDecoderCore options, ExifProfile exifProfile)
        {
            TiffCompression compression = exifProfile.GetValue(ExifTag.Compression) != null ? (TiffCompression)exifProfile.GetValue(ExifTag.Compression).Value : TiffCompression.None;
            switch (compression)
            {
                case TiffCompression.None:
                {
                    options.CompressionType = TiffDecoderCompressionType.None;
                    break;
                }

                case TiffCompression.PackBits:
                {
                    options.CompressionType = TiffDecoderCompressionType.PackBits;
                    break;
                }

                case TiffCompression.Deflate:
                case TiffCompression.OldDeflate:
                {
                    options.CompressionType = TiffDecoderCompressionType.Deflate;
                    break;
                }

                case TiffCompression.Lzw:
                {
                    options.CompressionType = TiffDecoderCompressionType.Lzw;
                    break;
                }

                case TiffCompression.CcittGroup3Fax:
                {
                    options.CompressionType = TiffDecoderCompressionType.T4;
                    options.FaxCompressionOptions = exifProfile.GetValue(ExifTag.T4Options) != null ? (FaxCompressionOptions)exifProfile.GetValue(ExifTag.T4Options).Value : FaxCompressionOptions.None;

                    break;
                }

                case TiffCompression.Ccitt1D:
                {
                    options.CompressionType = TiffDecoderCompressionType.HuffmanRle;
                    break;
                }

                default:
                {
                    TiffThrowHelper.ThrowNotSupported($"The specified TIFF compression format {compression} is not supported");
                    break;
                }
            }
        }

        private static TiffBitsPerSample GetBitsPerSample(TiffBitsPerPixel? bitsPerPixel) => bitsPerPixel switch
        {
            TiffBitsPerPixel.Bit1 => TiffBitsPerSample.Bit1,
            TiffBitsPerPixel.Bit4 => TiffBitsPerSample.Bit4,
            TiffBitsPerPixel.Bit8 => TiffBitsPerSample.Bit8,
            TiffBitsPerPixel.Bit24 => TiffBitsPerSample.Bit24,
            _ => TiffBitsPerSample.Bit24,
        };
    }
}
