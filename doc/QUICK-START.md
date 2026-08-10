# ?? H??NG D?N NHANH - TRI?N KHAI ACTION WHEEL

## ? ?? BUILD TH?NH C?NG!

File th?c thi ?? ???c t?o t?i:
```
Action Wheel\bin\Publish\win-x64\Action Wheel.exe  (267 MB)
```

---

## ?? C?CH 1: CH?Y TR?C TI?P (??n Gi?n Nh?t)

### B??c 1: M? th? m?c ch?a file EXE
```powershell
explorer ".\bin\Publish\win-x64"
```

### B??c 2: Double-click file
```
Action Wheel.exe
```

### B??c 3: S? d?ng
- Nh?n **n?t gi?a chu?t** (middle mouse button) b?t k? ??u
- Menu tr?n xu?t hi?n t?i v? tr? con tr?!

---

## ?? C?CH 2: PH?N PH?I CHO NG??I KH?C

### Option A: N?n th?nh ZIP

```powershell
# T?o ZIP file
Compress-Archive -Path ".\bin\Publish\win-x64\Action Wheel.exe" -DestinationPath "ActionWheel-Portable-v1.0.0.zip"
```

**G?i file ZIP** cho ng??i kh?c, h? ch? c?n:
1. Gi?i n?n
2. Ch?y `Action Wheel.exe`
3. Xong!

### Option B: T?o Installer (N?ng Cao)

#### Y?u c?u: C?i Inno Setup
Download: https://jrsoftware.org/isdl.php

#### C?c b??c:
1. C?i Inno Setup
2. M? file: `ActionWheel-Setup.iss`
3. Click **Build Å® Compile**
4. File installer xu?t hi?n t?i: `Installer\ActionWheel-Setup-1.0.0.exe`

**?u ?i?m installer:**
- ? C?i v?o Program Files
- ? T?o Start Menu shortcut
- ? C? g? c?i ??t (uninstall)
- ? Chuy?n nghi?p h?n

---

## ?? C?CH 3: BUILD L?I (N?u C?n)

### D?ng Script T? ??ng

#### PowerShell Script:
```powershell
.\Build-Publish.ps1
```

#### Batch File (??n gi?n h?n):
```batch
.\Build-Quick.bat
```

### D?ng Command Line

```bash
# Build cho Windows 64-bit
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o "bin\Publish\win-x64"

# Build cho Windows 32-bit
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -o "bin\Publish\win-x86"

# Build cho ARM64
dotnet publish -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=true -o "bin\Publish\win-arm64"
```

---

## ?? Y?U C?U H? TH?NG

### ?? Ph?t Tri?n (Development):
- ? Windows 10/11
- ? Visual Studio 2022
- ? .NET 8 SDK
- ? Windows App SDK workload

### ?? Ch?y App (End User):
- ? **CH? C?N** Windows 10 version 1809+ ho?c Windows 11
- ? **KH?NG C?N** c?i .NET Runtime
- ? **KH?NG C?N** c?i Visual Studio

---

## ?? KI?M TRA APP

### Test C? B?n:
1. M? app: `Action Wheel.exe`
2. C?a s? ch?nh hi?n ra (c? h??ng d?n)
3. Nh?n **n?t gi?a chu?t** ? b?t k? ??u
4. Menu tr?n xu?t hi?n ngay t?i cursor ?
5. Click n?t ho?c click ngo?i ?? ??ng

### Test N?ng Cao:
- [ ] Menu xu?t hi?n ??ng v? tr? cursor
- [ ] Menu kh?ng hi?n trong Alt+Tab
- [ ] Menu kh?ng hi?n tr?n Taskbar
- [ ] Animation m??t m?
- [ ] Click outside ??ng menu
- [ ] Click button ??ng menu
- [ ] Ch? 1 menu m? t?i 1 th?i ?i?m

---

## ?? C?U TR?C TH? M?C

```
Action Wheel/
Ñ•ÑüÑü bin/
Ñ†   Ñ§ÑüÑü Publish/
Ñ†       Ñ§ÑüÑü win-x64/
Ñ†           Ñ§ÑüÑü Action Wheel.exe  Å© FILE CH?NH
Ñ•ÑüÑü Build-Publish.ps1            Å© Build script (PowerShell)
Ñ•ÑüÑü Build-Quick.bat              Å© Build script (Batch)
Ñ•ÑüÑü ActionWheel-Setup.iss         Å© Installer script (Inno Setup)
Ñ•ÑüÑü LICENSE.txt                  Å© License
Ñ•ÑüÑü README.md                    Å© H??ng d?n ??y ??
Ñ§ÑüÑü DEPLOYMENT.md                Å© H??ng d?n deploy chi ti?t
```

---

## ?? QUICK START - 3 L?NH DUY NH?T

N?u b?n mu?n build v? ch?y ngay:

```powershell
# 1. Build app
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "bin\Publish\win-x64"

# 2. M? th? m?c
explorer "bin\Publish\win-x64"

# 3. Double-click "Action Wheel.exe"
```

---

## ?? TIPS & TRICKS

### Gi?m K?ch Th??c File

N?u 267 MB qu? l?n, c? th?:

```powershell
# Enable trimming (gi?m ~30-40%)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "bin\Publish\win-x64-trimmed"
```

?? **L?u ?:** Trimming c? th? g?y l?i runtime, test k?!

### Ch?y Khi Kh?i ??ng Windows

Copy file v?o th? m?c Startup:
```powershell
Copy-Item "bin\Publish\win-x64\Action Wheel.exe" "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\"
```

### T?o Desktop Shortcut

```powershell
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$Home\Desktop\Action Wheel.lnk")
$Shortcut.TargetPath = "$PWD\bin\Publish\win-x64\Action Wheel.exe"
$Shortcut.Save()
```

---

## ? TROUBLESHOOTING

### L?i: "App kh?ng ch?y ???c"

**Gi?i ph?p:**
1. Ki?m tra Windows version >= 1809
2. Check file c? b? block: Click chu?t ph?i Å® Properties Å® Unblock
3. T?t antivirus t?m th?i

### L?i: "Hook kh?ng ho?t ??ng"

**Nguy?n nh?n:** Antivirus ch?n low-level hook

**Gi?i ph?p:** Add exception cho `Action Wheel.exe`

### File EXE qu? l?n

**B?nh th??ng!** WinUI 3 self-contained app th??ng 200-300 MB

**L? do:**
- Bao g?m .NET Runtime
- WinUI 3 framework
- Windows App SDK
- Native libraries

---

## ?? TH?NG K?

| Item | Value |
|------|-------|
| File Size | ~267 MB |
| Build Time | ~30-40 gi?y |
| .NET Version | 8.0 |
| Target OS | Windows 10 1809+ |
| Architecture | x64 |
| Self-Contained | ? Yes |
| Single File | ? Yes |

---

## ?? HO?N T?T!

B?n ?? c?:
- ? File EXE ho?n ch?nh
- ? Kh?ng c?n c?i ??t g? th?m
- ? Ch?y ???c tr?n m?i Windows 10/11
- ? D? d?ng ph?n ph?i

**B?t ??u s? d?ng ngay!** ??

---

## ?? H? TR?

- **README.md** - H??ng d?n s? d?ng chi ti?t
- **DEPLOYMENT.md** - H??ng d?n deploy ??y ??
- **PROJECT_SUMMARY.md** - T?i li?u k? thu?t

---

**Ch?c b?n deploy th?nh c?ng!** ??
