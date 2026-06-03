/*
 * @brief Entry Point
 * 
 * @author Prahlad Yeri <prahladyeri@yahoo.com>
 * @license MIT
 * @date 2026-05-31
 */
using System;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace mdglance
{
    internal static class Program
    {
        private static Mutex mutex = null;

        [STAThread]
        static void Main()
        {
            const string mutexName = "Global\\mdglanceMutex";
            bool createdNew;
            mutex = new Mutex(true, mutexName, out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("Another instance of the application is already running.", "Instance Already Running",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.ThreadException += (sender, args) =>
            {
                MessageBox.Show(args.Exception.ToString(), "Unhandled UI Exception");
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = args.ExceptionObject as Exception;
                MessageBox.Show(ex?.ToString() ?? "Unknown error", "Fatal Exception");
            };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
