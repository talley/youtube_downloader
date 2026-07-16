using DevExpress.XtraEditors;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace YouTubeDownloader
{
    public partial class MainScreen : DevExpress.XtraEditors.XtraForm
    {
        private Process _downloadProcess;
        public MainScreen()
        {
            InitializeComponent();
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {

        }

        private async void MainScreen_Load(object sender, EventArgs e)
        {
            try
            {
                await webView21.EnsureCoreWebView2Async();

                string webContentFolder = Path.Combine(
                    Application.StartupPath,
                    "WebContent");

                Directory.CreateDirectory(webContentFolder);
                CreatePlayerHtml(webContentFolder);

                webView21.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "youtubeapp.local",
                    webContentFolder,
                    CoreWebView2HostResourceAccessKind.Allow);


                txtOutputFolder.Text = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    "YouTube");

                Directory.CreateDirectory(txtOutputFolder.Text);
                lblStatus.Text = "Ready";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "WebView2 could not be initialized.\n\n" + ex.Message,
                    "WebView2 error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async void txtYouTubeUrl_EditValueChanged(object sender, EventArgs e)
        {
            string url = txtYouTubeUrl.Text.Trim();
            string videoId;

            if (!TryGetYouTubeVideoId(url, out videoId))
            {
                return;
            }

            try
            {
                await webView21.EnsureCoreWebView2Async();
                webView21.Source = new Uri(
                   "https://youtubeapp.local/player.html?videoId=" +
                   Uri.EscapeDataString(videoId));
            }
            catch
            {
                // Do not interrupt URL entry if preview initialization fails.
            }
        }

        private async void btnConvert_Click(object sender, EventArgs e)
        {
            if (txtOutputFolder.Text.Length == 0)
            {
                XtraMessageBox.Show("Please select an output folder.", "Output Folder Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (txtYouTubeUrl.Text.Length == 0)
            {
                XtraMessageBox.Show("Please enter a YouTube URL.", "YouTube URL Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                string url = txtYouTubeUrl.Text.Trim();
                string outputFolder = txtOutputFolder.Text.Trim();
                Uri videoUri;

                if (!Uri.TryCreate(url, UriKind.Absolute, out videoUri) ||
                    !IsSupportedYouTubeHost(videoUri.Host))
                {
                    MessageBox.Show(
                        "Enter a valid YouTube URL.",
                        "Invalid URL",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    txtYouTubeUrl.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(outputFolder))
                {
                    MessageBox.Show(
                        "Choose an output folder.",
                        "Output folder required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    Directory.CreateDirectory(outputFolder);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "The output folder could not be created.\n\n" + ex.Message,
                        "Folder error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                string ytDlpPath = Path.Combine(
                    Application.StartupPath,
                    "yt-dlp.exe");

                if (!File.Exists(ytDlpPath))
                {
                    MessageBox.Show(
                        "yt-dlp.exe was not found:\n\n" + ytDlpPath,
                        "Missing yt-dlp",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

             await DownloadVideoAsync(ytDlpPath, url, outputFolder);
             System.Diagnostics.Process.Start("explorer.exe", outputFolder);
            }
        }



        private async Task DownloadVideoAsync(
          string ytDlpPath,
          string url,
          string outputFolder)
        {
            SetDownloadingState(true);
            StringBuilder errorOutput = new StringBuilder();

            try
            {
                string outputTemplate = Path.Combine(
                    outputFolder,
                    "%(title)s.%(ext)s");

                // ProcessStartInfo.ArgumentList is not available in older
                // .NET Framework projects, so use Arguments instead.
                string arguments =
                    "-f " + QuoteArgument(
                        "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best") +
                    " --merge-output-format mp4" +
                    " --newline" +
                    " --progress-template " +
                    QuoteArgument("download:%(progress._percent_str)s") +
                    " --ffmpeg-location " +
                    QuoteArgument(Application.StartupPath) +
                    " -o " +
                    QuoteArgument(outputTemplate) +
                    " " +
                    QuoteArgument(url);

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = ytDlpPath;
                startInfo.Arguments = arguments;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.WorkingDirectory = Application.StartupPath;

                _downloadProcess = new Process();
                _downloadProcess.StartInfo = startInfo;
                _downloadProcess.EnableRaisingEvents = true;

                _downloadProcess.OutputDataReceived += delegate (
                    object processSender,
                    DataReceivedEventArgs args)
                {
                    if (string.IsNullOrWhiteSpace(args.Data))
                    {
                        return;
                    }

                    BeginInvoke(new Action(delegate
                    {
                        HandleYtDlpOutput(args.Data);
                    }));
                };

                _downloadProcess.ErrorDataReceived += delegate (
                    object processSender,
                    DataReceivedEventArgs args)
                {
                    if (string.IsNullOrWhiteSpace(args.Data))
                    {
                        return;
                    }

                    errorOutput.AppendLine(args.Data);

                    BeginInvoke(new Action(delegate
                    {
                        lblStatus.Text = args.Data;
                    }));
                };

                _downloadProcess.Start();
                _downloadProcess.BeginOutputReadLine();
                _downloadProcess.BeginErrorReadLine();

                // WaitForExitAsync is not available in older .NET Framework.
                await Task.Run(delegate
                {
                    _downloadProcess.WaitForExit();
                });

                int exitCode = _downloadProcess.ExitCode;

                if (exitCode == 0)
                {
                    progressBar1.Value = 100;
                    lblStatus.Text = "Download completed.";

                    MessageBox.Show(
                        "The video was downloaded successfully.",
                        "Completed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    string message = errorOutput.Length > 0
                        ? errorOutput.ToString()
                        : "yt-dlp returned an unknown error.";

                    MessageBox.Show(
                        message,
                        "Download failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    lblStatus.Text = "Download failed.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The video could not be downloaded.\n\n" + ex.Message,
                    "Download error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                lblStatus.Text = "Download failed.";
            }
            finally
            {
                if (_downloadProcess != null)
                {
                    _downloadProcess.Dispose();
                    _downloadProcess = null;
                }

                SetDownloadingState(false);
            }
        } // end of DownloadVideoAsync


        private void HandleYtDlpOutput(string output)
        {
            lblStatus.Text = output;

            Match match = Regex.Match(
                output,
                @"download:\s*(?<percent>\d+(?:\.\d+)?)%",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return;
            }

            double percent;

            if (double.TryParse(
                match.Groups["percent"].Value,
                out percent))
            {
                int progress = (int)Math.Round(percent);

                // Math.Clamp is unavailable in older .NET Framework.
                progress = Math.Max(0, Math.Min(100, progress));
                progressBar1.Value = progress;
            }
        } //end of HandleYtDlpOutput

        private void SetDownloadingState(bool downloading)
        {
            btnConvert.Enabled = !downloading;
            btnBrowse.Enabled = !downloading;
            txtYouTubeUrl.Enabled = !downloading;
            txtOutputFolder.Enabled = !downloading;

            progressBar1.Style = ProgressBarStyle.Continuous;

            if (downloading)
            {
                progressBar1.Value = 0;
                lblStatus.Text = "Starting download...";
                btnConvert.Text = "DOWNLOADING...";
            }
            else
            {
                btnConvert.Text = "CONVERT";
            }
        } //end of SetDownloadingState


        private static bool TryGetYouTubeVideoId(
           string url,
           out string videoId)
        {
            videoId = string.Empty;
            Uri uri;

            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                return false;
            }

            string host = uri.Host
                .Replace("www.", string.Empty)
                .ToLowerInvariant();

            if (host == "youtu.be")
            {
                videoId = uri.AbsolutePath.Trim('/');
                return !string.IsNullOrWhiteSpace(videoId);
            }

            if (host != "youtube.com" &&
                host != "m.youtube.com" &&
                host != "music.youtube.com")
            {
                return false;
            }

            string[] segments = uri.AbsolutePath
                .Trim('/')
                .Split(
                    new char[] { '/' },
                    StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length >= 2 &&
                (segments[0].Equals(
                    "shorts",
                    StringComparison.OrdinalIgnoreCase) ||
                 segments[0].Equals(
                    "embed",
                    StringComparison.OrdinalIgnoreCase)))
            {
                videoId = segments[1];
                return true;
            }

            if (segments.Length >= 1 &&
                segments[0].Equals(
                    "watch",
                    StringComparison.OrdinalIgnoreCase))
            {
                videoId = GetQueryParameter(uri, "v");
                return !string.IsNullOrWhiteSpace(videoId);
            }

            return false;
        }//end of TryGetYouTubeVideoId


        private static bool IsSupportedYouTubeHost(string host)
        {
            host = host
               .Replace("www.", string.Empty)
               .ToLowerInvariant();

            return host == "youtube.com" ||
                   host == "m.youtube.com" ||
                   host == "music.youtube.com" ||
                   host == "youtu.be";
        } //end of IsSupportedYouTubeHost



        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_downloadProcess != null &&
                !_downloadProcess.HasExited)
            {
                DialogResult result = MessageBox.Show(
                    "A video is currently downloading. Stop it and exit?",
                    "Download in progress",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                try
                {
                    // Kill(bool entireProcessTree) is unavailable in older
                    // .NET Framework, so use Kill().
                    _downloadProcess.Kill();
                }
                catch
                {
                    // The process may have already exited.
                }
            }

            base.OnFormClosing(e);
        }  //end of OnFormClosing


        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description =
                    "Choose where downloaded videos will be saved";
                dialog.SelectedPath = txtOutputFolder.Text;
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtOutputFolder.Text = dialog.SelectedPath;
                }
            }
        } //end of btnBrowse_Click


        private static string GetQueryParameter(
         Uri uri,
         string parameterName)
        {
            string query = uri.Query.TrimStart('?');
            string[] items = query.Split(
                new char[] { '&' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string item in items)
            {
                string[] parts = item.Split(new char[] { '=' }, 2);
                string name = Uri.UnescapeDataString(parts[0]);

                if (!name.Equals(
                    parameterName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return parts.Length > 1
                    ? Uri.UnescapeDataString(parts[1])
                    : string.Empty;
            }

            return string.Empty;
        } //end of GetQueryParameter


        private static string QuoteArgument(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            return "\"" + value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"") + "\"";
        } //end of QuoteArgument


        private static void CreatePlayerHtml(string webContentFolder)
        {
            string playerFile = Path.Combine(
                webContentFolder,
                "player.html");

            string html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <meta name=""referrer"" content=""strict-origin-when-cross-origin"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <style>
        html, body {
            width: 100%;
            height: 100%;
            margin: 0;
            overflow: hidden;
            background: #000;
        }

        #player {
            width: 100%;
            height: 100%;
            border: 0;
        }
    </style>
</head>
<body>
    <iframe
        id=""player""
        allow=""accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share""
        referrerpolicy=""strict-origin-when-cross-origin""
        allowfullscreen>
    </iframe>

    <script>
        (function () {
            var parameters = new URLSearchParams(window.location.search);
            var videoId = parameters.get('videoId');

            if (!videoId) {
                document.body.innerHTML =
                    '<div style=""color:white;font-family:Arial;padding:20px"">' +
                    'No YouTube video ID was supplied.</div>';
                return;
            }

            var origin = window.location.origin;
            var playerUrl =
                'https://www.youtube.com/embed/' +
                encodeURIComponent(videoId) +
                '?origin=' + encodeURIComponent(origin) +
                '&playsinline=1&rel=0';

            document.getElementById('player').src = playerUrl;
        })();
    </script>
</body>
</html>";

            File.WriteAllText(playerFile, html, Encoding.UTF8);
        } //end of CreatePlayerHtml

    }
}