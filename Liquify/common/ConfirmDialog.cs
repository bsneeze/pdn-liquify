using System;
using System.Drawing;
using System.Windows.Forms;

namespace pyrochild.effects.common
{
    /// <summary>
    /// A question with two answers, in a window that follows Paint.NET's theme. The system message
    /// box stays light in dark mode.
    /// </summary>
    public static class ConfirmDialog
    {
        /// <summary>
        /// Returns true if the confirm button was chosen. Enter chooses it; Esc and the close box
        /// choose the other one.
        /// </summary>
        public static bool Show(IWin32Window owner, string caption, string question, string confirmText, string cancelText)
        {
            using (QuestionForm form = new QuestionForm(caption, question, confirmText, cancelText, null))
            {
                return form.ShowDialog(owner) == DialogResult.OK;
            }
        }

        /// <summary>
        /// Shows a message with a single OK button.
        /// </summary>
        /// <param name="icon">optional, shown beside the message: SystemIcons.Warning, say</param>
        public static void Notify(IWin32Window owner, string caption, string message, Icon icon = null)
        {
            using (QuestionForm form = new QuestionForm(caption, message, "OK", null, icon))
            {
                form.ShowDialog(owner);
            }
        }

        private sealed class QuestionForm : PaintDotNet.PdnBaseForm
        {
            /// <param name="cancelText">null for a message with only the one button</param>
            public QuestionForm(string caption, string question, string confirmText, string cancelText, Icon icon)
            {
                // sizes are scaled by hand, like the rest of the plugin's dialogs
                float scale = this.DeviceDpi / 96f;
                Func<int, int> scaled = value => (int)Math.Round(value * scale);

                this.AutoScaleMode = AutoScaleMode.None;
                this.Text = caption;
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.MaximizeBox = false;
                this.MinimizeBox = false;
                this.ShowIcon = false;
                this.ShowInTaskbar = false;
                this.StartPosition = FormStartPosition.CenterParent;

                int margin = scaled(16);
                Size buttonSize = new Size(scaled(96), scaled(26));

                int iconSize = 0;
                int textLeft = margin;
                if (icon != null)
                {
                    iconSize = scaled(32);
                    PictureBox picture = new PictureBox();
                    picture.Size = new Size(iconSize, iconSize);
                    picture.Location = new Point(margin, margin);
                    picture.SizeMode = PictureBoxSizeMode.Zoom;

                    // asks the icon for its version nearest the size on screen, where it has one
                    using (Icon sized = new Icon(icon, iconSize, iconSize))
                    {
                        picture.Image = sized.ToBitmap();
                    }

                    this.Controls.Add(picture);
                    textLeft = margin + iconSize + scaled(12);
                }

                Label label = new Label();
                label.AutoSize = true;
                label.MaximumSize = new Size(scaled(380), 0);
                label.Text = question;
                label.Location = new Point(textLeft, margin);

                Button confirm = new Button();
                confirm.Text = confirmText;
                confirm.Size = buttonSize;
                confirm.DialogResult = DialogResult.OK;
                confirm.TabIndex = 0;

                Button cancel = new Button();
                cancel.Text = cancelText;
                cancel.Size = buttonSize;
                cancel.DialogResult = DialogResult.Cancel;
                cancel.TabIndex = 1;

                bool twoButtons = cancelText != null;

                this.Controls.Add(label);
                this.Controls.Add(confirm);
                if (twoButtons)
                {
                    this.Controls.Add(cancel);
                }

                Size textSize = label.GetPreferredSize(label.MaximumSize);
                int gap = scaled(8);
                int width = Math.Max(textLeft - margin + textSize.Width, 2 * buttonSize.Width + gap) + 2 * margin;
                int buttonTop = margin + Math.Max(textSize.Height, iconSize) + scaled(20);

                cancel.Location = new Point(width - margin - buttonSize.Width, buttonTop);
                confirm.Location = twoButtons
                    ? new Point(cancel.Left - gap - buttonSize.Width, buttonTop)
                    : cancel.Location;
                this.ClientSize = new Size(width, buttonTop + buttonSize.Height + margin);

                this.AcceptButton = confirm;
                this.CancelButton = twoButtons ? cancel : confirm;

                this.Load += (sender, e) => ThemeHelper.ApplyToChildForm(this);
            }
        }
    }
}
