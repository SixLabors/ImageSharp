// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff;

internal class TiffEncoderEntriesCollector
{
    private const string SoftwareValue = "ImageSharp";

    public List<IExifValue> Entries { get; } = [];

    public void ProcessMetadata(Image image, bool skipMetadata)
        => new MetadataProcessor(this).Process(image, skipMetadata);

    public void ProcessMetadata(ImageFrame frame, bool skipMetadata)
        => new MetadataProcessor(this).Process(frame, skipMetadata);

    public void ProcessFrameInfo(ImageFrame frame, Size encodingSize, ImageMetadata imageMetadata)
        => new FrameInfoProcessor(this).Process(frame, encodingSize, imageMetadata);

    public void ProcessImageFormat(TiffEncoderCore encoder)
        => new ImageFormatProcessor(this).Process(encoder);

    /// <summary>
    /// Adds the frame's ICC profile to the TIFF directory when a compatible profile is available.
    /// </summary>
    /// <remarks>
    /// The IccProfile entry stores the profile bytes for this frame's encoded color samples.
    /// The caller checks color-space compatibility before this method. SkipMetadata does not suppress this entry.
    /// </remarks>
    /// <param name="profile">The retained profile compatible with the output samples, or null to omit the IccProfile entry.</param>
    public void ProcessIccProfile(IccProfile profile)
    {
        if (profile is not null)
        {
            // EXIF byte-array values are mutable, so this entry owns a copy rather than exposing the profile's backing bytes.
            // The IFD writer can write that owned array directly without another serialization buffer.
            this.AddOrReplace(new ExifByteArray(ExifTagValue.IccProfile, ExifDataType.Undefined)
            {
                Value = profile.ToByteArray()
            });
        }
    }

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
            this.ProcessExifProfile(skipMetadata, imageMetadata.ExifProfile);
            this.ProcessIptcProfile(skipMetadata, imageMetadata.IptcProfile);
            this.ProcessXmpProfile(skipMetadata, imageMetadata.XmpProfile);
        }

        private void ProcessProfiles(ImageFrameMetadata frameMetadata, bool skipMetadata)
        {
            this.ProcessExifProfile(skipMetadata, frameMetadata.ExifProfile);
            this.ProcessIptcProfile(skipMetadata, frameMetadata.IptcProfile);
            this.ProcessXmpProfile(skipMetadata, frameMetadata.XmpProfile);
        }

        /// <summary>
        /// Copies selected EXIF entries without changing the original profile.
        /// </summary>
        /// <param name="skipMetadata">Whether optional metadata is omitted.</param>
        /// <param name="exifProfile">The source EXIF profile.</param>
        private void ProcessExifProfile(bool skipMetadata, ExifProfile exifProfile)
        {
            if (!skipMetadata && (exifProfile != null && exifProfile.Parts != ExifParts.None))
            {
                foreach (IExifValue entry in exifProfile.Values)
                {
                    // This encoder writes strips. Source tile entries would point readers at data that the new file does not contain.
                    // ProcessIccProfile writes the checked output profile separately. A copied EXIF entry can describe a different color space.
                    ExifTagValue tag = (ExifTagValue)(ushort)entry.Tag;
                    if (tag is ExifTagValue.IccProfile or ExifTagValue.TileWidth or ExifTagValue.TileLength
                        or ExifTagValue.TileOffsets or ExifTagValue.TileByteCounts)
                    {
                        continue;
                    }

                    if (!this.Collector.Entries.Exists(t => t.Tag == entry.Tag) && entry.GetValue() != null)
                    {
                        ExifParts entryPart = ExifTags.GetPart(entry.Tag);
                        if (entryPart != ExifParts.None && exifProfile.Parts.HasFlag(entryPart))
                        {
                            this.Collector.AddOrReplace(entry.DeepClone());
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds the selected IPTC payload to the output directory.
        /// </summary>
        /// <param name="skipMetadata">Whether optional metadata is omitted.</param>
        /// <param name="iptcProfile">The source IPTC profile.</param>
        private void ProcessIptcProfile(bool skipMetadata, IptcProfile iptcProfile)
        {
            if (!skipMetadata && iptcProfile != null)
            {
                iptcProfile.UpdateData();
                ExifByteArray iptc = new(ExifTagValue.IPTC, ExifDataType.Byte)
                {
                    Value = iptcProfile.Data
                };

                this.Collector.AddOrReplace(iptc);
            }
        }

        /// <summary>
        /// Adds the selected XMP payload to the output directory.
        /// </summary>
        /// <param name="skipMetadata">Whether optional metadata is omitted.</param>
        /// <param name="xmpProfile">The source XMP profile.</param>
        private void ProcessXmpProfile(bool skipMetadata, XmpProfile xmpProfile)
        {
            if (!skipMetadata && xmpProfile != null)
            {
                ExifByteArray xmp = new(ExifTagValue.XMP, ExifDataType.Byte)
                {
                    Value = xmpProfile.Data
                };

                this.Collector.AddOrReplace(xmp);
            }
        }
    }

    private class FrameInfoProcessor : BaseProcessor
    {
        public FrameInfoProcessor(TiffEncoderEntriesCollector collector)
            : base(collector)
        {
        }

        public void Process(ImageFrame frame, Size encodingSize, ImageMetadata imageMetadata)
        {
            this.Collector.AddOrReplace(new ExifLong(ExifTagValue.ImageWidth)
            {
                Value = (uint)encodingSize.Width
            });

            this.Collector.AddOrReplace(new ExifLong(ExifTagValue.ImageLength)
            {
                Value = (uint)encodingSize.Height
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
            // These tags describe the samples and predictor written by this encoder. Source EXIF
            // values can describe a different layout, so replace them from the selected options.
            this.Collector.Entries.RemoveAll(
                entry => entry.Tag == ExifTag.SampleFormat
                    || entry.Tag == ExifTag.ExtraSamples
                    || entry.Tag == ExifTag.Predictor
                    || entry.Tag == ExifTag.InkSet
                    || entry.Tag == ExifTag.NumberOfInks
                    || entry.Tag == ExifTag.InkNames);

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

            if (encoder.PhotometricInterpretation == TiffPhotometricInterpretation.Separated)
            {
                // Separated CMYK has four color channels. These tags identify the inks in those samples.
                this.Collector.AddOrReplace(new ExifShort(ExifTagValue.InkSet) { Value = (ushort)TiffInkSet.Cmyk });
                this.Collector.AddOrReplace(new ExifShort(ExifTagValue.NumberOfInks) { Value = 4 });
            }

            if (encoder.SampleFormat == TiffSampleFormat.Float)
            {
                ushort[] sampleFormats = new ushort[samplesPerPixel.Value];
                Array.Fill(sampleFormats, (ushort)TiffSampleFormat.Float);
                this.Collector.AddOrReplace(new ExifShortArray(ExifTagValue.SampleFormat)
                {
                    Value = sampleFormats
                });

                if (encoder.BitsPerPixel == TiffBitsPerPixel.Bit128)
                {
                    // The extra sample describes association; the binary32 sample layout is
                    // identical for straight and premultiplied source pixels.
                    TiffExtraSampleType alphaType = encoder.FloatAlphaRepresentation == PixelAlphaRepresentation.Associated
                        ? TiffExtraSampleType.AssociatedAlphaData
                        : TiffExtraSampleType.UnassociatedAlphaData;

                    this.Collector.AddOrReplace(new ExifShortArray(ExifTagValue.ExtraSamples)
                    {
                        Value = [(ushort)alphaType]
                    });
                }
            }
            else if (encoder.PhotometricInterpretation == TiffPhotometricInterpretation.Rgb
                && encoder.BitsPerPixel is TiffBitsPerPixel.Bit64 or TiffBitsPerPixel.Bit128)
            {
                // Integer color writers emit straight color with a fourth alpha sample.
                this.Collector.AddOrReplace(new ExifShortArray(ExifTagValue.ExtraSamples)
                {
                    Value = [(ushort)TiffExtraSampleType.UnassociatedAlphaData]
                });
            }

            if (encoder.HorizontalPredictor == TiffPredictor.Horizontal &&
                (encoder.PhotometricInterpretation is TiffPhotometricInterpretation.Rgb or
                                                      TiffPhotometricInterpretation.PaletteColor or
                                                      TiffPhotometricInterpretation.BlackIsZero or
                                                      TiffPhotometricInterpretation.WhiteIsZero or
                                                      TiffPhotometricInterpretation.Separated))
            {
                ExifShort predictor = new(ExifTagValue.Predictor) { Value = (ushort)TiffPredictor.Horizontal };

                this.Collector.AddOrReplace(predictor);
            }
        }

        private static ushort GetSamplesPerPixel(TiffEncoderCore encoder)
        {
            if (encoder.SampleFormat == TiffSampleFormat.Float)
            {
                return (ushort)((int)encoder.BitsPerPixel!.Value / 32);
            }

            return encoder.PhotometricInterpretation switch
            {
                TiffPhotometricInterpretation.PaletteColor or
                TiffPhotometricInterpretation.BlackIsZero or
                TiffPhotometricInterpretation.WhiteIsZero => 1,
                TiffPhotometricInterpretation.Separated => 4,
                _ => encoder.BitsPerPixel is TiffBitsPerPixel.Bit64 or TiffBitsPerPixel.Bit128 ? (ushort)4 : (ushort)3,
            };
        }

        private static ushort[] GetBitsPerSampleValue(TiffEncoderCore encoder)
        {
            if (encoder.SampleFormat == TiffSampleFormat.Float)
            {
                ushort[] bits = new ushort[(int)encoder.BitsPerPixel!.Value / 32];
                Array.Fill(bits, (ushort)32);
                return bits;
            }

            switch (encoder.PhotometricInterpretation)
            {
                case TiffPhotometricInterpretation.PaletteColor:
                    if (encoder.BitsPerPixel == TiffBitsPerPixel.Bit4)
                    {
                        return TiffConstants.BitsPerSample4Bit.ToArray();
                    }

                    return TiffConstants.BitsPerSample8Bit.ToArray();

                case TiffPhotometricInterpretation.Rgb:
                    return encoder.BitsPerPixel switch
                    {
                        TiffBitsPerPixel.Bit48 => [16, 16, 16],
                        TiffBitsPerPixel.Bit64 => [16, 16, 16, 16],
                        TiffBitsPerPixel.Bit96 => [32, 32, 32],
                        TiffBitsPerPixel.Bit128 => [32, 32, 32, 32],
                        _ => TiffConstants.BitsPerSampleRgb8Bit.ToArray(),
                    };

                case TiffPhotometricInterpretation.Separated:
                    return encoder.BitsPerPixel == TiffBitsPerPixel.Bit64 ? [16, 16, 16, 16] : [8, 8, 8, 8];

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
                    // LZW compresses the encoded sample bytes independently of whether
                    // grayscale zero represents white or black.
                    return (ushort)TiffCompression.Lzw;

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
