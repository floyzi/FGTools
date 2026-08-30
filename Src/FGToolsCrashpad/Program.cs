using System.Diagnostics;
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

            var box = MessageBox(IntPtr.Zero, "Looks like the game crashed! It is highly recommended for you to send a proper bug report in the FGTools Discord Server, it will help me to fix the issue that caused this and make sure this never happens again\n\nWould you like to open crashes folder?", "UH OH", 0x00000004 | 0x00000010);
            if (box == 6)
            {
                
            }
        }
    }
}