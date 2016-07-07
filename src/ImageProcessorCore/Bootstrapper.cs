// <copyright file="Bootstrapper.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text;

    using ImageProcessorCore.Formats;

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

        private readonly Dictionary<Type, Type> pixelAccessors;

        /// <summary>
        /// Prevents a default instance of the <see cref="Bootstrapper"/> class from being created.
        /// </summary>
        private Bootstrapper()
        {
            this.imageFormats = new List<IImageFormat>
            {
                new BmpFormat(),
                //new JpegFormat(),
                //new PngFormat(),
                //new GifFormat()
            };

            this.pixelAccessors = new Dictionary<Type, Type>
            {
                { typeof(Bgra32), typeof(Bgra32PixelAccessor) }
            };
        }

        /// <summary>
        /// Gets the current bootstrapper instance.
        /// </summary>
        public static Bootstrapper Instance = Lazy.Value;

        /// <summary>
        /// Gets the list of supported <see cref="IImageFormat"/>
        /// </summary>
        public IReadOnlyCollection<IImageFormat> ImageFormats => new ReadOnlyCollection<IImageFormat>(this.imageFormats);

        /// <summary>
        /// Adds a new <see cref="IImageFormat"/> to the collection of supported image formats.
        /// </summary>
        /// <param name="format">The new format to add.</param>
        public void AddImageFormat(IImageFormat format)
        {
            this.imageFormats.Add(format);
        }

        /// <summary>
        /// Gets an instance of the correct <see cref="IPixelAccessor"/> for the packed vector.
        /// </summary>
        /// <typeparam name="TPackedVector">The type of pixel data.</typeparam>
        /// <param name="image">The image</param>
        /// <returns>The <see cref="IPixelAccessor"/></returns>
        public IPixelAccessor<TPackedVector> GetPixelAccessor<TPackedVector>(IImageBase image)
            where TPackedVector : IPackedVector, new()
        {
            Type packed = typeof(TPackedVector);
            if (this.pixelAccessors.ContainsKey(packed))
            {
                // TODO: Double check this. It should work...

                return (IPixelAccessor<TPackedVector>)new Bgra32PixelAccessor<TPackedVector>(image);
                //return (IPixelAccessor)Activator.CreateInstance(this.pixelAccessors[packed], image);
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("PixelAccessor cannot be loaded. Available accessors:");

            foreach (Type value in this.pixelAccessors.Values)
            {
                stringBuilder.AppendLine("-" + value.Name);
            }

            throw new NotSupportedException(stringBuilder.ToString());
        }

        /// <summary>
        /// Gets an instance of the correct <see cref="IPixelAccessor"/> for the packed vector.
        /// </summary>
        /// <typeparam name="TPackedVector">The type of pixel data.</typeparam>
        /// <param name="image">The image</param>
        /// <returns>The <see cref="IPixelAccessor"/></returns>
        public IPixelAccessor GetPixelAccessor<TPackedVector>(ImageFrame<TPackedVector> image)
            where TPackedVector : IPackedVector
        {
            Type packed = typeof(TPackedVector);
            if (!this.pixelAccessors.ContainsKey(packed))
            {
                return (IPixelAccessor)Activator.CreateInstance(this.pixelAccessors[packed], image);
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("PixelAccessor cannot be loaded. Available accessors:");

            foreach (Type value in this.pixelAccessors.Values)
            {
                stringBuilder.AppendLine("-" + value.Name);
            }

            throw new NotSupportedException(stringBuilder.ToString());
        }
    }
}
