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
    using System.Web;

    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// The remote image service.
    /// </summary>
    public class RemoteImageService : IImageService
    {
        /// <summary>
        /// The prefix for the given implementation.
        /// </summary>
        private string prefix = "remote.axd";

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteImageService"/> class.
        /// </summary>
        public RemoteImageService()
        {
            this.Settings = new Dictionary<string, string>
            {
                { "MaxBytes", "4194304" }, 
                { "Timeout", "30000" },
                { "Protocol", "http" }
            };

            this.WhiteList = new Uri[] { };
        }

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
                return false;
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
            // Check the url is from a whitelisted location.
            Uri url = new Uri(path);
            string upper = url.Host.ToUpperInvariant();

            // Check for root or sub domain.
            bool validUrl = false;
            foreach (Uri uri in this.WhiteList)
            {
                if (!uri.IsAbsoluteUri)
                {
                    Uri rebaseUri = new Uri("http://" + uri.ToString().TrimStart('.', '/'));
                    validUrl = upper.StartsWith(rebaseUri.Host.ToUpperInvariant()) || upper.EndsWith(rebaseUri.Host.ToUpperInvariant());
                }
                else
                {
                    validUrl = upper.StartsWith(uri.Host.ToUpperInvariant()) || upper.EndsWith(uri.Host.ToUpperInvariant());
                }

                if (validUrl)
                {
                    break;
                }
            }

            return validUrl;
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
            Uri uri = new Uri(id.ToString());
            RemoteFile remoteFile = new RemoteFile(uri)
            {
                MaxDownloadSize = int.Parse(this.Settings["MaxBytes"]),
                TimeoutLength = int.Parse(this.Settings["Timeout"])
            };

            byte[] buffer;

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
                        else
                        {
                            throw new HttpException(404, "No image exists at " + uri);
                        }
                    }
                }
            }

            return buffer;
        }
    }
}
