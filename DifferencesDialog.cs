using System;
using System.Drawing;
using System.Windows.Forms;
using C_SHARP_MNI_FTP_UPLOADER_2025;

namespace C_SHARP_MNI_FTP_UPLOADER_2025
{
    public class DifferencesDialog : Form
    {
        private bool _centeredOnce = false;
        private RichTextBox richTextBox;

        public DifferencesDialog(string message, string title, Color? backgroundColor = null, Color? textColor = null)
        {
            this.Text = title;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.Size = new Size(700, 700);
            this.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12, FontStyle.Bold);

            richTextBox = new RichTextBox()
            {
                Dock = DockStyle.Fill,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12, FontStyle.Bold),
                ReadOnly = true,
                BackColor = backgroundColor ?? SystemColors.ActiveCaption,
                ForeColor = textColor ?? Color.Black,
                Text = message,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            this.Controls.Add(richTextBox);

            this.BackColor = backgroundColor ?? SystemColors.ActiveCaption;

            var panel = new FlowLayoutPanel()
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 60
            };
            this.Controls.Add(panel);

            var okButton = new Button()
            {
                Text = "OK",
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(10, 10, 10, 10),
                DialogResult = DialogResult.OK
            };
            okButton.Click += (s, e) => this.Close();
            panel.Controls.Add(okButton);
            this.AcceptButton = okButton;
        }

        // Ensure dialog is centered over owner (main form) even when shown non-modally
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!_centeredOnce && this.Owner != null)
            {
                // Center over owner
                var owner = this.Owner;
                int x = owner.Location.X + (owner.Width - this.Width) / 2;
                int y = owner.Location.Y + (owner.Height - this.Height) / 2;
                this.Location = new Point(Math.Max(0, x), Math.Max(0, y));
                _centeredOnce = true;
            }
        }

        // Allow updating the content if dialog is already open
        public void UpdateContent(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateContent(message)));
                return;
            }
            if (richTextBox != null)
                richTextBox.Text = message;

            // Optionally, bring to front and re-center if needed
            if (this.Owner != null && !this.Modal)
            {
                var owner = this.Owner;
                int x = owner.Location.X + (owner.Width - this.Width) / 2;
                int y = owner.Location.Y + (owner.Height - this.Height) / 2;
                this.Location = new Point(Math.Max(0, x), Math.Max(0, y));
                this.BringToFront();
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // DifferencesDialog
            // 
            this.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Name = "DifferencesDialog";
            this.ResumeLayout(false);

        }
    }
}
