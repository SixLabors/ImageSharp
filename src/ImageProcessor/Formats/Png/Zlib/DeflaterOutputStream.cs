namespace ImageProcessor.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// A special stream deflating or compressing the bytes that are
    /// written to it.  It uses a Deflater to perform actual deflating.<br/>
    /// Authors of the original java version : Tom Tromey, Jochen Hoenicke 
    /// </summary>
    public class DeflaterOutputStream : Stream
    {
        #region Constructors
        /// <summary>
        /// Creates a new DeflaterOutputStream with a default Deflater and default buffer size.
        /// </summary>
        /// <param name="baseOutputStream">
        /// the output stream where deflated output should be written.
        /// </param>
        public DeflaterOutputStream(Stream baseOutputStream)
            : this(baseOutputStream, new Deflater(), 512)
        {
        }

        /// <summary>
        /// Creates a new DeflaterOutputStream with the given Deflater and
        /// default buffer size.
        /// </summary>
        /// <param name="baseOutputStream">
        /// the output stream where deflated output should be written.
        /// </param>
        /// <param name="deflater">
        /// the underlying deflater.
        /// </param>
        public DeflaterOutputStream(Stream baseOutputStream, Deflater deflater)
            : this(baseOutputStream, deflater, 512)
        {
        }

        /// <summary>
        /// Creates a new DeflaterOutputStream with the given Deflater and
        /// buffer size.
        /// </summary>
        /// <param name="baseOutputStream">
        /// The output stream where deflated output is written.
        /// </param>
        /// <param name="deflater">
        /// The underlying deflater to use
        /// </param>
        /// <param name="bufferSize">
        /// The buffer size in bytes to use when deflating (minimum value 512)
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// bufsize is less than or equal to zero.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// baseOutputStream does not support writing
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// deflater instance is null
        /// </exception>
        public DeflaterOutputStream(Stream baseOutputStream, Deflater deflater, int bufferSize)
        {
            if (baseOutputStream == null)
            {
                throw new ArgumentNullException("baseOutputStream");
            }

            if (baseOutputStream.CanWrite == false)
            {
                throw new ArgumentException("Must support writing", "baseOutputStream");
            }

            if (deflater == null)
            {
                throw new ArgumentNullException("deflater");
            }

            if (bufferSize < 512)
            {
                throw new ArgumentOutOfRangeException("bufferSize");
            }

            baseOutputStream_ = baseOutputStream;
            buffer_ = new byte[bufferSize];
            deflater_ = deflater;
        }
        #endregion

        #region Public API
        /// <summary>
        /// Finishes the stream by calling finish() on the deflater. 
        /// </summary>
        /// <exception cref="ImageFormatException">
        /// Not all input is deflated
        /// </exception>
        public virtual void Finish()
        {
            deflater_.Finish();
            while (!deflater_.IsFinished)
            {
                int len = deflater_.Deflate(buffer_, 0, buffer_.Length);
                if (len <= 0)
                {
                    break;
                }

                if (keys != null)
                {
                    EncryptBlock(buffer_, 0, len);
                }

                baseOutputStream_.Write(buffer_, 0, len);
            }

            if (!deflater_.IsFinished)
            {
                throw new ImageFormatException("Can't deflate all input?");
            }

            baseOutputStream_.Flush();

            if (keys != null)
            {
                keys = null;
            }
        }

        /// <summary>
        /// Get/set flag indicating ownership of the underlying stream.
        /// When the flag is true <see cref="Dispose"></see> will close the underlying stream also.
        /// </summary>
        public bool IsStreamOwner
        {
            get { return isStreamOwner_; }
            set { isStreamOwner_ = value; }
        }

        ///	<summary>
        /// Allows client to determine if an entry can be patched after its added
        /// </summary>
        public bool CanPatchEntries
        {
            get
            {
                return baseOutputStream_.CanSeek;
            }
        }

        #endregion

        #region Encryption

        string password;

        uint[] keys;

        /// <summary>
        /// Get/set the password used for encryption.
        /// </summary>
        /// <remarks>When set to null or if the password is empty no encryption is performed</remarks>
        public string Password
        {
            get
            {
                return password;
            }
            set
            {
                if ((value != null) && (value.Length == 0))
                {
                    password = null;
                }
                else
                {
                    password = value;
                }
            }
        }

        /// <summary>
        /// Encrypt a block of data
        /// </summary>
        /// <param name="buffer">
        /// Data to encrypt.  NOTE the original contents of the buffer are lost
        /// </param>
        /// <param name="offset">
        /// Offset of first byte in buffer to encrypt
        /// </param>
        /// <param name="length">
        /// Number of bytes in buffer to encrypt
        /// </param>
        protected void EncryptBlock(byte[] buffer, int offset, int length)
        {
            for (int i = offset; i < offset + length; ++i)
            {
                byte oldbyte = buffer[i];
                buffer[i] ^= EncryptByte();
                UpdateKeys(oldbyte);
            }
        }

        /// <summary>
        /// Initializes encryption keys based on given <paramref name="password"/>.
        /// </summary>
        /// <param name="password">The password.</param>
        protected void InitializePassword(string password)
        {
            keys = new uint[] {
                0x12345678,
                0x23456789,
                0x34567890
            };

            byte[] rawPassword = ZipConstants.ConvertToArray(password);

            for (int i = 0; i < rawPassword.Length; ++i)
            {
                UpdateKeys((byte)rawPassword[i]);
            }
        }

#if !NET_1_1 && !NETCF_2_0 && !NOCRYPTO
		/// <summary>
		/// Initializes encryption keys based on given password.
		/// </summary>
		protected void InitializeAESPassword(ZipEntry entry, string rawPassword,
											out byte[] salt, out byte[] pwdVerifier) {
			salt = new byte[entry.AESSaltLen];
			// Salt needs to be cryptographically random, and unique per file
			if (_aesRnd == null)
				_aesRnd = new RNGCryptoServiceProvider();
			_aesRnd.GetBytes(salt);
			int blockSize = entry.AESKeySize / 8;	// bits to bytes

			cryptoTransform_ = new ZipAESTransform(rawPassword, salt, blockSize, true);
			pwdVerifier = ((ZipAESTransform)cryptoTransform_).PwdVerifier;
		}
#endif

#if NETCF_1_0 || NOCRYPTO

        /// <summary>
		/// Encrypt a single byte 
		/// </summary>
		/// <returns>
		/// The encrypted value
		/// </returns>
		protected byte EncryptByte()
        {
            uint temp = ((keys[2] & 0xFFFF) | 2);
            return (byte)((temp * (temp ^ 1)) >> 8);
        }

        /// <summary>
        /// Update encryption keys 
        /// </summary>		
        protected void UpdateKeys(byte ch)
        {
            keys[0] = Crc32.ComputeCrc32(keys[0], ch);
            keys[1] = keys[1] + (byte)keys[0];
            keys[1] = keys[1] * 134775813 + 1;
            keys[2] = Crc32.ComputeCrc32(keys[2], (byte)(keys[1] >> 24));
        }
#endif

        #endregion

        #region Deflation Support
        /// <summary>
		/// Deflates everything in the input buffers.  This will call
		/// <code>def.deflate()</code> until all bytes from the input buffers
		/// are processed.
		/// </summary>
		protected void Deflate()
        {
            while (!deflater_.IsNeedingInput)
            {
                int deflateCount = deflater_.Deflate(buffer_, 0, buffer_.Length);

                if (deflateCount <= 0)
                {
                    break;
                }
#if NETCF_1_0 || NOCRYPTO
                if (keys != null)
#else
				if (cryptoTransform_ != null) 
#endif
                {
                    EncryptBlock(buffer_, 0, deflateCount);
                }

                baseOutputStream_.Write(buffer_, 0, deflateCount);
            }

            if (!deflater_.IsNeedingInput)
            {
                throw new ImageFormatException("DeflaterOutputStream can't deflate all input?");
            }
        }
        #endregion

        #region Stream Overrides
        /// <summary>
        /// Gets value indicating stream can be read from
        /// </summary>
        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating if seeking is supported for this stream
        /// This property always returns false
        /// </summary>
        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Get value indicating if this stream supports writing
        /// </summary>
        public override bool CanWrite
        {
            get
            {
                return baseOutputStream_.CanWrite;
            }
        }

        /// <summary>
        /// Get current length of stream
        /// </summary>
        public override long Length
        {
            get
            {
                return baseOutputStream_.Length;
            }
        }

        /// <summary>
        /// Gets the current position within the stream.
        /// </summary>
        /// <exception cref="NotSupportedException">Any attempt to set position</exception>
        public override long Position
        {
            get
            {
                return baseOutputStream_.Position;
            }
            set
            {
                throw new NotSupportedException("Position property not supported");
            }
        }

        /// <summary>
        /// Sets the current position of this stream to the given value. Not supported by this class!
        /// </summary>
        /// <param name="offset">The offset relative to the <paramref name="origin"/> to seek.</param>
        /// <param name="origin">The <see cref="SeekOrigin"/> to seek from.</param>
        /// <returns>The new position in the stream.</returns>
        /// <exception cref="NotSupportedException">Any access</exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("DeflaterOutputStream Seek not supported");
        }

        /// <summary>
        /// Sets the length of this stream to the given value. Not supported by this class!
        /// </summary>
        /// <param name="value">The new stream length.</param>
        /// <exception cref="NotSupportedException">Any access</exception>
        public override void SetLength(long value)
        {
            throw new NotSupportedException("DeflaterOutputStream SetLength not supported");
        }

        /// <summary>
        /// Read a byte from stream advancing position by one
        /// </summary>
        /// <returns>The byte read cast to an int.  THe value is -1 if at the end of the stream.</returns>
        /// <exception cref="NotSupportedException">Any access</exception>
        public override int ReadByte()
        {
            throw new NotSupportedException("DeflaterOutputStream ReadByte not supported");
        }

        /// <summary>
        /// Read a block of bytes from stream
        /// </summary>
        /// <param name="buffer">The buffer to store read data in.</param>
        /// <param name="offset">The offset to start storing at.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>The actual number of bytes read.  Zero if end of stream is detected.</returns>
        /// <exception cref="NotSupportedException">Any access</exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("DeflaterOutputStream Read not supported");
        }
#if !PCL
		/// <summary>
		/// Asynchronous reads are not supported a NotSupportedException is always thrown
		/// </summary>
		/// <param name="buffer">The buffer to read into.</param>
		/// <param name="offset">The offset to start storing data at.</param>
		/// <param name="count">The number of bytes to read</param>
		/// <param name="callback">The async callback to use.</param>
		/// <param name="state">The state to use.</param>
		/// <returns>Returns an <see cref="IAsyncResult"/></returns>
		/// <exception cref="NotSupportedException">Any access</exception>
		public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			throw new NotSupportedException("DeflaterOutputStream BeginRead not currently supported");
		}
		
		/// <summary>
		/// Asynchronous writes arent supported, a NotSupportedException is always thrown
		/// </summary>
		/// <param name="buffer">The buffer to write.</param>
		/// <param name="offset">The offset to begin writing at.</param>
		/// <param name="count">The number of bytes to write.</param>
		/// <param name="callback">The <see cref="AsyncCallback"/> to use.</param>
		/// <param name="state">The state object.</param>
		/// <returns>Returns an IAsyncResult.</returns>
		/// <exception cref="NotSupportedException">Any access</exception>
		public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			throw new NotSupportedException("BeginWrite is not supported");
		}
#endif
        /// <summary>
        /// Flushes the stream by calling <see cref="DeflaterOutputStream.Flush">Flush</see> on the deflater and then
        /// on the underlying stream.  This ensures that all bytes are flushed.
        /// </summary>
        public override void Flush()
        {
            deflater_.Flush();
            Deflate();
            baseOutputStream_.Flush();
        }

        /// <summary>
        /// Calls <see cref="Finish"/> and closes the underlying
        /// stream when <see cref="IsStreamOwner"></see> is true.
        /// </summary>
#if !PCL
		public override void Close()
        {
            if (!isClosed_)
            {
				isClosed_ = true;

				try {
					Finish();
#if NETCF_1_0 || NOCRYPTO
                    keys =null;
#else
					if ( cryptoTransform_ != null ) {
						GetAuthCodeIfAES();
						cryptoTransform_.Dispose();
						cryptoTransform_ = null;
					}
#endif
				}
				finally {
					if( isStreamOwner_ ) {
						baseOutputStream_.Close();
					}
				}
			}
		}
#else
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && !isClosed_)
            {
                isClosed_ = true;

                try
                {
                    Finish();
                    keys = null;
                }
                finally
                {
                    if (isStreamOwner_)
                    {
                        baseOutputStream_.Dispose();
                    }
                }
            }
        }
#endif

        private void GetAuthCodeIfAES()
        {
#if !NET_1_1 && !NETCF_2_0 && !NOCRYPTO
			if (cryptoTransform_ is ZipAESTransform) {
				AESAuthCode = ((ZipAESTransform)cryptoTransform_).GetAuthCode();
			}
#endif
        }

        /// <summary>
        /// Writes a single byte to the compressed output stream.
        /// </summary>
        /// <param name="value">
        /// The byte value.
        /// </param>
        public override void WriteByte(byte value)
        {
            byte[] b = new byte[1];
            b[0] = value;
            Write(b, 0, 1);
        }

        /// <summary>
        /// Writes bytes from an array to the compressed stream.
        /// </summary>
        /// <param name="buffer">
        /// The byte array
        /// </param>
        /// <param name="offset">
        /// The offset into the byte array where to start.
        /// </param>
        /// <param name="count">
        /// The number of bytes to write.
        /// </param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            deflater_.SetInput(buffer, offset, count);
            Deflate();
        }
        #endregion

        #region Instance Fields
        /// <summary>
        /// This buffer is used temporarily to retrieve the bytes from the
        /// deflater and write them to the underlying output stream.
        /// </summary>
        byte[] buffer_;

        /// <summary>
        /// The deflater which is used to deflate the stream.
        /// </summary>
        protected Deflater deflater_;

        /// <summary>
        /// Base stream the deflater depends on.
        /// </summary>
        protected Stream baseOutputStream_;

        bool isClosed_;

        bool isStreamOwner_ = true;
        #endregion

        #region Static Fields

#if !NET_1_1 && !NETCF_2_0 && !NOCRYPTO
		// Static to help ensure that multiple files within a zip will get different random salt
		private static RNGCryptoServiceProvider _aesRnd;
#endif
        #endregion
    }
}
