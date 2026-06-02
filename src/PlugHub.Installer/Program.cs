using System;
using System.Windows.Forms;

namespace PlugHub.Installer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Application.Run(new InstallerForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "PlugHub Installer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
