using System;
using System.Drawing;
using System.Windows.Forms;

namespace pyrochild.effects.common
{
    /// <summary>
    /// A strip across the top of a dialog saying that a newer version is out, with a link to see
    /// what is new, one to get it, one to put it off, and a close button. The dialog places it and
    /// makes room for it.
    /// </summary>
    internal sealed class UpdateBanner : Panel, IDarkThemeable
    {
        private readonly Label message;
        private readonly LinkLabel whatsNew;
        private bool showWhatsNew = true;
        private readonly LinkLabel get;
        private readonly LinkLabel notNow;
        private readonly Label close;
        private readonly ToolTip tooltip;
        private Color edge = Color.FromArgb(230, 200, 120);

        public event EventHandler WhatsNewClicked;
        public event EventHandler GetClicked;
        public event EventHandler NotNowClicked;
        public event EventHandler CloseClicked;

        public UpdateBanner()
        {
            // the pale yellow of an information bar; dark mode replaces it in ApplyDarkTheme
            BackColor = Color.FromArgb(255, 244, 206);
            ForeColor = Color.Black;
            ResizeRedraw = true;

            // Not optional. OnPaint draws on the window, and a GDI+ line drawn straight onto a
            // window (not through a buffer) left the canvas beside it with a third of its mouse
            // moves and repaints for as long as this was showing. Measured in Paint.NET.
            DoubleBuffered = true;

            message = new Label();
            message.AutoSize = true;

            whatsNew = new LinkLabel();
            whatsNew.AutoSize = true;
            whatsNew.Text = "What's new?";
            whatsNew.LinkClicked += (sender, e) => WhatsNewClicked?.Invoke(this, EventArgs.Empty);

            get = new LinkLabel();
            get.AutoSize = true;
            get.Text = "Get it";
            get.LinkClicked += (sender, e) => GetClicked?.Invoke(this, EventArgs.Empty);

            notNow = new LinkLabel();
            notNow.AutoSize = true;
            notNow.Text = "Not now";
            notNow.LinkClicked += (sender, e) => NotNowClicked?.Invoke(this, EventArgs.Empty);

            // A label, not a button: ThemeHelper would give a button a border and a fill.
            close = new Label();
            close.Text = "✕";
            close.TextAlign = ContentAlignment.MiddleCenter;
            close.BackColor = Color.Transparent;
            close.AccessibleRole = AccessibleRole.PushButton;
            close.AccessibleName = "Close";
            close.Click += (sender, e) => CloseClicked?.Invoke(this, EventArgs.Empty);
            close.MouseEnter += (sender, e) => close.BackColor = ThemeHelper.Blend(BackColor, ForeColor, 0.15f);
            close.MouseLeave += (sender, e) => close.BackColor = Color.Transparent;

            tooltip = new ToolTip();
            tooltip.SetToolTip(close, "Close");

            Controls.Add(message);
            Controls.Add(whatsNew);
            Controls.Add(get);
            Controls.Add(notNow);
            Controls.Add(close);
        }

        public string Message
        {
            get { return message.Text; }
            set { message.Text = value; }
        }

        // Off when the page "Get it" opens already says what is new. Kept in a field: a control's
        // own Visible reads false while its parent is hidden.
        public bool ShowWhatsNew
        {
            get { return showWhatsNew; }
            set
            {
                showWhatsNew = value;
                whatsNew.Visible = value;
                PerformLayout();
            }
        }

        private int Pad
        {
            get { return (int)Math.Round(6 * DeviceDpi / 96f); }
        }

        // the height to give it
        public int PreferredBannerHeight
        {
            get { return message.PreferredHeight + 2 * Pad; }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);

            int x = Pad;
            foreach (Control c in new Control[] { message, whatsNew, get, notNow })
            {
                if (c == whatsNew && !showWhatsNew)
                {
                    continue;
                }
                c.Location = new Point(x, (Height - c.Height) / 2);
                x = c.Right + Pad;
            }

            int side = Math.Max(0, Height - Pad);
            close.SetBounds(Width - side - Pad / 2, (Height - side) / 2, side, side);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using (Pen pen = new Pen(edge))
            {
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
        }

        public void ApplyDarkTheme(Color back, Color fore, Color field, Color border)
        {
            // dark enough for the light blue that ThemeHelper gives the links
            BackColor = ThemeHelper.Blend(back, Color.FromArgb(70, 130, 220), 0.25f);
            ForeColor = fore;
            message.ForeColor = fore;
            close.ForeColor = fore;
            edge = border;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tooltip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
