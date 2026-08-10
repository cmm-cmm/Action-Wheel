# ?? T?M T?T C?C PH??NG ?N TRI?N KHAI ACTION WHEEL

## ?? TL;DR - C?CH NHANH NH?T

```powershell
# Ch? c?n 1 l?nh n?y!
.\Build-Quick.bat

# Ho?c
.\Build-Publish.ps1

# File output:
# Å® bin\Publish\win-x64\Action Wheel.exe (267 MB)
```

---

## ?? SO S?NH C?C PH??NG ?N

| Ph??ng ?n | K?ch Th??c | Y?u C?u | ?? Kh? | Khuy?n Ngh? |
|-----------|------------|---------|--------|-------------|
| **Self-Contained EXE** | ~267 MB | Ch? c?n Windows 10+ | ? D? | ? **KHUY?N NGH?** |
| **Framework-Dependent** | ~10 MB | C?n .NET 8 Runtime | ?? Trung B?nh | ?? Kh?ng khuy?n ngh? |
| **MSIX Package** | ~200 MB | Windows 10+ | ??? Kh? | ?? Cho Store |
| **Installer (Inno Setup)** | ~267 MB | Inno Setup | ?? Trung B?nh | ? Chuy?n nghi?p |
| **Portable ZIP** | ~267 MB | Ch? c?n Windows 10+ | ? D? nh?t | ? Ph?n ph?i nhanh |

---

## ?? PH??NG ?N 1: SELF-CONTAINED EXE (?? XU?T)

### ? ?u ?i?m
- ? **Kh?ng c?n c?i ??t g?** - Ch?y ngay tr?n b?t k? Windows 10+ n?o
- ? **File duy nh?t** - D? ph?n ph?i
- ? **Kh?ng xung ??t** - Kh?ng ?nh h??ng .NET versions kh?c
- ? **??n gi?n nh?t** cho end-user

### ?? Nh??c ?i?m
- File l?n (~267 MB)

### ??? C?ch Build

#### Option A: D?ng Script (Khuy?n Ngh?)
```batch
.\Build-Quick.bat
```

Ho?c PowerShell:
```powershell
.\Build-Publish.ps1
```

#### Option B: Command Line
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o "bin\Publish\win-x64"
```

#### Option C: Visual Studio
1. Right-click project Å® **Publish...**
2. Ch?n profile **win-x64**
3. Click **Publish**

### ?? C?ch Ph?n Ph?i
```powershell
# T?o ZIP
Compress-Archive -Path "bin\Publish\win-x64\Action Wheel.exe" -DestinationPath "ActionWheel-v1.0.0.zip"

# Upload l?n:
# - Google Drive
# - Dropbox
# - GitHub Releases
# - Website
```

### ?? End-User Instructions
```
1. Download "ActionWheel-v1.0.0.zip"
2. Gi?i n?n
3. Ch?y "Action Wheel.exe"
4. Nh?n n?t gi?a chu?t Å® Menu xu?t hi?n!
```

---

## ?? PH??NG ?N 2: PORTABLE VERSION

### ? ?u ?i?m
- ? Gi?ng self-contained nh?ng c? th?m files h? tr?
- ? Ch?y t? USB ???c
- ? Kh?ng c?n c?i ??t

### ??? C?ch T?o
```powershell
# 1. Build self-contained
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "bin\Portable"

# 2. Copy README
Copy-Item "README.md" "bin\Portable\"
Copy-Item "LICENSE.txt" "bin\Portable\"

# 3. T?o LENH-DOC.txt
@"
==============================================
  ACTION WHEEL - PORTABLE VERSION
==============================================

C?ch s? d?ng:
1. Ch?y "Action Wheel.exe"
2. Nh?n n?t gi?a chu?t (middle mouse button)
3. Menu tr?n xu?t hi?n!

Kh?ng c?n c?i ??t, kh?ng c?n .NET Runtime!
"@ | Out-File "bin\Portable\LENH-DOC.txt" -Encoding UTF8

# 4. N?n th?nh ZIP
Compress-Archive -Path "bin\Portable\*" -DestinationPath "ActionWheel-Portable-v1.0.0.zip"
```

### ?? K?t Qu?
```
ActionWheel-Portable-v1.0.0.zip
Ñ•ÑüÑü Action Wheel.exe
Ñ•ÑüÑü README.md
Ñ•ÑüÑü LICENSE.txt
Ñ§ÑüÑü LENH-DOC.txt
```

---

## ??? PH??NG ?N 3: INSTALLER (CHUY?N NGHI?P)

### ? ?u ?i?m
- ? C?i v?o **Program Files**
- ? T?o **Start Menu** shortcuts
- ? C? **Uninstaller**
- ? T?y ch?n **Desktop icon**
- ? T?y ch?n **Run at startup**
- ? Ki?m tra Windows version

### ?? Y?u C?u
Download Inno Setup: https://jrsoftware.org/isdl.php

### ??? C?c B??c

#### 1. Build app tr??c
```batch
.\Build-Quick.bat
```

#### 2. C?i Inno Setup

#### 3. Compile installer
```
- M? Inno Setup Compiler
- File Å® Open Å® Ch?n "ActionWheel-Setup.iss"
- Build Å® Compile
```

#### 4. L?y file installer
```
Installer\ActionWheel-Setup-1.0.0.exe  (~267 MB)
```

### ?? End-User Experience
```
1. Download "ActionWheel-Setup-1.0.0.exe"
2. Double-click ?? c?i ??t
3. Ch?n t?y ch?n:
   [Å„] Desktop shortcut
   [Å„] Ch?y khi kh?i ??ng
4. Next Å® Next Å® Install
5. Xong!
```

### ?? T?y Ch?nh Installer

S?a file `ActionWheel-Setup.iss`:
```ini
; ??i version
#define MyAppVersion "1.0.1"

; ??i company
#define MyAppPublisher "C?ng Ty C?a B?n"

; Th?m file
[Files]
Source: "docs\*"; DestDir: "{app}\docs"

; Th?m icon
SetupIconFile=myicon.ico
```

---

## ?? PH??NG ?N 4: MSIX PACKAGE (CHO MICROSOFT STORE)

### ? ?u ?i?m
- ? C?i ??t **sandbox** (an to?n)
- ? **T? ??ng update**
- ? Deploy qua **Microsoft Store**
- ? **Clean uninstall**

### ?? Nh??c ?i?m
- Ph?c t?p h?n
- C?n certificate
- Kh?ng single-file

### ??? C?ch T?o (Visual Studio)

#### 1. C?u h?nh MSIX
```xml
<!-- Trong .csproj, thay ??i: -->
<WindowsPackageType>MSIX</WindowsPackageType>
<EnableMsixTooling>true</EnableMsixTooling>
```

#### 2. T?o MSIX Package
```
1. Right-click project
2. Package and Publish Å® Create App Packages
3. Ch?n: Sideloading (ho?c Microsoft Store)
4. Ch?n architectures: x64, x86, ARM64
5. Create certificate (n?u ch?a c?)
6. Create
```

#### 3. K?t Qu?
```
AppPackages\
Ñ•ÑüÑü ActionWheel_1.0.0.0_x64.msix
Ñ•ÑüÑü ActionWheel_1.0.0.0_x86.msix
Ñ§ÑüÑü ActionWheel_1.0.0.0_ARM64.msix
```

### ?? C?i ??t MSIX

**Developer mode:**
```powershell
Add-AppxPackage -Path "ActionWheel_1.0.0.0_x64.msix"
```

**End-user:**
- Double-click `.msix` file
- Windows t? ??ng c?i ??t

### ?? Certificate Issue

N?u g?p l?i certificate:
```powershell
# 1. T?o self-signed cert
New-SelfSignedCertificate -Type Custom -Subject "CN=YourCompany" -KeyUsage DigitalSignature -FriendlyName "ActionWheel Cert" -CertStoreLocation "Cert:\CurrentUser\My"

# 2. Export certificate
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Select-Object -First 1
Export-Certificate -Cert $cert -FilePath ActionWheel.cer

# 3. C?i tr?n m?y ??ch
Import-Certificate -FilePath ActionWheel.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

---

## ?? PH??NG ?N 5: FRAMEWORK-DEPENDENT (KH?NG KHUY?N NGH?)

### ?? L? Do Kh?ng Khuy?n Ngh?
- End-user ph?i c?i .NET 8 Runtime
- Ph?c t?p cho ng??i kh?ng tech-savvy
- D? g?p l?i "missing runtime"

### ??? N?u V?n Mu?n D?ng

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o "bin\Publish\win-x64-framework"
```

### ?? End-User Y?u C?u
1. C?i .NET 8 Desktop Runtime: https://dotnet.microsoft.com/download/dotnet/8.0
2. Sau ?? m?i ch?y ???c app

### ? ?u ?i?m Duy Nh?t
- File nh? (~10 MB)

---

## ?? B?NG QUY?T ??NH

### Ch?n Ph??ng ?n N?o?

| T?nh Hu?ng | Ph??ng ?n ?? Xu?t |
|------------|-------------------|
| **Ph?n ph?i cho b?n b?/??ng nghi?p** | Self-Contained EXE + ZIP |
| **Chia s? online (Google Drive, etc.)** | Portable ZIP |
| **Ph?n ph?i chuy?n nghi?p** | Installer (Inno Setup) |
| **L?n Microsoft Store** | MSIX Package |
| **Developer testing** | Self-Contained EXE |
| **Y?u c?u file nh? nh?t** | Framework-Dependent (kh?ng khuy?n ngh?) |

---

## ?? WORKFLOW ?? XU?T

### Cho Developer (Testing)
```powershell
# Quick build & run
.\Build-Quick.bat
.\bin\Publish\win-x64\"Action Wheel.exe"
```

### Cho End-User (Ph?n Ph?i ??n Gi?n)
```powershell
# 1. Build
.\Build-Quick.bat

# 2. T?o ZIP
Compress-Archive -Path "bin\Publish\win-x64\Action Wheel.exe" -DestinationPath "ActionWheel-v1.0.0.zip"

# 3. Upload & chia s? link
```

### Cho Kh?ch H?ng (Chuy?n Nghi?p)
```powershell
# 1. Build app
.\Build-Quick.bat

# 2. T?o installer
# - M? Inno Setup
# - Compile "ActionWheel-Setup.iss"

# 3. Upload installer
# Å® Installer\ActionWheel-Setup-1.0.0.exe
```

---

## ?? FILES QUAN TR?NG

| File | M?c ??ch |
|------|----------|
| `Build-Quick.bat` | Build script ??n gi?n (Windows) |
| `Build-Publish.ps1` | Build script n?ng cao (PowerShell) |
| `ActionWheel-Setup.iss` | Installer script (Inno Setup) |
| `QUICK-START.md` | H??ng d?n nhanh |
| `DEPLOYMENT.md` | H??ng d?n deploy chi ti?t |
| `README.md` | T?i li?u ng??i d?ng |

---

## ?? TIPS QUAN TR?NG

### 1. Gi?m K?ch Th??c File

```bash
# Enable trimming (gi?m ~30-40%)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

?? **L?u ?:** Test k? sau khi trim!

### 2. T?i ?u Performance

```bash
# Enable ReadyToRun compilation
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true
```

### 3. Multi-Platform Build

```powershell
# Build cho t?t c? platforms
foreach ($platform in @("x64", "x86", "arm64")) {
    dotnet publish -c Release -r "win-$platform" --self-contained true -p:PublishSingleFile=true -o "bin\Publish\win-$platform"
}
```

---

## ? CHECKLIST TR??C KHI DEPLOY

### Code Quality
- [ ] Build ? mode **Release** (kh?ng ph?i Debug)
- [ ] Test tr?n m?y s?ch (kh?ng c? VS)
- [ ] T?t c? features ho?t ??ng
- [ ] Kh?ng c? crash/exception
- [ ] Memory leak test

### Metadata
- [ ] Version number ch?nh x?c
- [ ] Company name
- [ ] Copyright info
- [ ] License file (LICENSE.txt)

### Documentation
- [ ] README.md ??y ??
- [ ] H??ng d?n s? d?ng
- [ ] System requirements
- [ ] Known issues (n?u c?)

### Security
- [ ] Antivirus scan
- [ ] VirusTotal check
- [ ] Code signing (n?u c? certificate)

### Testing
- [ ] Test tr?n Windows 10
- [ ] Test tr?n Windows 11
- [ ] Test v?i different DPI settings
- [ ] Test tr?n multiple monitors

---

## ?? K?T LU?N

### ?? Ph??ng ?n T?t Nh?t: Self-Contained EXE

**L? do:**
- ? ??n gi?n nh?t cho end-user
- ? Kh?ng c?n c?i ??t g?
- ? Ch?y ngay tr?n m?i Windows 10/11
- ? M?t l?nh build xong

**Quick Command:**
```batch
.\Build-Quick.bat
```

**K?t qu?:**
```
bin\Publish\win-x64\Action Wheel.exe (267 MB)
Å® Ch?y ngay, kh?ng c?n c?i ??t g?!
```

---

**Ch?c b?n deploy th?nh c?ng! ??**

N?u g?p v?n ??, check:
- **QUICK-START.md** - H??ng d?n nhanh
- **DEPLOYMENT.md** - H??ng d?n chi ti?t
- **README.md** - T?i li?u ??y ??
