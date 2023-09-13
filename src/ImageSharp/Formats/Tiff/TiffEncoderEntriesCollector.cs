// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff;

internal class TiffEncoderEntriesCollector
{
    private const string SoftwareValue = "ImageSharp";

    public List<IExifValue> Entries { get; } = new List<IExifValue>();

    public void ProcessMetadata(Image image, bool skipMetadata)
        => new MetadataProcessor(this).Process(image, skipMetadata);

    public void ProcessMetadata(ImageFrame frame, bool skipMetadata)
        => new MetadataProcessor(this).Process(frame, skipMetadata);

    public void ProcessFrameInfo(ImageFrame frame, ImageMetadata imageMetadata)
        => new FrameInfoProcessor(this).Process(frame, imageMetadata);

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

    private abstract class BaseProcessor
    {
        protected BaseProcessor(TiffEncoderEntriesCollector collector) => this.Collector = collector;

        protected TiffEncoderEntriesCollector Collector { get; }
    }

    private class MetadataProcessor : BaseProcessor
    {
        public MetadataProcessor(TiffEncoderEntriesCollector collector)
            : base(collector)
        {
        }

        public void Process(Image image, bool skipMetadata)
        {
            this.ProcessProfiles(image.Metadata, skipMetadata);

            if (!skipMetadata)
            {
                this.ProcessMetadata(image.Metadata.ExifProfile ?? new ExifProfile());
            }

            if (!this.Collector.Entries.Exists(t => t.Tag == ExifTag.Software))
            {
                this.Collector.Add(new ExifString(ExifTagValue.Software)
                {
                    Value = SoftwareValue
                });
            }
        }


        public void Process(ImageFrame frame, bool skipMetadata)
        {
            this.ProcessProfiles(frame.Metadata, skipMetadata);

            if (!skipMetadata)
            {
                this.ProcessMetadata(frame.Metadata.ExifProfile ?? new ExifProfile());
            }

            if (!this.Collector.Entries.Exists(t => t.Tag == ExifTag.Software))
            {
                this.Collector.Add(new ExifString(ExifTagValue.Software)
                {
                    Value = SoftwareValue
                });
            }
        }

        private static bool IsPureMetadata(ExifTag tag)
            => (ExifTagValue)(ushort)tag switch
            {
                ExifTagValue.DocumentName or
                ExifTagValue.ImageDescription or
                ExifTagValue.Make or
                ExifTagValue.Model or
                ExifTagValue.Software or
                ExifTagValue.DateTime or
                ExifTagValue.Artist or
                ExifTagValue.HostComputer or
                ExifTagValue.TargetPrinter or
                ExifTagValue.XMP or
                ExifTagValue.Rating or
                ExifTagValue.RatingPercent or
                ExifTagValue.ImageID or
                ExifTagValue.Copyright or
                ExifTagValue.MDLabName or
                ExifTagValue.MDSampleInfo or
                ExifTagValue.MDPrepDate or
                ExifTagValue.MDPrepTime or
                ExifTagValue.MDFileUnits or
                ExifTagValue.SEMInfo or
                ExifTagValue.XPTitle or
                ExifTagValue.XPComment or
                ExifTagValue.XPAuthor or
                ExifTagValue.XPKeywords or
                ExifTagValue.XPSubject => true,
                _ => false,
            };

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

                if (!this.Collector.Entries.Exists(t => t.Tag == entry.Tag))
                {
                    this.Collector.AddOrReplace(entry.DeepClone());
                }
            }
        }

        private void ProcessProfiles(ImageMetadata imageMetadata, bool skipMetadata)
        {
            if (!skipMetadata && (imageMetadata.ExifProfile != null && imageMetadata.ExifProfile.Parts != ExifParts.None))
            {
                foreach (IExifValue entry in imageMetadata.ExifProfile.Values)
                {
                    if (!this.Collector.Entries.Exists(t => t.Tag == entry.Tag) && entry.GetValue() != null)
                    {
                        ExifParts entryPart = ExifTags.GetPart(entry.Tag);
                        if (entryPart != ExifParts.None && imageMetadata.ExifProfile.Parts.HasFlag(entryPart))
                        {
                            this.Collector.AddOrReplace(entry.DeepClone());
                        }
                    }
                }
            }
            else
            {
                imageMetadata.ExifProfile?.RemoveValue(ExifTag.SubIFDOffset);
            }

            if (!skipMetadata && imageMetadata.IptcProfile != null)
            {
                imageMetadata.IptcProfile.UpdateData();
                ExifByteArray iptc = new(ExifTagValue.IPTC, ExifDataType.Byte)
                {
                    Value = imageMetadata.IptcProfile.Data
                };

                this.Collector.AddOrReplace(iptc);
            }
            else
            {
                imageMetadata.ExifProfile?.RemoveValue(ExifTag.IPTC);
            }

            if (imageMetadata.IccProfile != null)
            {
                ExifByteArray icc = new(ExifTagValue.IccProfile, ExifDataType.Undefined)
                {
                    Value = imageMetadata.IccProfile.ToByteArray()
                };

                this.Collector.AddOrReplace(icc);
            }
            else
            {
                imageMetadata.ExifProfile?.RemoveValue(ExifTag.IccProfile);
            }

            if (!skipMetadata && imageMetadata.XmpProfile != null)
            {
                ExifByteArray xmp = new(ExifTagValue.XMP, ExifDataType.Byte)
                {
                    Value = imageMetadata.XmpProfile.Data
                };

                this.Collector.AddOrReplace(xmp);
            }
            else
            {
                imageMetadata.ExifProfile?.RemoveValue(ExifTag.XMP);
            }
        }

        private void ProcessProfiles(ImageFrameMetadata frameMetadata, bool skipMetadata)
        {
            if (!skipMetadata && (frameMetadata.ExifProfile != null && frameMetadata.ExifProfile.Parts != ExifParts.None))
            {
                foreach (IExifValue entry in frameMetadata.ExifProfile.Values)
                {
                    if (!this.Collector.Entries.Exists(t => t.Tag == entry.Tag) && entry.GetValue() != null)
                    {
                        ExifParts entryPart = ExifTags.GetPart(entry.Tag);
                        if (entryPart != ExifParts.None && frameMetadata.ExifProfile.Parts.HasFlag(entryPart))
                        {
                            this.Collector.AddOrReplace(entry.DeepClone());
                        }
                    }
                }
            }
            else
            {
                frameMetadata.ExifProfile?.RemoveValue(ExifTag.SubIFDOffset);
            }

            if (!skipMetadata && frameMetadata.IptcProfile != null)
            {
                frameMetadata.IptcProfile.UpdateData();
                ExifByteArray iptc = new(ExifTagValue.IPTC, ExifDataType.Byte)
                {
                    Value = frameMetadata.IptcProfile.Data
                };

                this.Collector.AddOrReplace(iptc);
            }
            else
            {
                frameMetadata.ExifProfile?.RemoveValue(ExifTag.IPTC);
            }

            if (frameMetadata.IccProfile != null)
            {
                ExifByteArray icc = new(ExifTagValue.IccProfile, ExifDataType.Undefined)
                {
                    Value = frameMetadata.IccProfile.ToByteArray()
                };

                this.Collector.AddOrReplace(icc);
            }
            else
            {
                frameMetadata.ExifProfile?.RemoveValue(ExifTag.IccProfile);
            }

            if (!skipMetadata && frameMetadata.XmpProfile != null)
            {
                ExifByteArray xmp = new(ExifTagValue.XMP, ExifDataType.Byte)
                {
                    Value = frameMetadata.XmpProfile.Data
                };

                this.Collector.AddOrReplace(xmp);
            }
            else
            {
                frameMetadata.ExifProfile?.RemoveValue(ExifTag.XMP);
            }
        }
    }

    private class FrameInfoProcessor : BaseProcessor
    {
        public FrameInfoProcessor(TiffEncoderEntriesCollector collector)
            : base(collector)
        {
        }

        public void Process(ImageFrame frame, ImageMetadata imageMetadata)
        {
            this.Collector.AddOrReplace(new ExifLong(ExifTagValue.ImageWidth)
            {
                Value = (uint)frame.Width
            });

            this.Collector.AddOrReplace(new ExifLong(ExifTagValue.ImageLength)
            {
                Value = (uint)frame.Height
            });

            this.ProcessResolution(imageMetadata);
        }

        private void ProcessResolution(ImageMetadata imageMetadata)
        {
            ExifResolutionValues resolution = UnitConverter.GetExifResolutionValues(
                imageMetadata.ResolutionUnits,
                imageMetadata.HorizontalResolution,
                imageMetadata.VerticalResolution);

            this.Collector.AddOrReplace(new ExifShort(ExifTagValue.ResolutionUnit)
            {
                Value = resolution.ResolutionUnit
            });

            if (resolution.VerticalResolution.HasValue && resolution.HorizontalResolution.HasValue)
            {
                this.Collector.AddOrReplace(new ExifRational(ExifTagValue.XResolution)
                {
                    Value = new Rational(resolution.HorizontalResolution.Value)
                });

                this.Collector.AddOrReplace(new ExifRational(ExifTagValue.YResolution)
                {
                    Value = new Rational(resolution.VerticalResolution.Value)
                });
            }
        }
    }

    private class ImageFormatProcessor : BaseProcessor
    {
        public ImageFormatProcessor(TiffEncoderEntriesCollector collector)
            : base(collector)
        {
        }

        public void Process(TiffEncoderCore encoder)
        {
            ExifShort planarConfig = new(ExifTagValue.PlanarConfiguration)
            {
                Value = (ushort)TiffPlanarConfiguration.Chunky
            };

            ExifShort samplesPerPixel = new(ExifTagValue.SamplesPerPixel)
            {
                Value = GetSamplesPerPixel(encoder)
            };

            ushort[] bitsPerSampleValue = GetBitsPerSampleValue(encoder);
            ExifShortArray bitPerSample = new(ExifTagValue.BitsPerSample)
            {
                Value = bitsPerSampleValue
            };

            ushort compressionType = GetCompressionType(encoder);
            ExifShort compression = new(ExifTagValue.Compression)
            {
                Value = compressionType
            };

            ExifShort photometricInterpretation = new(ExifTagValue.PhotometricInterpretation)
            {
                Value = (ushort)encoder.PhotometricInterpretation
            };

            this.Collector.AddOrReplace(planarConfig);
            this.Collector.AddOrReplace(samplesPerPixel);
            this.Collector.AddOrReplace(bitPerSample);
            this.Collector.AddOrReplace(compression);
            this.Collector.AddOrReplace(photometricInterpretation);

            if (encoder.HorizontalPredictor == TiffPredictor.Horizontal &&
                (encoder.PhotometricInterpretation is TiffPhotometricInterpretation.Rgb or
                                                      TiffPhotometricInterpretation.PaletteColor or
                                                      TiffPhotometricInterpretation.BlackIsZero))
            {
                ExifShort predictor = new(ExifTagValue.Predictor) { Value = (ushort)TiffPredictor.Horizontal };

                this.Collector.AddOrReplace(predictor);
            }
        }

        private static ushort GetSamplesPerPixel(TiffEncoderCore encoder)
            => encoder.PhotometricInterpretation switch
            {
                TiffPhotometricInterpretation.PaletteColor or
                TiffPhotometricInterpretation.BlackIsZero or
                TiffPhotometricInterpretation.WhiteIsZero => 1,
                _ => 3,
            };

        private static ushort[] GetBitsPerSampleValue(TiffEncoderCore encoder)
        {
            switch (encoder.PhotometricInterpretation)
            {
                case TiffPhotometricInterpretation.PaletteColor:
                    if (encoder.BitsPerPixel == TiffBitsPerPixel.Bit4)
                    {
                        return TiffConstants.BitsPerSample4Bit.ToArray();
                    }

                    return TiffConstants.BitsPerSample8Bit.ToArray();

                case TiffPhotometricInterpretation.Rgb:
                    return TiffConstants.BitsPerSampleRgb8Bit.ToArray();

                case TiffPhotometricInterpretation.WhiteIsZero:
                    return encoder.BitsPerPixel switch
                    {
                        TiffBitsPerPixel.Bit1 => TiffConstants.BitsPerSample1Bit.ToArray(),
                        TiffBitsPerPixel.Bit16 => TiffConstants.BitsPerSample16Bit.ToArray(),
                        _ => TiffConstants.BitsPerSample8Bit.ToArray()
                    };

                case TiffPhotometricInterpretation.BlackIsZero:
                    return encoder.BitsPerPixel switch
                    {
                        TiffBitsPerPixel.Bit1 => TiffConstants.BitsPerSample1Bit.ToArray(),
                        TiffBitsPerPixel.Bit16 => TiffConstants.BitsPerSample16Bit.ToArray(),
                        _ => TiffConstants.BitsPerSample8Bit.ToArray()
                    };

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
                    if (encoder.PhotometricInterpretation is TiffPhotometricInterpretation.Rgb or
                                                             TiffPhotometricInterpretation.PaletteColor or
                                                             TiffPhotometricInterpretation.BlackIsZero)
                    {
                        return (ushort)TiffCompression.Lzw;
                    }

                    break;

                case TiffCompression.CcittGroup3Fax:
                    return (ushort)TiffCompression.CcittGroup3Fax;

                case TiffCompression.CcittGroup4Fax:
                    return (ushort)TiffCompression.CcittGroup4Fax;

                case TiffCompression.Ccitt1D:
                    return (ushort)TiffCompression.Ccitt1D;

                case TiffCompression.Jpeg:
                    return (ushort)TiffCompression.Jpeg;
            }

            return (ushort)TiffCompression.None;
        }
    }
}
