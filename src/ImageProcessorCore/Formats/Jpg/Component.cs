namespace ImageProcessorCore.Formats.Jpg
{
    using System;
    using System.IO;

	public class Component
	{
		public int h; // Horizontal sampling factor.
		public int v; // Vertical sampling factor.
		public byte c; // Component identifier.
		public byte tq; // Quantization table destination selector.
	}
}
