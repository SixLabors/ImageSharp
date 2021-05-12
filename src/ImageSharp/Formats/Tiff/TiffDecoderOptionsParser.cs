// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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
        /// <summary>
        /// Determines the TIFF compression and color types, and reads any associated parameters.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="entries">The IFD entries container to read the image format information for.</param>
        public static void VerifyAndParse(this TiffDecoderCore options, TiffFrameMetadata entries)
        {
            if (entries.TileOffsets != null)
            {
                TiffThrowHelper.ThrowNotSupported("Tiled images are not supported.");
            }

            if (entries.ExtraSamples != null)
            {
                TiffThrowHelper.ThrowNotSupported("ExtraSamples is not supported.");
            }

            if (entries.FillOrder != TiffFillOrder.MostSignificantBitFirst)
            {
                TiffThrowHelper.ThrowNotSupported("The lower-order bits of the byte FillOrder is not supported.");
            }

            if (entries.Predictor == TiffPredictor.FloatingPoint)
            {
                TiffThrowHelper.ThrowNotSupported("TIFF images with FloatingPoint horizontal predictor are not supported.");
            }

            if (entries.SampleFormat != null)
            {
                foreach (TiffSampleFormat format in entries.SampleFormat)
                {
                    if (format != TiffSampleFormat.UnsignedInteger)
                    {
                        TiffThrowHelper.ThrowNotSupported("ImageSharp only supports the UnsignedInteger SampleFormat.");
                    }
                }
            }

            if (entries.StripRowCounts != null)
            {
                TiffThrowHelper.ThrowNotSupported("Variable-sized strips are not supported.");
            }

            entries.VerifyRequiredFieldsArePresent();

            options.PlanarConfiguration = entries.PlanarConfiguration;
            options.Predictor = entries.Predictor;
            options.PhotometricInterpretation = entries.PhotometricInterpretation;
            options.BitsPerSample = entries.BitsPerSample.GetValueOrDefault();
            options.BitsPerPixel = entries.BitsPerSample.GetValueOrDefault().BitsPerPixel();

            ParseColorType(options, entries);
            ParseCompression(options, entries);
        }

        private static void ParseColorType(this TiffDecoderCore options, TiffFrameMetadata entries)
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
                    options.ColorMap = entries.ColorMap;
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

        private static void ParseCompression(this TiffDecoderCore options, TiffFrameMetadata entries)
        {
            TiffCompression compression = entries.Compression;
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
                    IExifValue t4options = entries.ExifProfile.GetValue(ExifTag.T4Options);
                    if (t4options != null)
                    {
                        var t4OptionValue = (FaxCompressionOptions)t4options.GetValue();
                        options.FaxCompressionOptions = t4OptionValue;
                    }

                    break;
                }

                case TiffCompression.Ccitt1D:
                {
                    options.CompressionType = TiffDecoderCompressionType.HuffmanRle;
                    break;
                }

                default:
                {
                    TiffThrowHelper.ThrowNotSupported("The specified TIFF compression format is not supported: " + compression);
                    break;
                }
            }
        }
    }
}
