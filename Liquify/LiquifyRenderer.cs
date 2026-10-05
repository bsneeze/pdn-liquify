using pyrochild.effects.common;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace pyrochild.effects.liquify
{
    internal class LiquifyRenderer : QueuedToolRenderer
    {
        DisplacementMesh mesh;
        Point lastmouse;
        int strokesize;
        int radius;
        int spacing;
        float pressure;
        float[] density;
        LiquifyMode mode;
        DisplacementMesh buffer;
        float remainingSpace;

        public LiquifyRenderer(DisplacementMesh mesh)
            : base(mesh)
        {
            this.mesh = mesh;
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
                if (buffer != null)
                {
                    buffer.Dispose();
                }

                // size, pressure, density and mode are fixed for the whole stroke
                strokesize = e.Size;
                radius = strokesize / 2;
                spacing = Math.Max(1, radius / 4);
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

        protected override void OnMouseUp(QueuedToolEventArgs args)
        {
            LiquifyEventArgs e = args as LiquifyEventArgs;

            if (e.Button == MouseButtons.Left && buffer != null)
            {
                buffer.Dispose();
                buffer = null;
            }
        }

        protected override void OnDispose()
        {
            // the render thread has stopped by now
            if (buffer != null)
            {
                buffer.Dispose();
                buffer = null;
            }
        }

        protected override bool CanCoalesce(QueuedToolEventArgs earlier, QueuedToolEventArgs later)
        {
            LiquifyEventArgs a = earlier as LiquifyEventArgs;
            LiquifyEventArgs b = later as LiquifyEventArgs;

            if (a == null || b == null || a.Button != b.Button)
            {
                return false;
            }

            if (a.Button != MouseButtons.Left || buffer == null)
            {
                return true;
            }

            // mid-stroke, only skip points that haven't moved a full brush step, so the path barely changes
            return Utility.Distance(lastmouse, a.Location) <= spacing;
        }

        protected override void OnMouseMove(QueuedToolEventArgs args)
        {
            LiquifyEventArgs e = args as LiquifyEventArgs;

            if (e.Button != MouseButtons.Left || buffer == null)
            {
                // not in a stroke: nothing changes, so only keep track of where the next one starts
                lastmouse = e.Location;
                return;
            }

            Size brushsize = new Size(strokesize, strokesize);

            Rectangle invrect = new Rectangle(new Point(lastmouse.X - radius, lastmouse.Y - radius), brushsize);
            invrect = Rectangle.Union(invrect, new Rectangle(new Point(e.X - radius, e.Y - radius), brushsize));

            float dist = Utility.Distance(lastmouse, e.Location);
            if (dist == 0) dist = 1;

            //unit vector in the direction of motion
            DisplacementVector u = new DisplacementVector((e.X - lastmouse.X) / dist, (e.Y - lastmouse.Y) / dist);

            float f;
            for (f = remainingSpace; f < dist && !IsAborted; f += spacing)
            {
                PointF currentpoint = Utility.Lerp(lastmouse, e.Location, f / dist);

                Point dstpt = new Point((int)(currentpoint.X - radius), (int)(currentpoint.Y - radius));
                Rectangle dstRect = new Rectangle(dstpt, brushsize);
                Rectangle clippedRect = Rectangle.Intersect(dstRect, mesh.Bounds);

                // build the brush area in an off-mesh buffer
                DisplacementMesh.ForEachRow(clippedRect, y => BuildRow(y, clippedRect, dstRect, u));

                //copy the buffer onto the mesh
                DisplacementMesh.ForEachRow(clippedRect, y => CopyRow(y, clippedRect, dstRect));
            }
            remainingSpace = f - dist;

            OnInvalidated(invrect);
            lastmouse = e.Location;
        }

        private unsafe void BuildRow(int y, Rectangle clippedRect, Rectangle dstRect, DisplacementVector u)
        {
            DisplacementVector* meshptr = mesh.GetPointAddressUnchecked(clippedRect.Left, y);
            DisplacementVector* bufferptr = buffer.GetPointAddressUnchecked(clippedRect.Left - dstRect.Left, y - dstRect.Top);
            DisplacementVector displace = DisplacementVector.Zero;
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

        private unsafe void CopyRow(int y, Rectangle clippedRect, Rectangle dstRect)
        {
            DisplacementVector* meshptr = mesh.GetPointAddressUnchecked(clippedRect.Left, y);
            DisplacementVector* bufferptr = buffer.GetPointAddressUnchecked(clippedRect.Left - dstRect.Left, y - dstRect.Top);

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
}
