using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.TestUtilities
{
    /// <summary>
    /// <see cref="IXunitSerializationInfo"/>-compatible serialization for cross-process use-cases.
    /// </summary>
    internal class BasicSerializer : IXunitSerializationInfo
    {
        private readonly Dictionary<string, string> map = new Dictionary<string, string>();

        public const char Separator = ':';

        private string DumpToString()
        {
            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms);
            foreach (KeyValuePair<string, string> kv in this.map)
            {
                writer.WriteLine($"{kv.Key}{Separator}{kv.Value}");
            }
            writer.Flush();
            byte[] data = ms.ToArray();
            return System.Convert.ToBase64String(data);
        }

        private void LoadDump(string dump)
        {
            byte[] data = System.Convert.FromBase64String(dump);

            using var ms = new MemoryStream(data);
            using var reader = new StreamReader(ms);
            for (string s = reader.ReadLine(); s != null ; s = reader.ReadLine())
            {
                string[] kv = s.Split(Separator);
                this.map[kv[0]] = kv[1];
            }
        }

        public static string Serialize(IXunitSerializable serializable)
        {
            var serializer = new BasicSerializer();
            serializable.Serialize(serializer);
            return serializer.DumpToString();
        }

        public static T Deserialize<T>(string dump) where T : IXunitSerializable
        {
            T result = Activator.CreateInstance<T>();
            var serializer = new BasicSerializer();
            serializer.LoadDump(dump);
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
