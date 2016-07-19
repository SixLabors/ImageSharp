// <copyright file="Bootstrapper.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Reflection;
    using System.Threading.Tasks;

    using Formats;

    /// <summary>
    /// Provides initialization code which allows extending the library.
    /// </summary>
    public class Bootstrapper
    {
        /// <summary>
        /// A new instance Initializes a new instance of the <see cref="Bootstrapper"/> class.
        /// with lazy initialization.
        /// </summary>
        private static readonly Lazy<Bootstrapper> Lazy = new Lazy<Bootstrapper>(() => new Bootstrapper());

        /// <summary>
        /// The default list of supported <see cref="IImageFormat"/>
        /// </summary>
        private readonly List<IImageFormat> imageFormats;

        private readonly Dictionary<Type, Func<IImageBase, IPixelAccessor>> pixelAccessors;

        /// <summary>
        /// Prevents a default instance of the <see cref="Bootstrapper"/> class from being created.
        /// </summary>
        private Bootstrapper()
        {
            this.imageFormats = new List<IImageFormat>
            {
                new BmpFormat(),
                //new JpegFormat(),
                new PngFormat(),
                new GifFormat()
            };

            this.pixelAccessors = new Dictionary<Type, Func<IImageBase, IPixelAccessor>>
            {
                { typeof(Color), i=> new ColorPixelAccessor(i) }
            };
        }

        /// <summary>
        /// Gets the current bootstrapper instance.
        /// </summary>
        public static Bootstrapper Instance = Lazy.Value;

        /// <summary>
        /// Gets the collection of supported <see cref="IImageFormat"/>
        /// </summary>
        public IReadOnlyCollection<IImageFormat> ImageFormats =>
            new ReadOnlyCollection<IImageFormat>(this.imageFormats);

        /// <summary>
        /// Gets the collection of supported pixel accessors
        /// </summary>
        public IReadOnlyDictionary<Type, Func<IImageBase, IPixelAccessor>> PixelAccessors =>
            new ReadOnlyDictionary<Type, Func<IImageBase, IPixelAccessor>>(this.pixelAccessors);

        /// <summary>
        /// Gets or sets the global parallel options for processing tasks in parallel.
        /// </summary>
        public ParallelOptions ParallelOptions { get; set; } = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

        /// <summary>
        /// Adds a new <see cref="IImageFormat"/> to the collection of supported image formats.
        /// </summary>
        /// <param name="format">The new format to add.</param>
        public void AddImageFormat(IImageFormat format)
        {
            this.imageFormats.Add(format);
        }

        /// <summary>
        /// Adds a pixel accessor for the given pixel format.
        /// </summary>
        /// <param name="packedType">The packed format type, must implement <see cref="IPackedVector"/></param>
        /// <param name="initializer">The function to return a new instance of the pixel accessor.</param>
        public void AddPixelAccessor(Type packedType, Func<IImageBase, IPixelAccessor> initializer)
        {
            if (!typeof(IPackedVector).GetTypeInfo().IsAssignableFrom(packedType.GetTypeInfo()))
            {
                throw new ArgumentException($"Type {packedType} must implement {nameof(IPackedVector)}");
            }

            this.pixelAccessors.Add(packedType, initializer);
        }

        /// <summary>
        /// Gets an instance of the correct <see cref="IPixelAccessor"/> for the packed vector.
        /// </summary>
        /// <typeparam name="T">The type of pixel data.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam> 
        /// <param name="image">The image</param>
        /// <returns>The <see cref="IPixelAccessor"/></returns>
        public IPixelAccessor<T, TP> GetPixelAccessor<T, TP>(IImageBase image)
            where T : IPackedVector<TP>
            where TP : struct
        {
            Type packed = typeof(T);
            if (this.pixelAccessors.ContainsKey(packed))
            {
                return (IPixelAccessor<T, TP>)this.pixelAccessors[packed].Invoke(image);
            }

            throw new NotSupportedException($"PixelAccessor cannot be loaded for {packed}:");
        }
    }
}
