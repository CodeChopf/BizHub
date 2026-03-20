using System.Diagnostics;
using System.Net.Http;

namespace BizHubLauncher
{
    public partial class Form1 : Form
    {
        private Process? apiProcess;
        private readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
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
            await WaitForApiAsync();

            webView.Source = new Uri(ApiUrl);
            Text = "BizHub";
        }

        private void StartApi()
        {
            try
            {
                apiProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "AuraPrintsApi.exe",
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                apiProcess.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Start: " + ex.Message);
            }
        }

        private async Task WaitForApiAsync()
        {
            for (int i = 0; i < 60; i++)
            {
                try
                {
                    var response = await httpClient.GetAsync(ApiUrl);
                    if (response.IsSuccessStatusCode) return;
                }
                catch { }
                await Task.Delay(500);
            }
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
