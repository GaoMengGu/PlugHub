using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PlugHub.Uninstaller
{
    internal sealed class UninstallerForm : Form
    {
        private readonly string _installDirectory;
        private readonly Button _uninstallButton = new Button();
        private readonly Button _closeButton = new Button();
        private readonly Label _statusLabel = new Label();

        public UninstallerForm(string installDirectory)
        {
            _installDirectory = Path.GetFullPath(installDirectory ?? string.Empty);

            Text = "PlugHub Uninstall";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(620, 230);

            var titleLabel = new Label
            {
                Text = "Uninstall PlugHub for Revit 2020",
                AutoSize = true,
                Font = new Font(Font.FontFamily, 13, FontStyle.Bold),
                Location = new Point(18, 18)
            };

            var descriptionLabel = new Label
            {
                Text = "This removes the machine-wide Revit 2020 addin manifest and deletes the PlugHub install directory.",
                AutoSize = false,
                Location = new Point(20, 52),
                Size = new Size(570, 42)
            };

            var directoryLabel = new Label
            {
                Text = "Install directory:",
                AutoSize = true,
                Location = new Point(20, 104)
            };

            var directoryTextBox = new TextBox
            {
                Location = new Point(20, 126),
                Size = new Size(573, 23),
                ReadOnly = true,
                Text = _installDirectory
            };

            _statusLabel.AutoSize = false;
            _statusLabel.Location = new Point(20, 162);
            _statusLabel.Size = new Size(570, 24);
            _statusLabel.Text = "Ready.";

            _uninstallButton.Text = "Uninstall";
            _uninstallButton.Location = new Point(410, 192);
            _uninstallButton.Size = new Size(88, 28);
            _uninstallButton.Click += UninstallButton_Click;

            _closeButton.Text = "Close";
            _closeButton.Location = new Point(505, 192);
            _closeButton.Size = new Size(88, 28);
            _closeButton.Click += (sender, args) => Close();

            Controls.Add(titleLabel);
            Controls.Add(descriptionLabel);
            Controls.Add(directoryLabel);
            Controls.Add(directoryTextBox);
            Controls.Add(_statusLabel);
            Controls.Add(_uninstallButton);
            Controls.Add(_closeButton);
        }

        private void UninstallButton_Click(object sender, EventArgs e)
        {
            var confirmation = MessageBox.Show(
                this,
                "Uninstall PlugHub from this computer?",
                Text,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            SetBusy(true);
            try
            {
                ValidateInstallDirectory(_installDirectory);

                _statusLabel.Text = "Removing Revit addin manifest...";
                RemoveAddinManifest();

                _statusLabel.Text = "Deleting PlugHub install directory...";
                if (Directory.Exists(_installDirectory))
                {
                    Directory.Delete(_installDirectory, true);
                }

                _statusLabel.Text = "Uninstalled.";
                MessageBox.Show(this, "PlugHub was uninstalled successfully.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Uninstall failed.";
                MessageBox.Show(
                    this,
                    ex.Message + Environment.NewLine + Environment.NewLine + "Close Revit and check administrator permissions, then run the uninstaller again.",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _uninstallButton.Enabled = !busy;
            _closeButton.Enabled = !busy;
            UseWaitCursor = busy;
        }

        private static void RemoveAddinManifest()
        {
            var addinPath = AddinManifestPath();
            if (File.Exists(addinPath))
            {
                File.Delete(addinPath);
            }
        }

        private static string AddinManifestPath()
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrWhiteSpace(programData))
            {
                throw new InvalidOperationException("ProgramData could not be resolved for machine-wide Revit addin registration.");
            }

            return Path.Combine(programData, "Autodesk", "Revit", "Addins", "2020", "PlugHub.addin");
        }

        private static void ValidateInstallDirectory(string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                throw new InvalidOperationException("Install directory is required.");
            }

            var fullPath = Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to delete a drive root: " + fullPath);
            }

            if (!string.Equals(Path.GetFileName(fullPath), "PlugHub", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to delete an install directory that is not named PlugHub: " + fullPath);
            }
        }
    }
}
