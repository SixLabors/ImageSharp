// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Pbm
{
    internal static class StreamExtensions
    {
        public static int WriteDecimal(this Stream stream, int value)
        {
            string str = value.ToString();
            byte[] bytes = Encoding.ASCII.GetBytes(str);
            stream.Write(bytes);
            return bytes.Length;
        }
    }
}
