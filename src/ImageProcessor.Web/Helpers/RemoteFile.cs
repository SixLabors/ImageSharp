// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RemoteFile.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods used to download files from a website address.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Security;
    using System.Threading.Tasks;
    using System.Web;

    /// <summary>
    /// Encapsulates methods used to download files from a website address.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The purpose of this class is so there's one core way of downloading remote files with url[s] that are from
    /// outside users. There's various areas in application where an attacker could supply an external url to the server
    /// and tie up resources.
    /// </para>
    /// For example, the ImageProcessingModule accepts off-server addresses as a path. An attacker could, for instance, pass the url
    /// to a file that's a few gigs in size, causing the server to get out-of-memory exceptions or some other errors. An attacker
    /// could also use this same method to use one application instance to hammer another site by, again, passing an off-server
    /// address of the victims site to the ImageProcessingModule. 
    /// This class will not throw an exception if the Uri supplied points to a resource local to the running application instance.
    /// <para>
    /// There shouldn't be any security issues there, as the internal WebRequest instance is still calling it remotely. 
    /// Any local files that shouldn't be accessed by this won't be allowed by the remote call.
    /// </para>
    /// Adapted from <see href="http://blogengine.codeplex.com">BlogEngine.Net</see>
    /// </remarks>
    internal sealed class RemoteFile
    {
        #region Fields
        /// <summary>
        /// The <see cref="T:System.Uri">Uri</see> of the remote file being downloaded.
        /// </summary>
        private readonly Uri url;

        /// <summary>
        /// The maximum allowable download size in bytes.
        /// </summary>
        private int maxDownloadSize;

        /// <summary>
        /// The length of time, in milliseconds, that a remote file download attempt can last before timing out.
        /// </summary>
        private int timeoutLength;

        /// <summary>
        /// The <see cref="T:System.Net.WebResponse">WebResponse</see> object used internally for this RemoteFile instance.
        /// </summary>
        private WebRequest webRequest;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="T:ImageProcessor.Web.Helpers.RemoteFile">RemoteFile</see> class. 
        /// </summary>
        /// <param name="filePath">The url of the file to be downloaded.</param>
        internal RemoteFile(Uri filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException("filePath");
            }

            this.url = filePath;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the Uri of the remote file being downloaded.
        /// </summary>
        public Uri Uri
        {
            get
            {
                return this.url;
            }
        }

        /// <summary>
        /// Gets or sets the length of time, in milliseconds, that a remote file download attempt can 
        /// last before timing out.
        /// <remarks>
        /// <para>
        /// This value can only be set if the instance is supposed to ignore the remote download settings set
        /// in the current application instance. 
        /// </para>
        /// <para>
        /// Set this value to 0 if there should be no timeout.
        /// </para>
        /// </remarks>
        /// </summary>
        public int TimeoutLength
        {
            get
            {
                return this.timeoutLength;
            }

            set
            {
                if (value < 0)
                {
                    // ReSharper disable once NotResolvedInText
                    throw new ArgumentOutOfRangeException("TimeoutLength");
                }

                this.timeoutLength = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum download size, in bytes, that a remote file download attempt can be.
        /// <remarks>
        /// <para>
        /// This value can only be set if the instance is supposed to ignore the remote download settings set
        /// in the current application instance. 
        /// </para>
        /// <para>
        /// Set this value to 0 if there should be no max bytes.
        /// </para>
        /// </remarks>
        /// </summary>
        public int MaxDownloadSize
        {
            get
            {
                return this.maxDownloadSize;
            }

            set
            {
                if (value < 0)
                {
                    // ReSharper disable once NotResolvedInText
                    throw new ArgumentOutOfRangeException("MaxDownloadSize");
                }

                this.maxDownloadSize = value;
            }
        }
        #endregion

        #region Methods
        #region Internal
        /// <summary>
        /// Returns the <see cref="T:System.Net.WebResponse">WebResponse</see> used to download this file.
        /// <remarks>
        /// <para>
        /// This method is meant for outside users who need specific access to the WebResponse this class
        /// generates. They're responsible for disposing of it.
        /// </para>
        /// </remarks>
        /// </summary>
        /// <returns>The <see cref="T:System.Net.WebResponse">WebResponse</see> used to download this file.</returns>
        /// <returns>
        /// The <see cref="IEnumerable{Task}"/>.
        /// </returns>
        internal async Task<WebResponse> GetWebResponseAsync()
        {
            WebResponse response = null;
            try
            {
                response = await this.GetWebRequest().GetResponseAsync();
            }
            catch (WebException ex)
            {
                if (response != null)
                {
                    HttpWebResponse errorResponse = (HttpWebResponse)ex.Response;
                    if (errorResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        throw new HttpException(404, "No image exists at " + this.Uri);
                    }
                }

                throw;
            }

            if (response != null)
            {
                long contentLength = response.ContentLength;

                // WebResponse.ContentLength doesn't always know the value, it returns -1 in this case.
                if (contentLength == -1)
                {
                    // Response headers may still have the Content-Length inside of it.
                    string headerContentLength = response.Headers["Content-Length"];

                    if (!string.IsNullOrWhiteSpace(headerContentLength))
                    {
                        contentLength = long.Parse(headerContentLength, CultureInfo.InvariantCulture);
                    }
                }

                // We don't need to check the url here since any external urls are available only from the web.config.
                if ((this.MaxDownloadSize > 0) && (contentLength > this.MaxDownloadSize))
                {
                    response.Close();
                    throw new SecurityException("An attempt to download a remote file has been halted because the file is larger than allowed.");
                }
            }

            return response;
        }
        #endregion

        #region Private
        /// <summary>
        /// Creates the WebRequest object used internally for this RemoteFile instance.
        /// </summary>
        /// <returns>
        /// <para>
        /// The WebRequest should not be passed outside of this instance, as it will allow tampering. Anyone
        /// that needs more fine control over the downloading process should probably be using the WebRequest
        /// class on its own.
        /// </para>
        /// </returns>
        private WebRequest GetWebRequest()
        {
            if (this.webRequest == null)
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(this.Uri);
                request.Headers["Accept-Encoding"] = "gzip";
                request.Headers["Accept-Language"] = "en-us";
                request.Credentials = CredentialCache.DefaultNetworkCredentials;
                request.AutomaticDecompression = DecompressionMethods.GZip;

                if (this.TimeoutLength > 0)
                {
                    request.Timeout = this.TimeoutLength;
                }

                this.webRequest = request;
            }

            return this.webRequest;
        }
        #endregion
        #endregion
    }
}
