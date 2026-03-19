using System.Diagnostics;

namespace BizHubLauncher
{
    public partial class Form1 : Form
    {
        private Process? apiProcess;

        public Form1()
        {
            InitializeComponent();
            UpdateStatus(false);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (apiProcess == null || apiProcess.HasExited)
            {
                try
                {
                    apiProcess = new Process();

                    apiProcess.StartInfo = new ProcessStartInfo
                    {
                        FileName = "AuraPrintsApi.exe",
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                        UseShellExecute = false
                    };

                    apiProcess.Start();

                    UpdateStatus(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Fehler beim Start: " + ex.Message);
                }
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            try
            {
                if (apiProcess != null && !apiProcess.HasExited)
                {
                    apiProcess.Kill(true);
                    apiProcess = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Stoppen: " + ex.Message);
            }

            UpdateStatus(false);
        }

        private void UpdateStatus(bool running)
        {
            lblStatus.Text = running ? "🟢 Läuft" : "🔴 Gestoppt";
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // optional
        }
    }
}