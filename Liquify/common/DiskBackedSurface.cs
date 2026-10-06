///////////////////////////////////////////////////////////////////////////////////
//// Paint.NET                                                                   //
//// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
//// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
//// See src/Resources/Files/License.txt for full licensing and attribution      //
//// details.                                                                    //
//// .                                                                           //
///////////////////////////////////////////////////////////////////////////////////

// This file comes from the Paint.NET source code and has been modified for this plugin.
// Modifications Copyright (C) Zach Walker. The Paint.NET license that the notice above
// refers to is reproduced in the LICENSE file at the root of this repository.

using System;
using System.IO;
using System.IO.Compression;
using PaintDotNet;
using State = pyrochild.effects.liquify.DiskBackedSurfaceState;
using System.Drawing;
using System.Threading;

namespace pyrochild.effects.liquify
{
    public sealed class DiskBackedSurface
        : IDisposable
    {
        // The file is made when the data is first written and kept open from then on. It is
        // marked delete-on-close, so Windows removes it when this is disposed and also when the
        // process ends any other way, a crash included.
        private FileStream file;
        private string backingfile;
        private State state;
        private DisplacementMesh surface;
        private int width;
        private int height;

        private void Initialize()
        {
            width = surface.Width;
            height = surface.Height;
            state = State.Memory;
        }

        private void Write(DisplacementMesh mesh, Rectangle rect)
        {
            backingfile = Path.Combine(Path.GetTempPath(), "Liquify-" + Guid.NewGuid().ToString("N") + ".tmp");
            file = new FileStream(backingfile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);

            try
            {
                // most of a mesh is zeros or smooth, so even the fastest compression shrinks it a lot
                using (DeflateStream ds = new DeflateStream(file, CompressionLevel.Fastest, true))
                {
                    FailIfTesting();
                    mesh.SaveRaw(ds, rect);
                }

                // so a full disk shows up now and not when the data is wanted back
                file.Flush();
            }
            catch
            {
                file.Dispose();
                file = null;
                throw;
            }
        }

        public DiskBackedSurface(int width, int height)
        {
            surface = new DisplacementMesh(width, height);
            Initialize();
        }

        public DiskBackedSurface(Size size)
        {
            surface = new DisplacementMesh(size);
            Initialize();
        }

        public DiskBackedSurface(DisplacementMesh surface, bool takeownership)
        {
            if (takeownership)
            {
                this.surface = surface;
            }
            else
            {
                this.surface = surface.Clone();
            }
            Initialize();
        }

        private DiskBackedSurface()
        {
        }

        /// <summary>
        /// For trying out what happens when the disk can't be written to (full, say): while set,
        /// every write made from this thread fails the way a real one would, without needing a
        /// full disk. It is per thread so that one test can't trip up another.
        /// </summary>
        [ThreadStatic]
        internal static bool TestFailWritesOnThisThread;

        private static void FailIfTesting()
        {
            if (TestFailWritesOnThisThread)
            {
                throw new IOException("There is not enough space on the disk. (Simulated for testing.)");
            }
        }

        /// <summary>
        /// Writes a part of a mesh straight to disk, without making an in-memory copy of it first.
        /// </summary>
        public static DiskBackedSurface FromRect(DisplacementMesh mesh, Rectangle rect)
        {
            DiskBackedSurface ret = new DiskBackedSurface();
            ret.width = rect.Width;
            ret.height = rect.Height;
            ret.Write(mesh, rect);
            ret.state = State.Disk;
            return ret;
        }

        public string BackingFilePath { get { return backingfile; } }
        public DisplacementMesh Surface { get { return surface; } }
        public int Width { get { return width; } }
        public int Height { get { return height; } }
        public Size Size { get { return new Size(width, height); } }
        public State State { get { return state; } }
        public Rectangle Bounds { get { return new Rectangle(0, 0, width, height); } }

        public void ToMemory()
        {
            if (state == State.Memory) { return; }

            file.Seek(0, SeekOrigin.Begin);
            using (DeflateStream ds = new DeflateStream(file, CompressionMode.Decompress, true))
            {
                DisplacementMesh loaded = new DisplacementMesh(width, height);
                try
                {
                    loaded.LoadRaw(ds);
                }
                catch
                {
                    loaded.Dispose();
                    throw;
                }
                surface = loaded;
                state = State.Memory;
            }
        }

        public void ToDisk()
        {
            if (state == State.Disk) { return; }

            // the surface isn't modified once it has been written, so the file only needs writing once
            if (file == null)
            {
                Write(surface, surface.Bounds);
            }

            surface.Dispose();
            state = State.Disk;
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (file != null)
            {
                file.Dispose();
                file = null;
            }
            if (surface != null)
            {
                surface.Dispose();
            }
            state = State.Disposed;
        }

        #endregion
    }

    public enum DiskBackedSurfaceState
    {
        Memory,
        Disk,
        Disposed
    }
}