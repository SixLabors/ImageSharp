// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RemoteImageService.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The remote image service.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;

    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// The remote image service.
    /// </summary>
    public class RemoteImageService : IImageService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteImageService"/> class.
        /// </summary>
        public RemoteImageService()
        {
            this.Settings = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets the key for the given implementation.
        /// <remarks>
        /// This value is used as a prefix for any image requests that should use this service.
        /// </remarks>
        /// </summary>
        public string Key
        {
            get
            {
                return "remote.axd";
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
                return false;
            }
        }

        /// <summary>
        /// Gets or sets any additional settings required by the service.
        /// </summary>
        public Dictionary<string, string> Settings { get; set; }

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
            Uri uri = new Uri(id.ToString());
            RemoteFile remoteFile = new RemoteFile(uri, false);
            byte[] buffer = { };

            // Prevent response blocking.
            WebResponse webResponse = await remoteFile.GetWebResponseAsync().ConfigureAwait(false);

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (WebResponse response = webResponse)
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        if (responseStream != null)
                        {
                            responseStream.CopyTo(memoryStream);

                            // Reset the position of the stream to ensure we're reading the correct part.
                            memoryStream.Position = 0;

                            buffer = memoryStream.ToArray();
                        }
                    }
                }
            }

            return buffer;
        }
    }
}
