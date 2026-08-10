# ?? FIX L?I DEPLOYMENT & DEBUG

## ? L?I HI?N T?I

```
"The project needs to be deployed before we can debug.
Please enable Deploy in the Configuration Manager"
```

## ?? NGUY?N NH?N

Conflict gi?a:
- `EnableMsixTooling=true` (B?t MSIX packaging)
- `WindowsPackageType=None` (Unpackaged app)

Visual Studio kh?ng bi?t ph?i deploy nh? th? n?o!

---

## ? GI?I PH?P 1: T?T MSIX TOOLING (KHUY?N NGH?)

### C?ch 1: S?a .csproj

**Thay ??i trong `Action Wheel.csproj`:**

```xml
<!-- TR??C (C? conflict) -->
<EnableMsixTooling>true</EnableMsixTooling>
<WindowsPackageType>None</WindowsPackageType>

<!-- SAU (Fix conflict) -->
<EnableMsixTooling>false</EnableMsixTooling>
<WindowsPackageType>None</WindowsPackageType>
```

### C?ch 2: D?ng Configuration Manager

1. **Visual Studio** Å® **Build** Å® **Configuration Manager**
2. T?m d?ng **Action Wheel** project
3. Check v?o ? **Deploy** ?
4. Click **Close**
5. Th? debug l?i (F5)

---

## ? GI?I PH?P 2: CHUY?N SANG MSIX (CHO STORE)

N?u b?n mu?n deploy qua Microsoft Store ho?c MSIX:

```xml
<!-- Gi? nguy?n EnableMsixTooling -->
<EnableMsixTooling>true</EnableMsixTooling>

<!-- ??i WindowsPackageType -->
<WindowsPackageType>MSIX</WindowsPackageType>

<!-- X?a PublishSingleFile (kh?ng t??ng th?ch MSIX) -->
<!-- <PublishSingleFile>true</PublishSingleFile> -->
```

?? **L?u ?:** MSIX kh?ng h? tr? single-file publish!

---

## ?? QUICK FIX - TH?C HI?N NGAY

### Step-by-step:

#### 1. M? file .csproj
```
Action Wheel\Action Wheel.csproj
```

#### 2. T?m d?ng n?y:
```xml
<EnableMsixTooling>true</EnableMsixTooling>
```

#### 3. ??i th?nh:
```xml
<EnableMsixTooling>false</EnableMsixTooling>
```

#### 4. Save file

#### 5. Reload project trong Visual Studio
- Right-click project Å® **Reload Project**

#### 6. Clean solution
- Build Å® **Clean Solution**

#### 7. Rebuild
- Build Å® **Rebuild Solution**

#### 8. Debug
- Nh?n **F5** ho?c **Debug Å® Start Debugging**

---

## ?? FILE .CSPROJ ?? FIX (FULL)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RootNamespace>Action_Wheel</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Platforms>x86;x64;ARM64</Platforms>
    <RuntimeIdentifiers>win-x86;win-x64;win-arm64</RuntimeIdentifiers>
    <UseWinUI>true</UseWinUI>
    <WinUISDKReferences>false</WinUISDKReferences>
    <Nullable>enable</Nullable>
    
    <!-- FIX: T?t MSIX tooling cho unpackaged app -->
    <EnableMsixTooling>false</EnableMsixTooling>
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    
    <!-- Version Info -->
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <FileVersion>1.0.0.0</FileVersion>
    <Version>1.0.0</Version>
    <Company>Your Company</Company>
    <Product>Action Wheel</Product>
    <Copyright>Copyright ? 2025</Copyright>
    <Description>Global Radial Menu Launcher for Windows</Description>
  </PropertyGroup>

  <ItemGroup>
    <Content Include="Assets\SplashScreen.scale-200.png" />
    <Content Include="Assets\LockScreenLogo.scale-200.png" />
    <Content Include="Assets\Square150x150Logo.scale-200.png" />
    <Content Include="Assets\Square44x44Logo.scale-200.png" />
    <Content Include="Assets\Square44x44Logo.targetsize-24_altform-unplated.png" />
    <Content Include="Assets\StoreLogo.png" />
    <Content Include="Assets\Wide310x150Logo.scale-200.png" />
  </ItemGroup>

  <ItemGroup>
    <Manifest Include="$(ApplicationManifest)" />
  </ItemGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.26100.7175" />
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.8.251106002" />
  </ItemGroup>

  <!-- Publish Properties -->
  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <PublishReadyToRun>True</PublishReadyToRun>
    <PublishTrimmed>False</PublishTrimmed>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  </PropertyGroup>
  
  <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
    <PublishReadyToRun>False</PublishReadyToRun>
    <PublishTrimmed>False</PublishTrimmed>
  </PropertyGroup>
</Project>
```

---

## ?? SAU KHI FIX

### B?n c? th?:
? Debug b?nh th??ng (F5)
? Run without debugging (Ctrl+F5)
? Set breakpoints
? Step through code
? Publish single-file EXE

### B?n KH?NG th?:
? Package as MSIX
? Deploy to Microsoft Store (c?n MSIX)
? Use MSIX-specific features

N?u mu?n c? hai, c?n t?o 2 build configurations ri?ng bi?t!

---

## ?? N?U V?N B? L?I

### Option 1: Clean to?n b?
```powershell
# X?a bin v? obj
Remove-Item -Recurse -Force bin,obj

# Restore packages
dotnet restore

# Build l?i
dotnet build
```

### Option 2: T?o solution configuration m?i

1. **Build** Å® **Configuration Manager**
2. Active solution configuration Å® **<New...>**
3. Name: `DebugUnpackaged`
4. Copy settings from: `Debug`
5. Trong project, check **Deploy** ?
6. OK

### Option 3: Launch Settings

T?o file `launchSettings.json`:
```json
{
  "profiles": {
    "Action Wheel": {
      "commandName": "Project",
      "nativeDebugging": false
    }
  }
}
```

---

## ?? SO S?NH C?C OPTION

| Feature | Unpackaged (None) | MSIX Package |
|---------|-------------------|--------------|
| **Debug** | ? D? | ?? Ph?c t?p h?n |
| **Deploy** | ? Copy EXE | ?? C?n install |
| **Single File** | ? Yes | ? No |
| **File Size** | ~267 MB | ~200 MB |
| **Store** | ? No | ? Yes |
| **Auto Update** | ? Manual | ? Yes |
| **Sandbox** | ? No | ? Yes |

---

## ?? KHUY?N NGH?

### Cho Development:
```xml
<EnableMsixTooling>false</EnableMsixTooling>
<WindowsPackageType>None</WindowsPackageType>
```
Å® **Debug d? d?ng, build nhanh**

### Cho Production Release:
C? 2 c?ch:
1. **Unpackaged EXE** - Ph?n ph?i tr?c ti?p
2. **MSIX** - L?n Store ho?c enterprise deploy

**Kh?ng n?n mix c? hai trong c?ng 1 config!**

---

## ? CHECKLIST

- [ ] S?a `.csproj`: `EnableMsixTooling=false`
- [ ] Save file
- [ ] Reload project trong VS
- [ ] Clean solution
- [ ] Rebuild solution
- [ ] F5 ?? debug
- [ ] N?u v?n l?i Å® Restart Visual Studio

---

## ?? TROUBLESHOOTING

### L?i: "Cannot find the deployment target"
**Fix:** Restart Visual Studio

### L?i: "The project is out of date"
**Fix:** Clean + Rebuild

### L?i: "Unable to activate Windows Store app"
**Fix:** ??m b?o `WindowsPackageType=None`

### App crash ngay khi debug
**Fix:** Check app.manifest, ??m b?o c? Windows 10 compatibility

---

**H?y th? fix v? cho t?i bi?t k?t qu?! ??**
