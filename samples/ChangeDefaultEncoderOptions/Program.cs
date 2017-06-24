using System;
using ImageSharp;

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

            // now lets say we don't want animated gifs, lets skip decoding the alternative frames
            Configuration.Default.SetMimeTypeDecoder("image/gif", new ImageSharp.Formats.GifDecoder()
            {
                IgnoreFrames = true,
                IgnoreMetadata = true
            });

            // and just to be douple sure we don't want animations lets disable them on encode too.
            Configuration.Default.SetMimeTypeEncoder("image/gif", new ImageSharp.Formats.GifEncoder()
            {
                IgnoreFrames = true,
                IgnoreMetadata = true
            });


        }
    }
}