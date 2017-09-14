// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests
{
    public abstract partial class TestImageProvider<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private class FileProvider : TestImageProvider<TPixel>, IXunitSerializable
        {
            // Need PixelTypes in the dictionary key, because result images of TestImageProvider<TPixel>.FileProvider
            // are shared between PixelTypes.Color & PixelTypes.Rgba32
            private class Key : IEquatable<Key>
            {
                private Tuple<PixelTypes, string, Type> commonValues;

                private Dictionary<string, object> decoderParameters;

                public Key(PixelTypes pixelType, string filePath, IImageDecoder customDecoder)
                {
                    Type customType = customDecoder?.GetType();
                    this.commonValues = new Tuple<PixelTypes, string, Type>(pixelType, filePath, customType);
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
                    if (ReferenceEquals(null, other)) return false;
                    if (ReferenceEquals(this, other)) return true;
                    if (!this.commonValues.Equals(other.commonValues)) return false;

                    if (this.decoderParameters.Count != other.decoderParameters.Count)
                    {
                        return false;
                    }

                    foreach (KeyValuePair<string, object> kv in this.decoderParameters)
                    {
                        object otherVal;
                        if (!other.decoderParameters.TryGetValue(kv.Key, out otherVal))
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
                    if (ReferenceEquals(null, obj)) return false;
                    if (ReferenceEquals(this, obj)) return true;
                    if (obj.GetType() != this.GetType()) return false;
                    return this.Equals((Key)obj);
                }

                public override int GetHashCode()
                {
                    return this.commonValues.GetHashCode();
                }

                public static bool operator ==(Key left, Key right)
                {
                    return Equals(left, right);
                }

                public static bool operator !=(Key left, Key right)
                {
                    return !Equals(left, right);
                }
            }

            private static readonly ConcurrentDictionary<Key, Image<TPixel>> cache = new ConcurrentDictionary<Key, Image<TPixel>>();

            public FileProvider(string filePath)
            {
                this.FilePath = filePath;
            }

            public FileProvider()
            {
            }

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

                Key key = new Key(this.PixelType, this.FilePath, decoder);

                Image<TPixel> cachedImage = cache.GetOrAdd(
                    key,
                    fn =>
                        {
                            TestFile testFile = TestFile.Create(this.FilePath);
                            return Image.Load<TPixel>(testFile.Bytes, decoder);
                        });

                return cachedImage.Clone();
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
        }

        public static string GetFilePathOrNull(ITestImageProvider provider)
        {
            var fileProvider = provider as FileProvider;
            return fileProvider?.FilePath;
        }
    }
}