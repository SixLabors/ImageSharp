/* Copyright (C) 2008-2011, Bit Miracle
 * http://www.bitmiracle.com
 * 
 * Copyright (C) 1994-1996, Thomas G. Lane.
 * This file is part of the Independent JPEG Group's software.
 * For conditions of distribution and use, see the accompanying README file.
 *
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace BitMiracle.LibJpeg.Classic
{
    /// <summary>
    /// The progress monitor object.
    /// </summary>
    /// <seealso href="febdc6af-ca72-4f3b-8cfe-3473ce6a7c7f.htm" target="_self">Progress monitoring</seealso>
#if EXPOSE_LIBJPEG
    public
#endif
    class jpeg_progress_mgr
    {
        private int m_passCounter;
        private int m_passLimit;
        private int m_completedPasses;
        private int m_totalPasses;

        /// <summary>
        /// Occurs when progress is changed.
        /// </summary>
        /// <seealso href="febdc6af-ca72-4f3b-8cfe-3473ce6a7c7f.htm" target="_self">Progress monitoring</seealso>
        public event EventHandler OnProgress;

        /// <summary>
        /// Gets or sets the number of work units completed in this pass.
        /// </summary>
        /// <value>The number of work units completed in this pass.</value>
        /// <seealso href="febdc6af-ca72-4f3b-8cfe-3473ce6a7c7f.htm" target="_self">Progress monitoring</seealso>
        public int Pass_counter
        {
            get { return m_passCounter; }
            set { m_passCounter = value; }
        }
        
        /// <summary>
        /// Gets or sets the total number of work units in this pass.
        /// </summary>
        /// <value>The total number of work units in this pass.</value>
        /// <seealso href="febdc6af-ca72-4f3b-8cfe-3473ce6a7c7f.htm" target="_self">Progress monitoring</seealso>
        public int Pass_limit
        {
            get { return m_passLimit; }
            set { m_passLimit = value; }
        }
        
        /// <summary>
        /// Gets or sets the number of passes completed so far.
        /// </summary>
        /// <value>The number of passes completed so far.</value>
        /// <seealso href="febdc6af-ca72-4f3b-8cfe-3473ce6a7c7f.htm" target="_self">Progress monitoring</seealso>
        public int Completed_passes
        {
            get { return m_completedPasses; }
            set { m_completedPasses = value; }
        }
        
        /// <summary>
        /// Gets or sets the total number of passes expected.
        /// </summary>
        /// <value>The total number of passes expected.</value>
        /// <seealso href="febdc6af-ca72-4f3b-8cfe-3473ce6a7c7f.htm" target="_self">Progress monitoring</seealso>
        public int Total_passes
        {
            get { return m_totalPasses; }
            set { m_totalPasses = value; }
        }

        /// <summary>
        /// Indicates that progress was changed.
        /// </summary>
        /// <remarks>Call this method if you change some progress parameters manually.
        /// This method ensures happening of the <see cref="jpeg_progress_mgr.OnProgress">OnProgress</see> event.</remarks>
        public void Updated()
        {
            if (OnProgress != null)
                OnProgress(this, new EventArgs());
        }
    }
}