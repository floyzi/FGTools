using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace FGToolsCrashpad
{
    public static class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int MessageBox(IntPtr ptr, string msg, string title, uint type);

        [STAThread]
        public static async Task Main(string[] args)
        {
            if (args.Length == 0 || !int.TryParse(args[0], out int pid)) return;
            var bepinPath = args[1];

            if (!Directory.Exists(bepinPath)) return;
 
            try
            {
                using var client = Process.GetProcessById(pid);
                client.WaitForExit();
            }
            catch
            {

            }

            var errorLog = Path.Combine(bepinPath, "ErrorLog.log");
            if (!File.Exists(errorLog) || string.IsNullOrWhiteSpace(File.ReadAllText(errorLog))) return;

            var localLow = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "..", "LocalLow");
            var unityLog = Path.Combine(localLow, "Mediatonic", "FallGuys_client", "Player.log");
            var logOutput = Path.Combine(bepinPath, "LogOutput.log");
            var pluginsPath = Path.Combine(bepinPath, "plugins");
            var fgtPath = Path.Combine(pluginsPath, "FGTools");
            var reportsPath = Path.Combine(fgtPath, "Reports", "Crashes");

            if (!Directory.Exists(pluginsPath) || !Directory.Exists(fgtPath)) return;

            if (!Directory.Exists(reportsPath)) Directory.CreateDirectory(reportsPath);

            var reportName = $"CrashReport_{DateTime.Now:HHMMssFF}";
            var report = Path.Combine(reportsPath, reportName);
            if (Directory.Exists(report)) Directory.Delete(report, true);

            Directory.CreateDirectory(report);

            File.Copy(logOutput, Path.Combine(report, "LogOutput.log"));
            File.Copy(errorLog, Path.Combine(report, "ErrorLog.log"));
            File.Copy(unityLog, Path.Combine(report, "Player.log"));

            ZipFile.CreateFromDirectory(report, Path.Combine(reportsPath, $"{reportName}.zip"));
            Directory.Delete(report, true);

            var box = MessageBox(IntPtr.Zero, "Сongratulations! Your game just crashed!\n" +
                $"A report with name \"{reportName}\" was created\n\n" +
                $"It is highly recommended for you to send this report in the FGTools Discord Server, it will help me fix the issue that caused this crash and make sure it never happens again (can't promise that though)\n\n" +
                $"Would you like to open reports folder? This report is saved as \"{reportName}.zip\"\n\nNeed extra help with this? Check out the Troubleshooting section on the GitHub repository", "Welp...", 0x00000004 | 0x00000010);
            
            if (box == 6)
            {
                Process.Start("explorer.exe", @reportsPath);
            }
        }
    }
}