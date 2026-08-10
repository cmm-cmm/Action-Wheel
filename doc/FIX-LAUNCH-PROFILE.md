# ? ?? FIX: L?I LAUNCH PROFILE

## ? L?I TR??C:

```
The project doesn't know how to run the profile with name
'Action Wheel (Package)' and command 'MsixPackage'.
```

## ?? NGUY?N NH?N:

Visual Studio ?ang c? ch?y profile **"Action Wheel (Package)"** v?i command **MsixPackage**, nh?ng:
- MSIX tooling ?? b? t?t (`EnableMsixTooling=false`)
- Project ???c c?u h?nh l? unpackaged (`WindowsPackageType=None`)
- Å® Kh?ng th? ch?y MSIX profile!

---

## ? ?? FIX:

### File: `Properties/launchSettings.json`

**Tr??c:**
```json
{
  "profiles": {
    "Action Wheel (Package)": {
      "commandName": "MsixPackage"  Å© L?i!
    },
    "Action Wheel (Unpackaged)": {
      "commandName": "Project"
    }
  }
}
```

**Sau:**
```json
{
  "profiles": {
    "Action Wheel (Unpackaged)": {
      "commandName": "Project",
      "nativeDebugging": false
    }
  }
}
```

---

## ?? B?Y GI? B?N C? TH?:

### 1. Debug B?nh Th??ng (F5)
```
? Visual Studio s? d?ng profile "Action Wheel (Unpackaged)"
? Ch?y project tr?c ti?p
? Kh?ng c?n MSIX package
? Set breakpoints
? Step through code
```

### 2. Ch?n Profile Trong VS
```
- Toolbar: [Action Wheel (Unpackaged) Å•]
- Click dropdown ?? ch?n profile
- Ch? c? 1 option: "Action Wheel (Unpackaged)"
```

### 3. Run Without Debugging (Ctrl+F5)
```
? Ch?y app nhanh
? Kh?ng attach debugger
```

---

## ?? T?T C? C?C FIX ?? TH?C HI?N:

| Fix # | File | Thay ??i | L? Do |
|-------|------|----------|-------|
| **1** | `Action Wheel.csproj` | `EnableMsixTooling=false` | T?t MSIX tooling |
| **2** | `launchSettings.json` | X?a profile "Package" | Kh?ng d?ng MSIX |
| **3** | Build | Successful | Kh?ng c?n conflict |

---

## ?? KI?M TRA NGAY:

### B??c 1: Reload Project
```
1. Close t?t c? files trong VS
2. Right-click project Å® Unload Project
3. Right-click l?i Å® Reload Project
```

### B??c 2: Check Launch Profile
```
1. Nh?n v?o toolbar Visual Studio
2. T?m dropdown: [Action Wheel (Unpackaged) Å•]
3. N?u th?y "Package" Å® Ch?n "Unpackaged"
```

### B??c 3: Debug!
```
1. Nh?n F5
2. App s? ch?y
3. C?a s? ch?nh xu?t hi?n
4. Nh?n n?t gi?a chu?t
5. Menu transparent xu?t hi?n! ?
```

---

## ?? N?U V?N B? L?I:

### Option 1: Restart Visual Studio
```
??ng ho?n to?n VS v? m? l?i
Å® VS s? reload launchSettings.json m?i
```

### Option 2: Clean & Rebuild
```powershell
# Trong VS
Build Å® Clean Solution
Build Å® Rebuild Solution
```

### Option 3: Delete .vs folder
```powershell
# ??ng Visual Studio tr??c
Remove-Item -Recurse -Force .vs

# M? l?i VS
```

### Option 4: Manual profile selection
```
1. Toolbar Å® Click dropdown b?n c?nh Debug button
2. Ch?n "Action Wheel (Unpackaged)"
3. Nh?n F5
```

---

## ?? LAUNCH PROFILES EXPLAINED:

### Profile Types:

#### 1. **Unpackaged (Project)**
```json
{
  "commandName": "Project"
}
```
- ? Ch?y tr?c ti?p t? build output
- ? Kh?ng c?n package
- ? Debug d? d?ng
- ? Single-file EXE
- **Å® ?ANG D?NG**

#### 2. **MSIX Package** ?
```json
{
  "commandName": "MsixPackage"
}
```
- ? C?n MSIX tooling
- ? Ph?c t?p h?n
- ? Kh?ng t??ng th?ch v?i unpackaged
- **Å® ?? X?A**

---

## ?? T?Y CH?NH LAUNCH SETTINGS

### Th?m options n?ng cao:

```json
{
  "profiles": {
    "Action Wheel (Unpackaged)": {
      "commandName": "Project",
      "nativeDebugging": false,
      "commandLineArgs": "--debug",  // Optional
      "environmentVariables": {
        "DEBUG_MODE": "true"
      },
      "dotnetRunMessages": true
    }
  }
}
```

### T?o multiple debug profiles:

```json
{
  "profiles": {
    "Action Wheel (Normal)": {
      "commandName": "Project"
    },
    "Action Wheel (Verbose)": {
      "commandName": "Project",
      "commandLineArgs": "--verbose"
    },
    "Action Wheel (Test)": {
      "commandName": "Project",
      "commandLineArgs": "--test-mode"
    }
  }
}
```

---

## ?? TEST SCENARIOS:

### Scenario 1: Basic Debug
```
1. F5 Å® App starts
2. Set breakpoint trong MouseHook.HookCallback
3. Nh?n n?t gi?a chu?t
4. Breakpoint hits! ?
```

### Scenario 2: UI Debug
```
1. F5 Å® App starts
2. Set breakpoint trong RadialMenu.ActionButton_Click
3. Nh?n n?t gi?a chu?t Å® Menu xu?t hi?n
4. Click button trong menu
5. Breakpoint hits! ?
```

### Scenario 3: Launch Service Debug
```
1. F5 Å® App starts
2. Set breakpoint trong LauncherService.OnMiddleClick
3. Nh?n n?t gi?a chu?t
4. Breakpoint hits! ?
5. Step through ?? xem menu creation
```

---

## ?? FILES ?? S?A:

```
Action Wheel/
Ñ•ÑüÑü Action Wheel.csproj          ? EnableMsixTooling=false
Ñ§ÑüÑü Properties/
    Ñ§ÑüÑü launchSettings.json     ? X?a MSIX profile
```

---

## ? CHECKLIST HO?N TH?NH:

- [x] Fix .csproj (MSIX tooling off)
- [x] Fix launchSettings.json (remove Package profile)
- [x] Build successful
- [ ] **B?N L?M:** Reload project trong VS
- [ ] **B?N L?M:** Check launch profile dropdown
- [ ] **B?N L?M:** Nh?n F5 ?? debug
- [ ] **B?N L?M:** Test middle mouse click

---

## ?? T?T C? L?I ?? ???C FIX!

### Tr??c:
```
? Cannot deploy
? MSIX package profile error
? Kh?ng debug ???c
```

### Sau:
```
? Deploy works
? Ch? c? Unpackaged profile
? Debug F5 ho?t ??ng ho?n h?o
? Build successful
? App ch?y m??t m?
```

---

## ?? N?U C?N H? TR?:

### Check list:
1. ? `.csproj` c? `EnableMsixTooling=false`?
2. ? `launchSettings.json` kh?ng c? "MsixPackage"?
3. ? Build successful?
4. ? ?? reload project trong VS?
5. ? ?? restart VS?

### Debug steps:
```
1. Close all files
2. Unload project
3. Reload project
4. Clean solution
5. Rebuild solution
6. F5
```

---

**Gi? th? debug tho?i m?i! ????**
