// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Tiff;

/// <summary>
/// The decoder options parser.
/// </summary>
internal static class TiffDecoderOptionsParser
{
    private const TiffPlanarConfiguration DefaultPlanarConfiguration = TiffPlanarConfiguration.Chunky;

    /// <summary>
    /// Finds the TIFF compression and the color type, and reads their parameters. The method owns the checks of an IFD that occur before the decoder allocates
    /// its frame. The method returns a status for each check that fails. Thus the caller applies one integrity rule to each IFD.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="exifProfile">The exif profile of the frame to decode.</param>
    /// <param name="frameMetadata">The IFD entries container to read the image format information for current frame.</param>
    /// <param name="rootSize">The size of the first frame, or <see langword="null"/> for the first IFD.</param>
    /// <param name="isTiled">The method sets this value to <see langword="true"/> if the image uses tiles, and to <see langword="false"/> if the image uses
    /// strips.</param>
    /// <param name="message">The message for a status other than <see cref="TiffDirectoryStatus.Valid"/>, or <see langword="null"/> for
    /// <see cref="TiffDirectoryStatus.Valid"/>.</param>
    /// <returns>The status of the IFD.</returns>
    public static TiffDirectoryStatus VerifyAndParse(
        this TiffDecoderCore options,
        ExifProfile exifProfile,
        TiffFrameMetadata frameMetadata,
        Size? rootSize,
        out bool isTiled,
        out string message)
    {
        isTiled = false;

        // DecodeFrame reuses this core across IFDs. Reset optional coding tags before reading the next frame,
        // so absent tables, YCbCr settings or alpha cannot inherit the preceding frame's interpretation.
        options.JpegTables = null;
        options.ReferenceBlackAndWhite = null;
        options.YcbcrCoefficients = null;
        options.OldJpegCompressionStartOfImageMarker = null;

        // TiffFrameMetadata.Parse reads the first ExtraSamples value. It keeps only an alpha sample. It maps the CorelDRAW value to unassociated alpha, as
        // libtiff does.
        options.ExtraSamplesType = frameMetadata.ExtraSampleType;

        TiffFillOrder fillOrder;
        if (exifProfile.TryGetValue(ExifTag.FillOrder, out IExifValue<ushort> value))
        {
            fillOrder = (TiffFillOrder)value.Value;
        }
        else
        {
            fillOrder = TiffFillOrder.MostSignificantBitFirst;
        }

        if (fillOrder == TiffFillOrder.LeastSignificantBitFirst && frameMetadata.BitsPerPixel != TiffBitsPerPixel.Bit1)
        {
            return Unsupported("The lower-order bits of the byte FillOrder is only supported in combination with 1bit per pixel bicolor tiff's.", out message);
        }

        if (frameMetadata.Predictor == TiffPredictor.FloatingPoint)
        {
            return Unsupported("TIFF images with FloatingPoint horizontal predictor are not supported.", out message);
        }

        TiffSampleFormat? sampleFormat = null;
        if (exifProfile.TryGetValue(ExifTag.SampleFormat, out IExifValue<ushort[]> formatValue))
        {
            TiffSampleFormat[] sampleFormats = [.. formatValue.Value.Select(a => (TiffSampleFormat)a)];
            sampleFormat = sampleFormats[0];
            foreach (TiffSampleFormat format in sampleFormats)
            {
                if (format is not TiffSampleFormat.UnsignedInteger and not TiffSampleFormat.Float)
                {
                    return Unsupported("ImageSharp only supports the UnsignedInteger and Float SampleFormat.", out message);
                }
            }
        }

        ushort[] ycbcrSubSampling = null;
        if (exifProfile.TryGetValue(ExifTag.YCbCrSubsampling, out IExifValue<ushort[]> subSamplingValue))
        {
            ycbcrSubSampling = subSamplingValue.Value;
        }

        if (ycbcrSubSampling != null && ycbcrSubSampling.Length != 2)
        {
            return Damaged("Invalid YCbCrSubsampling, expected 2 values.", out message);
        }

        if (ycbcrSubSampling != null && ycbcrSubSampling[1] > ycbcrSubSampling[0])
        {
            return Damaged("ChromaSubsampleVert shall always be less than or equal to ChromaSubsampleHoriz.", out message);
        }

        if (exifProfile.TryGetValue(ExifTag.StripRowCounts, out _))
        {
            return Unsupported("Variable-sized strips are not supported.", out message);
        }

        // "If SamplesPerPixel is 1, PlanarConfiguration is irrelevant, and need not be included." See TIFF 6.0 specification, section 8, PlanarConfiguration,
        // page 38. The two layouts store one sample in the same way. Thus the decoder reads a one-sample image as chunky.
        if (exifProfile.TryGetValue(ExifTag.PlanarConfiguration, out IExifValue<ushort> planarValue) && frameMetadata.BitsPerSample.Channels > 1)
        {
            options.PlanarConfiguration = (TiffPlanarConfiguration)planarValue.Value;
        }
        else
        {
            options.PlanarConfiguration = DefaultPlanarConfiguration;
        }

        options.Predictor = frameMetadata.Predictor;
        options.PhotometricInterpretation = frameMetadata.PhotometricInterpretation;
        options.SampleFormat = sampleFormat ?? TiffSampleFormat.UnsignedInteger;
        frameMetadata.SampleFormat = options.SampleFormat;

        // The typed frame metadata now owns this value. Remove the raw IFD tag so
        // re-encoding cannot retain a stale SampleFormat when encoder options change it.
        exifProfile.RemoveValue(ExifTag.SampleFormat);
        options.BitsPerPixel = (int)frameMetadata.BitsPerPixel;
        options.BitsPerSample = frameMetadata.BitsPerSample;

        if (exifProfile.TryGetValue(ExifTag.ReferenceBlackWhite, out IExifValue<Rational[]> blackWhiteValue))
        {
            options.ReferenceBlackAndWhite = blackWhiteValue.Value;
        }

        if (exifProfile.TryGetValue(ExifTag.YCbCrCoefficients, out IExifValue<Rational[]> coefficientsValue))
        {
            options.YcbcrCoefficients = coefficientsValue.Value;
        }

        options.YcbcrSubSampling = ycbcrSubSampling;

        options.FillOrder = fillOrder;

        if (exifProfile.TryGetValue(ExifTag.JPEGTables, out IExifValue<byte[]> jpegTablesValue))
        {
            options.JpegTables = jpegTablesValue.Value;
        }

        if (exifProfile.TryGetValue(ExifTag.JPEGInterchangeFormat, out IExifValue<uint> jpegInterchangeFormatValue))
        {
            options.OldJpegCompressionStartOfImageMarker = jpegInterchangeFormatValue.Value;
        }

        TiffDirectoryStatus status = options.ParseCompression(frameMetadata.Compression, exifProfile, out message);
        if (status != TiffDirectoryStatus.Valid)
        {
            return status;
        }

        status = options.ParseColorType(exifProfile, out message);
        if (status != TiffDirectoryStatus.Valid)
        {
            return status;
        }

        if (options.CompressionType is TiffDecoderCompressionType.Jpeg or TiffDecoderCompressionType.OldJpeg)
        {
            // Nested JPEG decoding writes TPixel directly and bypasses the raw TIFF sample readers.
            // Keep these byte-layout fields for the TIFF strip setup. PhotometricInterpretation retains the encoded color model;
            // the JPEG decoder handles its subsampling and final pixel conversion.
            options.ColorType = TiffColorType.Rgb888;
            options.BitsPerSample = new TiffBitsPerSample(8, 8, 8);
            options.BitsPerPixel = 24;
        }

        // A frame must have pixels. The check occurs before the frame allocation. The frame allocation requires a positive size.
        if (frameMetadata.EncodingWidth <= 0 || frameMetadata.EncodingHeight <= 0)
        {
            return Damaged("The TIFF image frame width and height must be greater than zero.", out message);
        }

        // The frames after the first frame use the size of the first frame.
        if (rootSize.HasValue && (rootSize.Value.Width < frameMetadata.EncodingWidth || rootSize.Value.Height < frameMetadata.EncodingHeight))
        {
            return Unsupported("Images with frames of size greater than the root frame are not supported.", out message);
        }

        status = options.VerifyRequiredFieldsArePresent(exifProfile, frameMetadata.EncodingHeight, out isTiled, out message);
        if (status != TiffDirectoryStatus.Valid)
        {
            return status;
        }

        return options.VerifySampleBuffers(frameMetadata.EncodingWidth, isTiled, out message);
    }

    /// <summary>
    /// Makes sure that all the fields that the decoder requires are present, and that the strip or tile size is valid. The method stores the tile size or
    /// the row count of a strip in the options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="exifProfile">The exif profile.</param>
    /// <param name="height">The height of the frame.</param>
    /// <param name="isTiled">The method sets this value to <see langword="true"/> if the image uses tiles, and to <see langword="false"/> if the image uses
    /// strips.</param>
    /// <param name="message">The message for a status other than <see cref="TiffDirectoryStatus.Valid"/>, or <see langword="null"/> for
    /// <see cref="TiffDirectoryStatus.Valid"/>.</param>
    /// <returns>The status of the fields.</returns>
    private static TiffDirectoryStatus VerifyRequiredFieldsArePresent(
        this TiffDecoderCore options,
        ExifProfile exifProfile,
        int height,
        out bool isTiled,
        out string message)
    {
        isTiled = false;
        if (exifProfile.GetValueInternal(ExifTag.TileWidth) is not null || exifProfile.GetValueInternal(ExifTag.TileLength) is not null)
        {
            if (exifProfile.GetValueInternal(ExifTag.TileOffsets) is not null)
            {
                if (exifProfile.GetValueInternal(ExifTag.TileByteCounts) is null)
                {
                    return Damaged("TileByteCounts are missing and are required for decoding the TIFF image!", out message);
                }
            }
            else if (options.PlanarConfiguration == TiffPlanarConfiguration.Planar || exifProfile.GetValueInternal(ExifTag.StripOffsets) is null)
            {
                return Damaged("TileOffsets are missing and are required for decoding the TIFF image!", out message);
            }
            else if (exifProfile.GetValueInternal(ExifTag.StripByteCounts) is null)
            {
                // A chunky tiled image can store its tile layout in the strip tags. libtiff also reads this layout. Then the strip byte counts are the tile
                // byte counts.
                return Damaged("StripByteCounts are missing and are required for decoding the TIFF image!", out message);
            }

            // A tag with a type that is not a number is not valid.
            if (!exifProfile.TryGetValue(ExifTag.TileWidth, out IExifValue<Number> tileWidth))
            {
                return Damaged("TileWidth are missing and are required for decoding the TIFF image!", out message);
            }

            if (!exifProfile.TryGetValue(ExifTag.TileLength, out IExifValue<Number> tileLength))
            {
                return Damaged("TileLength are missing and are required for decoding the TIFF image!", out message);
            }

            options.TileWidth = (int)tileWidth.Value;
            options.TileLength = (int)tileLength.Value;
            if (options.TileWidth <= 0 || options.TileLength <= 0)
            {
                return Damaged("TIFF tile dimensions must be positive.", out message);
            }

            isTiled = true;
            message = null;
            return TiffDirectoryStatus.Valid;
        }

        if (exifProfile.GetValueInternal(ExifTag.StripOffsets) is null)
        {
            return Damaged("StripOffsets are missing and are required for decoding the TIFF image!", out message);
        }

        if (exifProfile.GetValueInternal(ExifTag.StripByteCounts) is null)
        {
            return Damaged("StripByteCounts are missing and are required for decoding the TIFF image!", out message);
        }

        // The default RowsPerStrip value in TIFF 6.0 is 2^32 - 1 (page 39). A value that is equal to or more than the height describes one strip. Both layouts
        // allocate memory for the rows that exist, not for the larger declared strip height.
        options.RowsPerStrip = height;
        if (exifProfile.TryGetValue(ExifTag.RowsPerStrip, out IExifValue<Number> rowsPerStrip))
        {
            // TIFF 6.0 requires one or more rows in each strip. A zero value cannot define row offsets or a strip count.
            if ((uint)rowsPerStrip.Value == 0)
            {
                return Damaged("RowsPerStrip must be greater than zero.", out message);
            }

            options.RowsPerStrip = Math.Min((int)rowsPerStrip.Value, height);
        }

        message = null;
        return TiffDirectoryStatus.Valid;
    }

    /// <summary>
    /// Selects the color type from the photometric interpretation and the samples.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="exifProfile">The exif profile of the frame to decode.</param>
    /// <param name="message">The message for a status other than <see cref="TiffDirectoryStatus.Valid"/>, or <see langword="null"/> for
    /// <see cref="TiffDirectoryStatus.Valid"/>.</param>
    /// <returns>The status of the color type.</returns>
    private static TiffDirectoryStatus ParseColorType(this TiffDecoderCore options, ExifProfile exifProfile, out string message)
    {
        switch (options.PhotometricInterpretation)
        {
            case TiffPhotometricInterpretation.WhiteIsZero:
            {
                if (options.BitsPerSample.Channels != 1)
                {
                    return Unsupported("The number of samples in the TIFF BitsPerSample entry is not supported.", out message);
                }

                ushort bitsPerChannel = options.BitsPerSample.Channel0;
                if (bitsPerChannel > 32)
                {
                    return Unsupported("Bits per sample is not supported.", out message);
                }

                switch (bitsPerChannel)
                {
                    case 32:
                        if (options.SampleFormat == TiffSampleFormat.Float)
                        {
                            options.ColorType = TiffColorType.WhiteIsZero32Float;
                            break;
                        }

                        options.ColorType = TiffColorType.WhiteIsZero32;
                        break;

                    case 24:
                        options.ColorType = TiffColorType.WhiteIsZero24;
                        break;

                    case 16:
                        options.ColorType = TiffColorType.WhiteIsZero16;
                        break;

                    case 8:
                        options.ColorType = TiffColorType.WhiteIsZero8;
                        break;

                    case 4:
                        options.ColorType = TiffColorType.WhiteIsZero4;
                        break;

                    case 1:
                        options.ColorType = TiffColorType.WhiteIsZero1;
                        break;

                    default:
                        options.ColorType = TiffColorType.WhiteIsZero;
                        break;
                }

                break;
            }

            case TiffPhotometricInterpretation.BlackIsZero:
            {
                if (options.BitsPerSample.Channels != 1)
                {
                    return Unsupported("The number of samples in the TIFF BitsPerSample entry is not supported.", out message);
                }

                ushort bitsPerChannel = options.BitsPerSample.Channel0;
                if (bitsPerChannel > 32)
                {
                    return Unsupported("Bits per sample is not supported.", out message);
                }

                switch (bitsPerChannel)
                {
                    case 32:
                        if (options.SampleFormat == TiffSampleFormat.Float)
                        {
                            options.ColorType = TiffColorType.BlackIsZero32Float;
                            break;
                        }

                        options.ColorType = TiffColorType.BlackIsZero32;
                        break;

                    case 24:
                        options.ColorType = TiffColorType.BlackIsZero24;
                        break;

                    case 16:
                        options.ColorType = TiffColorType.BlackIsZero16;
                        break;

                    case 8:
                        options.ColorType = TiffColorType.BlackIsZero8;
                        break;

                    case 4:
                        options.ColorType = TiffColorType.BlackIsZero4;
                        break;

                    case 1:
                        options.ColorType = TiffColorType.BlackIsZero1;
                        break;

                    default:
                        options.ColorType = TiffColorType.BlackIsZero;
                        break;
                }

                break;
            }

            case TiffPhotometricInterpretation.Rgb:
            {
                TiffBitsPerSample bitsPerSample = options.BitsPerSample;
                if (bitsPerSample.Channels is not (3 or 4))
                {
                    return Unsupported("The number of samples in the TIFF BitsPerSample entry is not supported.", out message);
                }

                if ((bitsPerSample.Channels == 3 && !(bitsPerSample.Channel0 == bitsPerSample.Channel1 && bitsPerSample.Channel1 == bitsPerSample.Channel2)) ||
                    (bitsPerSample.Channels == 4 && !(bitsPerSample.Channel0 == bitsPerSample.Channel1 && bitsPerSample.Channel1 == bitsPerSample.Channel2
                        && bitsPerSample.Channel2 == bitsPerSample.Channel3)))
                {
                    return Unsupported("Only BitsPerSample with equal bits per channel are supported.", out message);
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
                                break;
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
                            return Unsupported("Bits per sample is not supported.", out message);
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
                if (!exifProfile.TryGetValue(ExifTag.ColorMap, out IExifValue<ushort[]> colorMap))
                {
                    return Unsupported("The TIFF ColorMap entry is missing for a palette color image.", out message);
                }

                options.ColorMap = colorMap.Value;
                if (options.BitsPerSample.Channels is not 1 and not 2)
                {
                    return Unsupported("The number of samples in the TIFF BitsPerSample entry is not supported.", out message);
                }

                options.ColorType = TiffColorType.PaletteColor;
                break;
            }

            case TiffPhotometricInterpretation.YCbCr:
            {
                if (exifProfile.TryGetValue(ExifTag.ColorMap, out IExifValue<ushort[]> value))
                {
                    options.ColorMap = value.Value;
                }

                if (options.BitsPerSample.Channels != 3)
                {
                    return Unsupported("The number of samples in the TIFF BitsPerSample entry is not supported for YCbCr images.", out message);
                }

                ushort bitsPerChannel = options.BitsPerSample.Channel0;
                if (bitsPerChannel != 8)
                {
                    return Unsupported("Only 8 bits per channel is supported for YCbCr images.", out message);
                }

                // The TIFF YCbCr readers use 6 reference values and 3 luma coefficients. See TIFF 6.0 specification, section 21, YCbCrCoefficients and
                // ReferenceBlackWhite, pages 90-94. The nested JPEG decoder does not read these tags. Thus the parser ignores these tags for JPEG data.
                if (options.CompressionType is not (TiffDecoderCompressionType.Jpeg or TiffDecoderCompressionType.OldJpeg))
                {
                    // The TIFF YCbCr readers divide the image width and height by the subsampling factors. TIFF 6.0 allows only the values 1, 2 and 4 (section
                    // 21, YCbCrSubSampling, page 92). Thus a zero factor is not valid.
                    if (options.YcbcrSubSampling != null && (options.YcbcrSubSampling[0] == 0 || options.YcbcrSubSampling[1] == 0))
                    {
                        return Damaged("YCbCrSubsampling factors must be greater than zero.", out message);
                    }

                    if (options.ReferenceBlackAndWhite != null && options.ReferenceBlackAndWhite.Length != 6)
                    {
                        return Damaged("reference black and white array should have 6 entry's", out message);
                    }

                    if (options.YcbcrCoefficients != null && options.YcbcrCoefficients.Length != 3)
                    {
                        return Damaged("luma coefficients array should have 3 entries", out message);
                    }
                }

                options.ColorType = options.PlanarConfiguration == TiffPlanarConfiguration.Chunky ? TiffColorType.YCbCr : TiffColorType.YCbCrPlanar;

                break;
            }

            case TiffPhotometricInterpretation.CieLab:
            {
                if (options.BitsPerSample.Channels != 3)
                {
                    return Unsupported("The number of samples in the TIFF BitsPerSample entry is not supported for CieLab images.", out message);
                }

                ushort bits = options.BitsPerSample.Channel0;
                if (bits is not (8 or 16) || options.BitsPerSample.Channel1 != bits || options.BitsPerSample.Channel2 != bits)
                {
                    return Unsupported("CIE Lab requires three equally sized eight-bit or sixteen-bit samples.", out message);
                }

                // Prediction operates on stored words, including signed chroma bits. Retain the depth for decompression.
                options.ColorType = options.PlanarConfiguration == TiffPlanarConfiguration.Chunky
                    ? bits == 8 ? TiffColorType.CieLab : TiffColorType.CieLab161616
                    : bits == 8 ? TiffColorType.CieLabPlanar : TiffColorType.CieLab161616Planar;

                break;
            }

            case TiffPhotometricInterpretation.Separated:
            {
                if (options.BitsPerSample.Channels != 4)
                {
                    return Unsupported("The number of samples in the TIFF BitsPerSample entry is not supported for CMYK images.", out message);
                }

                ushort bitsPerChannel = options.BitsPerSample.Channel0;
                if (bitsPerChannel is not (8 or 16)
                    || options.BitsPerSample.Channel1 != bitsPerChannel
                    || options.BitsPerSample.Channel2 != bitsPerChannel
                    || options.BitsPerSample.Channel3 != bitsPerChannel)
                {
                    return Unsupported("CMYK requires four equally sized 8-bit or 16-bit samples.", out message);
                }

                if (exifProfile.GetValueInternal(ExifTag.InkNames) is not null)
                {
                    return Unsupported("The custom ink name strings are not supported for CMYK images.", out message);
                }

                // Horizontal prediction restores each sample at its stored width. CMYK16 must not use byte-wise addition.
                options.ColorType = bitsPerChannel == 16 ? TiffColorType.Cmyk16161616 : TiffColorType.Cmyk;
                break;
            }

            default:
                return Unsupported($"The specified TIFF photometric interpretation is not supported: {options.PhotometricInterpretation}", out message);
        }

        message = null;
        return TiffDirectoryStatus.Valid;
    }

    /// <summary>
    /// Reads the compression of the IFD and the parameters of that compression.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="compression">The compression of the IFD.</param>
    /// <param name="exifProfile">The exif profile of the frame to decode.</param>
    /// <param name="message">The message for a status other than <see cref="TiffDirectoryStatus.Valid"/>, or <see langword="null"/> for
    /// <see cref="TiffDirectoryStatus.Valid"/>.</param>
    /// <returns>The status of the compression.</returns>
    private static TiffDirectoryStatus ParseCompression(this TiffDecoderCore options, TiffCompression? compression, ExifProfile exifProfile, out string message)
    {
        // Default 1 (No compression) https://www.awaresystems.be/imaging/tiff/tifftags/compression.html
        switch (compression ?? TiffCompression.None)
        {
            case TiffCompression.None:
                options.CompressionType = TiffDecoderCompressionType.None;
                break;

            case TiffCompression.PackBits:
                options.CompressionType = TiffDecoderCompressionType.PackBits;
                break;

            case TiffCompression.Deflate:
            case TiffCompression.OldDeflate:
                options.CompressionType = TiffDecoderCompressionType.Deflate;
                break;

            case TiffCompression.Lzw:
                options.CompressionType = TiffDecoderCompressionType.Lzw;
                break;

            case TiffCompression.CcittGroup3Fax:
            {
                options.CompressionType = TiffDecoderCompressionType.T4;

                // T4Options bit 0 selects 2D coding. See TIFF 6.0 specification, section 11, T4Options, page 51.
                if (exifProfile.TryGetValue(ExifTag.T4Options, out IExifValue<uint> t4OptionsValue)
                    && ((FaxCompressionOptions)t4OptionsValue.Value).HasFlag(FaxCompressionOptions.TwoDimensionalCoding))
                {
                    return Unsupported("TIFF CCITT 2D compression is not yet supported", out message);
                }

                // Some encoders do not set the BitsPerSample correctly, so we set those values here to the required values:
                // https://github.com/SixLabors/ImageSharp/issues/2587
                options.BitsPerSample = new TiffBitsPerSample(1, 0, 0);
                options.BitsPerPixel = 1;

                break;
            }

            case TiffCompression.CcittGroup4Fax:
            {
                options.CompressionType = TiffDecoderCompressionType.T6;
                options.BitsPerSample = new TiffBitsPerSample(1, 0, 0);
                options.BitsPerPixel = 1;

                break;
            }

            case TiffCompression.Ccitt1D:
                options.CompressionType = TiffDecoderCompressionType.HuffmanRle;
                options.BitsPerSample = new TiffBitsPerSample(1, 0, 0);
                options.BitsPerPixel = 1;

                break;

            case TiffCompression.OldJpeg:
                if (!options.OldJpegCompressionStartOfImageMarker.HasValue)
                {
                    return Unsupported("Missing SOI marker offset for tiff with old jpeg compression", out message);
                }

                if (options.PlanarConfiguration is TiffPlanarConfiguration.Planar)
                {
                    // The nested decoder writes complete pixels; the planar path requires separate raw channel samples.
                    return Unsupported("Old Jpeg compression is not supported with planar configuration", out message);
                }

                options.CompressionType = TiffDecoderCompressionType.OldJpeg;
                break;

            case TiffCompression.Jpeg:
                if (options.PlanarConfiguration is TiffPlanarConfiguration.Planar)
                {
                    // The nested decoder writes complete pixels; the planar path requires separate raw channel samples.
                    return Unsupported("Jpeg compression is not supported with planar configuration", out message);
                }

                options.CompressionType = TiffDecoderCompressionType.Jpeg;

                break;

            case TiffCompression.Webp:
                if (options.PlanarConfiguration is TiffPlanarConfiguration.Planar)
                {
                    // Reject planar layout here. The libtiff WebP codec also requires contiguous RGB or RGBA data.
                    return Unsupported("WebP compression is not supported with planar configuration", out message);
                }

                options.CompressionType = TiffDecoderCompressionType.Webp;
                break;

            default:
                return Unsupported($"The specified TIFF compression format '{compression}' is not supported", out message);
        }

        // The JPEG and old JPEG decompressors decode only these color spaces.
        if (options.CompressionType is TiffDecoderCompressionType.Jpeg or TiffDecoderCompressionType.OldJpeg
            && options.PhotometricInterpretation is not (TiffPhotometricInterpretation.BlackIsZero
                or TiffPhotometricInterpretation.WhiteIsZero
                or TiffPhotometricInterpretation.YCbCr
                or TiffPhotometricInterpretation.Rgb
                or TiffPhotometricInterpretation.Separated))
        {
            return Unsupported($"Jpeg compressed tiff with photometric interpretation {options.PhotometricInterpretation} is not supported", out message);
        }

        message = null;
        return TiffDirectoryStatus.Valid;
    }

    /// <summary>
    /// Sets the message of an IFD that is not valid TIFF data.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <param name="message">The message to set.</param>
    /// <returns><see cref="TiffDirectoryStatus.Damaged"/>.</returns>
    private static TiffDirectoryStatus Damaged(string text, out string message)
    {
        message = text;
        return TiffDirectoryStatus.Damaged;
    }

    /// <summary>
    /// Sets the message of an IFD that uses a feature that the decoder does not support.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <param name="message">The message to set.</param>
    /// <returns><see cref="TiffDirectoryStatus.Unsupported"/>.</returns>
    private static TiffDirectoryStatus Unsupported(string text, out string message)
    {
        message = text;
        return TiffDirectoryStatus.Unsupported;
    }
}
