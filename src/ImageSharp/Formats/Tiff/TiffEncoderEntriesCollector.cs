// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    internal class TiffEncoderEntriesCollector
    {
        public List<IExifValue> Entries { get; } = new List<IExifValue>();

        public void ProcessGeneral<TPixel>(Image<TPixel> image)
                where TPixel : unmanaged, IPixel<TPixel>
            => new GeneralProcessor(this).Process(image);

        public void ProcessImageFormat(TiffEncoderCore encoder)
            => new ImageFormatProcessor(this).Process(encoder);

        public void Add(IExifValue entry)
        {
            IExifValue exist = this.Entries.Find(t => t.Tag == entry.Tag);
            if (exist != null)
            {
                this.Entries.Remove(exist);
            }

            this.Entries.Add(entry);
        }

        private void AddInternal(IExifValue entry) => this.Entries.Add(entry);

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
                    Value = "ImageSharp"
                };

                this.collector.AddInternal(width);
                this.collector.AddInternal(height);
                this.collector.AddInternal(software);

                this.ProcessResolution(image.Metadata, frameMetadata);

                this.ProcessProfiles(image.Metadata, frameMetadata);
                this.ProcessMetadata(frameMetadata);
            }

            private static bool IsMetadata(ExifTag tag)
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
                frameMetadata.SetResolutions(
                    imageMetadata.ResolutionUnits,
                    imageMetadata.HorizontalResolution,
                    imageMetadata.VerticalResolution);

                var xResolution = new ExifRational(ExifTagValue.XResolution)
                {
                    Value = frameMetadata.ExifProfile.GetValue(ExifTag.XResolution).Value
                };

                var yResolution = new ExifRational(ExifTagValue.YResolution)
                {
                    Value = frameMetadata.ExifProfile.GetValue(ExifTag.YResolution).Value
                };

                var resolutionUnit = new ExifShort(ExifTagValue.ResolutionUnit)
                {
                    Value = frameMetadata.ExifProfile.GetValue(ExifTag.ResolutionUnit).Value
                };

                this.collector.AddInternal(xResolution);
                this.collector.AddInternal(yResolution);
                this.collector.AddInternal(resolutionUnit);
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
                            if (!IsMetadata(entry.Tag))
                            {
                                continue;
                            }

                            break;
                    }

                    if (!this.collector.Entries.Exists(t => t.Tag == entry.Tag))
                    {
                        this.collector.AddInternal(entry.DeepClone());
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

                    this.collector.AddInternal(iptc);
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

                    this.collector.AddInternal(icc);
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

                    this.collector.AddInternal(xmp);
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

                this.collector.Add(samplesPerPixel);
                this.collector.Add(bitPerSample);
                this.collector.Add(compression);
                this.collector.Add(photometricInterpretation);

                if (encoder.UseHorizontalPredictor)
                {
                    if (encoder.Mode == TiffEncodingMode.Rgb || encoder.Mode == TiffEncodingMode.Gray || encoder.Mode == TiffEncodingMode.ColorPalette)
                    {
                        var predictor = new ExifShort(ExifTagValue.Predictor) { Value = (ushort)TiffPredictor.Horizontal };

                        this.collector.Add(predictor);
                    }
                }
            }

            private static uint GetSamplesPerPixel(TiffEncoderCore encoder)
            {
                switch (encoder.PhotometricInterpretation)
                {
                    case TiffPhotometricInterpretation.Rgb:
                        return 3;
                    case TiffPhotometricInterpretation.PaletteColor:
                    case TiffPhotometricInterpretation.BlackIsZero:
                    case TiffPhotometricInterpretation.WhiteIsZero:
                        return 1;
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
                    case TiffEncoderCompression.Deflate:
                        // Deflate is allowed for all modes.
                        return (ushort)TiffCompression.Deflate;
                    case TiffEncoderCompression.PackBits:
                        // PackBits is allowed for all modes.
                        return (ushort)TiffCompression.PackBits;
                    case TiffEncoderCompression.Lzw:
                        if (encoder.Mode == TiffEncodingMode.Rgb || encoder.Mode == TiffEncodingMode.Gray || encoder.Mode == TiffEncodingMode.ColorPalette)
                        {
                            return (ushort)TiffCompression.Lzw;
                        }

                        break;

                    case TiffEncoderCompression.CcittGroup3Fax:
                        if (encoder.Mode == TiffEncodingMode.BiColor)
                        {
                            return (ushort)TiffCompression.CcittGroup3Fax;
                        }

                        break;

                    case TiffEncoderCompression.ModifiedHuffman:
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
