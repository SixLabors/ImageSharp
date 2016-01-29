namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// Defines jpeg constants defined in the specification.
    /// </summary>
    internal static class JpegConstants
    {
        public static ushort MaxLength = 65535;

        /// <summary>
        /// Describes common Jpeg markers
        /// </summary>
        internal static class Markers
        {
            /// <summary>
            /// Marker prefix. Next byte is a marker.
            /// </summary>
            public const byte XFF = 0xff;

            /// <summary>
            /// Start of Image
            /// </summary>
            public const byte SOI = 0xd8;

            /// <summary>
            /// Start of Frame (baseline DCT)
            /// <remarks>
            /// Indicates that this is a baseline DCT-based JPEG, and specifies the width, height, number of components, 
            /// and component subsampling (e.g., 4:2:0).
            /// </remarks>
            /// </summary>
            public const byte SOF0 = 0xc0;

            /// <summary>
            /// Start Of Frame (progressive DCT)
            /// <remarks>
            /// Indicates that this is a progressive DCT-based JPEG, and specifies the width, height, number of components, 
            /// and component subsampling (e.g., 4:2:0).
            /// </remarks>
            /// </summary>
            public const byte SOF2 = 0xc0;

            /// <summary>
            /// Define Huffman Table(s)
            /// <remarks>
            /// Specifies one or more Huffman tables.
            /// </remarks>
            /// </summary>
            public const byte DHT = 0xc4;

            /// <summary>
            /// Define Quantization Table(s)
            /// <remarks>
            /// Specifies one or more quantization tables.
            /// </remarks>
            /// </summary>
            public const byte DQT = 0xdb;

            /// <summary>
            /// Define Restart Interval
            /// <remarks>
            /// Specifies the interval between RSTn markers, in macroblocks. This marker is followed by two bytes 
            /// indicating the fixed size so it can be treated like any other variable size segment.
            /// </remarks>
            /// </summary>
            public const byte DRI = 0xdd;

            /// <summary>
            /// Start of Scan
            /// <remarks>
            /// Begins a top-to-bottom scan of the image. In baseline DCT JPEG images, there is generally a single scan. 
            /// Progressive DCT JPEG images usually contain multiple scans. This marker specifies which slice of data it 
            /// will contain, and is immediately followed by entropy-coded data.
            /// </remarks>
            /// </summary>
            public const byte SOS = 0xda;

            /// <summary>
            /// Comment
            /// <remarks>
            /// Contains a text comment.
            /// </remarks>
            /// </summary>
            public const byte COM = 0xfe;

            /// <summary>
            /// End of Image
            /// </summary>
            public const byte EOI = 0xd9;

            /// <summary>
            /// Application specific marker for marking the jpeg format.
            /// </summary>
            public const byte APP0 = 0xe0;

            /// <summary>
            /// Application specific marker for marking where to store metadata.
            /// </summary>
            public const byte APP1 = 0xe1;
        }
    }
}
