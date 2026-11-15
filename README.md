# NetworkSharing.exe – Windows Network Drive & Folder Sharing Manager

A production-grade, secure, lightweight Windows desktop application designed to easily share drives and folders over a local network, configure custom credentials, enforce granular NTFS/SMB permissions, and connect from other Windows PCs in 1 click.

---

## ⚡ Highlights

* **Single Standalone Executable:** Zero installation and zero external dependencies required (137 KB self-contained).
* **Role-Based Privilege Separation:** Client operations run as standard user without elevation; Host setup requests UAC on demand only when needed.
* **Modern Windows 11 Fluent UI:** Clean Windows Forms interface with Light and Dark theme toggles, rounded cards, vector icons, and rigid dock layout.
* **Native Windows SMB 2/3:** Uses native Windows file sharing services via NetAPI32 without third-party servers.
* **Zero Hardcoded Passwords:** Dedicated local standard Windows user account (`ShareUser` or custom) with cryptographically secure random password generation.
* **Dual-Layer Credential Vault:** Windows Credential Manager API (`Advapi32.dll` `CredWriteW`) with Windows DPAPI fallback.
* **Local Subnet Firewall Restriction:** Inbound TCP 445 scoped strictly to the local subnet on confirmed Private network profiles.
* **Windows Error 1219 Explicit Handling:** Automatically resolves multiple-credential session conflicts.
* **Transactional Rollback & Recovery:** Operation journal (`journal.txt`) and SDDL security descriptor backups for clean, atomic rollback.
* **Built-in Automated Test Suite:** Run `NetworkSharing.exe --test` for instant verification of security and functional components.

---

## 🚀 Quick Start Guide

### Running the Application
Launch [NetworkSharing.exe](file:///d:/Antigravity/NetworkSharing/NetworkSharing.exe):

```text
Primary Unified Navigation:
├── 🖥️ Host Sharing        -> Share drives/folders + Active Shares table with 1-click Disconnect / Stop Share
├── 💻 Client Connect       -> Connect & map shares + Mapped Drives table with 1-click Disconnect Drive
├── ⚡ Speed & Ping Test    -> Real ICMP latency in ms + 2-computer throughput benchmark (MB/s, Mbps, GB/s, Gbps)
└── ⚙️ Settings / Rollback  -> Fluent UI theme toggles + Complete atomic rollback & uninstall
```

---

### A. Sharing from This PC (Host Mode)
1. Navigate to **Host Setup** in the left sidebar.
2. **Step 1 - Select Drive or Folder:**
   - Choose any detected drive (`C:`, `D:`, etc.) from the list or click **Browse...** to pick any specific folder.
   - *Note: If Drive `C:` is selected, a security warning is displayed and Read-Only mode is automatically recommended.*
3. **Step 2 - Configure Share & Credentials:**
   - Enter your preferred **Share Name** (e.g. `Movies`, `Documents`, `Drive_D`).
   - Enter a **Username** (default: `ShareUser`).
   - Enter a **Password** or click **Generate Strong Password** for an instant cryptographic password.
4. **Step 3 - Set Permission Level:**
   - **Read & Write:** Full collaboration (browse, upload, edit, rename, delete).
   - **Read-Only:** Safe mode (browse, download, view only; cannot modify or delete).
5. **Step 4 - Click "Start Sharing Now":**
   - The app verifies administrator privilege (prompting UAC elevation if needed), configures the local standard account, applies NTFS ACLs, opens the scoped firewall port, and activates the SMB share.
   - A confirmation card appears displaying the live UNC path (e.g. `\\192.168.0.128\Drive_D`) with a **Copy Path** button.

---

### B. Connecting from Another PC (Client Mode - Standard User)
1. Copy **`NetworkSharing.exe`** to the client Windows computer (no administrator rights needed).
2. Navigate to **Client Connect** in the sidebar.
3. **Step 1:** Enter the host IP or computer name (or click **Scan Local Wi-Fi** to auto-detect).
4. Click **Test Connection** to verify TCP 445 SMB connectivity.
5. **Step 2:** Enter the username and password configured on the host. Ensure **Save credentials** is checked.
6. **Step 3:** Click **Discover Shares** to auto-populate available shares, select your desired share, and choose an available Drive Letter (e.g. `Z:`, `Y:`).
7. **Step 4:** Click **Connect & Map Drive in 1-Click**.
   - The drive is mapped immediately with persistent reconnection (`CONNECT_UPDATE_PROFILE`).
   - File Explorer opens directly into the shared drive.
   - Click **Desktop Shortcut** to place a permanent shortcut on your Desktop.

---

### C. Clean Rollback & Uninstall
Navigate to **Settings & Rollback** and click **Rollback All & Remove Sharing Configuration**:
* Deletes all shares created by `NetworkSharing.exe`.
* Removes application-created Windows Firewall rules.
* Unmounts all mapped network drives.
* Clears saved credentials from Windows Credential Manager.
* Deletes the dedicated `ShareUser` account.
* Restores original NTFS security descriptors from SDDL backups.
* **Never deletes any user data.**

---

## 🛡️ Security Architecture

| Security Domain | Implementation Details |
| :--- | :--- |
| **Privilege Separation** | Client mode runs as standard user. Host mode requests UAC via Shell `runas` with operation tokens. No secrets passed in CLI arguments. |
| **Credential Storage** | Windows Credential Manager API (`Advapi32.dll` `CredWriteW` / `CredDeleteW`) with Windows DPAPI fallback. No plaintext passwords on disk. |
| **Account Isolation** | Non-administrator local Windows standard account in the `Users` group. Prohibits administrative access or privilege escalation. |
| **Password Entropy** | Cryptographic PRNG (`RNGCryptoServiceProvider`) enforcing uppercase, lowercase, numbers, and symbols. |
| **Network Boundary** | Windows Defender Firewall rule restricts inbound TCP 445 to `LocalSubnet` on `Private` network profiles. Never exposed to WAN. |
| **Permissions Enforcement** | Dual-layer: SMB Share ACL + NTFS Directory ACLs (`ReadAndExecute` or `Modify`). Original ACLs backed up via SDDL. |
| **System Integrity** | Path validation blocks sharing critical Windows OS directories (`C:\Windows`, `C:\Program Files`, `System32`, `System Volume Information`, `$Recycle.Bin`). Reparse points and path traversal sequences (`..`) are rejected. |
| **Secret Redaction** | Structured logging with automatic regex-based scrubbing of passwords, tokens, and credentials (`[REDACTED]`). |

---

## 🧪 Automated Testing Suite

Run the built-in automated verification tests from the command line:

```powershell
.\NetworkSharing.exe --test
```

### Verified Acceptance Test Results (20/20 Passed):

| Status | Category | Test Name | Result |
| :---: | :--- | :--- | :---: |
| **PASS** | Security | Directory Traversal Protection (`..`) | Blocked |
| **PASS** | Security | Protected Core System Folder Block (`C:\Windows`) | Blocked |
| **PASS** | Security | Protected Program Files Block | Blocked |
| **PASS** | Security | Protected System Volume Information Block | Blocked |
| **PASS** | Security | System Drive Root Read-Only Warning | Verified |
| **PASS** | Security | Administrative Share Name Block (`$`) | Blocked |
| **PASS** | Security | Reserved Device Share Name Block (`COM1`, etc.) | Blocked |
| **PASS** | Security | Invalid Characters in Share Name | Blocked |
| **PASS** | Security | Built-in Account Name Protection (`Administrator`) | Blocked |
| **PASS** | Security | Cryptographic Password Generator Quality | Verified |
| **PASS** | Security | Secret Redactor Sanitization | Scrubbed |
| **PASS** | Security | Elevation Operation Dispatcher Parsing | Verified |
| **PASS** | Security | Security Descriptor SDDL Backup Capture | Verified |
| **PASS** | Security | Reparse Point & Symlink Safety Check | Blocked |
| **PASS** | Integration | Windows Credential Manager Save, Retrieve & Delete | Verified |
| **PASS** | Functional | Drive Detector Enumeration & System Flag | Verified |
| **PASS** | Functional | Local Network IP Resolution | Verified |
| **PASS** | Functional | Speed Test Format Unit Toggle Conversion | Verified |
| **PASS** | Functional | Ping Latency MS Measurement | Verified |
| **PASS** | Recovery | Transaction Manager Journal & Rollback Lifecycle | Verified |

---

## 📦 Binary Release Manifest

* **File Name:** `NetworkSharing.exe`
* **Size:** `164,352 bytes` (164.4 KB)
* **Architecture:** `x64`
* **Target Runtime:** .NET Framework 4.5+ / Windows 10 & 11 native
* **SHA-256 Checksum:** `B35A514692778D8CFD0E9DA1CA1BE145E688A33F5EC134FD679448180ED7EFFB`

