namespace ImageSharp.Formats.Jpg
{
    using System.Runtime.InteropServices;

    /// <summary>
    ///     Represents a component scan
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Scan
    {
        /// <summary>
        ///     Gets or sets the component index.
        /// </summary>
        public byte Index;

        /// <summary>
        ///     Gets or sets the DC table selector
        /// </summary>
        public byte DcTableSelector;

        /// <summary>
        ///     Gets or sets the AC table selector
        /// </summary>
        public byte AcTableSelector;
    }
}