using pyrochild.effects.common;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace pyrochild.effects.liquify
{
    internal class LiquifyRenderer : QueuedToolRenderer
    {
        DisplacementMesh mesh;
        int meshwidth;
        int meshheight;
        Point lastmouse;
        int strokesize;
        int radius;
        float pressure;
        float[] density;
        LiquifyMode mode;
        DisplacementMesh buffer;
        float remainingSpace;

        public unsafe LiquifyRenderer(DisplacementMesh mesh)
            : base(mesh)
        {
            this.mesh = mesh;
            meshwidth = mesh.Width;
            meshheight = mesh.Height;
        }

        protected override void OnMouseHold(QueuedToolEventArgs args)
        {
            if (mode != LiquifyMode.Push)
            {
                remainingSpace = 0;
            }
            OnMouseMove(args);
        }

        protected override void OnMouseDown(QueuedToolEventArgs args)
        {
            LiquifyEventArgs e = args as LiquifyEventArgs;

            if (e.Button == MouseButtons.Left)
            {
                // size, pressure, density and mode are fixed for the whole stroke
                strokesize = e.Size;
                radius = strokesize / 2;
                remainingSpace = 0;
                buffer = new DisplacementMesh(strokesize, strokesize);
                pressure = e.Pressure;
                mode = e.Mode;
                density = new float[radius];

                float densityexp = 2 - 2 * e.Density;
                for (int i = 0; i < density.Length; ++i)
                {
                    density[i] = (float)Math.Pow(1 - (float)i / radius, densityexp);
                }

                OnMouseMove(args);
            }
        }

        protected unsafe override void OnMouseMove(QueuedToolEventArgs args)
        {
            LiquifyEventArgs e = args as LiquifyEventArgs;

            bool stroking = e.Button == MouseButtons.Left && buffer != null;
            int size = stroking ? strokesize : e.Size;

            Rectangle invrect = new Rectangle(lastmouse.X - radius, lastmouse.Y - radius, size, size);
            invrect = Rectangle.Union(invrect, new Rectangle(e.X - radius, e.Y - radius, size, size));
            
            if (stroking)
            {
                Size brushsize = new Size(size, size);
                DisplacementVector* meshptr0 = (DisplacementVector*)mesh.Scan0;
                DisplacementVector* bufferptr0 = (DisplacementVector*)buffer.Scan0;
                DisplacementVector* meshptr;
                DisplacementVector* bufferptr;

                float dist = Utility.Distance(lastmouse, e.Location);
                if (dist == 0) dist = 1;

                //unit vector in the direction of motion
                DisplacementVector u = new DisplacementVector((e.X - lastmouse.X) / dist, (e.Y - lastmouse.Y) / dist);
                
                DisplacementVector displace = DisplacementVector.Zero;

                int spacing = Math.Max(1, radius / 4);
                float f;
                for (f = remainingSpace; f < dist && !IsAborted; f += spacing)
                {
                    PointF currentpoint = Utility.Lerp(lastmouse, e.Location, f / dist);

                    Point dstpt = new Point((int)(currentpoint.X - radius), (int)(currentpoint.Y - radius));
                    Rectangle dstRect = new Rectangle(dstpt, brushsize);
                    Rectangle clippedRect = Rectangle.Intersect(dstRect, mesh.Bounds);

                    // build the brush area in an off-mesh buffer
                    for (int y = clippedRect.Top; y < clippedRect.Bottom; ++y)
                    {
                        meshptr = meshptr0 + meshwidth * y + clippedRect.Left;
                        bufferptr = bufferptr0 + size * (y - dstRect.Top) + clippedRect.Left - dstRect.Left;
                        int yc = y - dstRect.Top - radius;
                        int xc;
                        int densityindex;

                        for (int x = clippedRect.Left; x < clippedRect.Right; ++x)
                        {
                            xc = x - dstRect.Left - radius;
                            densityindex = (xc * xc + yc * yc) / radius;

                            if (densityindex < radius)
                            {
                                float mask = meshptr->Mask / 255f;
                                float amount = density[densityindex] * pressure * (1 - mask);
                                
                                *bufferptr = *meshptr;

                                switch (mode)
                                {
                                    case LiquifyMode.Push:
                                        displace.X = -amount * spacing * u.X;
                                        displace.Y = -amount * spacing * u.Y;
                                        *bufferptr = mesh.GetBilinearSample(x + displace.X, y + displace.Y);
                                        break;

                                    case LiquifyMode.TwistLeft:
                                        displace.X = -amount * yc;
                                        displace.Y = amount * xc;
                                        *bufferptr = mesh.GetBilinearSample(x + displace.X, y + displace.Y);
                                        break;

                                    case LiquifyMode.TwistRight:
                                        displace.X = amount * yc;
                                        displace.Y = -amount * xc;
                                        *bufferptr = mesh.GetBilinearSample(x + displace.X, y + displace.Y);
                                        break;

                                    case LiquifyMode.Bloat:
                                        displace.X = -amount * xc;
                                        displace.Y = -amount * yc;
                                        *bufferptr = mesh.GetBilinearSample(x + displace.X, y + displace.Y);
                                        break;

                                    case LiquifyMode.Pucker:
                                        displace.X = amount * xc;
                                        displace.Y = amount * yc;
                                        *bufferptr = mesh.GetBilinearSample(x + displace.X, y + displace.Y);
                                        break;

                                    case LiquifyMode.Reconstruct:
                                        bufferptr->X = bufferptr->X * (1 - amount);
                                        bufferptr->Y = bufferptr->Y * (1 - amount);
                                        break;

                                    case LiquifyMode.Freeze:
                                        mask = Math.Min(mask + density[densityindex] * pressure, 1);
                                        break;

                                    case LiquifyMode.Thaw:
                                        mask = Math.Max(mask - density[densityindex] * pressure, 0);
                                        break;
                                }
                                bufferptr->X += displace.X;
                                bufferptr->Y += displace.Y;
                                bufferptr->Mask = (byte)(mask * 255);
                            }
                            ++bufferptr;
                            ++meshptr;
                        }
                    }
                    
                    //copy the buffer onto the mesh
                    for (int y = clippedRect.Top; y < clippedRect.Bottom; ++y)
                    {
                        meshptr = meshptr0 + meshwidth * y + clippedRect.Left;
                        bufferptr = bufferptr0 + size * (y - dstRect.Top) + clippedRect.Left - dstRect.Left;

                        int yc = y - dstRect.Top - radius;
                        int xc;

                        for (int x = clippedRect.Left; x < clippedRect.Right; ++x)
                        {
                            xc = x - dstRect.Left - radius;
                            if ((xc * xc + yc * yc) / radius < radius)
                            {
                                *meshptr = *bufferptr;
                            }
                            ++bufferptr;
                            ++meshptr;
                        }
                    }
                }
                remainingSpace = f - dist;
            }
            OnInvalidated(invrect);
            lastmouse = e.Location;
        }
    }
}