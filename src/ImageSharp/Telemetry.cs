using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    internal static class Telemetry
    {
        private static readonly string NugetVersion;

        static Telemetry()
        {
            AssemblyInformationalVersionAttribute info = typeof(Telemetry)
                .GetTypeInfo()
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            NugetVersion = info?.InformationalVersion ?? "unknown";
        }

        private static SixLabors.Telemetry.TelemetryDetails Create(string operation, string operationType, Configuration configuration, Action<SixLabors.Telemetry.TelemetryDetails> config = null)
        {
            var details = new SixLabors.Telemetry.TelemetryDetails()
            {
                Operation = operation,
                OperationType = operationType,
                Library = "ImageSharp",
                Properties = new Dictionary<string, string>()
            };

            details.Properties.Add("Default Config", (Configuration.Default == configuration).ToString());
            details.Properties.Add("ImageSharp.Version", NugetVersion);

            config?.Invoke(details);

            return details;
        }

        private static SixLabors.Telemetry.TelemetryDetails Create<TPixel>(string operation, string operationType, Configuration configuration, Action<SixLabors.Telemetry.TelemetryDetails> config = null)
           where TPixel : struct, IPixel<TPixel>
        {
            return Create(operation, operationType, configuration, d =>
            {
                d.Properties.Add("PixelType", typeof(TPixel).FullName);

                config?.Invoke(d);
            });
        }

        private static SixLabors.Telemetry.TelemetryDetails Create<TPixel>(string operation, string operationType, Image<TPixel> image, Action<SixLabors.Telemetry.TelemetryDetails> config = null)
            where TPixel : struct, IPixel<TPixel>
        {
            return Create<TPixel>(operation, operationType, image.GetConfiguration(), d =>
            {
                d.Properties.Add("Image.Width", image?.Width.ToString());
                d.Properties.Add("Image.Height", image?.Height.ToString());

                config?.Invoke(d);
            });
        }

        public static IDisposable ApplyingProcessor<TPixel>(Image<TPixel> source, IImageProcessor<TPixel> processor, Rectangle bounds)
         where TPixel : struct, IPixel<TPixel>
        {
            return SixLabors.Telemetry.Telemetry.Provider.Operation(() => Create("Processing Image", "Processing", source, d =>
            {
                d.Properties.Add("Processor", processor?.GetType().FullName);
                d.Properties.Add("Processor.CustomSource", (source.Bounds() == bounds).ToString());
            }));
        }

        public static IDisposable MutatingImage<TPixel>(Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
        {
            return SixLabors.Telemetry.Telemetry.Provider.Operation(() => Create("Mutating Image", "Mutating", source));
        }

        public static IDisposable CloningImage<TPixel>(Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
        {
            return SixLabors.Telemetry.Telemetry.Provider.Operation(() => Create("Cloning Image", "Mutating", source));
        }

        public static void DecodingException<TPixel>(Exception ex, long imageSize, IImageFormat format, IImageDecoder decoder, Configuration configuration)
           where TPixel : struct, IPixel<TPixel>
        {
            SixLabors.Telemetry.Telemetry.Provider.Exception(ex, () => Create<TPixel>("Exception Decodeing Image", "Decoding", configuration, d =>
            {
                d.Properties.Add("File Size", imageSize.ToString());
                d.Properties.Add("Decoder", decoder?.GetType().FullName);
                d.Properties.Add("Format", format?.Name ?? "Unknown");
            }));
        }

        public static IDisposable StartDecodeImage<TPixel>(long imageSize, IImageFormat format, IImageDecoder decoder, Configuration configuration)
           where TPixel : struct, IPixel<TPixel>
        {
            return SixLabors.Telemetry.Telemetry.Provider.Operation(() => Create<TPixel>("Decode Image From Stream", "Decoding", configuration, d =>
            {
                d.Properties.Add("File Size", imageSize.ToString());
                d.Properties.Add("Decoder", decoder?.GetType().FullName);
                d.Properties.Add("Format", format?.Name ?? "Unknown");
            }));
        }

        public static IDisposable StartDecodeImageFromFile<TPixel>(string path, Configuration configuration)
           where TPixel : struct, IPixel<TPixel>
        {
            return SixLabors.Telemetry.Telemetry.Provider.Operation(() => Create("Decode Image From File", "Decoding", configuration, d =>
            {
                d.Properties.Add("File Extension", Path.GetExtension(path));
            }));
        }

        public static IDisposable StartEncodeToFile<TPixel>(Image<TPixel> source, string fileExtension)
            where TPixel : struct, IPixel<TPixel>
        {
            return SixLabors.Telemetry.Telemetry.Provider.Operation(() => Create("Encode Image To File", "Encoding", source, d =>
            {
                d.Properties.Add("File Extension", fileExtension);
            }));
        }

        public static IDisposable StartEncode<TPixel>(Image<TPixel> source, IImageEncoder encoder, Stream targetStream)
            where TPixel : struct, IPixel<TPixel>
        {
            return SixLabors.Telemetry.Telemetry.Provider.Operation(() => Create("Encode Image To Stream", "Encoding", source, d =>
            {
                d.Properties.Add("Encoder", encoder?.GetType().FullName);
                d.Properties.Add("StreamType", targetStream?.GetType().FullName); // are encoding to file stream, memory stream or even directly to some other response stream
            }));
        }

        public static void EncodingException<TPixel>(Exception ex, Image<TPixel> source, IImageEncoder encoder, Stream targetStream)
            where TPixel : struct, IPixel<TPixel>
        {
            SixLabors.Telemetry.Telemetry.Provider.Exception(ex, () => Create("Exception Encodeing Image", "Encoding", source, d =>
            {
                d.Properties.Add("Encoder", encoder?.GetType().FullName);
                d.Properties.Add("StreamType", targetStream?.GetType().FullName); // are encoding to file stream, memory stream or even directly to some other response stream
            }));
        }

        public static IDisposable StartEncodeAsBase64<TPixel>(Image<TPixel> source, IImageFormat format)
            where TPixel : struct, IPixel<TPixel>
        {
            return SixLabors.Telemetry.Telemetry.Provider.Operation(() => Create("Encode Image as base64 string", "Encoding", source, d =>
            {
                d.Properties.Add("Format", format?.Name);
            }));
        }
    }
}
