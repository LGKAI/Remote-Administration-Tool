# Remote Administration Tool

Đồ án Mạng máy tính

Video Demo: https://youtu.be/uTRhVYS-dbo?si=z7fixEOgxTJXViWt

> **⚠️ TUYÊN BỐ MIỄN TRỪ TRÁCH NHIỆM:** Phần mềm này được phát triển hoàn toàn vì **mục đích giáo dục** (đồ án môn Mạng máy tính). Nhóm phát triển không chịu trách nhiệm pháp lý và không chịu trách nhiệm cho bất kỳ sự lạm dụng hoặc thiệt hại nào do chương trình này gây ra.

![.NET](https://img.shields.io/badge/.NET-8.0-purple) ![Language](https://img.shields.io/badge/Language-C%23-blue) ![Platform](https://img.shields.io/badge/Platform-Windows-blue) ![License](https://img.shields.io/badge/License-MIT-green)

## 📖 Giới thiệu
Nexus Control là một công cụ Quản trị Từ xa (RAT) gọn nhẹ được xây dựng từ đầu để minh họa các khái niệm lập trình mạng ở mức hệ thống.

Khác với các công cụ thương mại, dự án này tập trung vào kiến trúc **"Không phụ thuộc" (Zero-Dependency)**:
* **Giao thức tùy chỉnh:** Tự triển khai bắt tay **WebSocket (RFC 6455)** và truyền dữ liệu qua TCP Socket thô mà không sử dụng thư viện WebSocket bên ngoài.
* **Tương tác hệ thống:** Tương tác trực tiếp với Windows API (User32, Kernel32) để theo dõi và quản lý tiến trình.

## 🚀 Tính năng chính

### 1. Kết nối cốt lõi
* **Máy chủ WebSocket tùy chỉnh:** TCP Listener tự triển khai xử lý nâng cấp HTTP và mã hóa/giải mã WebSocket một cách thủ công.
* **Kiến trúc đa luồng:** Xử lý nhiều lệnh cùng lúc mà không làm đóng băng giao diện người dùng.

### 2. Giám sát hệ thống
* **Truyền phát Webcam theo thời gian thực:** Sử dụng `AForge.Video` với tính năng nén JPEG tối ưu (chất lượng 50%) để truyền qua TCP mượt mà.
* **Chụp ảnh màn hình trực tiếp:** Chụp các khung hình desktop sử dụng GDI+ và truyền qua mã hóa Base64.
* **Keylogger toàn cầu:** Triển khai `SetWindowsHookEx` (WH_KEYBOARD_LL) để nắm bắt các phím bấm ở cấp độ kernel.

### 3. Quản lý từ xa
* **Quản lý tiến trình:** Liệt kê các tác vụ đang chạy và buộc dừng tiến trình theo PID.
* **Kiểm soát ứng dụng:** Quét phần mềm đã cài đặt thông qua Registry và khả năng khởi chạy từ xa.
* **Trình quản lý tệp:** Liệt kê thư mục từ xa, tải tệp xuống (được tái tạo thông qua Blob trong trình duyệt) và xóa tệp.
* **Điều khiển nguồn:** Chức năng Tắt máy và Khởi động lại từ xa.

### 4. Kênh C2 dự phòng (Tích hợp Telegram)
* Hoạt động như một kênh điều khiển dự phòng khi kết nối TCP trực tiếp bị chặn.
* **Khả năng của Bot:**
    * `/scan`: Kiểm tra trạng thái mục tiêu.
    * `/screen`: Nhận ngay ảnh chụp màn hình qua Telegram.
    * `/get <path>`: Đánh cắp tệp một cách âm thầm.
    * `/cmd`: Thực thi các lệnh shell ẩn.
    * Sử dụng kỹ thuật **Long Polling** với `HttpClient` gốc.

## 🛠️ Ngăn xếp Công nghệ

* **Máy chủ (Agent):** C# .NET 8.0 (Windows Forms - Chế độ ẩn).
* **Máy khách (Dashboard):** HTML5, CSS3 (Giao diện Glassmorphism), Vanilla JavaScript.
* **Giao tiếp:** Raw TCP Sockets, Giao thức WebSocket, Telegram Bot API.

## 📂 Cấu trúc thư mục

```text
source/
├── client/                 # Mã nguồn máy khách (Web Dashboard)
│   ├── public/             # Các tệp tĩnh (HTML, CSS, JS) cho giao diện
│   ├── package.json        # Thông tin cấu hình và dependencies Node.js
│   ├── package-lock.json
│   └── web_server.js       # Máy chủ web cục bộ phục vụ Dashboard
├── server/                 # Mã nguồn máy chủ (Agent chạy trên máy mục tiêu)
│   ├── Program.cs          # Điểm bắt đầu (Entry point) của ứng dụng
│   ├── server.cs           # Logic xử lý chính (Sockets, Commands, API...)
│   ├── server.csproj       # Tệp cấu hình project C# (.NET 8.0)
│   └── server.resx         # Tài nguyên ứng dụng
└── README.md               # Tài liệu hướng dẫn dự án
```

## 🔧 Hướng dẫn Cài đặt & Sử dụng

### Yêu cầu hệ thống
- Máy chủ (Server): Yêu cầu .NET 8.0 SDK để biên dịch.
- Máy khách (Client): Môi trường Node.js để chạy web server cục bộ.

### Cài đặt và Chạy

1. **Khởi chạy Máy chủ (Server):**
   Mở terminal trong thư mục `server` và chạy các lệnh:
   ```bash
   # Dựng lại mã nguồn (mỗi khi có thay đổi)
   dotnet build
   
   # Chạy máy chủ
   dotnet run
   ```

   *Hoặc nếu muốn đóng gói thành file thực thi duy nhất (server.exe):*
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
   ```
   Sau đó bạn có thể lấy file `server.exe` để chạy trực tiếp trên máy nạn nhân (máy chủ).

2. **Khởi chạy Máy khách (Client):**
   Mở terminal trong thư mục `client`, cài đặt các gói (nếu cần) và khởi chạy server cục bộ:
   ```bash
   node web_server.js
   ```

3. **Sử dụng:**
   - Khi chạy `server.exe` (hoặc `dotnet run`), máy chủ sẽ chạy ngầm trên máy mục tiêu.
   - Truy cập vào giao diện web từ máy khách (thường là qua cổng mà `web_server.js` cấu hình).
   - Nhập địa chỉ IP của máy mục tiêu trong Bảng điều khiển Web (Web Dashboard) và nhấp vào **Connect** để bắt đầu điều khiển từ xa.