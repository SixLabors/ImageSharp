// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WebPFormat.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides the necessary information to support webp images.
//   Adapted from <see href="https://groups.google.com/a/webmproject.org/forum/#!topic/webp-discuss/1coeidT0rQU"/>
//   by Jose M. Piñeiro
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Plugins.WebP.Imaging.Formats
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging.Formats;

    /// <summary>
    /// Provides the necessary information to support webp images.
    /// Adapted from <see href="http://groups.google.com/a/webmproject.org/forum/#!topic/webp-discuss/1coeidT0rQU"/>
    /// by Jose M. Piñeiro
    /// </summary>
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "Reviewed. Suppression is OK here.")]
    public class WebPFormat : FormatBase
    {
        /// <summary>
        /// Gets the file headers.
        /// </summary>
        public override byte[][] FileHeaders
        {
            get
            {
                return new[] { Encoding.ASCII.GetBytes("RIFF") };
            }
        }

        /// <summary>
        /// Gets the list of file extensions.
        /// </summary>
        public override string[] FileExtensions
        {
            get
            {
                return new[] { "webp" };
            }
        }

        /// <summary>
        /// Gets the standard identifier used on the Internet to indicate the type of data that a file contains.
        /// </summary>
        public override string MimeType
        {
            get
            {
                return "image/webp";
            }
        }

        /// <summary>
        /// Gets the file format of the image. 
        /// </summary>
        public override ImageFormat ImageFormat
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Applies the given processor the current image.
        /// </summary>
        /// <param name="processor">The processor delegate.</param>
        /// <param name="factory">The <see cref="ImageFactory" />.</param>
        public override void ApplyProcessor(Func<ImageFactory, Image> processor, ImageFactory factory)
        {
            base.ApplyProcessor(processor, factory);

            // Set the property item information from any Exif metadata.
            // We do this here so that they can be changed between processor methods.
            if (factory.PreserveExifData)
            {
                foreach (KeyValuePair<int, PropertyItem> propertItem in factory.ExifPropertyItems)
                {
                    factory.Image.SetPropertyItem(propertItem.Value);
                }
            }
        }

        /// <summary>
        /// Decodes the image to process.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="T:System.IO.stream" /> containing the image information.
        /// </param>
        /// <returns>
        /// The <see cref="T:System.Drawing.Image" />.
        /// </returns>
        public override Image Load(Stream stream)
        {
            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);
            return Decode(bytes);
        }

        /// <summary>
        /// Saves the current image to the specified file path.
        /// </summary>
        /// <param name="path">The path to save the image to.</param>
        /// <param name="image">The 
        /// <see cref="T:System.Drawing.Image" /> to save.</param>
        /// <returns>
        /// The <see cref="T:System.Drawing.Image" />.
        /// </returns>
        public override Image Save(string path, Image image)
        {
            byte[] bytes;

            // Encode in webP format.
            if (EncodeLossly((Bitmap)image, this.Quality, out bytes))
            {
                File.WriteAllBytes(path, bytes);
            }

            return image;
        }

        /// <summary>
        /// Saves the current image to the specified output stream.
        /// </summary>
        /// <param name="stream">The <see cref="T:System.IO.Stream" /> to save the image information to.</param>
        /// <param name="image">The <see cref="T:System.Drawing.Image" /> to save.</param>
        /// <returns>
        /// The <see cref="T:System.Drawing.Image" />.
        /// </returns>
        public override Image Save(Stream stream, Image image)
        {
            byte[] bytes;

            // Encode in webP format.
            if (EncodeLossly((Bitmap)image, this.Quality, out bytes))
            {
                using (MemoryStream memoryStream = new MemoryStream(bytes))
                {
                    memoryStream.CopyTo(stream);
                    memoryStream.Position = stream.Position = 0;
                }
            }
            else
            {
                throw new ImageFormatException("Unable to encode WebP image.");
            }

            return image;
        }

        /// <summary>
        /// Decodes a WebP image
        /// </summary>
        /// <param name="webpData">
        /// The data to uncompress
        /// </param>
        /// <returns>
        /// The <see cref="T:System.Drawing.Bitmap" />.
        /// </returns>
        private static Bitmap Decode(byte[] webpData)
        {
            // Get the image width and height
            GCHandle pinnedWebP = GCHandle.Alloc(webpData, GCHandleType.Pinned);
            IntPtr ptrData = pinnedWebP.AddrOfPinnedObject();
            uint dataSize = (uint)webpData.Length;

            Bitmap bitmap = null;
            BitmapData bitmapData = null;
            IntPtr outputBuffer = IntPtr.Zero;
            int width;
            int height;

            if (NativeMethods.WebPGetInfo(ptrData, dataSize, out width, out height) != 1)
            {
                throw new ImageFormatException("WebP image header is corrupted.");
            }

            try
            {
                // Create a BitmapData and Lock all pixels to be written
                bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);

                // Allocate memory for uncompress image
                int outputBufferSize = bitmapData.Stride * height;
                outputBuffer = Marshal.AllocHGlobal(outputBufferSize);

                // Uncompress the image
                outputBuffer = NativeMethods.WebPDecodeBGRAInto(ptrData, dataSize, outputBuffer, outputBufferSize, bitmapData.Stride);

                // Write image to bitmap using Marshal
                byte[] buffer = new byte[outputBufferSize];
                Marshal.Copy(outputBuffer, buffer, 0, outputBufferSize);
                Marshal.Copy(buffer, 0, bitmapData.Scan0, outputBufferSize);
            }
            finally
            {
                // Unlock the pixels
                if (bitmap != null)
                {
                    bitmap.UnlockBits(bitmapData);
                }

                // Free memory
                pinnedWebP.Free();
                Marshal.FreeHGlobal(outputBuffer);
            }

            return bitmap;
        }

        /// <summary>
        /// Lossy encodes the image in bitmap.
        /// </summary>
        /// <param name="bitmap">
        /// Bitmap with the image
        /// </param>
        /// <param name="quality">
        /// Quality. 0 = minimum ... 100 = maximum quality
        /// </param>
        /// <param name="webpData">
        /// The byte array containing the encoded image data.
        /// </param>
        /// <returns>
        /// True if success; False otherwise
        /// </returns>
        private static bool EncodeLossly(Bitmap bitmap, int quality, out byte[] webpData)
        {
            webpData = null;
            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            IntPtr unmanagedData = IntPtr.Zero;
            bool encoded;

            try
            {
                // Attempt to lossy encode the image.
                int size = NativeMethods.WebPEncodeBGRA(bmpData.Scan0, bitmap.Width, bitmap.Height, bmpData.Stride, quality, out unmanagedData);

                // Copy image compress data to output array
                webpData = new byte[size];
                Marshal.Copy(unmanagedData, webpData, 0, size);
                encoded = true;
            }
            catch
            {
                encoded = false;
            }
            finally
            {
                // Unlock the pixels
                bitmap.UnlockBits(bmpData);

                // Free memory
                NativeMethods.WebPFree(unmanagedData);
            }

            return encoded;
        }
    }
}