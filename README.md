# Remote Administration Tool

Đồ án Mạng máy tính

> **⚠️ DISCLAIMER:** This software is developed solely for **educational purposes** as a final project for the Network Programming course at **University of Science - VNUHCM** (Trường ĐHKHTN - ĐHQG HCM). The developers assume no liability and are not responsible for any misuse or damage caused by this program.

![.NET](https://img.shields.io/badge/.NET-8.0-purple) ![Language](https://img.shields.io/badge/Language-C%23-blue) ![Platform](https://img.shields.io/badge/Platform-Windows-blue) ![License](https://img.shields.io/badge/License-MIT-green)

## 📖 Introduction
Nexus Control is a lightweight Remote Administration Tool (RAT) built from scratch to demonstrate low-level network programming concepts.

Unlike commercial tools, this project focuses on **"Zero-Dependency" architecture**:
* **Custom Protocol:** Manually implements **WebSocket (RFC 6455)** handshake and data framing over raw TCP Sockets without using external WebSocket libraries.
* **System Internals:** Interacts directly with Windows API (User32, Kernel32) for hooking and process management.

## 🚀 Key Features

### 1. Core Connectivity
* **Custom WebSocket Server:** Self-implemented TCP Listener that handles HTTP upgrades and WebSocket framing (masking/unmasking) manually.
* **Multi-threaded Architecture:** Handles multiple commands simultaneously without freezing the UI.

### 2. System Surveillance
* **Real-time Webcam Streaming:** Uses `AForge.Video` with optimized JPEG compression (50% quality) for smooth transmission over TCP.
* **Live Screen Capture:** Captures desktop frames using GDI+ and streams via Base64 encoding.
* **Global Keylogger:** Implements `SetWindowsHookEx` (WH_KEYBOARD_LL) to capture keystrokes at the kernel level.

### 3. Remote Management
* **Process Manager:** List running tasks and force-kill processes by PID.
* **Application Control:** Scan installed software via Registry and remote launch capability.
* **File Explorer:** Remote directory listing, file downloading (reconstructed via Blob in browser), and file deletion.
* **Power Control:** Remote Shutdown and Restart functionality.

### 4. Fail-safe C2 Channel (Telegram Integration)
* Acts as a backup control channel when direct TCP connection is blocked.
* **Bot Capabilities:**
    * `/scan`: Check victim status.
    * `/screen`: Get instant screenshot via Telegram.
    * `/get <path>`: Steal files stealthily.
    * `/cmd`: Execute hidden shell commands.
    * Uses **Long Polling** technique with native `HttpClient`.

## 🛠️ Tech Stack

* **Server (Agent):** C# .NET 8.0 (Windows Forms - Hidden Mode).
* **Client (Dashboard):** HTML5, CSS3 (Glassmorphism UI), Vanilla JavaScript.
* **Communication:** Raw TCP Sockets, WebSocket Protocol, Telegram Bot API.

## 🔧 Installation & Setup

1.  **Clone the repo:**
    ```bash
    git clone [https://github.com/your-username/your-repo-name.git](https://github.com/your-username/your-repo-name.git)
    ```
2.  **Server Side:**
    * Open `server.sln` in Visual Studio 2022.
    * Restore NuGet packages (AForge, System.Management).
    * Build in `Release` mode.
3.  **Client Side:**
    * No installation required. Simply open `index.html` in any modern browser.
4.  **Usage:**
    * Run `Server.exe` on the target machine (it will run in background).
    * Enter the target IP in the Web Dashboard and click **Connect**.