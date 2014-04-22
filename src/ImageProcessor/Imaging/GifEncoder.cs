

namespace ImageProcessor.Imaging
{
    using System;

    /// <summary>
    /// Encodes multiple images as an animated gif to a stream.
    /// <remarks>
    /// Always wire this up in a using block.
    /// Disposing the encoder will complete the file.
    /// Uses default .NET GIF encoding and adds animation headers.
    /// </remarks>
    /// </summary>
    public class GifEncoder : IDisposable
    {
        #region Fields
        private const string FileType = "GIF";
        private const string FileVersion = "89a";
        private const byte FileTrailer = 0x3b;

        private const int ApplicationExtensionBlockIdentifier = 0xff21;
        private const byte ApplicationBlockSize = 0x0b;
        private const string ApplicationIdentification = "NETSCAPE2.0";

        private const int GraphicControlExtensionBlockIdentifier = 0xf921;
        private const byte GraphicControlExtensionBlockSize = 0x04;

        private const long SourceGlobalColorInfoPosition = 10;
        private const long SourceGraphicControlExtensionPosition = 781;
        private const long SourceGraphicControlExtensionLength = 8;
        private const long SourceImageBlockPosition = 789;
        private const long SourceImageBlockHeaderLength = 11;
        private const long SourceColorBlockPosition = 13;
        private const long SourceColorBlockLength = 768;

        /// <summary>
        /// A value indicating whether this instance of the given entity has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if this instance has been disposed; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// If the entity is disposed, it must not be disposed a second
        /// time. The isDisposed field is set the first time the entity
        /// is disposed. If the isDisposed field is true, then the Dispose()
        /// method will not dispose again. This help not to prolong the entity's
        /// life in the Garbage Collector.
        /// </remarks>
        private bool isDisposed;
        #endregion

        #region IDisposable Members
        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);

            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        /// <param name="disposing">If true, the object gets disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose of any managed resources here.
                //if (this.Image != null)
                //{
                //    // Dispose of the memory stream from Load and the image.
                //    if (this.inputStream != null)
                //    {
                //        this.inputStream.Dispose();
                //        this.inputStream = null;
                //    }

                //    this.Image.Dispose();
                //    this.Image = null;
                //}
            }

            // Call the appropriate methods to clean up
            // unmanaged resources here.
            // Note disposing is done.
            this.isDisposed = true;
        }
        #endregion
    }
}
