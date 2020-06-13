// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Png.Chunks
{
    /// <summary>
    /// The pHYs chunk specifies the intended pixel size or aspect ratio for display of the image.
    /// </summary>
    internal readonly struct PhysicalChunkData
    {
        public const int Size = 9;

        public PhysicalChunkData(uint x, uint y, byte unitSpecifier)
        {
            this.XAxisPixelsPerUnit = x;
            this.YAxisPixelsPerUnit = y;
            this.UnitSpecifier = unitSpecifier;
        }

        /// <summary>
        /// Gets the number of pixels per unit on the X axis.
        /// </summary>
        public uint XAxisPixelsPerUnit { get; }

        /// <summary>
        /// Gets the number of pixels per unit on the Y axis.
        /// </summary>
        public uint YAxisPixelsPerUnit { get; }

        /// <summary>
        /// Gets the unit specifier.
        /// 0: unit is unknown
        /// 1: unit is the meter
        /// When the unit specifier is 0, the pHYs chunk defines pixel aspect ratio only; the actual size of the pixels remains unspecified.
        /// </summary>
        public byte UnitSpecifier { get; }

        /// <summary>
        /// Parses the PhysicalChunkData from the given buffer.
        /// </summary>
        /// <param name="data">The data buffer.</param>
        /// <returns>The parsed PhysicalChunkData.</returns>
        public static PhysicalChunkData Parse(ReadOnlySpan<byte> data)
        {
            uint hResolution = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(0, 4));
            uint vResolution = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4));
            byte unit = data[8];

            return new PhysicalChunkData(hResolution, vResolution, unit);
        }

        /// <summary>
        /// Constructs the PngPhysicalChunkData from the provided metadata.
        /// If the resolution units are not in meters, they are automatically converted.
        /// </summary>
        /// <param name="meta">The metadata.</param>
        /// <returns>The constructed PngPhysicalChunkData instance.</returns>
        public static PhysicalChunkData FromMetadata(ImageMetadata meta)
        {
            byte unitSpecifier = 0;
            uint x;
            uint y;

            switch (meta.ResolutionUnits)
            {
                case PixelResolutionUnit.AspectRatio:
                    unitSpecifier = 0; // Unspecified
                    x = (uint)Math.Round(meta.HorizontalResolution);
                    y = (uint)Math.Round(meta.VerticalResolution);
                    break;

                case PixelResolutionUnit.PixelsPerInch:
                    unitSpecifier = 1; // Per meter
                    x = (uint)Math.Round(UnitConverter.InchToMeter(meta.HorizontalResolution));
                    y = (uint)Math.Round(UnitConverter.InchToMeter(meta.VerticalResolution));
                    break;

                case PixelResolutionUnit.PixelsPerCentimeter:
                    unitSpecifier = 1; // Per meter
                    x = (uint)Math.Round(UnitConverter.CmToMeter(meta.HorizontalResolution));
                    y = (uint)Math.Round(UnitConverter.CmToMeter(meta.VerticalResolution));
                    break;

                default:
                    unitSpecifier = 1; // Per meter
                    x = (uint)Math.Round(meta.HorizontalResolution);
                    y = (uint)Math.Round(meta.VerticalResolution);
                    break;
            }

            return new PhysicalChunkData(x, y, unitSpecifier);
        }

        /// <summary>
        /// Writes the data to the given buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        public void WriteTo(Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(0, 4), this.XAxisPixelsPerUnit);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(4, 4), this.YAxisPixelsPerUnit);
            buffer[8] = this.UnitSpecifier;
        }
    }
}
