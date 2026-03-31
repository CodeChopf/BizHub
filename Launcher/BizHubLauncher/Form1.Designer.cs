namespace BizHubLauncher
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            webView = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((System.ComponentModel.ISupportInitialize)webView).BeginInit();
            SuspendLayout();

            // webView
            webView.Dock = DockStyle.Fill;
            webView.Name = "webView";
            webView.TabIndex = 0;

            // Form1
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1280, 800);
            Controls.Add(webView);
            Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "favicon.ico"));
            Name = "Form1";
            Text = "BizHub";
            WindowState = FormWindowState.Maximized;
            Load += Form1_Load;
            FormClosing += Form1_FormClosing;

            ((System.ComponentModel.ISupportInitialize)webView).EndInit();
            ResumeLayout(false);
        }

        private Microsoft.Web.WebView2.WinForms.WebView2 webView;
    }
}
