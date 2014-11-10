// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LocalFileImageService.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The local file image service for retrieving images from the file system.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using System.Web;

    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// The local file image service for retrieving images from the file system.
    /// </summary>
    public class LocalFileImageService : IImageService
    {
        /// <summary>
        /// The prefix for the given implementation.
        /// </summary>
        private string prefix = string.Empty;

        /// <summary>
        /// Gets or sets the prefix for the given implementation.
        /// <remarks>
        /// This value is used as a prefix for any image requests that should use this service.
        /// </remarks>
        /// </summary>
        public string Prefix
        {
            get
            {
                return this.prefix;
            }

            set
            {
                this.prefix = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the image service requests files from
        /// the locally based file system.
        /// </summary>
        public bool IsFileLocalService
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets or sets any additional settings required by the service.
        /// </summary>
        public Dictionary<string, string> Settings { get; set; }

        /// <summary>
        /// Gets or sets the white list of <see cref="System.Uri"/>. 
        /// </summary>
        public Uri[] WhiteList { get; set; }

        /// <summary>
        /// Gets a value indicating whether the current request passes sanitizing rules.
        /// </summary>
        /// <param name="path">
        /// The image path.
        /// </param>
        /// <returns>
        /// <c>True</c> if the request is valid; otherwise, <c>False</c>.
        /// </returns>
        public bool IsValidRequest(string path)
        {
            return ImageHelpers.IsValidImageExtension(path);
        }

        /// <summary>
        /// Gets the image using the given identifier.
        /// </summary>
        /// <param name="id">
        /// The value identifying the image to fetch.
        /// </param>
        /// <returns>
        /// The <see cref="System.Byte"/> array containing the image data.
        /// </returns>
        public async Task<byte[]> GetImage(object id)
        {
            string path = id.ToString();
            byte[] buffer;

            // Check to see if the file exists.
            // ReSharper disable once AssignNullToNotNullAttribute
            FileInfo fileInfo = new FileInfo(path);

            if (!fileInfo.Exists)
            {
                throw new HttpException(404, "No image exists at " + path);
            }

            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            {
                buffer = new byte[file.Length];
                await file.ReadAsync(buffer, 0, (int)file.Length);
            }

            return buffer;
        }
    }
}
