using System;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;

namespace ModernServer
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // --- FIX LỖI STARTUP QUAN TRỌNG NHẤT ---
            // Ép chương trình lấy thư mục chứa file .exe làm thư mục gốc
            // Nếu không có dòng này, khi Startup nó sẽ chạy ở C:\Windows\System32 và bị lỗi
            try 
            {
                string currentPath = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
                if (!string.IsNullOrEmpty(currentPath))
                {
                    Directory.SetCurrentDirectory(currentPath);
                }
            }
            catch { }
            // ----------------------------------------

            // GHI LOG KIỂM TRA SỰ SỐNG
            try 
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "rat_debug.txt");
                File.AppendAllText(logPath, $"\n[{DateTime.Now}] >>> PROGRAM.CS ĐÃ CHẠY (FIXED PATH)! <<<\n");
            }
            catch { }

            ApplicationConfiguration.Initialize();
            
            try 
            {
                // Chạy Form chính
                Application.Run(new ServerForm());
            }
            catch (Exception ex)
            {
                try
                {
                    string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "rat_debug.txt");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] ☠️ LỖI CHẾT CHƯƠNG TRÌNH: {ex.Message}\n");
                }
                catch { }
            }
        }
    }
}