// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// The decoder helper methods.
    /// </summary>
    internal static class TiffDecoderHelpers
    {
        /// <summary>
        /// Reads the image metadata from a specified IFD.
        /// </summary>
        /// <param name="metaData">The image metadata to write the metadata to.</param>
        /// <param name="entries">The entries.</param>
        /// <param name="ignoreMetadata">if set to <c>true</c> [ignore metadata].</param>
        public static void FillMetadata(ImageMetadata metaData, TiffIfdEntriesContainer entries, bool ignoreMetadata)
        {
            TiffResolutionUnit resolutionUnit = entries.ResolutionUnit;

            if (resolutionUnit != TiffResolutionUnit.None)
            {
                double resolutionUnitFactor = resolutionUnit == TiffResolutionUnit.Centimeter ? 2.54 : 1.0;

                if (entries.TryGetSingleValue(TiffTagId.XResolution, out Rational xResolution))
                {
                    metaData.HorizontalResolution = xResolution.ToDouble() * resolutionUnitFactor;
                }

                if (entries.TryGetSingleValue(TiffTagId.YResolution, out Rational yResolution))
                {
                    metaData.VerticalResolution = yResolution.ToDouble() * resolutionUnitFactor;
                }
            }

            if (!ignoreMetadata)
            {
                TiffMetaData tiffMetadata = metaData.GetFormatMetadata(TiffFormat.Instance);
                foreach (var tag in TiffIfdEntryDefinitions.MetadataTags)
                {
                    if (entries.TryGetSingleElementValue(tag.Key, out string value))
                    {
                        tiffMetadata.TextTags.Add(new TiffMetadataTag(tag.Value, value));
                    }
                }
            }
        }

        /// <summary>
        /// Determines the TIFF compression and color types, and reads any associated parameters.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="entries">The IFD entries container to read the image format information for.</param>
        public static void ReadFormatOptions(ITiffDecoderCoreOptions options, TiffIfdEntriesContainer entries)
        {
            FillCompression(options, entries.Compression);

            options.PlanarConfiguration = entries.PlanarConfiguration;

            FillPhotometric(options, entries);

            FillBitsPerSample(options, entries);

            FillColorType(options, entries);
        }

        private static void FillColorType(ITiffDecoderCoreOptions options, TiffIfdEntriesContainer entries)
        {
            switch (options.PhotometricInterpretation)
            {
                case TiffPhotometricInterpretation.WhiteIsZero:
                {
                    if (options.BitsPerSample.Length == 1)
                    {
                        switch (options.BitsPerSample[0])
                        {
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
                    }
                    else
                    {
                        throw new NotSupportedException("The number of samples in the TIFF BitsPerSample entry is not supported.");
                    }

                    break;
                }

                case TiffPhotometricInterpretation.BlackIsZero:
                {
                    if (options.BitsPerSample.Length == 1)
                    {
                        switch (options.BitsPerSample[0])
                        {
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
                    }
                    else
                    {
                        throw new NotSupportedException("The number of samples in the TIFF BitsPerSample entry is not supported.");
                    }

                    break;
                }

                case TiffPhotometricInterpretation.Rgb:
                {
                    if (options.BitsPerSample.Length == 3)
                    {
                        if (options.PlanarConfiguration == TiffPlanarConfiguration.Chunky)
                        {
                            if (options.BitsPerSample[0] == 8 && options.BitsPerSample[1] == 8 && options.BitsPerSample[2] == 8)
                            {
                                options.ColorType = TiffColorType.Rgb888;
                            }
                            else
                            {
                                options.ColorType = TiffColorType.Rgb;
                            }
                        }
                        else
                        {
                            options.ColorType = TiffColorType.RgbPlanar;
                        }
                    }
                    else
                    {
                        throw new NotSupportedException("The number of samples in the TIFF BitsPerSample entry is not supported.");
                    }

                    break;
                }

                case TiffPhotometricInterpretation.PaletteColor:
                {
                    if (entries.TryGetArrayValue(TiffTagId.ColorMap, out uint[] colorMap))
                    {
                        options.ColorMap = colorMap;

                        if (options.BitsPerSample.Length == 1)
                        {
                            switch (options.BitsPerSample[0])
                            {
                                default:
                                {
                                    options.ColorType = TiffColorType.PaletteColor;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            throw new NotSupportedException("The number of samples in the TIFF BitsPerSample entry is not supported.");
                        }
                    }
                    else
                    {
                        throw new ImageFormatException("The TIFF ColorMap entry is missing for a palette color image.");
                    }

                    break;
                }

                default:
                    throw new NotSupportedException("The specified TIFF photometric interpretation is not supported.");
            }
        }

        private static void FillBitsPerSample(ITiffDecoderCoreOptions options, TiffIfdEntriesContainer entries)
        {
            if (entries.TryGetArrayValue(TiffTagId.BitsPerSample, out uint[] bitsPerSample))
            {
                options.BitsPerSample = bitsPerSample;
            }
            else
            {
                if (options.PhotometricInterpretation == TiffPhotometricInterpretation.WhiteIsZero
                    || options.PhotometricInterpretation == TiffPhotometricInterpretation.BlackIsZero)
                {
                    options.BitsPerSample = new[] { 1u };
                }
                else
                {
                    throw new ImageFormatException("The TIFF BitsPerSample entry is missing.");
                }
            }
        }

        private static void FillPhotometric(ITiffDecoderCoreOptions options, TiffIfdEntriesContainer entries)
        {
            if (!entries.TryGetSingleValue(TiffTagId.PhotometricInterpretation, out TiffPhotometricInterpretation photometricInterpretation))
            {
                if (entries.Compression == TiffCompression.Ccitt1D)
                {
                    photometricInterpretation = TiffPhotometricInterpretation.WhiteIsZero;
                }
                else
                {
                    throw new ImageFormatException("The TIFF photometric interpretation entry is missing.");
                }
            }

            options.PhotometricInterpretation = photometricInterpretation;
        }

        private static void FillCompression(ITiffDecoderCoreOptions options, TiffCompression compression)
        {
            switch (compression)
            {
                case TiffCompression.None:
                {
                    options.CompressionType = TiffCompressionType.None;
                    break;
                }

                case TiffCompression.PackBits:
                {
                    options.CompressionType = TiffCompressionType.PackBits;
                    break;
                }

                case TiffCompression.Deflate:
                case TiffCompression.OldDeflate:
                {
                    options.CompressionType = TiffCompressionType.Deflate;
                    break;
                }

                case TiffCompression.Lzw:
                {
                    options.CompressionType = TiffCompressionType.Lzw;
                    break;
                }

                default:
                {
                    throw new NotSupportedException("The specified TIFF compression format is not supported.");
                }
            }
        }
    }
}
