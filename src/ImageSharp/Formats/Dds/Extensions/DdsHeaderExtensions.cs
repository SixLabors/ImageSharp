// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Dds.Emums;

namespace SixLabors.ImageSharp.Formats.Dds.Extensions
{
    internal static class DdsHeaderExtensions
    {
        /// <summary>
        /// Gets a value indicating whether determines whether this resource is a cubemap.
        /// </summary>
        /// <returns>
        /// <c>true</c> if this resource is a cubemap; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsCubemap(this DdsHeader ddsHeader)
        {
            return (ddsHeader.Caps2 & DdsCaps2.Cubemap) != 0;
        }

        /// <summary>
        /// Gets a value indicating whether determines whether this resource is a volume texture.
        /// </summary>
        /// <returns>
        /// <c>true</c> if this resource is a volume texture; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsVolumeTexture(this DdsHeader ddsHeader)
        {
            return (ddsHeader.Caps2 & DdsCaps2.Volume) != 0;
        }

        /// <summary>
        /// Gets a value indicating whether determines whether this resource contains compressed surface data.
        /// </summary>
        /// <value>
        /// <c>true</c> if this resource contains compressed surface data; otherwise, <c>false</c>.
        /// </value>
        public static bool IsCompressed(this DdsHeader ddsHeader)
        {
            return (ddsHeader.Flags & DdsFlags.LinearSize) != 0;
        }

        /// <summary>
        /// Gets a value indicating whether determines whether this resource contains alpha data.
        /// </summary>
        /// <value>
        /// <c>true</c> if this resource contains alpha data; otherwise, <c>false</c>.
        /// </value>
        public static bool HasAlpha(this DdsHeader ddsHeader)
        {
            return (ddsHeader.PixelFormat.Flags & DdsPixelFormatFlags.AlphaPixels) != 0;
        }

        /// <summary>
        /// Gets a value indicating whether determines whether this resource contains mipmap data.
        /// </summary>
        /// <value>
        /// <c>true</c> if this resource contains mipmap data; otherwise, <c>false</c>.
        /// </value>
        public static bool HasMipmaps(this DdsHeader ddsHeader)
        {
            return (ddsHeader.Caps1 & DdsCaps1.MipMap) != 0 && (ddsHeader.Flags & DdsFlags.MipMapCount) != 0;
        }

        /// <summary>
        /// Checks if dds resource should have the <see cref="DdsHeaderDxt10" /> header.
        /// </summary>
        /// <returns>
        /// <c>true</c> if dds resource should have the <see cref="DdsHeaderDxt10" /> header;
        /// otherwise <c>false</c>.
        /// </returns>
        public static bool ShouldHaveDxt10Header(this DdsHeader ddsHeader)
        {
            return (ddsHeader.PixelFormat.Flags == DdsPixelFormatFlags.FourCC) && (ddsHeader.PixelFormat.FourCC == DdsFourCc.DX10);
        }

        /// <summary>
        /// Determines whether width and height flags are set, so <see cref="DdsHeader.Width" /> and
        /// <see cref="DdsHeader.Height" /> contain valid values.
        /// </summary>
        /// <returns>
        /// <c>true</c> if dimensions flags are set; otherwise, <c>false</c>.
        /// </returns>
        public static bool AreDimensionsSet(this DdsHeader ddsHeader)
        {
            return (ddsHeader.Flags & DdsFlags.Width) != 0 && (ddsHeader.Flags & DdsFlags.Height) != 0;
        }

        /// <summary>
        /// Returns either depth of a volume texture in pixels, amount of faces in a cubemap or
        /// a 1 for a flat resource.
        /// </summary>
        /// <returns>
        /// Actual depth of a resource.
        /// </returns>
        public static int ComputeDepth(this DdsHeader ddsHeader)
        {
            int result = 1;
            if (ddsHeader.IsVolumeTexture())
            {
                result = (int)ddsHeader.Depth;
            }
            else if (ddsHeader.IsCubemap())
            {
                result = 0;

                // Partial cubemaps are not supported by Direct3D >= 11, but lets support them for the legacy sake.
                // So cubemaps can store up to 6 faces:
                if ((ddsHeader.Caps2 & DdsCaps2.CubemapPositiveX) != 0)
                {
                    result++;
                }

                if ((ddsHeader.Caps2 & DdsCaps2.CubemapNegativeX) != 0)
                {
                    result++;
                }

                if ((ddsHeader.Caps2 & DdsCaps2.CubemapPositiveY) != 0)
                {
                    result++;
                }

                if ((ddsHeader.Caps2 & DdsCaps2.CubemapNegativeY) != 0)
                {
                    result++;
                }

                if ((ddsHeader.Caps2 & DdsCaps2.CubemapPositiveZ) != 0)
                {
                    result++;
                }

                if ((ddsHeader.Caps2 & DdsCaps2.CubemapNegativeZ) != 0)
                {
                    result++;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the existing cube map faces, if this header represents a cube map.
        /// </summary>
        /// <returns>
        /// Types of cube map faces stored in this cube map or null if this is not a cubemap.
        /// </returns>
        public static DdsSurfaceType[] GetExistingCubemapFaces(this DdsHeader ddsHeader)
        {
            int depth = ddsHeader.ComputeDepth();
            var result = new DdsSurfaceType[depth];
            int index = 0;

            if (depth > 0)
            {
                if ((ddsHeader.Caps2 & DdsCaps2.CubemapPositiveX) != 0)
                {
                    result[index++] = DdsSurfaceType.CubemapPositiveX;
                }

                if ((ddsHeader.Caps2 & DdsCaps2.CubemapNegativeX) != 0)
                {
                    result[index++] = DdsSurfaceType.CubemapNegativeX;
                }

                if ((ddsHeader.Caps2 & DdsCaps2.CubemapPositiveY) != 0)
                {
                    result[index++] = DdsSurfaceType.CubemapPositiveY;
                }

                if ((ddsHeader.Caps2 & DdsCaps2.CubemapNegativeY) != 0)
                {
                    result[index++] = DdsSurfaceType.CubemapNegativeY;
                }

                if ((ddsHeader.Caps2 & DdsCaps2.CubemapPositiveZ) != 0)
                {
                    result[index++] = DdsSurfaceType.CubemapPositiveZ;
                }

                if ((ddsHeader.Caps2 & DdsCaps2.CubemapNegativeZ) != 0)
                {
                    result[index++] = DdsSurfaceType.CubemapNegativeZ;
                }
            }

            return (index > 0) ? result : null;
        }
    }
}
