using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PlugHub.Installer
{
    internal sealed class InstallerForm : Form
    {
        private const string DefaultInstallDirectory = @"D:\Program Files\PlugHub";

        private readonly TextBox _installDirectoryTextBox = new TextBox();
        private readonly Button _browseButton = new Button();
        private readonly Button _installButton = new Button();
        private readonly Button _closeButton = new Button();
        private readonly Label _statusLabel = new Label();

        public InstallerForm()
        {
            Text = "PlugHub Setup";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(620, 240);

            var titleLabel = new Label
            {
                Text = "Install PlugHub for Revit 2020",
                AutoSize = true,
                Font = new Font(Font.FontFamily, 13, FontStyle.Bold),
                Location = new Point(18, 18)
            };

            var descriptionLabel = new Label
            {
                Text = "Choose the PlugHub install directory. The installer will copy files and register the Revit 2020 addin for all Windows users.",
                AutoSize = false,
                Location = new Point(20, 52),
                Size = new Size(570, 42)
            };

            var directoryLabel = new Label
            {
                Text = "Install directory:",
                AutoSize = true,
                Location = new Point(20, 108)
            };

            _installDirectoryTextBox.Location = new Point(20, 130);
            _installDirectoryTextBox.Size = new Size(475, 23);
            _installDirectoryTextBox.Text = DefaultInstallDirectory;

            _browseButton.Text = "Browse...";
            _browseButton.Location = new Point(505, 128);
            _browseButton.Size = new Size(88, 27);
            _browseButton.Click += BrowseButton_Click;

            _statusLabel.AutoSize = false;
            _statusLabel.Location = new Point(20, 166);
            _statusLabel.Size = new Size(570, 24);
            _statusLabel.Text = "Ready.";

            _installButton.Text = "Install";
            _installButton.Location = new Point(410, 200);
            _installButton.Size = new Size(88, 28);
            _installButton.Click += InstallButton_Click;

            _closeButton.Text = "Close";
            _closeButton.Location = new Point(505, 200);
            _closeButton.Size = new Size(88, 28);
            _closeButton.Click += (sender, args) => Close();

            Controls.Add(titleLabel);
            Controls.Add(descriptionLabel);
            Controls.Add(directoryLabel);
            Controls.Add(_installDirectoryTextBox);
            Controls.Add(_browseButton);
            Controls.Add(_statusLabel);
            Controls.Add(_installButton);
            Controls.Add(_closeButton);
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose PlugHub install directory";
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
                MessageBox.Show(this, "Install directory is required.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            try
            {
                _statusLabel.Text = "Copying PlugHub files...";
                InstallerPayload.ExtractTo(installDirectory);

                _statusLabel.Text = "Registering Revit addin...";
                var addinPath = AddinManifestWriter.Install(installDirectory);

                _statusLabel.Text = "Installed. Addin: " + addinPath;
                MessageBox.Show(this, "PlugHub was installed successfully.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Install failed.";
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
