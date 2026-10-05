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
            using (QuestionForm form = new QuestionForm(caption, question, confirmText, cancelText))
            {
                return form.ShowDialog(owner) == DialogResult.OK;
            }
        }

        private sealed class QuestionForm : PaintDotNet.PdnBaseForm
        {
            public QuestionForm(string caption, string question, string confirmText, string cancelText)
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

                Label label = new Label();
                label.AutoSize = true;
                label.MaximumSize = new Size(scaled(380), 0);
                label.Text = question;
                label.Location = new Point(margin, margin);

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

                this.Controls.Add(label);
                this.Controls.Add(confirm);
                this.Controls.Add(cancel);

                Size textSize = label.GetPreferredSize(label.MaximumSize);
                int gap = scaled(8);
                int width = Math.Max(textSize.Width, 2 * buttonSize.Width + gap) + 2 * margin;
                int buttonTop = margin + textSize.Height + scaled(20);

                cancel.Location = new Point(width - margin - buttonSize.Width, buttonTop);
                confirm.Location = new Point(cancel.Left - gap - buttonSize.Width, buttonTop);
                this.ClientSize = new Size(width, buttonTop + buttonSize.Height + margin);

                this.AcceptButton = confirm;
                this.CancelButton = cancel;

                this.Load += (sender, e) => ThemeHelper.ApplyToChildForm(this);
            }
        }
    }
}
