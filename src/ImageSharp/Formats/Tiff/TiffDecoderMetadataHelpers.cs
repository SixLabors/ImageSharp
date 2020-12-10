// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;

using SixLabors.ImageSharp.Formats.Experimental.Tiff.Constants;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    /// <summary>
    /// The decoder metadata helper methods.
    /// </summary>
    internal static class TiffDecoderMetadataHelpers
    {
        public static ImageMetadata CreateMetadata(this IList<TiffFrameMetadata> frames, bool ignoreMetadata, ByteOrder byteOrder)
        {
            var coreMetadata = new ImageMetadata();

            TiffFrameMetadata rootFrameMetadata = frames.First();
            switch (rootFrameMetadata.ResolutionUnit)
            {
                case TiffResolutionUnit.None:
                    coreMetadata.ResolutionUnits = PixelResolutionUnit.AspectRatio;
                    break;
                case TiffResolutionUnit.Inch:
                    coreMetadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;
                    break;
                case TiffResolutionUnit.Centimeter:
                    coreMetadata.ResolutionUnits = PixelResolutionUnit.PixelsPerCentimeter;
                    break;
            }

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
                        byte[] buf = frame.GetArray<byte>(ExifTag.XMP, true);
                        if (buf != null)
                        {
                            tiffMetadata.XmpProfile = buf;
                        }
                    }

                    if (coreMetadata.IptcProfile == null)
                    {
                        byte[] buf = frame.GetArray<byte>(ExifTag.IPTC, true);
                        if (buf != null)
                        {
                            coreMetadata.IptcProfile = new IptcProfile(buf);
                        }
                    }

                    if (coreMetadata.IccProfile == null)
                    {
                        byte[] buf = frame.GetArray<byte>(ExifTag.IccProfile, true);
                        if (buf != null)
                        {
                            coreMetadata.IccProfile = new IccProfile(buf);
                        }
                    }
                }
            }

            return coreMetadata;
        }

        private static TiffBitsPerPixel GetBitsPerPixel(TiffFrameMetadata firstFrameMetaData)
        {
            ushort[] bitsPerSample = firstFrameMetaData.BitsPerSample;
            var bitsPerPixel = 0;
            foreach (var bps in bitsPerSample)
            {
                bitsPerPixel += bps;
            }

            if (bitsPerPixel == 24)
            {
                return TiffBitsPerPixel.Pixel24;
            }
            else if (bitsPerPixel == 8)
            {
                return TiffBitsPerPixel.Pixel8;
            }
            else if (bitsPerPixel == 1)
            {
                return TiffBitsPerPixel.Pixel1;
            }

            return 0;
        }
    }
}
