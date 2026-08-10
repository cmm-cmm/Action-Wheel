# ?? H??ng D?n Tri?n Khai Action Wheel

## ?? M?c L?c
1. [Self-Contained Deployment](#1-self-contained-deployment-khuy?n-ngh?)
2. [Framework-Dependent Deployment](#2-framework-dependent-deployment)
3. [MSIX Package](#3-msix-package)
4. [T?o Installer](#4-t?o-installer-v?i-inno-setup)
5. [Portable Version](#5-portable-version)

---

## 1. Self-Contained Deployment (Khuy?n Ngh?)

### ? ?u ?i?m
- ? Kh?ng c?n c?i ??t .NET Runtime
- ? File EXE duy nh?t, d? ph?n ph?i
- ? Ch?y ngay tr?n b?t k? Windows 10+ n?o
- ? Kh?ng xung ??t v?i .NET versions kh?c

### ?? Nh??c ?i?m
- K?ch th??c l?n h?n (~150-200 MB)

### ??? C?ch Build

#### **Ph??ng ph?p 1: D?ng Build Script (??n gi?n nh?t)**

```powershell
# Ch?y PowerShell script
.\Build-Publish.ps1

# Ho?c ch?y Batch file
.\Build-Quick.bat
```

#### **Ph??ng ph?p 2: D?ng Visual Studio**

1. **M? Solution** trong Visual Studio 2022
2. **Click chu?t ph?i** v?o project "Action Wheel"
3. **Ch?n "Publish..."**
4. **Ch?n profile**: `win-x64` (ho?c x86, ARM64)
5. **Click "Publish"**
6. File output ?: `bin\Publish\win-x64\`

#### **Ph??ng ph?p 3: D?ng Command Line**

```bash
# Build cho Windows 64-bit
dotnet publish "Action Wheel.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:PublishReadyToRun=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o "bin\Publish\win-x64"

# Build cho Windows 32-bit
dotnet publish "Action Wheel.csproj" -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -o "bin\Publish\win-x86"

# Build cho Windows ARM64
dotnet publish "Action Wheel.csproj" -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=true -o "bin\Publish\win-arm64"
```

### ?? K?t qu?

Sau khi build, b?n s? c?:
```
bin\Publish\win-x64\
„¥„Ÿ„Ÿ Action Wheel.exe          © File ch?nh (~150-200 MB)
„¤„Ÿ„Ÿ Action Wheel.pdb          © Debug symbols (c? th? x?a)
```

### ?? C?ch S? D?ng

**?? ch?y:**
```bash
cd bin\Publish\win-x64
"Action Wheel.exe"
```

**?? ph?n ph?i:**
- N?n `Action Wheel.exe` th?nh ZIP
- Ho?c t?o installer (xem ph?n 4)

---

## 2. Framework-Dependent Deployment

### ? ?u ?i?m
- ? K?ch th??c nh? (~10-20 MB)
- ? Build nhanh

### ?? Nh??c ?i?m
- Y?u c?u c?i .NET 8 Runtime tr?n m?y ??ch

### ??? C?ch Build

```bash
dotnet publish "Action Wheel.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained false ^
    -o "bin\Publish\win-x64-framework"
```

### ?? Y?u c?u tr?n m?y ??ch

Ng??i d?ng c?n c?i ??t:
- **.NET 8 Desktop Runtime**: https://dotnet.microsoft.com/download/dotnet/8.0
- Ho?c **.NET 8 SDK** (n?u l? developer)

---

## 3. MSIX Package

### ? ?u ?i?m
- ? C?i ??t/g? c?i ??t s?ch s?
- ? T? ??ng update
- ? C? th? deploy qua Microsoft Store
- ? Sandbox security

### ??? C?ch T?o MSIX

#### **Trong Visual Studio:**

1. **Click chu?t ph?i** v?o project
2. **Ch?n "Package and Publish" ¨ "Create App Packages"**
3. **Ch?n**:
   - "Sideloading" (kh?ng l?n Store)
   - Ho?c "Microsoft Store" (n?u mu?n l?n Store)
4. **C?u h?nh**:
   - Version: 1.0.0.0
   - Architectures: x64, x86, ARM64
   - Certificate: T?o ho?c ch?n certificate
5. **Click "Create"**

#### **Output:**
```
AppPackages\
„¥„Ÿ„Ÿ Action Wheel_1.0.0.0_x64.msix
„¥„Ÿ„Ÿ Action Wheel_1.0.0.0_x86.msix
„¥„Ÿ„Ÿ Action Wheel_1.0.0.0_ARM64.msix
„¤„Ÿ„Ÿ Dependencies\
    „¤„Ÿ„Ÿ (runtime dependencies)
```

### ?? C?ch C?i ??t MSIX

**Tr?n m?y dev:**
```powershell
Add-AppxPackage -Path "Action Wheel_1.0.0.0_x64.msix"
```

**Tr?n m?y end-user:**
- Double-click file `.msix`
- Windows s? h?i x?c nh?n c?i ??t

### ?? Certificate

N?u g?p l?i certificate, t?o self-signed cert:

```powershell
# T?o certificate
New-SelfSignedCertificate -Type Custom -Subject "CN=YourCompany" -KeyUsage DigitalSignature -FriendlyName "ActionWheel Cert" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# Export certificate
Export-Certificate -Cert (Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert)[0] -FilePath ActionWheel.cer

# C?i ??t tr?n m?y ??ch
Import-Certificate -FilePath ActionWheel.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

---

## 4. T?o Installer v?i Inno Setup

### ?? C?i ??t Inno Setup

Download t?: https://jrsoftware.org/isdl.php

### ??? C?c B??c

1. **Build app** tr??c (d?ng method 1 ho?c command line)
2. **M? Inno Setup Compiler**
3. **File ¨ Open**: Ch?n `ActionWheel-Setup.iss`
4. **Build ¨ Compile**
5. File installer ?: `Installer\ActionWheel-Setup-1.0.0.exe`

### ? T?nh N?ng Installer

- C?i ??t v?o Program Files
- T?o Start Menu shortcuts
- T?y ch?n Desktop icon
- T?y ch?n ch?y khi kh?i ??ng Windows
- H? tr? g? c?i ??t s?ch s?
- Ki?m tra Windows version

### ?? T?y Ch?nh Installer

S?a file `ActionWheel-Setup.iss`:

```ini
; Thay ??i th?ng tin
#define MyAppName "Action Wheel"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Your Company"

; Thay ??i icon
SetupIconFile=myicon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Th?m file b? sung
[Files]
Source: "docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs
```

---

## 5. Portable Version

T?o phi?n b?n portable kh?ng c?n c?i ??t.

### ??? C?c B??c

1. **Build self-contained** (xem ph?n 1)
2. **Copy to?n b? th? m?c** `bin\Publish\win-x64`
3. **N?n th?nh ZIP**:
   ```
   ActionWheel-Portable-v1.0.0.zip
   „¥„Ÿ„Ÿ Action Wheel.exe
   „¤„Ÿ„Ÿ README.txt
   ```

### ?? T?o README.txt cho portable

```text
==============================================
  Action Wheel - Portable Version
==============================================

C?ch s? d?ng:
1. Gi?i n?n th? m?c n?y v?o b?t k? ??u
2. Ch?y "Action Wheel.exe"
3. Nh?n n?t gi?a chu?t ?? m? menu

Y?u c?u:
- Windows 10 version 1809 tr? l?n

L?u ?:
- Kh?ng c?n c?i ??t
- Kh?ng c?n .NET Runtime
- C? th? ch?y t? USB

H? tr?:
- Email: support@yourcompany.com
- Web: https://yourwebsite.com
```

---

## 6. Checklist Tr??c Khi Deploy

### ? Ki?m Tra Code

- [ ] Build th?nh c?ng ? mode **Release**
- [ ] Test tr?n m?y s?ch (kh?ng c? Visual Studio)
- [ ] Ki?m tra t?t c? ch?c n?ng
- [ ] Kh?ng c? exception/crash
- [ ] Memory leak test (ch?y l?u d?i)

### ? Ki?m Tra Assets

- [ ] Icon ??p v? r? r?ng
- [ ] Logo hi?n th? ??ng
- [ ] Manifest ??ng (app.manifest)

### ? Ki?m Tra Metadata

- [ ] Version number ch?nh x?c
- [ ] Company name
- [ ] Copyright info
- [ ] Product description

### ? Ki?m Tra Security

- [ ] Code signing (n?u c? cert)
- [ ] Antivirus scan
- [ ] VirusTotal check

### ? T?i Li?u

- [ ] README.md ??y ??
- [ ] LICENSE.txt
- [ ] CHANGELOG.md
- [ ] User guide

---

## 7. C?c L?nh H?u ?ch

### Xem th?ng tin .NET

```bash
# Ki?m tra .NET version
dotnet --info

# List runtimes
dotnet --list-runtimes

# List SDKs
dotnet --list-sdks
```

### Clean Build

```bash
# X?a build artifacts
dotnet clean

# X?a obj v? bin
Remove-Item -Path "obj","bin" -Recurse -Force
```

### T?i ?u h?a

```bash
# Build v?i IL trimming (gi?m size)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishTrimmed=true -p:PublishSingleFile=true

# Build v?i ReadyToRun (t?ng performance)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true
```

---

## 8. Troubleshooting

### ? L?i: "This app can't run on your PC"

**Nguy?n nh?n:** Sai architecture (x64 vs x86)

**Gi?i ph?p:** Build ??ng architecture cho m?y ??ch

### ? L?i: "The application requires .NET Runtime"

**Nguy?n nh?n:** Build framework-dependent nh?ng m?y kh?ng c? runtime

**Gi?i ph?p:** Build v?i `--self-contained true`

### ? L?i: "Application failed to start"

**Nguy?n nh?n:** Thi?u dependencies ho?c Windows version c?

**Gi?i ph?p:** 
- Check Windows version >= 1809
- Build v?i `IncludeNativeLibrariesForSelfExtract=true`

### ? File EXE qu? l?n

**Gi?i ph?p:**
```bash
# Enable trimming
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishTrimmed=true -p:TrimMode=partial
```

---

## 9. Ph?n Ph?i

### ?? C?c K?nh Ph?n Ph?i

1. **Direct Download** - Host file tr?n website/Google Drive/Dropbox
2. **GitHub Releases** - T?o release tr?n GitHub repo
3. **Microsoft Store** - Submit MSIX package
4. **Winget** - ??ng k? tr?n Windows Package Manager
5. **Chocolatey** - T?o Chocolatey package

### ?? GitHub Release Example

```bash
# Tag version
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0

# Upload files:
- ActionWheel-Setup-1.0.0.exe (Installer)
- ActionWheel-Portable-v1.0.0.zip (Portable)
- ActionWheel_1.0.0.0_x64.msix (MSIX)
```

---

## 10. H? Tr? & Li?n H?

N?u g?p v?n ?? khi deploy, check:
- README.md - H??ng d?n s? d?ng
- GitHub Issues - Report bugs
- Email: support@yourcompany.com

---

**Ch?c b?n deploy th?nh c?ng! ??**
