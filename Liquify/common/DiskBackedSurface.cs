///////////////////////////////////////////////////////////////////////////////////
//// Paint.NET                                                                   //
//// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
//// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
//// See src/Resources/Files/License.txt for full licensing and attribution      //
//// details.                                                                    //
//// .                                                                           //
///////////////////////////////////////////////////////////////////////////////////

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
        private string backingfile;
        private State state;
        private bool written;
        private DisplacementMesh surface;
        private int width;
        private int height;

        private void Initialize()
        {
            width = surface.Width;
            height = surface.Height;
            backingfile = Path.GetTempFileName();
            state = State.Memory;
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
            ret.backingfile = Path.GetTempFileName();

            try
            {
                using (FileStream fs = new FileStream(ret.backingfile, FileMode.Create))
                using (DeflateStream ds = new DeflateStream(fs, CompressionLevel.Fastest))
                {
                    FailIfTesting();
                    mesh.SaveRaw(ds, rect);
                }
            }
            catch
            {
                File.Delete(ret.backingfile);
                throw;
            }

            ret.written = true;
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

            using (FileStream fs = new FileStream(backingfile, FileMode.Open, FileAccess.Read))
            using (DeflateStream ds = new DeflateStream(fs, CompressionMode.Decompress))
            {
                DisplacementMesh loaded = new DisplacementMesh(width, height);
                loaded.LoadRaw(ds);
                surface = loaded;
                state = State.Memory;
            }
        }

        public void ToDisk()
        {
            if (state == State.Disk) { return; }

            // the surface isn't modified once it has been written, so the file only needs writing once
            if (!written)
            {
                // most of a mesh is zeros or smooth, so even the fastest compression shrinks it a lot
                using (FileStream fs = new FileStream(backingfile, FileMode.Create))
                using (DeflateStream ds = new DeflateStream(fs, CompressionLevel.Fastest))
                {
                    FailIfTesting();
                    surface.SaveRaw(ds);
                }
                written = true;
            }

            surface.Dispose();
            state = State.Disk;
        }

        #region IDisposable Members

        public void Dispose()
        {
            File.Delete(backingfile);
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