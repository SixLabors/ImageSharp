// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DecompressorToJpegImage.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
// Decompresses a jpeg image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace BitMiracle.LibJpeg
{
    using System;
    using System.IO;

    using ImageProcessor;

    /// <summary>
    /// Decompresses a jpeg image.
    /// </summary>
    internal class DecompressorToJpegImage : IDecompressDestination
    {
        /// <summary>
        /// The jpeg image.
        /// </summary>
        private readonly JpegImage jpegImage;

        /// <summary>
        /// Initializes a new instance of the <see cref="DecompressorToJpegImage"/> class.
        /// </summary>
        /// <param name="jpegImage">
        /// The jpeg image.
        /// </param>
        internal DecompressorToJpegImage(JpegImage jpegImage)
        {
            this.jpegImage = jpegImage;
        }

        /// <summary>
        /// Gets the stream with decompressed data.
        /// </summary>
        public Stream Output => null;

        /// <summary>
        /// Sets the image attributes.
        /// </summary>
        /// <param name="parameters">
        /// The <see cref="LoadedImageAttributes"/> containing attributes.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// </exception>
        public void SetImageAttributes(LoadedImageAttributes parameters)
        {
            if (parameters.Width > ImageBase.MaxWidth || parameters.Height > ImageBase.MaxHeight)
            {
                throw new ArgumentOutOfRangeException(
                    $"The input jpg '{ parameters.Width }x{ parameters.Height }' is bigger then the max allowed size '{ ImageBase.MaxWidth }x{ ImageBase.MaxHeight }'");
            }

            this.jpegImage.Width = parameters.Width;
            this.jpegImage.Height = parameters.Height;
            this.jpegImage.BitsPerComponent = 8;
            this.jpegImage.ComponentsPerSample = (byte)parameters.ComponentsPerSample;
            this.jpegImage.Colorspace = parameters.Colorspace;
        }

        /// <summary>
        /// Begins writing.
        /// </summary>
        /// <remarks>Not implemented.</remarks>
        public void BeginWrite()
        {
        }

        /// <summary>
        /// Processes the given row of pixels.
        /// </summary>
        /// <param name="row">
        /// The <see cref="T:byte[]"/> representing the row.
        /// </param>
        public void ProcessPixelsRow(byte[] row)
        {
            SampleRow samplesRow = new SampleRow(row, this.jpegImage.Width, this.jpegImage.BitsPerComponent, this.jpegImage.ComponentsPerSample);
            this.jpegImage.addSampleRow(samplesRow);
        }

        /// <summary>
        /// Ends write.
        /// </summary>
        /// <remarks>Not implemented.</remarks>
        public void EndWrite()
        {
        }
    }
}
