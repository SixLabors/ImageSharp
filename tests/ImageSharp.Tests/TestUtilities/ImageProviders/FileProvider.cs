// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests
{
    public abstract partial class TestImageProvider<TPixel> : IXunitSerializable
        where TPixel : unmanaged, IPixel<TPixel>
    {
        internal class FileProvider : TestImageProvider<TPixel>, IXunitSerializable
        {
            // Need PixelTypes in the dictionary key, because result images of TestImageProvider<TPixel>.FileProvider
            // are shared between PixelTypes.Color & PixelTypes.Rgba32
            private class Key : IEquatable<Key>
            {
                private readonly Tuple<PixelTypes, string, Type, int> commonValues;

                private readonly Dictionary<string, object> decoderParameters;

                public Key(PixelTypes pixelType, string filePath, int allocatorBufferCapacity, IImageDecoder customDecoder)
                {
                    Type customType = customDecoder?.GetType();
                    this.commonValues = new Tuple<PixelTypes, string, Type, int>(
                        pixelType,
                        filePath,
                        customType,
                        allocatorBufferCapacity);
                    this.decoderParameters = GetDecoderParameters(customDecoder);
                }

                private static Dictionary<string, object> GetDecoderParameters(IImageDecoder customDecoder)
                {
                    Type type = customDecoder.GetType();

                    var data = new Dictionary<string, object>();

                    while (type != null && type != typeof(object))
                    {
                        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        foreach (PropertyInfo p in properties)
                        {
                            string key = $"{type.FullName}.{p.Name}";
                            object value = p.GetValue(customDecoder);
                            data[key] = value;
                        }

                        type = type.GetTypeInfo().BaseType;
                    }

                    return data;
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

                        if (!object.Equals(kv.Value, otherVal))
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

            private static readonly ConcurrentDictionary<Key, Image<TPixel>> Cache = new ConcurrentDictionary<Key, Image<TPixel>>();

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

            public override Image<TPixel> GetImage(IImageDecoder decoder)
            {
                Guard.NotNull(decoder, nameof(decoder));

                if (!TestEnvironment.Is64BitProcess)
                {
                    return this.LoadImage(decoder);
                }

                int bufferCapacity = this.Configuration.MemoryAllocator.GetBufferCapacityInBytes();
                var key = new Key(this.PixelType, this.FilePath, bufferCapacity, decoder);

                Image<TPixel> cachedImage = Cache.GetOrAdd(key, _ => this.LoadImage(decoder));

                return cachedImage.Clone(this.Configuration);
            }

            public override Task<Image<TPixel>> GetImageAsync(IImageDecoder decoder)
            {
                Guard.NotNull(decoder, nameof(decoder));

                // Used in small subset of decoder tests, no caching.
                string path = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.FilePath);
                return Image.LoadAsync<TPixel>(this.Configuration, path, decoder);
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

            private Image<TPixel> LoadImage(IImageDecoder decoder)
            {
                var testFile = TestFile.Create(this.FilePath);
                return Image.Load<TPixel>(this.Configuration, testFile.Bytes, decoder);
            }
        }

        public static string GetFilePathOrNull(ITestImageProvider provider)
        {
            var fileProvider = provider as FileProvider;
            return fileProvider?.FilePath;
        }
    }
}
