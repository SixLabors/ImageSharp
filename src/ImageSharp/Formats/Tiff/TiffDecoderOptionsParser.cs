// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
            if (exifProfile.GetValueInternal(ExifTag.TileOffsets) is not null || exifProfile.GetValueInternal(ExifTag.TileByteCounts) is not null)
            {
                TiffThrowHelper.ThrowNotSupported("Tiled images are not supported.");
            }

            IExifValue extraSamplesExifValue = exifProfile.GetValueInternal(ExifTag.ExtraSamples);
            if (extraSamplesExifValue is not null)
            {
                short[] extraSamples = (short[])extraSamplesExifValue.GetValue();
                if (extraSamples.Length != 1)
                {
                    TiffThrowHelper.ThrowNotSupported("ExtraSamples is only supported with one extra sample for alpha data.");
                }

                var extraSamplesType = (TiffExtraSampleType)extraSamples[0];
                options.ExtraSamplesType = extraSamplesType;
                if (extraSamplesType is not (TiffExtraSampleType.UnassociatedAlphaData or TiffExtraSampleType.AssociatedAlphaData))
                {
                    TiffThrowHelper.ThrowNotSupported("Decoding Tiff images with ExtraSamples is not supported with UnspecifiedData.");
                }
            }

            TiffFillOrder fillOrder = (TiffFillOrder?)exifProfile.GetValue(ExifTag.FillOrder)?.Value ?? TiffFillOrder.MostSignificantBitFirst;
            if (fillOrder == TiffFillOrder.LeastSignificantBitFirst && frameMetadata.BitsPerPixel != TiffBitsPerPixel.Bit1)
            {
                TiffThrowHelper.ThrowNotSupported("The lower-order bits of the byte FillOrder is only supported in combination with 1bit per pixel bicolor tiff's.");
            }

            if (frameMetadata.Predictor == TiffPredictor.FloatingPoint)
            {
                TiffThrowHelper.ThrowNotSupported("TIFF images with FloatingPoint horizontal predictor are not supported.");
            }

            TiffSampleFormat[] sampleFormats = exifProfile.GetValue(ExifTag.SampleFormat)?.Value?.Select(a => (TiffSampleFormat)a).ToArray();
            TiffSampleFormat? sampleFormat = null;
            if (sampleFormats != null)
            {
                sampleFormat = sampleFormats[0];
                foreach (TiffSampleFormat format in sampleFormats)
                {
                    if (format is not TiffSampleFormat.UnsignedInteger and not TiffSampleFormat.Float)
                    {
                        TiffThrowHelper.ThrowNotSupported("ImageSharp only supports the UnsignedInteger and Float SampleFormat.");
                    }
                }
            }

            ushort[] ycbcrSubSampling = exifProfile.GetValue(ExifTag.YCbCrSubsampling)?.Value;
            if (ycbcrSubSampling != null && ycbcrSubSampling.Length != 2)
            {
                TiffThrowHelper.ThrowImageFormatException("Invalid YCbCrSubsampling, expected 2 values.");
            }

            if (ycbcrSubSampling != null && ycbcrSubSampling[1] > ycbcrSubSampling[0])
            {
                TiffThrowHelper.ThrowImageFormatException("ChromaSubsampleVert shall always be less than or equal to ChromaSubsampleHoriz.");
            }

            if (exifProfile.GetValue(ExifTag.StripRowCounts)?.Value != null)
            {
                TiffThrowHelper.ThrowNotSupported("Variable-sized strips are not supported.");
            }

            VerifyRequiredFieldsArePresent(exifProfile, frameMetadata);

            options.PlanarConfiguration = (TiffPlanarConfiguration?)exifProfile.GetValue(ExifTag.PlanarConfiguration)?.Value ?? DefaultPlanarConfiguration;
            options.Predictor = frameMetadata.Predictor ?? TiffPredictor.None;
            options.PhotometricInterpretation = frameMetadata.PhotometricInterpretation ?? TiffPhotometricInterpretation.Rgb;
            options.SampleFormat = sampleFormat ?? TiffSampleFormat.UnsignedInteger;
            options.BitsPerPixel = frameMetadata.BitsPerPixel != null ? (int)frameMetadata.BitsPerPixel.Value : (int)TiffBitsPerPixel.Bit24;
            options.BitsPerSample = frameMetadata.BitsPerSample ?? new TiffBitsPerSample(0, 0, 0);
            options.ReferenceBlackAndWhite = exifProfile.GetValue(ExifTag.ReferenceBlackWhite)?.Value;
            options.YcbcrCoefficients = exifProfile.GetValue(ExifTag.YCbCrCoefficients)?.Value;
            options.YcbcrSubSampling = exifProfile.GetValue(ExifTag.YCbCrSubsampling)?.Value;
            options.FillOrder = fillOrder;
            options.JpegTables = exifProfile.GetValue(ExifTag.JPEGTables)?.Value;

            options.ParseColorType(exifProfile);
            options.ParseCompression(frameMetadata.Compression, exifProfile);
        }

        private static void VerifyRequiredFieldsArePresent(ExifProfile exifProfile, TiffFrameMetadata frameMetadata)
        {
            if (exifProfile.GetValueInternal(ExifTag.StripOffsets) is null)
            {
                TiffThrowHelper.ThrowImageFormatException("StripOffsets are missing and are required for decoding the TIFF image!");
            }

            if (exifProfile.GetValueInternal(ExifTag.StripByteCounts) is null)
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
                    if (bitsPerChannel > 32)
                    {
                        TiffThrowHelper.ThrowNotSupported("Bits per sample is not supported.");
                    }

                    switch (bitsPerChannel)
                    {
                        case 32:
                        {
                            if (options.SampleFormat == TiffSampleFormat.Float)
                            {
                                options.ColorType = TiffColorType.WhiteIsZero32Float;
                                return;
                            }

                            options.ColorType = TiffColorType.WhiteIsZero32;
                            break;
                        }

                        case 24:
                        {
                            options.ColorType = TiffColorType.WhiteIsZero24;
                            break;
                        }

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
                    if (bitsPerChannel > 32)
                    {
                        TiffThrowHelper.ThrowNotSupported("Bits per sample is not supported.");
                    }

                    switch (bitsPerChannel)
                    {
                        case 32:
                        {
                            if (options.SampleFormat == TiffSampleFormat.Float)
                            {
                                options.ColorType = TiffColorType.BlackIsZero32Float;
                                return;
                            }

                            options.ColorType = TiffColorType.BlackIsZero32;
                            break;
                        }

                        case 24:
                        {
                            options.ColorType = TiffColorType.BlackIsZero24;
                            break;
                        }

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
                    TiffBitsPerSample bitsPerSample = options.BitsPerSample;
                    if (bitsPerSample.Channels is not (3 or 4))
                    {
                        TiffThrowHelper.ThrowNotSupported("The number of samples in the TIFF BitsPerSample entry is not supported.");
                    }

                    if ((bitsPerSample.Channels == 3 && !(bitsPerSample.Channel0 == bitsPerSample.Channel1 && bitsPerSample.Channel1 == bitsPerSample.Channel2)) ||
                        (bitsPerSample.Channels == 4 && !(bitsPerSample.Channel0 == bitsPerSample.Channel1 && bitsPerSample.Channel1 == bitsPerSample.Channel2 && bitsPerSample.Channel2 == bitsPerSample.Channel3)))
                    {
                        TiffThrowHelper.ThrowNotSupported("Only BitsPerSample with equal bits per channel are supported.");
                    }

                    if (options.PlanarConfiguration == TiffPlanarConfiguration.Chunky)
                    {
                        ushort bitsPerChannel = options.BitsPerSample.Channel0;
                        switch (bitsPerChannel)
                        {
                            case 32:
                                if (options.SampleFormat == TiffSampleFormat.Float)
                                {
                                    options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.RgbFloat323232 : TiffColorType.RgbaFloat32323232;
                                    return;
                                }

                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb323232 : TiffColorType.Rgba32323232;
                                break;

                            case 24:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb242424 : TiffColorType.Rgba24242424;
                                break;

                            case 16:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb161616 : TiffColorType.Rgba16161616;
                                break;

                            case 14:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb141414 : TiffColorType.Rgba14141414;
                                break;

                            case 12:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb121212 : TiffColorType.Rgba12121212;
                                break;

                            case 10:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb101010 : TiffColorType.Rgba10101010;
                                break;

                            case 8:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb888 : TiffColorType.Rgba8888;
                                break;
                            case 6:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb666 : TiffColorType.Rgba6666;
                                break;
                            case 5:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb555 : TiffColorType.Rgba5555;
                                break;
                            case 4:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb444 : TiffColorType.Rgba4444;
                                break;
                            case 3:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb333 : TiffColorType.Rgba3333;
                                break;
                            case 2:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb222 : TiffColorType.Rgba2222;
                                break;
                            default:
                                TiffThrowHelper.ThrowNotSupported("Bits per sample is not supported.");
                                break;
                        }
                    }
                    else
                    {
                        ushort bitsPerChannel = options.BitsPerSample.Channel0;
                        switch (bitsPerChannel)
                        {
                            case 32:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb323232Planar : TiffColorType.Rgba32323232Planar;
                                break;
                            case 24:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb242424Planar : TiffColorType.Rgba24242424Planar;
                                break;
                            case 16:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb161616Planar : TiffColorType.Rgba16161616Planar;
                                break;
                            default:
                                options.ColorType = options.BitsPerSample.Channels is 3 ? TiffColorType.Rgb888Planar : TiffColorType.Rgba8888Planar;
                                break;
                        }
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
                        TiffThrowHelper.ThrowNotSupported("The number of samples in the TIFF BitsPerSample entry is not supported for YCbCr images.");
                    }

                    ushort bitsPerChannel = options.BitsPerSample.Channel0;
                    if (bitsPerChannel != 8)
                    {
                        TiffThrowHelper.ThrowNotSupported("Only 8 bits per channel is supported for YCbCr images.");
                    }

                    options.ColorType = options.PlanarConfiguration == TiffPlanarConfiguration.Chunky ? TiffColorType.YCbCr : TiffColorType.YCbCrPlanar;

                    break;
                }

                case TiffPhotometricInterpretation.CieLab:
                {
                    if (options.BitsPerSample.Channels != 3)
                    {
                        TiffThrowHelper.ThrowNotSupported("The number of samples in the TIFF BitsPerSample entry is not supported for CieLab images.");
                    }

                    ushort bitsPerChannel = options.BitsPerSample.Channel0;
                    if (bitsPerChannel != 8)
                    {
                        TiffThrowHelper.ThrowNotSupported("Only 8 bits per channel is supported for CieLab images.");
                    }

                    options.ColorType = options.PlanarConfiguration == TiffPlanarConfiguration.Chunky ? TiffColorType.CieLab : TiffColorType.CieLabPlanar;

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
            // Default 1 (No compression) https://www.awaresystems.be/imaging/tiff/tifftags/compression.html
            switch (compression ?? TiffCompression.None)
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

                case TiffCompression.CcittGroup4Fax:
                {
                    options.CompressionType = TiffDecoderCompressionType.T6;
                    options.FaxCompressionOptions = exifProfile.GetValue(ExifTag.T4Options) != null ? (FaxCompressionOptions)exifProfile.GetValue(ExifTag.T4Options).Value : FaxCompressionOptions.None;

                    break;
                }

                case TiffCompression.Ccitt1D:
                {
                    options.CompressionType = TiffDecoderCompressionType.HuffmanRle;
                    break;
                }

                case TiffCompression.Jpeg:
                {
                    options.CompressionType = TiffDecoderCompressionType.Jpeg;

                    if (options.PhotometricInterpretation is TiffPhotometricInterpretation.YCbCr && options.JpegTables is null)
                    {
                        // Note: Setting PhotometricInterpretation and color type to RGB here, since the jpeg decoder will handle the conversion of the pixel data.
                        options.PhotometricInterpretation = TiffPhotometricInterpretation.Rgb;
                        options.ColorType = TiffColorType.Rgb;
                    }

                    break;
                }

                case TiffCompression.Webp:
                {
                    options.CompressionType = TiffDecoderCompressionType.Webp;
                    break;
                }

                default:
                {
                    TiffThrowHelper.ThrowNotSupported($"The specified TIFF compression format '{compression}' is not supported");
                    break;
                }
            }
        }
    }
}
