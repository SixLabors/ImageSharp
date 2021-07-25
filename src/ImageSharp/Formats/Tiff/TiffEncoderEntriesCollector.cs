// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff
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
                ImageFrame<TPixel> rootFrame = image.Frames.RootFrame;
                ExifProfile rootFrameExifProfile = rootFrame.Metadata.ExifProfile ?? new ExifProfile();
                byte[] rootFrameXmpBytes = rootFrame.Metadata.XmpProfile;

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

                this.collector.AddOrReplace(width);
                this.collector.AddOrReplace(height);

                this.ProcessResolution(image.Metadata, rootFrameExifProfile);
                this.ProcessProfiles(image.Metadata, rootFrameExifProfile, rootFrameXmpBytes);
                this.ProcessMetadata(rootFrameExifProfile);

                if (!this.collector.Entries.Exists(t => t.Tag == ExifTag.Software))
                {
                    this.collector.Add(software);
                }
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

            private void ProcessResolution(ImageMetadata imageMetadata, ExifProfile exifProfile)
            {
                UnitConverter.SetResolutionValues(
                    exifProfile,
                    imageMetadata.ResolutionUnits,
                    imageMetadata.HorizontalResolution,
                    imageMetadata.VerticalResolution);

                this.collector.Add(exifProfile.GetValue(ExifTag.ResolutionUnit).DeepClone());

                IExifValue xResolution = exifProfile.GetValue(ExifTag.XResolution)?.DeepClone();
                IExifValue yResolution = exifProfile.GetValue(ExifTag.YResolution)?.DeepClone();

                if (xResolution != null && yResolution != null)
                {
                    this.collector.Add(xResolution);
                    this.collector.Add(yResolution);
                }
            }

            private void ProcessMetadata(ExifProfile exifProfile)
            {
                foreach (IExifValue entry in exifProfile.Values)
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

            private void ProcessProfiles(ImageMetadata imageMetadata, ExifProfile exifProfile, byte[] xmpProfile)
            {
                if (exifProfile != null && exifProfile.Parts != ExifParts.None)
                {
                    foreach (IExifValue entry in exifProfile.Values)
                    {
                        if (!this.collector.Entries.Exists(t => t.Tag == entry.Tag) && entry.GetValue() != null)
                        {
                            ExifParts entryPart = ExifTags.GetPart(entry.Tag);
                            if (entryPart != ExifParts.None && exifProfile.Parts.HasFlag(entryPart))
                            {
                                this.collector.AddOrReplace(entry.DeepClone());
                            }
                        }
                    }
                }
                else
                {
                    exifProfile.RemoveValue(ExifTag.SubIFDOffset);
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
                    exifProfile.RemoveValue(ExifTag.IPTC);
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
                    exifProfile.RemoveValue(ExifTag.IccProfile);
                }

                if (xmpProfile != null)
                {
                    var xmp = new ExifByteArray(ExifTagValue.XMP, ExifDataType.Byte)
                    {
                        Value = xmpProfile
                    };

                    this.collector.Add(xmp);
                }
                else
                {
                    exifProfile.RemoveValue(ExifTag.XMP);
                }
            }
        }

        private class ImageFormatProcessor
        {
            private readonly TiffEncoderEntriesCollector collector;

            public ImageFormatProcessor(TiffEncoderEntriesCollector collector) => this.collector = collector;

            public void Process(TiffEncoderCore encoder)
            {
                var planarConfig = new ExifShort(ExifTagValue.PlanarConfiguration)
                {
                    Value = (ushort)TiffPlanarConfiguration.Chunky
                };

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

                this.collector.AddOrReplace(planarConfig);
                this.collector.AddOrReplace(samplesPerPixel);
                this.collector.AddOrReplace(bitPerSample);
                this.collector.AddOrReplace(compression);
                this.collector.AddOrReplace(photometricInterpretation);

                if (encoder.HorizontalPredictor == TiffPredictor.Horizontal)
                {
                    if (encoder.PhotometricInterpretation == TiffPhotometricInterpretation.Rgb ||
                        encoder.PhotometricInterpretation == TiffPhotometricInterpretation.PaletteColor ||
                        encoder.PhotometricInterpretation == TiffPhotometricInterpretation.BlackIsZero)
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
                            return TiffConstants.BitsPerSample4Bit.ToArray();
                        }
                        else
                        {
                            return TiffConstants.BitsPerSample8Bit.ToArray();
                        }

                    case TiffPhotometricInterpretation.Rgb:
                        return TiffConstants.BitsPerSampleRgb8Bit.ToArray();

                    case TiffPhotometricInterpretation.WhiteIsZero:
                        if (encoder.BitsPerPixel == TiffBitsPerPixel.Bit1)
                        {
                            return TiffConstants.BitsPerSample1Bit.ToArray();
                        }

                        return TiffConstants.BitsPerSample8Bit.ToArray();

                    case TiffPhotometricInterpretation.BlackIsZero:
                        if (encoder.BitsPerPixel == TiffBitsPerPixel.Bit1)
                        {
                            return TiffConstants.BitsPerSample1Bit.ToArray();
                        }

                        return TiffConstants.BitsPerSample8Bit.ToArray();

                    default:
                        return TiffConstants.BitsPerSampleRgb8Bit.ToArray();
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
                        if (encoder.PhotometricInterpretation == TiffPhotometricInterpretation.Rgb ||
                            encoder.PhotometricInterpretation == TiffPhotometricInterpretation.PaletteColor ||
                            encoder.PhotometricInterpretation == TiffPhotometricInterpretation.BlackIsZero)
                        {
                            return (ushort)TiffCompression.Lzw;
                        }

                        break;

                    case TiffCompression.CcittGroup3Fax:
                        return (ushort)TiffCompression.CcittGroup3Fax;

                    case TiffCompression.Ccitt1D:
                        return (ushort)TiffCompression.Ccitt1D;
                }

                return (ushort)TiffCompression.None;
            }
        }
    }
}
