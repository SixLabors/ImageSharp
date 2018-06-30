using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    internal static class Telemetry
    {
        public static void DecodingException(Exception ex, long imageSize, IImageFormat format, IImageDecoder decoder, Configuration configuration)
        {
            SixLabors.Telemetry.Telemetry.Provider.Exception(ex, () => new SixLabors.Telemetry.TelemetryDetails()
            {
                Operation = "Exception Decodeing Image",
                Properties = new Dictionary<string, string> {
                    { "Default Config", (Configuration.Default == configuration).ToString() },
                    { "File Size", imageSize.ToString() },
                    { "Decoder", decoder?.GetType().FullName },
                    { "Format", format?.Name ?? "Unknown" },
                    { "Library", "ImageSharp" }
                }
            });
        }

        public static IDisposable StartDecodeImage(long imageSize, IImageFormat format, IImageDecoder decoder, Configuration configuration)
        {
            return SixLabors.Telemetry.Telemetry.Provider.Operation(() => new SixLabors.Telemetry.TelemetryDetails()
            {
                Operation = "Decode Image From Stream",
                Properties = new Dictionary<string, string> {
                    { "Default Config", (Configuration.Default == configuration).ToString() },
                    { "File Size", imageSize.ToString() },
                    { "Decoder", decoder?.GetType().FullName },
                    { "Format", format?.Name ?? "Unknown" },
                    { "Library", "ImageSharp" }
                }
            });
        }

        public static IDisposable StartDecodeImageFromFile(string path, Configuration configuration)
        {
            return SixLabors.Telemetry.Telemetry.Provider.Operation(() => new SixLabors.Telemetry.TelemetryDetails()
            {
                Operation = "Decode Image From File",
                Properties = new Dictionary<string, string> {
                    { "Default Config", (Configuration.Default == configuration).ToString() },
                    { "File Extension", Path.GetExtension(path) },
                    { "Library", "ImageSharp" }
                }
            });
        }

        public static IDisposable StartEncodeToFile<TPixel>(Image<TPixel> source, string fileExtension)
            where TPixel : struct, IPixel<TPixel>
        {
            return SixLabors.Telemetry.Telemetry.Provider.Operation(() => new SixLabors.Telemetry.TelemetryDetails()
            {
                Operation = "Encode Image To File",
                Properties = new Dictionary<string, string> {
                    { "Default Config", (Configuration.Default == source?.GetConfiguration()).ToString() },
                    { "Width", source?.Width.ToString() },
                    { "Height",  source?.Height.ToString() },
                    { "FileExtension", fileExtension },
                    { "Library", "ImageSharp" }
                }
            });
        }

        public static IDisposable StartEncode<TPixel>(Image<TPixel> source, IImageEncoder encoder, Stream targetStream)
            where TPixel : struct, IPixel<TPixel>
        {
            return SixLabors.Telemetry.Telemetry.Provider.Operation(() => new SixLabors.Telemetry.TelemetryDetails()
            {
                Operation = "Encode Image To Stream",
                Properties = new Dictionary<string, string> {
                    { "Default Config", (Configuration.Default == source?.GetConfiguration()).ToString() },
                    { "Width", source?.Width.ToString() },
                    { "Height",  source?.Height.ToString() },
                    { "Encoder", encoder?.GetType().FullName },
                    { "StreamType", targetStream?.GetType().FullName }, // are encoding to file stream, memory stream or even directly to some other response stream
                    { "Library", "ImageSharp" }
                }
            });
        }
        public static void EncodingException<TPixel>(Exception ex, Image<TPixel> source, IImageEncoder encoder, Stream targetStream)
            where TPixel : struct, IPixel<TPixel>
        {
            SixLabors.Telemetry.Telemetry.Provider.Exception(ex, () => new SixLabors.Telemetry.TelemetryDetails()
            {
                Operation = "Exception Encodeing Image",
                Properties = new Dictionary<string, string> {
                    { "Default Config", (Configuration.Default == source?.GetConfiguration()).ToString() },
                    { "Width", source?.Width.ToString() },
                    { "Height",  source?.Height.ToString() },
                    { "Encoder", encoder?.GetType().FullName },
                    { "StreamType", targetStream?.GetType().FullName }, // are encoding to file stream, memory stream or even directly to some other response stream
                    { "Library", "ImageSharp" }
                }
            });
        }


        public static IDisposable StartEncodeAsBase64<TPixel>(Image<TPixel> source, IImageFormat format)
            where TPixel : struct, IPixel<TPixel>
        {
            return SixLabors.Telemetry.Telemetry.Provider.Operation(() => new SixLabors.Telemetry.TelemetryDetails()
            {
                Operation = "Encode Image as base64 string",
                Properties = new Dictionary<string, string> {
                    { "Default Config", (Configuration.Default == source?.GetConfiguration()).ToString() },
                    { "Width", source?.Width.ToString() },
                    { "Height",  source?.Height.ToString() },
                    { "Format", format?.Name },
                    { "Library", "ImageSharp" }
                }
            });
        }
    }
}
