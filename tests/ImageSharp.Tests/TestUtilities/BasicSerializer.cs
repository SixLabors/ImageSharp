// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.TestUtilities
{
    /// <summary>
    /// RemoteExecutor can only execute static methods, which can only consume string arguments,
    /// because data is being passed on command line interface. This utility allows serialization
    /// of <see cref="IXunitSerializable"/> types to strings.
    /// </summary>
    internal class BasicSerializer : IXunitSerializationInfo
    {
        private readonly Dictionary<string, string> map = new Dictionary<string, string>();

        public const char Separator = ':';

        private string DumpToString(Type type)
        {
            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms);
            writer.WriteLine(type.FullName);
            foreach (KeyValuePair<string, string> kv in this.map)
            {
                writer.WriteLine($"{kv.Key}{Separator}{kv.Value}");
            }

            writer.Flush();
            byte[] data = ms.ToArray();
            return System.Convert.ToBase64String(data);
        }

        private Type LoadDump(string dump)
        {
            byte[] data = System.Convert.FromBase64String(dump);

            using var ms = new MemoryStream(data);
            using var reader = new StreamReader(ms);
            var type = Type.GetType(reader.ReadLine());
            for (string s = reader.ReadLine(); s != null; s = reader.ReadLine())
            {
                string[] kv = s.Split(Separator);
                this.map[kv[0]] = kv[1];
            }

            return type;
        }

        public static string Serialize(IXunitSerializable serializable)
        {
            var serializer = new BasicSerializer();
            serializable.Serialize(serializer);
            return serializer.DumpToString(serializable.GetType());
        }

        public static T Deserialize<T>(string dump)
            where T : IXunitSerializable
        {
            var serializer = new BasicSerializer();
            Type type = serializer.LoadDump(dump);

            var result = (T)Activator.CreateInstance(type);
            result.Deserialize(serializer);
            return result;
        }

        public void AddValue(string key, object value, Type type = null)
        {
            Guard.NotNull(key, nameof(key));
            if (value == null)
            {
                return;
            }

            type ??= value.GetType();

            this.map[key] = TypeDescriptor.GetConverter(type).ConvertToInvariantString(value);
        }

        public object GetValue(string key, Type type)
        {
            Guard.NotNull(key, nameof(key));

            if (!this.map.TryGetValue(key, out string str))
            {
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            }

            return TypeDescriptor.GetConverter(type).ConvertFromInvariantString(str);
        }

        public T GetValue<T>(string key) => (T)this.GetValue(key, typeof(T));
    }
}
