using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PaintDotNet;

namespace pyrochild.effects.common
{
    public partial class ColorDialog : Form
    {
        public ColorDialog(bool alphaslider)
        {
            InitializeComponent();

            if (!alphaslider)
            {
                label7.Visible = false;
                aupdown.Visible = false;
                aslider.Visible = false;
            }

            wheel.ColorChanged += new EventHandler(wheel_ColorChanged);
            hex.KeyPress += new KeyPressEventHandler(hex_KeyPress);
            hex.TextChanged += new EventHandler(hex_TextChanged);

            SetSliderGradients();
            AttachSliderEvents();
        }

        void hex_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = (e.KeyChar < '0' || e.KeyChar > '9')
                     && (e.KeyChar < 'a' || e.KeyChar > 'f')
                     && (e.KeyChar < 'A' || e.KeyChar > 'F')
                     && e.KeyChar != '\b'
                     && Control.ModifierKeys != Keys.Control; ;
        }

        void hex_TextChanged(object sender, EventArgs e)
        {
            if (!eventssuspended)
            {
                SuspendEvents();

                try
                {
                    ColorBgra c = ColorBgra.FromOpaqueInt32(int.Parse(hex.Text, System.Globalization.NumberStyles.HexNumber));
                    HsvColor h = HsvColor.FromColor(c);
                    wheel.Color = c;
                    SetRgbSliders(c);
                    SetHsvSliders(h);
                }
                catch
                {
                    hex.Text = "";
                }

                ResumeEvents();
            }
        }

        private void SuspendEvents() { eventssuspended = true; }
        private void ResumeEvents() { eventssuspended = false; }
        private bool eventssuspended = false;

        private void AttachSliderEvents()
        {
            rslider.ValueChanged += new EventHandler(rgbslider_ValueChanged);
            gslider.ValueChanged += new EventHandler(rgbslider_ValueChanged);
            bslider.ValueChanged += new EventHandler(rgbslider_ValueChanged);
            aslider.ValueChanged += new EventHandler(rgbslider_ValueChanged);

            hslider.ValueChanged += new EventHandler(hsvslider_ValueChanged);
            sslider.ValueChanged += new EventHandler(hsvslider_ValueChanged);
            vslider.ValueChanged += new EventHandler(hsvslider_ValueChanged);

            rupdown.ValueChanged += new EventHandler(rgbupdown_ValueChanged);
            gupdown.ValueChanged += new EventHandler(rgbupdown_ValueChanged);
            bupdown.ValueChanged += new EventHandler(rgbupdown_ValueChanged);
            aupdown.ValueChanged += new EventHandler(rgbupdown_ValueChanged);

            hupdown.ValueChanged += new EventHandler(hsvupdown_ValueChanged);
            supdown.ValueChanged += new EventHandler(hsvupdown_ValueChanged);
            vupdown.ValueChanged += new EventHandler(hsvupdown_ValueChanged);
        }

        void hsvupdown_ValueChanged(object sender, EventArgs e)
        {
            if (!eventssuspended)
            {
                SuspendEvents();

                hslider.Value = (float)hupdown.Value / 360f;
                sslider.Value = (float)supdown.Value / 100f;
                vslider.Value = (float)vupdown.Value / 100f;

                HsvColor h = new HsvColor((int)hupdown.Value, (int)supdown.Value, (int)vupdown.Value);
                ColorBgra c = h.ToColorBgra();
                c.A = (byte)aupdown.Value;

                wheel.Color = c;

                SetRgbSliders(c);
                SetHex(c.R, c.G, c.B);

                ResumeEvents();
            }
        }

        void rgbupdown_ValueChanged(object sender, EventArgs e)
        {
            if (!eventssuspended)
            {
                SuspendEvents();

                byte r = (byte)rupdown.Value;
                byte g = (byte)gupdown.Value;
                byte b = (byte)bupdown.Value;
                byte a = (byte)aupdown.Value;

                rslider.Value = r / 255f;
                gslider.Value = g / 255f;
                bslider.Value = b / 255f;
                aslider.Value = a / 255f;

                wheel.Color = ColorBgra.FromBgra(b, g, r, a);

                SetHex(r, g, b);
                SetHsvSliders(wheel.HsvColor);

                ResumeEvents();
            }
        }

        private void SetHex(byte r, byte g, byte b)
        {
            hex.Text = (r << 16 | g << 8 | b).ToString("X").PadLeft(6, '0');
        }

        void hsvslider_ValueChanged(object sender, EventArgs e)
        {
            if (!eventssuspended)
            {
                SuspendEvents();

                int h = (int)(hslider.Value * 360);
                int s = (int)(sslider.Value * 100);
                int v = (int)(vslider.Value * 100);
                hupdown.Value = h;
                supdown.Value = s;
                vupdown.Value = v;

                HsvColor hc = new HsvColor(h, s, v);
                ColorBgra c = hc.ToColorBgra();
                c.A = (byte)aupdown.Value;

                wheel.Color = c;

                SetHex(c.R, c.G, c.B);
                SetRgbSliders(c);

                ResumeEvents();
            }
        }

        void rgbslider_ValueChanged(object sender, EventArgs e)
        {

            if (!eventssuspended)
            {
                SuspendEvents();

                byte r = (byte)(rslider.Value * 255);
                byte g = (byte)(gslider.Value * 255);
                byte b = (byte)(bslider.Value * 255);
                byte a = (byte)(aslider.Value * 255);
                rupdown.Value = r;
                gupdown.Value = g;
                bupdown.Value = b;
                aupdown.Value = a;

                wheel.Color = ColorBgra.FromBgra(b, g, r, a);

                SetHex(r, g, b);
                SetHsvSliders(wheel.HsvColor);

                ResumeEvents();
            }
        }

        private void wheel_ColorChanged(object sender, EventArgs e)
        {
            SetSliderGradients();

            if (!eventssuspended)
            {
                SuspendEvents();

                HsvColor h = wheel.HsvColor;
                ColorBgra c = wheel.Color;

                SetRgbSliders(c);
                SetHsvSliders(h);
                SetHex(c.R, c.G, c.B);

                ResumeEvents();
            }
        }

        private void SetRgbSliders(ColorBgra c)
        {
            rslider.Value = c.R / 255f;
            gslider.Value = c.G / 255f;
            bslider.Value = c.B / 255f;
            aslider.Value = c.A / 255f;
            rupdown.Value = c.R;
            gupdown.Value = c.G;
            bupdown.Value = c.B;
            aupdown.Value = c.A;
        }

        private void SetHsvSliders(HsvColor h)
        {
            hslider.Value = h.Hue / 360f;
            sslider.Value = h.Saturation / 100f;
            vslider.Value = h.Value / 100f;
            hupdown.Value = h.Hue;
            supdown.Value = h.Saturation;
            vupdown.Value = h.Value;
        }

        private void SetSliderGradients()
        {
            HsvColor h = wheel.HsvColor;
            ColorBgra c = wheel.Color;

            rslider.Gradient = new ColorBgra[] { ColorBgra.FromBgr(c.B, c.G, 0), ColorBgra.FromBgr(c.B, c.G, 255) };
            gslider.Gradient = new ColorBgra[] { ColorBgra.FromBgr(c.B, 0, c.R), ColorBgra.FromBgr(c.B, 255, c.R) };
            bslider.Gradient = new ColorBgra[] { ColorBgra.FromBgr(0, c.G, c.R), ColorBgra.FromBgr(255, c.G, c.R) };
            aslider.Gradient = new ColorBgra[] { ColorBgra.FromBgra(c.B, c.G, c.R, 0), ColorBgra.FromBgra(c.B, c.G, c.R, 255) };

            sslider.Gradient = new ColorBgra[] { new HsvColor(h.Hue, 0, h.Value).ToColorBgra(), new HsvColor(h.Hue, 100, h.Value).ToColorBgra() };
            vslider.Gradient = new ColorBgra[] { new HsvColor(h.Hue, h.Saturation, 0).ToColorBgra(), new HsvColor(h.Hue, h.Saturation, 100).ToColorBgra() };

            if (hslider.Gradient == null)
            {
                ColorBgra[] hues = new ColorBgra[361];
                for (int hue = 0; hue <= 360; ++hue)
                {
                    hues[hue] = new HsvColor(hue, 100, 100).ToColorBgra();
                }
                hslider.Gradient = hues;
            }
        }

        public ColorBgra Color
        {
            get { return wheel.Color; }
            set { wheel.Color = value; }
        }
    }
}
