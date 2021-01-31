// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// The decoder metadata creator.
    /// </summary>
    internal static class TiffDecoderMetadataCreator
    {
        public static ImageMetadata Create(List<TiffFrameMetadata> frames, bool ignoreMetadata, ByteOrder byteOrder)
        {
            if (frames.Count < 1)
            {
                TiffThrowHelper.ThrowImageFormatException("Expected at least one frame.");
            }

            var coreMetadata = new ImageMetadata();
            TiffFrameMetadata rootFrameMetadata = frames[0];

            coreMetadata.ResolutionUnits = rootFrameMetadata.ResolutionUnit;
            if (rootFrameMetadata.HorizontalResolution != null)
            {
                coreMetadata.HorizontalResolution = rootFrameMetadata.HorizontalResolution.Value;
            }

            if (rootFrameMetadata.VerticalResolution != null)
            {
                coreMetadata.VerticalResolution = rootFrameMetadata.VerticalResolution.Value;
            }

            TiffMetadata tiffMetadata = coreMetadata.GetTiffMetadata();
            tiffMetadata.ByteOrder = byteOrder;
            tiffMetadata.BitsPerPixel = GetBitsPerPixel(rootFrameMetadata);
            tiffMetadata.Compression = rootFrameMetadata.Compression;

            if (!ignoreMetadata)
            {
                foreach (TiffFrameMetadata frame in frames)
                {
                    if (tiffMetadata.XmpProfile == null)
                    {
                        IExifValue<byte[]> val = frame.ExifProfile.GetValue(ExifTag.XMP);
                        if (val != null)
                        {
                            tiffMetadata.XmpProfile = val.Value;
                        }
                    }

                    if (coreMetadata.IptcProfile == null)
                    {
                        IExifValue<byte[]> val = frame.ExifProfile.GetValue(ExifTag.IPTC);
                        if (val != null)
                        {
                            coreMetadata.IptcProfile = new IptcProfile(val.Value);
                        }
                    }

                    if (coreMetadata.IccProfile == null)
                    {
                        IExifValue<byte[]> val = frame.ExifProfile.GetValue(ExifTag.IccProfile);
                        if (val != null)
                        {
                            coreMetadata.IccProfile = new IccProfile(val.Value);
                        }
                    }
                }
            }

            return coreMetadata;
        }

        private static TiffBitsPerPixel GetBitsPerPixel(TiffFrameMetadata firstFrameMetaData)
            => (TiffBitsPerPixel)firstFrameMetaData.BitsPerPixel;
    }
}
