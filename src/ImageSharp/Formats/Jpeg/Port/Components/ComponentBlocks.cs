// <copyright file="Components.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using System;

    /// <summary>
    /// Contains all the decoded component blocks
    /// </summary>
    internal class ComponentBlocks : IDisposable
    {
        private bool isDisposed;

        /// <summary>
        /// Gets or sets the component blocks
        /// </summary>
        public Component[] Components { get; set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing">Whether to dispose of managed objects</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    if (this.Components != null)
                    {
                        for (int i = 0; i < this.Components.Length; i++)
                        {
                            this.Components[i].Dispose();
                        }
                    }
                }

                // Set large fields to null.
                this.Components = null;
                this.isDisposed = true;
            }
        }
    }
}