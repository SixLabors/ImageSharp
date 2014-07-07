// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NativeMethods.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides access to unmanaged native methods.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Formats
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Provides access to unmanaged native methods.
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>
        /// Whether the process is running in 64bit mode. Used for calling the correct dllimport method.
        /// Clunky I know but I couldn't get dynamic methods to work.
        /// </summary>
        private static readonly bool Is64Bit = Environment.Is64BitProcess;

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
        public static int WebPGetInfo(IntPtr data, uint dataSize, out int width, out int height)
        {
            return Is64Bit ? WebPGetInfo64(data, dataSize, out width, out height)
                           : WebPGetInfo86(data, dataSize, out width, out height);
        }

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
        public static IntPtr WebPDecodeBGRAInto(IntPtr data, uint dataSize, IntPtr outputBuffer, int outputBufferSize, int outputStride)
        {
            return Is64Bit ? WebPDecodeBGRAInto64(data, dataSize, outputBuffer, outputBufferSize, outputStride)
                           : WebPDecodeBGRAInto86(data, dataSize, outputBuffer, outputBufferSize, outputStride);
        }

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
        public static int WebPEncodeBGRA(IntPtr rgb, int width, int height, int stride, float qualityFactor, out IntPtr output)
        {
            return Is64Bit ? WebPEncodeBGRA64(rgb, width, height, stride, qualityFactor, out output)
                           : WebPEncodeBGRA86(rgb, width, height, stride, qualityFactor, out output);
        }

        /// <summary>
        /// Frees the unmanaged memory.
        /// </summary>
        /// <param name="pointer">
        /// The pointer.
        /// </param>
        /// <returns>
        /// 1 if success, otherwise error code returned in the case of (a) error(s).
        /// </returns>
        public static int WebPFree(IntPtr pointer)
        {
            return Is64Bit ? WebPFree64(pointer) : WebPFree86(pointer);
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
        [DllImport("x86\\imageprocessor.libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPGetInfo")]
        private static extern int WebPGetInfo86(IntPtr data, uint dataSize, out int width, out int height);

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
        [DllImport("x64\\imageprocessor.libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPGetInfo")]
        private static extern int WebPGetInfo64(IntPtr data, uint dataSize, out int width, out int height);

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
        [DllImport("x86\\imageprocessor.libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecodeBGRAInto")]
        private static extern IntPtr WebPDecodeBGRAInto86(IntPtr data, uint dataSize, IntPtr outputBuffer, int outputBufferSize, int outputStride);

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
        [DllImport("x64\\imageprocessor.libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPDecodeBGRAInto")]
        private static extern IntPtr WebPDecodeBGRAInto64(IntPtr data, uint dataSize, IntPtr outputBuffer, int outputBufferSize, int outputStride);

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
        [DllImport("x86\\imageprocessor.libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeBGRA")]
        private static extern int WebPEncodeBGRA86(IntPtr rgb, int width, int height, int stride, float qualityFactor, out IntPtr output);

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
        [DllImport("x64\\imageprocessor.libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPEncodeBGRA")]
        private static extern int WebPEncodeBGRA64(IntPtr rgb, int width, int height, int stride, float qualityFactor, out IntPtr output);

        /// <summary>
        /// Frees the unmanaged memory.
        /// </summary>
        /// <param name="pointer">
        /// The pointer.
        /// </param>
        /// <returns>
        /// 1 if success, otherwise error code returned in the case of (a) error(s).
        /// </returns>
        [DllImport("x86\\imageprocessor.libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPFree")]
        private static extern int WebPFree86(IntPtr pointer);

        /// <summary>
        /// Frees the unmanaged memory.
        /// </summary>
        /// <param name="pointer">
        /// The pointer.
        /// </param>
        /// <returns>
        /// 1 if success, otherwise error code returned in the case of (a) error(s).
        /// </returns>
        [DllImport("x64\\imageprocessor.libwebp.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebPFree")]
        private static extern int WebPFree64(IntPtr pointer);
        #endregion
    }
}
