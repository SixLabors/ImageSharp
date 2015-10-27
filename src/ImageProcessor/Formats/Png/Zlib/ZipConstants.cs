namespace ImageProcessor.Formats
{
    using System.Text;

    /// <summary>
    /// This class contains constants used for Zip format files
    /// </summary>
    public static class ZipConstants
    {
        #region Versions
        /// <summary>
        /// The version made by field for entries in the central header when created by this library
        /// </summary>
        /// <remarks>
        /// This is also the Zip version for the library when comparing against the version required to extract
        /// for an entry. </remarks>
        public const int VersionMadeBy = 51; // was 45 before AES

        /// <summary>
        /// The minimum version required to support strong encryption
        /// </summary>
        public const int VersionStrongEncryption = 50;

        /// <summary>
        /// Version indicating AES encryption
        /// </summary>
        public const int VERSION_AES = 51;

        /// <summary>
        /// The version required for Zip64 extensions (4.5 or higher)
        /// </summary>
        public const int VersionZip64 = 45;
        #endregion

        #region Header Sizes
        /// <summary>
        /// Size of local entry header (excluding variable length fields at end)
        /// </summary>
        public const int LocalHeaderBaseSize = 30;

        /// <summary>
        /// Size of Zip64 data descriptor
        /// </summary>
        public const int Zip64DataDescriptorSize = 24;

        /// <summary>
        /// Size of data descriptor
        /// </summary>
        public const int DataDescriptorSize = 16;

        /// <summary>
        /// Size of central header entry (excluding variable fields)
        /// </summary>
        public const int CentralHeaderBaseSize = 46;

        /// <summary>
        /// Size of end of central record (excluding variable fields)
        /// </summary>
        public const int EndOfCentralRecordBaseSize = 22;

        /// <summary>
        /// Size of 'classic' cryptographic header stored before any entry data
        /// </summary>
        public const int CryptoHeaderSize = 12;
        #endregion

        #region Header Signatures

        /// <summary>
        /// Signature for local entry header
        /// </summary>
        public const int LocalHeaderSignature = 'P' | ('K' << 8) | (3 << 16) | (4 << 24);

        /// <summary>
        /// Signature for spanning entry
        /// </summary>
        public const int SpanningSignature = 'P' | ('K' << 8) | (7 << 16) | (8 << 24);

        /// <summary>
        /// Signature for temporary spanning entry
        /// </summary>
        public const int SpanningTempSignature = 'P' | ('K' << 8) | ('0' << 16) | ('0' << 24);

        /// <summary>
        /// Signature for data descriptor
        /// </summary>
        /// <remarks>
        /// This is only used where the length, Crc, or compressed size isnt known when the
        /// entry is created and the output stream doesnt support seeking.
        /// The local entry cannot be 'patched' with the correct values in this case
        /// so the values are recorded after the data prefixed by this header, as well as in the central directory.
        /// </remarks>
        public const int DataDescriptorSignature = 'P' | ('K' << 8) | (7 << 16) | (8 << 24);

        /// <summary>
        /// Signature for central header
        /// </summary>
        public const int CentralHeaderSignature = 'P' | ('K' << 8) | (1 << 16) | (2 << 24);

        /// <summary>
        /// Signature for Zip64 central file header
        /// </summary>
        public const int Zip64CentralFileHeaderSignature = 'P' | ('K' << 8) | (6 << 16) | (6 << 24);

        /// <summary>
        /// Signature for Zip64 central directory locator
        /// </summary>
        public const int Zip64CentralDirLocatorSignature = 'P' | ('K' << 8) | (6 << 16) | (7 << 24);

        /// <summary>
        /// Signature for archive extra data signature (were headers are encrypted).
        /// </summary>
        public const int ArchiveExtraDataSignature = 'P' | ('K' << 8) | (6 << 16) | (7 << 24);

        /// <summary>
        /// Central header digitial signature
        /// </summary>
        public const int CentralHeaderDigitalSignature = 'P' | ('K' << 8) | (5 << 16) | (5 << 24);

        /// <summary>
        /// End of central directory record signature
        /// </summary>
        public const int EndOfCentralDirectorySignature = 'P' | ('K' << 8) | (5 << 16) | (6 << 24);

        #endregion

#if NETCF_1_0 || NETCF_2_0
		// This isnt so great but is better than nothing.
        // Trying to work out an appropriate OEM code page would be good.
        // 850 is a good default for english speakers particularly in Europe.
		static int defaultCodePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
#elif PCL
        static Encoding defaultEncoding = Encoding.UTF8;
#else
	    /// <remarks>
	    /// Get OEM codepage from NetFX, which parses the NLP file with culture info table etc etc.
	    /// But sometimes it yields the special value of 1 which is nicknamed <c>CodePageNoOEM</c> in <see cref="Encoding"/> sources (might also mean <c>CP_OEMCP</c>, but Encoding puts it so).
	    /// This was observed on Ukranian and Hindu systems.
	    /// Given this value, <see cref="Encoding.GetEncoding(int)"/> throws an <see cref="ArgumentException"/>.
	    /// So replace it with some fallback, e.g. 437 which is the default cpcp in a console in a default Windows installation.
	    /// </remarks>
	    static int defaultCodePage =
            // these values cause ArgumentException in subsequent calls to Encoding::GetEncoding()
            ((Thread.CurrentThread.CurrentCulture.TextInfo.OEMCodePage == 1) || (Thread.CurrentThread.CurrentCulture.TextInfo.OEMCodePage == 2) || (Thread.CurrentThread.CurrentCulture.TextInfo.OEMCodePage == 3) || (Thread.CurrentThread.CurrentCulture.TextInfo.OEMCodePage == 42))
            ? 437 // The default OEM encoding in a console in a default Windows installation, as a fallback.
	        : Thread.CurrentThread.CurrentCulture.TextInfo.OEMCodePage;  
#endif
#if !PCL
		/// <summary>
		/// Default encoding used for string conversion.  0 gives the default system OEM code page.
		/// Dont use unicode encodings if you want to be Zip compatible!
		/// Using the default code page isnt the full solution neccessarily
		/// there are many variable factors, codepage 850 is often a good choice for
		/// European users, however be careful about compatability.
		/// </summary>
		public static int DefaultCodePage {
			get {
				return defaultCodePage; 
			}
			set {
                if ((value < 0) || (value > 65535) ||
                    (value == 1) || (value == 2) || (value == 3) || (value == 42)) {
                    throw new ArgumentOutOfRangeException("value");
                }

                defaultCodePage = value;
			}
		}
#else
        /// <summary>
        /// PCL don't support CodePage so we used Encoding instead of
        /// </summary>
        public static Encoding DefaultEncoding
        {
            get
            {
                return defaultEncoding;
            }
            set
            {
                defaultEncoding = value;
            }
        }
#endif

        /// <summary>
        /// Convert a portion of a byte array to a string.
        /// </summary>		
        /// <param name="data">
        /// Data to convert to string
        /// </param>
        /// <param name="count">
        /// Number of bytes to convert starting from index 0
        /// </param>
        /// <returns>
        /// data[0]..data[count - 1] converted to a string
        /// </returns>
        public static string ConvertToString(byte[] data, int count)
        {
            if (data == null)
            {
                return string.Empty;
            }
#if !PCL
			return Encoding.GetEncoding(DefaultCodePage).GetString(data, 0, count);
#else
            return DefaultEncoding.GetString(data, 0, count);
#endif
        }

        /// <summary>
        /// Convert a byte array to string
        /// </summary>
        /// <param name="data">
        /// Byte array to convert
        /// </param>
        /// <returns>
        /// <paramref name="data">data</paramref>converted to a string
        /// </returns>
        public static string ConvertToString(byte[] data)
        {
            if (data == null)
            {
                return string.Empty;
            }
            return ConvertToString(data, data.Length);
        }

        /// <summary>
        /// Convert a byte array to string
        /// </summary>
        /// <param name="flags">The applicable general purpose bits flags</param>
        /// <param name="data">
        /// Byte array to convert
        /// </param>
        /// <param name="count">The number of bytes to convert.</param>
        /// <returns>
        /// <paramref name="data">data</paramref>converted to a string
        /// </returns>
        public static string ConvertToStringExt(int flags, byte[] data, int count)
        {
            if (data == null)
            {
                return string.Empty;
            }

            if ((flags & (int)GeneralBitFlags.UnicodeText) != 0)
            {
                return Encoding.UTF8.GetString(data, 0, count);
            }
            else
            {
                return ConvertToString(data, count);
            }
        }

        /// <summary>
        /// Convert a byte array to string
        /// </summary>
        /// <param name="data">
        /// Byte array to convert
        /// </param>
        /// <param name="flags">The applicable general purpose bits flags</param>
        /// <returns>
        /// <paramref name="data">data</paramref>converted to a string
        /// </returns>
        public static string ConvertToStringExt(int flags, byte[] data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            if ((flags & (int)GeneralBitFlags.UnicodeText) != 0)
            {
                return Encoding.UTF8.GetString(data, 0, data.Length);
            }
            else
            {
                return ConvertToString(data, data.Length);
            }
        }

        /// <summary>
        /// Convert a string to a byte array
        /// </summary>
        /// <param name="str">
        /// String to convert to an array
        /// </param>
        /// <returns>Converted array</returns>
        public static byte[] ConvertToArray(string str)
        {
            if (str == null)
            {
                return new byte[0];
            }
#if !PCL
			return Encoding.GetEncoding(DefaultCodePage).GetBytes(str);
#else
            return DefaultEncoding.GetBytes(str);
#endif
        }

        /// <summary>
        /// Convert a string to a byte array
        /// </summary>
        /// <param name="flags">The applicable <see cref="GeneralBitFlags">general purpose bits flags</see></param>
        /// <param name="str">
        /// String to convert to an array
        /// </param>
        /// <returns>Converted array</returns>
        public static byte[] ConvertToArray(int flags, string str)
        {
            if (str == null)
            {
                return new byte[0];
            }

            if ((flags & (int)GeneralBitFlags.UnicodeText) != 0)
            {
                return Encoding.UTF8.GetBytes(str);
            }
            else
            {
                return ConvertToArray(str);
            }
        }
    }
}
