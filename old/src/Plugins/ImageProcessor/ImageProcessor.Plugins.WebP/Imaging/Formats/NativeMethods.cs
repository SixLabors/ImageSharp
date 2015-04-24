// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NativeMethods.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides access to unmanaged native methods.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Plugins.WebP.Imaging.Formats
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;

    using ImageProcessor.Configuration;

    /// <summary>
    /// Provides access to unmanaged native methods.
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>
        /// Initializes static members of the <see cref="NativeMethods"/> class.
        /// </summary>
        static NativeMethods()
        {
            string folder = ImageProcessorBootstrapper.Instance.NativeBinaryFactory.Is64BitEnvironment ? "x64" : "x86";
            string name = string.Format("ImageProcessor.Plugins.WebP.Resources.Unmanaged.{0}.libwebp.dll", folder);
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    if (stream != null)
                    {
                        stream.CopyTo(memoryStream);
                        ImageProcessorBootstrapper.Instance.NativeBinaryFactory.RegisterNativeBinary("libwebp.dll", memoryStream.ToArray());
                    }
                }
            }
        }

        #region WebP
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
        [DllImport("libwebp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPGetInfo")]
        public static extern int WebPGetInfo(IntPtr data, uint dataSize, out int width, out int height);

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
        /// Specifies the distance between scan-lines
        /// </param>
        /// <returns>
        /// output_buffer if function succeeds; NULL otherwise
        /// </returns>
        [DllImport("libwebp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecodeBGRAInto")]
        public static extern IntPtr WebPDecodeBGRAInto(IntPtr data, uint dataSize, IntPtr outputBuffer, int outputBufferSize, int outputStride);

        /// <summary>
        /// Lossy encoding images pointed to by *data in WebP format
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
        [DllImport("libwebp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeBGRA")]
        public static extern int WebPEncodeBGRA(IntPtr rgb, int width, int height, int stride, float qualityFactor, out IntPtr output);

        /// <summary>
        /// Frees the unmanaged memory.
        /// </summary>
        /// <param name="pointer">
        /// The pointer.
        /// </param>
        /// <returns>
        /// 1 if success, otherwise error code returned in the case of (a) error(s).
        /// </returns>
        [DllImport("libwebp", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPFree")]
        public static extern int WebPFree(IntPtr pointer);
        #endregion
    }
}
