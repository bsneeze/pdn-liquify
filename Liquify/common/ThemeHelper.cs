using System;
using System.Drawing;
using System.Windows.Forms;

namespace pyrochild.effects.common
{
    // Implemented by custom controls that style themselves in dark mode. It lives here so every
    // plugin has it, whether or not it has such controls.
    internal interface IDarkThemeable
    {
        void ApplyDarkTheme(Color back, Color fore, Color field, Color border);
    }

    internal static class ThemeHelper
    {
        private static bool isDarkMode;
        private static Color back, fore, field, hover, border;

        // Background to use for text fields (e.g. when clearing an "invalid value" red highlight).
        public static Color FieldBackColor { get; private set; } = SystemColors.Window;

        public static bool IsDarkMode { get { return isDarkMode; } }

        // Only meaningful when IsDarkMode.
        public static Color LightTextColor { get { return fore; } }

        // WinForms draws disabled text in a fixed dark gray, which can't be read on a dark background.
        // In dark mode this leaves the control enabled but dims its text, and check boxes and radio
        // buttons ignore clicks while "disabled".
        public static void SetEnabled(Control c, bool enabled)
        {
            if (!isDarkMode)
            {
                c.Enabled = enabled;
                return;
            }

            c.Enabled = true;
            c.ForeColor = enabled ? fore : Blend(fore, back, 0.45f);
            c.TabStop = enabled;
            if (c is CheckBox checkBox)
            {
                checkBox.AutoCheck = enabled;
            }
            else if (c is RadioButton radioButton)
            {
                radioButton.AutoCheck = enabled;
            }
        }

        // For controls created after Apply.
        public static void Restyle(Control c)
        {
            if (isDarkMode)
            {
                Style(c);
            }
        }

        // Color-inverted copy (alpha preserved), for dark glyphs on a dark background.
        public static Bitmap Inverted(Image source)
        {
            Bitmap result = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            System.Drawing.Imaging.ColorMatrix matrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
            {
                new float[] { -1,  0,  0, 0, 0 },
                new float[] {  0, -1,  0, 0, 0 },
                new float[] {  0,  0, -1, 0, 0 },
                new float[] {  0,  0,  0, 1, 0 },
                new float[] {  1,  1,  1, 0, 1 }
            });
            using (System.Drawing.Imaging.ImageAttributes attributes = new System.Drawing.Imaging.ImageAttributes())
            using (Graphics g = Graphics.FromImage(result))
            {
                attributes.SetColorMatrix(matrix);
                g.DrawImage(source, new Rectangle(0, 0, result.Width, result.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
            }
            return result;
        }

        // For the plugin's main (Paint.NET-themed) form.
        public static void Apply(Control root)
        {
            if (!PaintDotNet.PdnBaseForm.IsAppThemeDark)
            {
                isDarkMode = false;
                FieldBackColor = SystemColors.Window;
                return;
            }

            isDarkMode = true;
            back = root.BackColor;
            fore = root.ForeColor;
            field = Blend(back, Color.White, 0.10f);
            hover = Blend(back, Color.White, 0.20f);
            border = Blend(back, Color.White, 0.35f);
            FieldBackColor = field;

            Style(root);
        }

        // For secondary dialogs. Ones derived from PdnBaseForm get Paint.NET's own theme colors and a
        // dark title bar; plain Forms get the main form's colors copied over.
        public static void ApplyToChildForm(Form form)
        {
            if (form is PaintDotNet.PdnBaseForm pdnForm)
            {
                pdnForm.UseAppThemeColors = true;
            }

            if (!isDarkMode)
            {
                return;
            }

            if (!(form is PaintDotNet.PdnBaseForm))
            {
                form.BackColor = back;
                form.ForeColor = fore;
            }
            Style(form);
        }

        private static void Style(Control c)
        {
            // Controls created after Paint.NET themed the form can still have dark text.
            if ((c is Label || c is GroupBox || ((c is CheckBox || c is RadioButton) && !IsButtonAppearance(c)))
                && !(c is LinkLabel)
                && Luma(c.ForeColor) < 140)
            {
                c.ForeColor = fore;
            }

            if (c is Button button)
            {
                StyleButton(button);
            }
            else if ((c is RadioButton || c is CheckBox) && ((ButtonBase)c).FlatStyle != FlatStyle.Flat && IsButtonAppearance(c))
            {
                ButtonBase toggle = (ButtonBase)c;
                toggle.FlatStyle = FlatStyle.Flat;
                toggle.UseVisualStyleBackColor = false;
                toggle.BackColor = field;
                toggle.ForeColor = fore;
                toggle.FlatAppearance.BorderColor = border;
                toggle.FlatAppearance.MouseOverBackColor = hover;
                toggle.FlatAppearance.MouseDownBackColor = border;
                toggle.FlatAppearance.CheckedBackColor = Blend(back, Color.FromArgb(70, 130, 220), 0.55f);
            }
            else if (c is ComboBox combo)
            {
                StyleCombo(combo);
            }
            else if (c is NumericUpDown || c is TextBoxBase)
            {
                c.BackColor = field;
                c.ForeColor = fore;
            }
            else if (c is LinkLabel link)
            {
                link.LinkColor = Color.FromArgb(120, 170, 255);
                link.ActiveLinkColor = Color.FromArgb(170, 205, 255);
                link.VisitedLinkColor = Color.FromArgb(120, 170, 255);
                link.DisabledLinkColor = Color.FromArgb(110, 110, 110);
            }
            else if (c is ToolStrip strip)
            {
                strip.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable(back, field, hover, border)) { RoundedEdges = false };
                strip.BackColor = back;
                strip.ForeColor = fore;
                foreach (ToolStripItem item in strip.Items)
                {
                    StyleItem(item);
                }
            }
            else if (c is IDarkThemeable themeable)
            {
                themeable.ApplyDarkTheme(back, fore, field, border);
            }
            else if (c.BackColor.ToArgb() == Color.White.ToArgb() && (c is Panel || c is UserControl || c is PictureBox))
            {
                // White backgrounds hardcoded in designers.
                c.BackColor = back;
            }

            foreach (Control child in c.Controls)
            {
                Style(child);
            }
        }

        private static bool IsButtonAppearance(Control c)
        {
            RadioButton rb = c as RadioButton;
            if (rb != null)
            {
                return rb.Appearance == Appearance.Button;
            }
            CheckBox cb = c as CheckBox;
            return cb != null && cb.Appearance == Appearance.Button;
        }

        private static void StyleButton(Button button)
        {
            if (button.FlatStyle == FlatStyle.Flat)
            {
                return;
            }

            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = field;
            button.ForeColor = fore;
            button.FlatAppearance.BorderColor = border;
            button.FlatAppearance.MouseOverBackColor = hover;
            button.FlatAppearance.MouseDownBackColor = border;
        }

        private static void StyleItem(ToolStripItem item)
        {
            // A host's ForeColor is pushed onto the hosted control, so skip custom hosts.
            bool customHost = item is ToolStripControlHost
                && !(item is ToolStripComboBox)
                && !(item is ToolStripTextBox);
            if (!customHost)
            {
                item.ForeColor = fore;
            }

            if (item is ToolStripComboBox tsCombo)
            {
                StyleCombo(tsCombo.ComboBox);
            }
            else if (item is ToolStripTextBox tsText)
            {
                tsText.BackColor = field;
                tsText.ForeColor = fore;
            }
            else if (item is ToolStripControlHost host && host.Control != null)
            {
                // Hosted controls aren't in strip.Controls.
                Style(host.Control);
            }
        }

        private static void StyleCombo(ComboBox combo)
        {
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = field;
            combo.ForeColor = fore;

            // A flat drop-down list still paints its edit area in system colors while it has focus,
            // so draw the items ourselves. Combos that are already owner-drawn are left alone.
            if (combo.DrawMode == DrawMode.Normal)
            {
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.DrawItem += DrawDarkComboItem;
            }
        }

        private static void DrawDarkComboItem(object sender, DrawItemEventArgs e)
        {
            ComboBox combo = (ComboBox)sender;
            bool inList = (e.State & DrawItemState.ComboBoxEdit) == 0;
            bool highlighted = (e.State & DrawItemState.Selected) != 0 && inList;
            using (SolidBrush brush = new SolidBrush(highlighted ? hover : field))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            if (e.Index >= 0)
            {
                Rectangle textBounds = Rectangle.Inflate(e.Bounds, -2, 0);
                TextRenderer.DrawText(e.Graphics, combo.GetItemText(combo.Items[e.Index]), combo.Font, textBounds, fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        internal static double Luma(Color c)
        {
            return 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
        }

        internal static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        private sealed class DarkColorTable : ProfessionalColorTable
        {
            private readonly Color back, field, hover, border;

            public DarkColorTable(Color back, Color field, Color hover, Color border)
            {
                this.back = back;
                this.field = field;
                this.hover = hover;
                this.border = border;
                UseSystemColors = false;
            }

            public override Color ToolStripGradientBegin { get { return back; } }
            public override Color ToolStripGradientMiddle { get { return back; } }
            public override Color ToolStripGradientEnd { get { return back; } }
            public override Color ToolStripBorder { get { return back; } }
            public override Color ToolStripContentPanelGradientBegin { get { return back; } }
            public override Color ToolStripContentPanelGradientEnd { get { return back; } }
            public override Color ToolStripPanelGradientBegin { get { return back; } }
            public override Color ToolStripPanelGradientEnd { get { return back; } }
            public override Color ToolStripDropDownBackground { get { return field; } }
            public override Color MenuBorder { get { return border; } }
            public override Color MenuItemBorder { get { return border; } }
            public override Color MenuItemSelected { get { return hover; } }
            public override Color ImageMarginGradientBegin { get { return field; } }
            public override Color ImageMarginGradientMiddle { get { return field; } }
            public override Color ImageMarginGradientEnd { get { return field; } }
            public override Color SeparatorDark { get { return border; } }
            public override Color SeparatorLight { get { return back; } }
            public override Color GripDark { get { return border; } }
            public override Color GripLight { get { return back; } }
            public override Color ButtonSelectedHighlight { get { return hover; } }
            public override Color ButtonSelectedGradientBegin { get { return hover; } }
            public override Color ButtonSelectedGradientMiddle { get { return hover; } }
            public override Color ButtonSelectedGradientEnd { get { return hover; } }
            public override Color ButtonSelectedBorder { get { return border; } }
            public override Color ButtonPressedHighlight { get { return border; } }
            public override Color ButtonPressedGradientBegin { get { return border; } }
            public override Color ButtonPressedGradientMiddle { get { return border; } }
            public override Color ButtonPressedGradientEnd { get { return border; } }
            public override Color ButtonPressedBorder { get { return border; } }
            public override Color ButtonCheckedHighlight { get { return hover; } }
            public override Color ButtonCheckedGradientBegin { get { return hover; } }
            public override Color ButtonCheckedGradientMiddle { get { return hover; } }
            public override Color ButtonCheckedGradientEnd { get { return hover; } }
            public override Color CheckBackground { get { return hover; } }
        }
    }
}
