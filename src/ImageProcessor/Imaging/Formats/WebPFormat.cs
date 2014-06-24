// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WebPFormat.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides the necessary information to support webp images.
//   Adapted from <see cref="https://groups.google.com/a/webmproject.org/forum/#!topic/webp-discuss/1coeidT0rQU"/>
//   by Jose M. Piñeiro
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Formats
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Provides the necessary information to support webp images.
    /// Adapted from <see cref="http://groups.google.com/a/webmproject.org/forum/#!topic/webp-discuss/1coeidT0rQU"/>
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

            int imgWidth;
            int imgHeight;

            if (WebPGetInfo(ptrData, dataSize, out imgWidth, out imgHeight) != 1)
            {
                // TODO: Throw error?
                return null;
            }

            // Create a BitmapData and Lock all pixels to be written
            Bitmap bmp = new Bitmap(imgWidth, imgHeight, PixelFormat.Format24bppRgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, bmp.PixelFormat);

            // Allocate memory for uncompress image
            int outputBufferSize = bmpData.Stride * imgHeight;
            IntPtr outputBuffer = Marshal.AllocHGlobal(outputBufferSize);

            // Uncompress the image
            outputBuffer = WebPDecodeBGRInto(ptrData, dataSize, outputBuffer, outputBufferSize, bmpData.Stride);

            // Write image to bitmap using Marshal
            byte[] buffer = new byte[outputBufferSize];
            Marshal.Copy(outputBuffer, buffer, 0, outputBufferSize);
            Marshal.Copy(buffer, 0, bmpData.Scan0, outputBufferSize);

            // Unlock the pixels
            bmp.UnlockBits(bmpData);

            // Free memory
            pinnedWebP.Free();
            Marshal.FreeHGlobal(outputBuffer);

            return bmp;
        }

        /// <summary>
        /// Lossly encodes the image in bitmap.
        /// </summary>
        /// <param name="bitmap">
        /// Bitmap with the image
        /// </param>
        /// <param name="quality">
        /// Quality. 0 = minimum ... 100 = maximimun quality
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

            try
            {
                BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                IntPtr unmanagedData;
                int size = WebPEncodeBGR(bmpData.Scan0, bitmap.Width, bitmap.Height, bmpData.Stride, quality, out unmanagedData);

                // Copy image compress data to output array
                webpData = new byte[size];
                Marshal.Copy(unmanagedData, webpData, 0, size);

                // Unlock the pixels
                bitmap.UnlockBits(bmpData);

                // Free memory
                WebPFree(unmanagedData);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validate the WebP image header and retrieve the image height and width. Pointers *width and *height can be passed NULL if deemed irrelevant
        /// </summary>
        /// <param name="data">
        /// Pointer to WebP image data
        /// </param>
        /// <param name="dataSize">
        /// This is the size of the memory block pointed to by data containing the image data
        /// </param>
        /// <param name="width">
        /// The width range is limited currently from 1 to 16383
        /// </param>
        /// <param name="height">
        /// The height range is limited currently from 1 to 16383
        /// </param>
        /// <returns>
        /// 1 if success, otherwise error code returned in the case of (a) formatting error(s).
        /// </returns>
        [DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int WebPGetInfo(IntPtr data, uint dataSize, out int width, out int height);

        /// <summary>
        /// Decode WEBP image pointed to by *data and returns BGR samples into a pre-allocated buffer
        /// </summary>
        /// <param name="data">
        /// Pointer to WebP image data
        /// </param>
        /// <param name="dataSize">
        /// This is the size of the memory block pointed to by data containing the image data
        /// </param>
        /// <param name="outputBuffer">
        /// Pointer to decoded WebP image
        /// </param>
        /// <param name="outputBufferSize">
        /// Size of allocated buffer
        /// </param>
        /// <param name="outputStride">
        /// Specifies the distance between scanlines
        /// </param>
        /// <returns>
        /// output_buffer if function succeeds; NULL otherwise
        /// </returns>
        [DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr WebPDecodeBGRInto(IntPtr data, uint dataSize, IntPtr outputBuffer, int outputBufferSize, int outputStride);

        /// <summary>
        /// Lossless encoding images pointed to by *data in WebP format
        /// </summary>
        /// <param name="rgb">
        /// Pointer to RGB image data
        /// </param>
        /// <param name="width">
        /// The width range is limited currently from 1 to 16383
        /// </param>
        /// <param name="height">
        /// The height range is limited currently from 1 to 16383
        /// </param>
        /// <param name="stride">
        /// The stride.
        /// </param>
        /// <param name="qualityFactor">
        /// Ranges from 0 (lower quality) to 100 (highest quality). Controls the loss and quality during compression
        /// </param>
        /// <param name="output">
        /// output_buffer with WebP image
        /// </param>
        /// <returns>
        /// Size of WebP Image
        /// </returns>
        [DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int WebPEncodeBGR(IntPtr rgb, int width, int height, int stride, float qualityFactor, out IntPtr output);

        /// <summary>
        /// Frees the unmanaged memory.
        /// </summary>
        /// <param name="p">
        /// The pointer.
        /// </param>
        /// <returns>
        /// 1 if success, otherwise error code returned in the case of (a) error(s).
        /// </returns>
        [DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int WebPFree(IntPtr p);
    }
}