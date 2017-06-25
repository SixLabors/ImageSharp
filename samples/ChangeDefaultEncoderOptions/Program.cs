using System;
using ImageSharp;
using ImageSharp.Formats;

namespace ChangeDefaultEncoderOptions
{
    class Program
    {
        static void Main(string[] args)
        {
            // lets switch out the default encoder for jpeg to one 
            // that saves at 90 quality and ignores the matadata
            Configuration.Default.SetMimeTypeEncoder("image/jpeg", new ImageSharp.Formats.JpegEncoder()
            {
                Quality = 90,
                IgnoreMetadata = true
            });
        }
    }
}