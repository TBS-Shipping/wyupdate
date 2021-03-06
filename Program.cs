using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace wyUpdate
{
    static class Program
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        private static string programName = Assembly.GetExecutingAssembly().GetName().Name;

        public static void ConfigureNLog(string logPath, LogLevel level)
        {
            // Step 1. Create configuration object 
            var config = new LoggingConfiguration();

            var fileTarget = new FileTarget();
            config.AddTarget("file", fileTarget);

            // Step 3. Set target properties 
            string layout = @"${date:format=HH\:mm\:ss.mmm}|${level}|${callsite}|${threadid}|${logger} -> ${message} ${exception:format=ToString,StackTrace:maxInnerExceptionLevel=5}${newline}";
            fileTarget.FileName = logPath;
            fileTarget.Layout = layout;


            var rule2 = new LoggingRule("*", level, fileTarget);
            config.LoggingRules.Add(rule2);

            // Step 5. Activate the configuration
            LogManager.Configuration = config;
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            ConfigureNLog("./logs/" + programName + "-${shortdate}.log.txt", LogLevel.Debug);
            _logger.Debug("{0} is ready...", programName);
            Application.EnableVisualStyles();

            frmMain mainForm = new frmMain(args);

            // if the mainForm has been closed, return the code
            if (mainForm.IsDisposed)
                return mainForm.ReturnCode;

            StringBuilder mutexName = new StringBuilder("Local\\wyUpdate-" + mainForm.update.GUID);

            if (mainForm.IsAdmin)
                mutexName.Append('a');

            if (mainForm.SelfUpdateState == SelfUpdateState.FullUpdate)
                mutexName.Append('s');

            if (mainForm.IsNewSelf)
                mutexName.Append('n');

            Mutex mutex = new Mutex(true, mutexName.ToString());

            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                Application.Run(mainForm);

                mutex.ReleaseMutex();
            }
            else
            {
                FocusOtherProcess();
                return 4;
            }

            /*
             Possible return codes:

             0 = Success / no updates found
             1 = General error
             2 = Updates found
             3 = Update process cancelled
             4 = wyUpdate exited immediately to focus another wyUpdate instance
            */
            return mainForm.ReturnCode;
        }



        [DllImport("user32")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32")]
        static extern int ShowWindow(IntPtr hWnd, int swCommand);
        [DllImport("user32")]
        static extern bool IsIconic(IntPtr hWnd);

        public static void FocusOtherProcess()
        {
            Process proc = Process.GetCurrentProcess();

            // Using Process.ProcessName does not function properly when
            // the actual name exceeds 15 characters. Using the assembly 
            // name takes care of this quirk and is more accurate than 
            // other work arounds.

            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;

            foreach (Process otherProc in Process.GetProcessesByName(assemblyName))
            {
                //ignore "this" process, and ignore wyUpdate with a different filename

                if (proc.Id != otherProc.Id 
                        && otherProc.MainModule != null && proc.MainModule != null 
                        && proc.MainModule.FileName == otherProc.MainModule.FileName)
                {
                    // Found a "same named process".
                    // Assume it is the one we want brought to the foreground.
                    // Use the Win32 API to bring it to the foreground.

                    IntPtr hWnd = otherProc.MainWindowHandle;

                    if (IsIconic(hWnd))
                        ShowWindow(hWnd, 9); //SW_RESTORE

                    SetForegroundWindow(hWnd);
                    break;
                }
            }
        }
    }
}