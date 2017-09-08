namespace ImageSharp.Formats
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// This represents one Microsoft Windows BMP v3 and IBM OS/2 BMP v2 color table/palette element.
    /// <para>Supported since Windows 3.0 and OS/2 2.0x.</para>
    /// <para>From Windows BMP v2 and OS/2 BMP v1.</para>
    /// </summary>
    /// <remarks>
    /// Make shore that <c>sizeof(RGBQUAD)</c> returns the size of 4 bytes and is byte aligned.
    /// All structure fields are stored little-endian on the file.
    /// </remarks>
    /// See <a href="https://msdn.microsoft.com/en-us/library/dd162938(v=vs.85).aspx">this link</a> for more information.
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
    internal struct RGBQUAD
    {
        /// <summary>
        /// Specifies the intensity of blue in the color in the range 0 to 255.
        /// </summary>
        public byte Blue;

        /// <summary>
        /// Specifies the intensity of green in the color in the range 0 to 255.
        /// </summary>
        public byte Green;

        /// <summary>
        /// Specifies the intensity of red in the color in the range 0 to 255.
        /// </summary>
        public byte Red;

        /// <summary>
        /// Not used on Windows BMP v3 and OS/2 BMP v2 and older versions (reserved and must be 0).
        /// </summary>
        /// <remarks>Optional Alpha color channel on Windows BMP v4. Normaly set to 0.</remarks>
        public byte Alpha;
    }
}
