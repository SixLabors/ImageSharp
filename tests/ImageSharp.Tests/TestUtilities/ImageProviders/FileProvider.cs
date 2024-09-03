// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.Concurrent;
using System.Reflection;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests;

public abstract partial class TestImageProvider<TPixel> : IXunitSerializable
    where TPixel : unmanaged, IPixel<TPixel>
{
    internal class FileProvider : TestImageProvider<TPixel>, IXunitSerializable
    {
        // Need PixelTypes in the dictionary key, because result images of TestImageProvider<TPixel>.FileProvider
        // are shared between PixelTypes.Color & PixelTypes.Rgba32
        private class Key : IEquatable<Key>
        {
            private readonly Tuple<PixelTypes, string, Type> commonValues;

            private readonly Dictionary<string, object> decoderParameters;

            public Key(
                PixelTypes pixelType,
                string filePath,
                IImageDecoder customDecoder,
                DecoderOptions options,
                ISpecializedDecoderOptions specialized)
            {
                Type customType = customDecoder?.GetType();
                this.commonValues = new Tuple<PixelTypes, string, Type>(
                    pixelType,
                    filePath,
                    customType);
                this.decoderParameters = GetDecoderParameters(options, specialized);
            }

            private static Dictionary<string, object> GetDecoderParameters(
                DecoderOptions options,
                ISpecializedDecoderOptions specialized)
            {
                Type type = options.GetType();

                Dictionary<string, object> data = new Dictionary<string, object>();

                while (type != null && type != typeof(object))
                {
                    foreach (PropertyInfo p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        string key = $"{type.FullName}.{p.Name}";
                        data[key] = p.GetValue(options);
                    }

                    type = type.GetTypeInfo().BaseType;
                }

                GetSpecializedDecoderParameters(data, specialized);

                return data;
            }

            private static void GetSpecializedDecoderParameters(
                Dictionary<string, object> data,
                ISpecializedDecoderOptions options)
            {
                if (options is null)
                {
                    return;
                }

                Type type = options.GetType();

                while (type != null && type != typeof(object))
                {
                    foreach (PropertyInfo p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (p.PropertyType == typeof(DecoderOptions))
                        {
                            continue;
                        }

                        string key = $"{type.FullName}.{p.Name}";
                        data[key] = p.GetValue(options);
                    }

                    type = type.GetTypeInfo().BaseType;
                }
            }

            public bool Equals(Key other)
            {
                if (other is null)
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                if (!this.commonValues.Equals(other.commonValues))
                {
                    return false;
                }

                if (this.decoderParameters.Count != other.decoderParameters.Count)
                {
                    return false;
                }

                foreach (KeyValuePair<string, object> kv in this.decoderParameters)
                {
                    if (!other.decoderParameters.TryGetValue(kv.Key, out object otherVal))
                    {
                        return false;
                    }

                    if (!Equals(kv.Value, otherVal))
                    {
                        return false;
                    }
                }

                return true;
            }

            public override bool Equals(object obj)
            {
                if (obj is null)
                {
                    return false;
                }

                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                if (obj.GetType() != this.GetType())
                {
                    return false;
                }

                return this.Equals((Key)obj);
            }

            public override int GetHashCode() => this.commonValues.GetHashCode();

            public static bool operator ==(Key left, Key right) => Equals(left, right);

            public static bool operator !=(Key left, Key right) => !Equals(left, right);
        }

        private static readonly ConcurrentDictionary<Key, Image<TPixel>> Cache = new();

        // Needed for deserialization!
        // ReSharper disable once UnusedMember.Local
        public FileProvider()
        {
        }

        public FileProvider(string filePath) => this.FilePath = filePath;

        /// <summary>
        /// Gets the file path relative to the "~/tests/images" folder
        /// </summary>
        public string FilePath { get; private set; }

        public override string SourceFileOrDescription => this.FilePath;

        public override Image<TPixel> GetImage()
        {
            IImageDecoder decoder = TestEnvironment.GetReferenceDecoder(this.FilePath);
            return this.GetImage(decoder);
        }

        public override Image<TPixel> GetImage(IImageDecoder decoder, DecoderOptions options)
        {
            Guard.NotNull(decoder, nameof(decoder));
            Guard.NotNull(options, nameof(options));

            // Do not cache with 64 bits or if image has been created with non-default MemoryAllocator
            if (!TestEnvironment.Is64BitProcess || this.Configuration.MemoryAllocator != MemoryAllocator.Default)
            {
                return this.DecodeImage(decoder, options);
            }

            // do not cache so we can track allocation correctly when validating memory
            if (MemoryAllocatorValidator.MonitoringAllocations)
            {
                return this.DecodeImage(decoder, options);
            }

            Key key = new Key(this.PixelType, this.FilePath, decoder, options, null);
            Image<TPixel> cachedImage = Cache.GetOrAdd(key, _ => this.DecodeImage(decoder, options));

            return cachedImage.Clone(this.Configuration);
        }

        public override async Task<Image<TPixel>> GetImageAsync(IImageDecoder decoder, DecoderOptions options)
        {
            Guard.NotNull(decoder, nameof(decoder));
            Guard.NotNull(options, nameof(options));

            options.SetConfiguration(this.Configuration);

            // Used in small subset of decoder tests, no caching.
            // TODO: Check Path here. Why combined?
            string path = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.FilePath);
            using Stream stream = System.IO.File.OpenRead(path);
            return await decoder.DecodeAsync<TPixel>(options, stream);
        }

        public override Image<TPixel> GetImage<T>(ISpecializedImageDecoder<T> decoder, T options)
        {
            Guard.NotNull(decoder, nameof(decoder));
            Guard.NotNull(options, nameof(options));

            // Do not cache with 64 bits or if image has been created with non-default MemoryAllocator
            if (!TestEnvironment.Is64BitProcess || this.Configuration.MemoryAllocator != MemoryAllocator.Default)
            {
                return this.DecodeImage(decoder, options);
            }

            // do not cache so we can track allocation correctly when validating memory
            if (MemoryAllocatorValidator.MonitoringAllocations)
            {
                return this.DecodeImage(decoder, options);
            }

            Key key = new Key(this.PixelType, this.FilePath, decoder, options.GeneralOptions, options);
            Image<TPixel> cachedImage = Cache.GetOrAdd(key, _ => this.DecodeImage(decoder, options));

            return cachedImage.Clone(this.Configuration);
        }

        public override async Task<Image<TPixel>> GetImageAsync<T>(ISpecializedImageDecoder<T> decoder, T options)
        {
            Guard.NotNull(decoder, nameof(decoder));
            Guard.NotNull(options, nameof(options));

            options.GeneralOptions.SetConfiguration(this.Configuration);

            // Used in small subset of decoder tests, no caching.
            // TODO: Check Path here. Why combined?
            string path = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.FilePath);
            using Stream stream = System.IO.File.OpenRead(path);
            return await decoder.DecodeAsync<TPixel>(options, stream);
        }

        public override void Deserialize(IXunitSerializationInfo info)
        {
            this.FilePath = info.GetValue<string>("path");

            base.Deserialize(info); // must be called last
        }

        public override void Serialize(IXunitSerializationInfo info)
        {
            base.Serialize(info);
            info.AddValue("path", this.FilePath);
        }

        private Image<TPixel> DecodeImage(IImageDecoder decoder, DecoderOptions options)
        {
            options.SetConfiguration(this.Configuration);

            TestFile testFile = TestFile.Create(this.FilePath);
            using Stream stream = new MemoryStream(testFile.Bytes);
            return decoder.Decode<TPixel>(options, stream);
        }

        private Image<TPixel> DecodeImage<T>(ISpecializedImageDecoder<T> decoder, T options)
            where T : class, ISpecializedDecoderOptions, new()
        {
            options.GeneralOptions.SetConfiguration(this.Configuration);

            TestFile testFile = TestFile.Create(this.FilePath);
            using Stream stream = new MemoryStream(testFile.Bytes);
            return decoder.Decode<TPixel>(options, stream);
        }
    }

    public static string GetFilePathOrNull(ITestImageProvider provider)
    {
        FileProvider fileProvider = provider as FileProvider;
        return fileProvider?.FilePath;
    }
}
