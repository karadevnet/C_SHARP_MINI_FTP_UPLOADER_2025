using System;
using System.Drawing;
using System.Windows.Forms;
using C_SHARP_MNI_FTP_UPLOADER_2025;

namespace C_SHARP_MNI_FTP_UPLOADER_2025
{
    public class DifferencesDialog : Form
    {
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

            var richTextBox = new RichTextBox()
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
