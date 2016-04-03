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
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Runtime.Serialization;

namespace pyrochild.effects.liquify
{
    public sealed class DiskBackedSurface
        : IDisposable,
          ICloneable
    {
        private string backingfile;
        private State state;
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

        public string BackingFilePath { get { return backingfile; } }
        public DisplacementMesh Surface { get { return surface; } }
        public int Width { get { return width; } }
        public int Height { get { return height; } }
        public Size Size { get { return new Size(width, height); } }
        public State State { get { return state; } }
        public Rectangle Bounds { get { return new Rectangle(0, 0, width, height); } }

        private class SB:SerializationBinder
        {
            public override Type BindToType(string assemblyName, string typeName)
            {
                return Type.GetType(string.Format("{0}, {1}", typeName, assemblyName));
            }
        }

        public void ToMemory()
        {
            if (state == State.Memory) { return; }

            FileStream fs = new FileStream(backingfile, FileMode.Open, FileAccess.Read);
            try
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Binder = new SB();
                surface = (DisplacementMesh)bf.Deserialize(fs);
                state = State.Memory;
            }
            catch (ThreadAbortException) { }
            finally
            {
                fs.Close();
            }
        }

        public bool TryToMemory()
        {
            try
            {
                ToMemory();
                return true;
            }
            catch { return false; }
        }

        public void ToDisk()
        {
            if (state == State.Disk) { return; }

            FileStream fs = new FileStream(backingfile, FileMode.Create);
            try
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(fs, surface);
                state = State.Disk;
            }
            catch (ThreadAbortException) { }
            finally
            {
                fs.Close();
                surface.Dispose();
            }
        }

        public bool TryToDisk()
        {
            try
            {
                ToDisk();
                return true;
            }
            catch { return false; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            File.Delete(backingfile);
            surface.Dispose();
            state = State.Disposed;
        }

        #endregion

        #region ICloneable Members

        public object Clone()
        {
            DiskBackedSurface retval = new DiskBackedSurface(this.surface, true);
            retval.state = this.state;
            retval.backingfile = this.backingfile;
            return retval;
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