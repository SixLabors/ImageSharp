// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    internal class TiffEncoderEntriesCollector
    {
        private const string SoftwareValue = "ImageSharp";

        public List<IExifValue> Entries { get; } = new List<IExifValue>();

        public void ProcessGeneral<TPixel>(Image<TPixel> image)
                where TPixel : unmanaged, IPixel<TPixel>
            => new GeneralProcessor(this).Process(image);

        public void ProcessImageFormat(TiffEncoderCore encoder)
            => new ImageFormatProcessor(this).Process(encoder);

        public void AddOrReplace(IExifValue entry)
        {
            int index = this.Entries.FindIndex(t => t.Tag == entry.Tag);
            if (index >= 0)
            {
                this.Entries[index] = entry;
            }
            else
            {
                this.Entries.Add(entry);
            }
        }

        private void Add(IExifValue entry) => this.Entries.Add(entry);

        private class GeneralProcessor
        {
            private readonly TiffEncoderEntriesCollector collector;

            public GeneralProcessor(TiffEncoderEntriesCollector collector) => this.collector = collector;

            public void Process<TPixel>(Image<TPixel> image)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                TiffFrameMetadata frameMetadata = image.Frames.RootFrame.Metadata.GetTiffMetadata();

                var width = new ExifLong(ExifTagValue.ImageWidth)
                {
                    Value = (uint)image.Width
                };

                var height = new ExifLong(ExifTagValue.ImageLength)
                {
                    Value = (uint)image.Height
                };

                var software = new ExifString(ExifTagValue.Software)
                {
                    Value = SoftwareValue
                };

                this.collector.Add(width);
                this.collector.Add(height);
                this.collector.Add(software);

                this.ProcessResolution(image.Metadata, frameMetadata);

                this.ProcessProfiles(image.Metadata, frameMetadata);
                this.ProcessMetadata(frameMetadata);
            }

            private static bool IsPureMetadata(ExifTag tag)
            {
                switch ((ExifTagValue)(ushort)tag)
                {
                    case ExifTagValue.DocumentName:
                    case ExifTagValue.ImageDescription:
                    case ExifTagValue.Make:
                    case ExifTagValue.Model:
                    case ExifTagValue.Software:
                    case ExifTagValue.DateTime:
                    case ExifTagValue.Artist:
                    case ExifTagValue.HostComputer:
                    case ExifTagValue.TargetPrinter:
                    case ExifTagValue.XMP:
                    case ExifTagValue.Rating:
                    case ExifTagValue.RatingPercent:
                    case ExifTagValue.ImageID:
                    case ExifTagValue.Copyright:
                    case ExifTagValue.MDLabName:
                    case ExifTagValue.MDSampleInfo:
                    case ExifTagValue.MDPrepDate:
                    case ExifTagValue.MDPrepTime:
                    case ExifTagValue.MDFileUnits:
                    case ExifTagValue.SEMInfo:
                    case ExifTagValue.XPTitle:
                    case ExifTagValue.XPComment:
                    case ExifTagValue.XPAuthor:
                    case ExifTagValue.XPKeywords:
                    case ExifTagValue.XPSubject:
                        return true;
                    default:
                        return false;
                }
            }

            private void ProcessResolution(ImageMetadata imageMetadata, TiffFrameMetadata frameMetadata)
            {
                UnitConverter.SetResolutionValues(
                    frameMetadata.ExifProfile,
                    imageMetadata.ResolutionUnits,
                    imageMetadata.HorizontalResolution,
                    imageMetadata.VerticalResolution);

                this.collector.Add(frameMetadata.ExifProfile.GetValue(ExifTag.ResolutionUnit).DeepClone());

                IExifValue xResolution = frameMetadata.ExifProfile.GetValue(ExifTag.XResolution)?.DeepClone();
                IExifValue yResolution = frameMetadata.ExifProfile.GetValue(ExifTag.YResolution)?.DeepClone();

                if (xResolution != null && yResolution != null)
                {
                    this.collector.Add(xResolution);
                    this.collector.Add(yResolution);
                }
            }

            private void ProcessMetadata(TiffFrameMetadata frameMetadata)
            {
                foreach (IExifValue entry in frameMetadata.ExifProfile.Values)
                {
                    // todo: skip subIfd
                    if (entry.DataType == ExifDataType.Ifd)
                    {
                        continue;
                    }

                    switch ((ExifTagValue)(ushort)entry.Tag)
                    {
                        case ExifTagValue.SubIFDOffset:
                        case ExifTagValue.GPSIFDOffset:
                        case ExifTagValue.SubIFDs:
                        case ExifTagValue.XMP:
                        case ExifTagValue.IPTC:
                        case ExifTagValue.IccProfile:
                            continue;
                    }

                    switch (ExifTags.GetPart(entry.Tag))
                    {
                        case ExifParts.ExifTags:
                        case ExifParts.GpsTags:
                            break;

                        case ExifParts.IfdTags:
                            if (!IsPureMetadata(entry.Tag))
                            {
                                continue;
                            }

                            break;
                    }

                    if (!this.collector.Entries.Exists(t => t.Tag == entry.Tag))
                    {
                        this.collector.AddOrReplace(entry.DeepClone());
                    }
                }
            }

            private void ProcessProfiles(ImageMetadata imageMetadata, TiffFrameMetadata tiffFrameMetadata)
            {
                if (imageMetadata.ExifProfile != null)
                {
                    // todo: implement processing exif profile
                }
                else
                {
                    tiffFrameMetadata.ExifProfile.RemoveValue(ExifTag.SubIFDOffset);
                }

                if (imageMetadata.IptcProfile != null)
                {
                    imageMetadata.IptcProfile.UpdateData();
                    var iptc = new ExifByteArray(ExifTagValue.IPTC, ExifDataType.Byte)
                    {
                        Value = imageMetadata.IptcProfile.Data
                    };

                    this.collector.Add(iptc);
                }
                else
                {
                    tiffFrameMetadata.ExifProfile.RemoveValue(ExifTag.IPTC);
                }

                if (imageMetadata.IccProfile != null)
                {
                    var icc = new ExifByteArray(ExifTagValue.IccProfile, ExifDataType.Undefined)
                    {
                        Value = imageMetadata.IccProfile.ToByteArray()
                    };

                    this.collector.Add(icc);
                }
                else
                {
                    tiffFrameMetadata.ExifProfile.RemoveValue(ExifTag.IccProfile);
                }

                TiffMetadata tiffMetadata = imageMetadata.GetTiffMetadata();
                if (tiffMetadata.XmpProfile != null)
                {
                    var xmp = new ExifByteArray(ExifTagValue.XMP, ExifDataType.Byte)
                    {
                        Value = tiffMetadata.XmpProfile
                    };

                    this.collector.Add(xmp);
                }
                else
                {
                    tiffFrameMetadata.ExifProfile.RemoveValue(ExifTag.XMP);
                }
            }
        }

        private class ImageFormatProcessor
        {
            private readonly TiffEncoderEntriesCollector collector;

            public ImageFormatProcessor(TiffEncoderEntriesCollector collector) => this.collector = collector;

            public void Process(TiffEncoderCore encoder)
            {
                var samplesPerPixel = new ExifLong(ExifTagValue.SamplesPerPixel)
                {
                    Value = GetSamplesPerPixel(encoder)
                };

                ushort[] bitsPerSampleValue = GetBitsPerSampleValue(encoder);
                var bitPerSample = new ExifShortArray(ExifTagValue.BitsPerSample)
                {
                    Value = bitsPerSampleValue
                };

                ushort compressionType = GetCompressionType(encoder);
                var compression = new ExifShort(ExifTagValue.Compression)
                {
                    Value = compressionType
                };

                var photometricInterpretation = new ExifShort(ExifTagValue.PhotometricInterpretation)
                {
                    Value = (ushort)encoder.PhotometricInterpretation
                };

                this.collector.AddOrReplace(samplesPerPixel);
                this.collector.AddOrReplace(bitPerSample);
                this.collector.AddOrReplace(compression);
                this.collector.AddOrReplace(photometricInterpretation);

                if (encoder.UseHorizontalPredictor)
                {
                    if (encoder.Mode == TiffEncodingMode.Rgb || encoder.Mode == TiffEncodingMode.Gray || encoder.Mode == TiffEncodingMode.ColorPalette)
                    {
                        var predictor = new ExifShort(ExifTagValue.Predictor) { Value = (ushort)TiffPredictor.Horizontal };

                        this.collector.AddOrReplace(predictor);
                    }
                }
            }

            private static uint GetSamplesPerPixel(TiffEncoderCore encoder)
            {
                switch (encoder.PhotometricInterpretation)
                {
                    case TiffPhotometricInterpretation.PaletteColor:
                    case TiffPhotometricInterpretation.BlackIsZero:
                    case TiffPhotometricInterpretation.WhiteIsZero:
                        return 1;
                    case TiffPhotometricInterpretation.Rgb:
                    default:
                        return 3;
                }
            }

            private static ushort[] GetBitsPerSampleValue(TiffEncoderCore encoder)
            {
                switch (encoder.PhotometricInterpretation)
                {
                    case TiffPhotometricInterpretation.PaletteColor:
                        if (encoder.BitsPerPixel == TiffBitsPerPixel.Bit4)
                        {
                            return TiffConstants.BitsPerSample4Bit;
                        }
                        else
                        {
                            return TiffConstants.BitsPerSample8Bit;
                        }

                    case TiffPhotometricInterpretation.Rgb:
                        return TiffConstants.BitsPerSampleRgb8Bit;
                    case TiffPhotometricInterpretation.WhiteIsZero:
                        if (encoder.Mode == TiffEncodingMode.BiColor)
                        {
                            return TiffConstants.BitsPerSample1Bit;
                        }

                        return TiffConstants.BitsPerSample8Bit;
                    case TiffPhotometricInterpretation.BlackIsZero:
                        if (encoder.Mode == TiffEncodingMode.BiColor)
                        {
                            return TiffConstants.BitsPerSample1Bit;
                        }

                        return TiffConstants.BitsPerSample8Bit;
                    default:
                        return TiffConstants.BitsPerSampleRgb8Bit;
                }
            }

            private static ushort GetCompressionType(TiffEncoderCore encoder)
            {
                switch (encoder.CompressionType)
                {
                    case TiffCompression.Deflate:
                        // Deflate is allowed for all modes.
                        return (ushort)TiffCompression.Deflate;
                    case TiffCompression.PackBits:
                        // PackBits is allowed for all modes.
                        return (ushort)TiffCompression.PackBits;
                    case TiffCompression.Lzw:
                        if (encoder.Mode == TiffEncodingMode.Rgb || encoder.Mode == TiffEncodingMode.Gray || encoder.Mode == TiffEncodingMode.ColorPalette)
                        {
                            return (ushort)TiffCompression.Lzw;
                        }

                        break;

                    case TiffCompression.CcittGroup3Fax:
                        if (encoder.Mode == TiffEncodingMode.BiColor)
                        {
                            return (ushort)TiffCompression.CcittGroup3Fax;
                        }

                        break;

                    case TiffCompression.Ccitt1D:
                        if (encoder.Mode == TiffEncodingMode.BiColor)
                        {
                            return (ushort)TiffCompression.Ccitt1D;
                        }

                        break;
                }

                return (ushort)TiffCompression.None;
            }
        }
    }
}
