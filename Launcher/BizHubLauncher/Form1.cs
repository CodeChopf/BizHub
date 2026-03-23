using System.Diagnostics;
using System.Net.Http;

namespace BizHubLauncher
{
    public partial class Form1 : Form
    {
        private Process? apiProcess;
        private readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
        private const string ApiUrl = "http://localhost:5000";

        public Form1()
        {
            InitializeComponent();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            await webView.EnsureCoreWebView2Async();
            webView.NavigateToString(LoadingHtml());
            StartApi();

            bool apiReady = await WaitForApiAsync();

            if (!apiReady)
            {
                this.Invoke(() =>
                {
                    MessageBox.Show(
                        "BizHub konnte nicht gestartet werden.\nBitte sicherstellen dass die Installation korrekt ist.",
                        "BizHub Fehler",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    Application.Exit();
                });
                return;
            }

            webView.Source = new Uri(ApiUrl);
            Text = "BizHub";
        }

        private void StartApi()
        {
            try
            {
                var apiPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AuraPrintsApi.exe");

                apiProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = apiPath,
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                apiProcess.Start();
            }
            catch
            {
                // Still ignorieren — WaitForApiAsync() zeigt die Fehlermeldung nach Timeout
            }
        }

        private async Task<bool> WaitForApiAsync()
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    var response = await httpClient.GetAsync(ApiUrl);
                    if (response.IsSuccessStatusCode) return true;
                }
                catch { }
                await Task.Delay(1000);
            }
            return false;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (apiProcess != null && !apiProcess.HasExited)
                    apiProcess.Kill(entireProcessTree: true);
            }
            catch { }
            httpClient.Dispose();
        }

        private static string LoadingHtml() => """
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8"><style>
              body {
                margin: 0;
                display: flex;
                align-items: center;
                justify-content: center;
                height: 100vh;
                background: #0f172a;
                font-family: 'Segoe UI', sans-serif;
                color: #94a3b8;
              }
              .spinner {
                width: 40px; height: 40px;
                border: 3px solid #334155;
                border-top-color: #6366f1;
                border-radius: 50%;
                animation: spin 0.8s linear infinite;
                margin: 0 auto 16px;
              }
              @keyframes spin { to { transform: rotate(360deg); } }
              .wrap { text-align: center; }
              h2 { margin: 0 0 6px; color: #e2e8f0; font-size: 18px; font-weight: 500; }
              p  { margin: 0; font-size: 13px; }
            </style></head>
            <body>
              <div class="wrap">
                <div class="spinner"></div>
                <h2>BizHub startet</h2>
                <p>Einen Moment bitte…</p>
              </div>
            </body>
            </html>
            """;
    }
}