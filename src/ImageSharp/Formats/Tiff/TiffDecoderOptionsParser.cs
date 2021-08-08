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
        private const TiffPlanarConfiguration DefaultPlanarConfiguration = TiffPlanarConfiguration.Chunky;

        /// <summary>
        /// Determines the TIFF compression and color types, and reads any associated parameters.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="exifProfile">The exif profile of the frame to decode.</param>
        /// <param name="frameMetadata">The IFD entries container to read the image format information for current frame.</param>
        public static void VerifyAndParse(this TiffDecoderCore options, ExifProfile exifProfile, TiffFrameMetadata frameMetadata)
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

            if (frameMetadata.Predictor == TiffPredictor.FloatingPoint)
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

            ushort[] ycbcrSubSampling = exifProfile.GetValue(ExifTag.YCbCrSubsampling)?.Value;
            if (ycbcrSubSampling != null && ycbcrSubSampling[0] != ycbcrSubSampling[1])
            {
                TiffThrowHelper.ThrowNotSupported("ImageSharp only supports YCbCr images with equal luma and chroma samples.");
            }

            if (exifProfile.GetValue(ExifTag.StripRowCounts)?.Value != null)
            {
                TiffThrowHelper.ThrowNotSupported("Variable-sized strips are not supported.");
            }

            VerifyRequiredFieldsArePresent(exifProfile, frameMetadata);

            options.PlanarConfiguration = (TiffPlanarConfiguration?)exifProfile.GetValue(ExifTag.PlanarConfiguration)?.Value ?? DefaultPlanarConfiguration;
            options.Predictor = frameMetadata.Predictor ?? TiffPredictor.None;
            options.PhotometricInterpretation = frameMetadata.PhotometricInterpretation ?? TiffPhotometricInterpretation.Rgb;
            options.BitsPerPixel = frameMetadata.BitsPerPixel != null ? (int)frameMetadata.BitsPerPixel.Value : (int)TiffBitsPerPixel.Bit24;
            options.BitsPerSample = frameMetadata.BitsPerSample ?? new TiffBitsPerSample(0, 0, 0);
            options.ReferenceBlackAndWhite = exifProfile.GetValue(ExifTag.ReferenceBlackWhite)?.Value;
            options.YcbcrCoefficients = exifProfile.GetValue(ExifTag.YCbCrCoefficients)?.Value;

            options.ParseColorType(exifProfile);
            options.ParseCompression(frameMetadata.Compression, exifProfile);
        }

        private static void VerifyRequiredFieldsArePresent(ExifProfile exifProfile, TiffFrameMetadata frameMetadata)
        {
            if (exifProfile.GetValue(ExifTag.StripOffsets) == null)
            {
                TiffThrowHelper.ThrowImageFormatException("StripOffsets are missing and are required for decoding the TIFF image!");
            }

            if (exifProfile.GetValue(ExifTag.StripByteCounts) == null)
            {
                TiffThrowHelper.ThrowImageFormatException("StripByteCounts are missing and are required for decoding the TIFF image!");
            }

            if (frameMetadata.BitsPerPixel == null)
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
                    if (options.BitsPerSample.Channels != 1)
                    {
                        TiffThrowHelper.ThrowNotSupported("The number of samples in the TIFF BitsPerSample entry is not supported.");
                    }

                    ushort bitsPerChannel = options.BitsPerSample.Channel0;
                    if (bitsPerChannel > 16)
                    {
                        TiffThrowHelper.ThrowNotSupported("Bits per sample is not supported.");
                    }

                    switch (bitsPerChannel)
                    {
                        case 16:
                        {
                            options.ColorType = TiffColorType.WhiteIsZero16;
                            break;
                        }

                        case 8:
                        {
                            options.ColorType = TiffColorType.WhiteIsZero8;
                            break;
                        }

                        case 4:
                        {
                            options.ColorType = TiffColorType.WhiteIsZero4;
                            break;
                        }

                        case 1:
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
                    if (options.BitsPerSample.Channels != 1)
                    {
                        TiffThrowHelper.ThrowNotSupported("The number of samples in the TIFF BitsPerSample entry is not supported.");
                    }

                    ushort bitsPerChannel = options.BitsPerSample.Channel0;
                    if (bitsPerChannel > 16)
                    {
                        TiffThrowHelper.ThrowNotSupported("Bits per sample is not supported.");
                    }

                    switch (bitsPerChannel)
                    {
                        case 16:
                        {
                            options.ColorType = TiffColorType.BlackIsZero16;
                            break;
                        }

                        case 8:
                        {
                            options.ColorType = TiffColorType.BlackIsZero8;
                            break;
                        }

                        case 4:
                        {
                            options.ColorType = TiffColorType.BlackIsZero4;
                            break;
                        }

                        case 1:
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
                    if (options.BitsPerSample.Channels != 3)
                    {
                        TiffThrowHelper.ThrowNotSupported("The number of samples in the TIFF BitsPerSample entry is not supported.");
                    }

                    if (options.PlanarConfiguration == TiffPlanarConfiguration.Chunky)
                    {
                        ushort bitsPerChannel = options.BitsPerSample.Channel0;
                        switch (bitsPerChannel)
                        {
                            case 16:
                                options.ColorType = TiffColorType.Rgb161616;
                                break;

                            case 14:
                                options.ColorType = TiffColorType.Rgb141414;
                                break;

                            case 12:
                                options.ColorType = TiffColorType.Rgb121212;
                                break;

                            case 10:
                                options.ColorType = TiffColorType.Rgb101010;
                                break;

                            case 8:
                                options.ColorType = TiffColorType.Rgb888;
                                break;
                            case 4:
                                options.ColorType = TiffColorType.Rgb444;
                                break;
                            case 2:
                                options.ColorType = TiffColorType.Rgb222;
                                break;
                            default:
                                TiffThrowHelper.ThrowNotSupported("Bits per sample is not supported.");
                                break;
                        }
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
                        if (options.BitsPerSample.Channels != 1)
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

                case TiffPhotometricInterpretation.YCbCr:
                {
                    options.ColorMap = exifProfile.GetValue(ExifTag.ColorMap)?.Value;
                    if (options.BitsPerSample.Channels != 3)
                    {
                        TiffThrowHelper.ThrowNotSupported("The number of samples in the TIFF BitsPerSample entry is not supported.");
                    }

                    ushort bitsPerChannel = options.BitsPerSample.Channel0;
                    if (bitsPerChannel != 8)
                    {
                        TiffThrowHelper.ThrowNotSupported("Only 8 bits per channel is supported for YCbCr images.");
                    }

                    options.ColorType = options.PlanarConfiguration == TiffPlanarConfiguration.Chunky ? TiffColorType.YCbCr : TiffColorType.YCbCrPlanar;

                    break;
                }

                default:
                {
                    TiffThrowHelper.ThrowNotSupported($"The specified TIFF photometric interpretation is not supported: {options.PhotometricInterpretation}");
                }

                break;
            }
        }

        private static void ParseCompression(this TiffDecoderCore options, TiffCompression? compression, ExifProfile exifProfile)
        {
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
    }
}
