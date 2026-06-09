using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PlugHub.Installer
{
    internal sealed class InstallerForm : Form
    {
        private const string DefaultInstallDirectory = @"D:\Program Files\PlugHub";

        private static readonly Color AccentColor = Color.FromArgb(0, 120, 212);
        private static readonly Color TextColor = Color.FromArgb(31, 35, 41);
        private static readonly Color MutedTextColor = Color.FromArgb(92, 99, 112);
        private static readonly Color BorderColor = Color.FromArgb(214, 219, 225);
        private static readonly Color SurfaceColor = Color.FromArgb(245, 247, 250);

        private readonly TextBox _installDirectoryTextBox = new TextBox();
        private readonly Button _browseButton = new Button();
        private readonly Button _installButton = new Button();
        private readonly Button _closeButton = new Button();
        private readonly Label _statusLabel = new Label();

        public InstallerForm()
        {
            Text = "PlugHub 安装器";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(640, 292);
            ApplyModernInstallerStyle();

            var executableIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (executableIcon != null)
            {
                Icon = executableIcon;
            }

            var accentBar = new Panel
            {
                BackColor = AccentColor,
                Location = new Point(0, 0),
                Size = new Size(4, ClientSize.Height)
            };

            var logo = new PictureBox
            {
                Location = new Point(28, 24),
                Size = new Size(40, 40),
                SizeMode = PictureBoxSizeMode.CenterImage
            };
            if (executableIcon != null)
            {
                logo.Image = executableIcon.ToBitmap();
            }

            var titleLabel = new Label
            {
                Text = "PlugHub for Revit 2020",
                AutoSize = true,
                ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
                Location = new Point(84, 22)
            };

            var descriptionLabel = new Label
            {
                Text = "安装框架文件，并以 machine-wide 方式注册 Revit 2020 addin。",
                AutoSize = false,
                ForeColor = MutedTextColor,
                Location = new Point(86, 52),
                Size = new Size(530, 28)
            };

            var directoryLabel = new Label
            {
                Text = "安装目录",
                AutoSize = true,
                ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold),
                Location = new Point(28, 104)
            };

            _installDirectoryTextBox.Location = new Point(30, 128);
            _installDirectoryTextBox.Size = new Size(500, 24);
            _installDirectoryTextBox.Text = DefaultInstallDirectory;
            _installDirectoryTextBox.BorderStyle = BorderStyle.FixedSingle;

            _browseButton.Location = new Point(540, 126);
            _browseButton.Size = new Size(84, 28);
            BuildSecondaryButton(_browseButton, "浏览...");
            _browseButton.Click += BrowseButton_Click;

            _statusLabel.AutoSize = false;
            _statusLabel.Location = new Point(30, 174);
            _statusLabel.Size = new Size(594, 32);
            _statusLabel.ForeColor = MutedTextColor;
            _statusLabel.Text = "准备安装。";

            var divider = new Panel
            {
                BackColor = BorderColor,
                Location = new Point(28, 222),
                Size = new Size(596, 1)
            };

            _installButton.Location = new Point(412, 242);
            _installButton.Size = new Size(104, 32);
            BuildPrimaryButton(_installButton, "安装");
            _installButton.Click += InstallButton_Click;

            _closeButton.Location = new Point(524, 242);
            _closeButton.Size = new Size(100, 32);
            BuildSecondaryButton(_closeButton, "关闭");
            _closeButton.Click += (sender, args) => Close();

            Controls.Add(accentBar);
            Controls.Add(logo);
            Controls.Add(titleLabel);
            Controls.Add(descriptionLabel);
            Controls.Add(directoryLabel);
            Controls.Add(_installDirectoryTextBox);
            Controls.Add(_browseButton);
            Controls.Add(_statusLabel);
            Controls.Add(divider);
            Controls.Add(_installButton);
            Controls.Add(_closeButton);
        }

        private void ApplyModernInstallerStyle()
        {
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9, FontStyle.Regular, GraphicsUnit.Point);
        }

        private static void BuildPrimaryButton(Button button, string text)
        {
            button.Text = text;
            button.BackColor = AccentColor;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.UseVisualStyleBackColor = false;
        }

        private static void BuildSecondaryButton(Button button, string text)
        {
            button.Text = text;
            button.BackColor = SurfaceColor;
            button.ForeColor = TextColor;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = BorderColor;
            button.FlatAppearance.BorderSize = 1;
            button.UseVisualStyleBackColor = false;
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择 PlugHub 安装目录";
                dialog.ShowNewFolderButton = true;
                var selectedPath = _installDirectoryTextBox.Text.Trim();
                if (Directory.Exists(selectedPath))
                {
                    dialog.SelectedPath = selectedPath;
                }
                else if (Directory.Exists(@"D:\Program Files"))
                {
                    dialog.SelectedPath = @"D:\Program Files";
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _installDirectoryTextBox.Text = NormalizeSelectedInstallDirectory(dialog.SelectedPath);
                }
            }
        }

        private void InstallButton_Click(object sender, EventArgs e)
        {
            var installDirectory = _installDirectoryTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                MessageBox.Show(this, "安装目录不能为空。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            try
            {
                _statusLabel.Text = "正在复制 PlugHub 文件...";
                InstallerPayload.ExtractTo(installDirectory);

                _statusLabel.Text = "正在注册 Revit addin...";
                var addinPath = AddinManifestWriter.Install(installDirectory);

                _statusLabel.Text = "安装完成。Addin: " + addinPath;
                MessageBox.Show(this, "PlugHub 安装完成。", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "安装失败。";
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _installDirectoryTextBox.Enabled = !busy;
            _browseButton.Enabled = !busy;
            _installButton.Enabled = !busy;
            _closeButton.Enabled = !busy;
            UseWaitCursor = busy;
        }

        private static string NormalizeSelectedInstallDirectory(string selectedPath)
        {
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return DefaultInstallDirectory;
            }

            var directoryName = new DirectoryInfo(selectedPath).Name;
            return string.Equals(directoryName, "PlugHub", StringComparison.OrdinalIgnoreCase)
                ? selectedPath
                : Path.Combine(selectedPath, "PlugHub");
        }
    }
}
