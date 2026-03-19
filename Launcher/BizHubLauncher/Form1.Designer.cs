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
            btnStart = new Button();
            btnStop = new Button();
            lblStatus = new Label();
            SuspendLayout();

            // btnStart
            btnStart.Font = new Font("Segoe UI", 15F);
            btnStart.Location = new Point(210, 186);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(148, 68);
            btnStart.TabIndex = 0;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;

            // btnStop
            btnStop.Font = new Font("Segoe UI", 15F);
            btnStop.Location = new Point(413, 186);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(148, 68);
            btnStop.TabIndex = 1;
            btnStop.Text = "Stop";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;

            // lblStatus
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Segoe UI", 30F);
            lblStatus.Location = new Point(297, 102);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(187, 54);
            lblStatus.TabIndex = 2;
            lblStatus.Text = "Gestoppt";

            // Form1
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(lblStatus);
            Controls.Add(btnStop);
            Controls.Add(btnStart);
            Name = "Form1";
            Text = "BizHub Launcher";
            Load += Form1_Load;

            ResumeLayout(false);
            PerformLayout();
        }

        private Button btnStart;
        private Button btnStop;
        private Label lblStatus;
    }
}