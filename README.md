# Remote Administration Tool

Đồ án Môn Mạng máy tính

🎥 **Video Demo:** [Xem trên YouTube](https://youtu.be/uTRhVYS-dbo?si=z7fixEOgxTJXViWt)

> **⚠️ TUYÊN BỐ MIỄN TRỪ TRÁCH NHIỆM:** Phần mềm này được phát triển hoàn toàn vì **mục đích giáo dục** (đồ án môn Mạng máy tính). Nhóm phát triển không chịu trách nhiệm pháp lý và không chịu trách nhiệm cho bất kỳ sự lạm dụng hoặc thiệt hại nào do chương trình này gây ra.

---

## 📖 Giới thiệu
Nexus Control là một công cụ Quản trị Từ xa (RAT) được xây dựng từ đầu để minh họa các khái niệm lập trình mạng ở mức hệ thống. Điểm đặc biệt của dự án này là kiến trúc **"Không phụ thuộc" (Zero-Dependency)**. Chúng tôi tự triển khai giao thức WebSocket, truyền dữ liệu qua TCP nguyên thủy, và giao tiếp trực tiếp với Windows API mà không dùng đến các thư viện bên thứ ba cồng kềnh.

## ⚙️ Cơ chế hoạt động
Hệ thống này được chia làm 3 thành phần chính hoạt động phối hợp với nhau:

1. **Server (Máy mục tiêu / Bị điều khiển):** 
   - Là một ứng dụng (được viết bằng C# .NET 8.0). 
   - Khi chạy trên máy mục tiêu, nó sẽ ẩn mình, tự động khởi động cùng Windows và mở sẵn các cổng kết nối (như WebSocket) để chờ lệnh. 
   - Ngoài ra nó sẽ báo danh qua Telegram khi máy tính có kết nối mạng.
2. **Client (Trang Web Quản trị):** 
   - Là một trang web có giao diện hiện đại (HTML/CSS/JS) dùng để điều khiển Server.
   - Trang web này sẽ kết nối trực tiếp đến **Server (Máy mục tiêu)** qua địa chỉ IP bằng giao thức WebSocket để gửi các lệnh theo thời gian thực (như xem webcam, chụp màn hình, tắt máy, quản lý file, v.v.).
3. **Telegram Bot (Kênh dự phòng):**
   - Đóng vai trò như một kênh điều khiển phụ trợ. Bạn có thể nhắn tin trực tiếp với Bot Telegram để ra lệnh cho máy mục tiêu (lấy ảnh màn hình, đánh cắp file, thực thi lệnh) kể cả khi không vào được trang Web điều khiển.

---

## 🚀 Tính năng nổi bật

* **Điều khiển thời gian thực:** Truyền phát Webcam, chụp màn hình trực tiếp.
* **Theo dõi hệ thống:** Keylogger ngầm (lưu lại phím bấm), quản lý tiến trình (Task Manager), duyệt và tải file từ xa.
* **Lệnh hệ thống:** Tắt máy, khởi động lại, thực thi mã ẩn.
* **Chống chịu ngắt kết nối:** Tự động báo danh lại qua Telegram, cơ chế gửi file/ảnh qua Bot khi đường truyền chính bị chặn.
* **Giao diện Web siêu đẹp:** Hỗ trợ Dark mode, hiệu ứng Glassmorphism.

---

## 📁 Cấu trúc thư mục mã nguồn

```text
source/
├── client/                     # Web Dashboard điều khiển (Node.js & Vanilla Web)
│   ├── public/                 # Giao diện và tài nguyên tĩnh
│   │   ├── background/         # Video nền giao diện
│   │   ├── sound/              # Hiệu ứng âm thanh tương tác
│   │   ├── favicon.jpg         # Icon ứng dụng
│   │   ├── hello.png           # Ảnh minh họa
│   │   ├── index.html          # Trang giao diện quản trị trung tâm
│   │   ├── script.js           # Xử lý logic giao diện, kết nối WebSocket và gửi/nhận lệnh
│   │   └── style.css           # Phong cách giao diện (Glassmorphism, Dark mode)
│   ├── package.json            # Khai báo phụ thuộc ứng dụng Node.js (Express)
│   ├── package-lock.json       # Khóa phiên bản các package Node.js
│   └── web_server.js           # Máy chủ Web tĩnh phục vụ giao diện điều khiển (Port 8080)
│
├── server/                     # Ứng dụng quản trị phía máy mục tiêu (C# .NET 8.0 Windows)
│   ├── Program.cs              # Entry point ứng dụng, xử lý khởi động và định tuyến luồng chạy
│   ├── server.cs               # Logic cốt lõi: WebSocket Server, Keylogger, Webcam, Screen capture, Telegram Bot
│   ├── server.csproj           # Cấu hình dự án .NET và các thư viện (AForge, System.Management...)
│   └── server.resx             # Tệp tài nguyên của ứng dụng Windows Forms
│
└── README.md                   # Tài liệu hướng dẫn sử dụng và giới thiệu đồ án
```

---

## 🛠️ Hướng dẫn Cài đặt & Sử dụng (Dành cho người mới)

Dưới đây là các bước chi tiết để chạy hệ thống. Bạn có thể thử nghiệm chạy cả Server và Client trên cùng một máy tính.

### Bước 1: Khởi động Server (Phần mềm bị điều khiển)
Yêu cầu máy tính phải cài đặt **.NET 8.0 SDK**.
1. Mở Terminal (Command Prompt / PowerShell) và trỏ vào thư mục `server`:
   ```bash
   cd source/server
   ```
2. Chạy ứng dụng Server:
   ```bash
   dotnet run
   ```
   *(Lúc này Server sẽ khởi động, kiểm tra mạng, gửi báo danh Telegram và mở cổng kết nối. Bạn hãy để cửa sổ Terminal này chạy ngầm)*

### Bước 2: Mở Giao diện Web Điều khiển (Client)
Yêu cầu máy tính phải cài đặt **Node.js**.
1. Mở một cửa sổ Terminal **mới** (không tắt Terminal ở Bước 1) và trỏ vào thư mục `client`:
   ```bash
   cd source/client
   ```
2. Cài đặt các thư viện phụ thuộc cho Web Server (chỉ cần làm ở lần chạy đầu tiên):
   ```bash
   npm install
   ```
3. Khởi chạy máy chủ Web:
   ```bash
   node web_server.js
   ```
4. **Vào trang điều khiển:** Mở trình duyệt web của bạn (Chrome, Edge, Cốc Cốc...) và truy cập vào đường dẫn:
   👉 **[http://localhost:8080](http://localhost:8080)**

   Tại giao diện này, bạn nhập IP là `127.0.0.1` (nếu đang test Server ngay trên máy này) và bấm **Connect** để bắt đầu kết nối!

### Bước 3: Đóng gói thành file .exe hoàn chỉnh (Tùy chọn)
Nếu bạn muốn đóng gói Server thành một file `server.exe` duy nhất để mang sang máy khác chạy (mà máy đó không cần cài đặt .NET), hãy mở Terminal ở thư mục `server` và chạy lệnh này:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
File thực thi cuối cùng sẽ được tạo ra tại thư mục: `server/bin/Release/net8.0-windows/win-x64/publish/`. Bạn chỉ cần copy file `server.exe` bên trong đó đi là được.