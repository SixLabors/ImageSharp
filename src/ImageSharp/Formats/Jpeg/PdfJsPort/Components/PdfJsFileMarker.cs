// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Represents a jpeg file marker
    /// </summary>
    internal readonly struct PdfJsFileMarker
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PdfJsFileMarker"/> struct.
        /// </summary>
        /// <param name="marker">The marker</param>
        /// <param name="position">The position within the stream</param>
        public PdfJsFileMarker(byte marker, long position)
        {
            this.Marker = marker;
            this.Position = position;
            this.Invalid = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfJsFileMarker"/> struct.
        /// </summary>
        /// <param name="marker">The marker</param>
        /// <param name="position">The position within the stream</param>
        /// <param name="invalid">Whether the current marker is invalid</param>
        public PdfJsFileMarker(byte marker, long position, bool invalid)
        {
            this.Marker = marker;
            this.Position = position;
            this.Invalid = invalid;
        }

        /// <summary>
        /// Gets a value indicating whether the current marker is invalid
        /// </summary>
        public bool Invalid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <summary>
        /// Gets the position of the marker within a stream
        /// </summary>
        public byte Marker
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <summary>
        /// Gets the position of the marker within a stream
        /// </summary>
        public long Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.Marker.ToString("X");
        }
    }
}