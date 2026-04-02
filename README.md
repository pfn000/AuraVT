# AuraVT 👻

**Next-generation open-source VTuber desktop overlay application.**

A dramatically faster, lighter, and more compatible alternative to VSeeFace — built with Godot 4 for native cross-platform performance.

---

## ✨ Features

| Feature | Windows | Linux | macOS |
|---------|---------|-------|-------|
| Transparent desktop overlay | ✅ | ✅ | ✅ |
| Drag & drop VRM/GLB loading | ✅ | ✅ | ✅ |
| Click-through mode | ✅ | ✅ | ✅ |
| Always-on-top | ✅ | ✅ | ✅ |
| Runs on 4GB RAM + i3 | ✅ | ✅ | ✅ |

**Graphics Support:**
- Windows: DirectX 12 / Vulkan
- Linux: Vulkan / OpenGL
- macOS: Metal (via MoltenVK)

---

## 📦 Installation (End Users)

### Windows
1. Download `AuraVT-Windows.zip` from [Releases](https://github.com/pfn000/AuraVT/releases)
2. Extract anywhere
3. Run `AuraVT.exe`

### Linux
1. Download `AuraVT-Linux.tar.gz` from Releases
2. Extract: `tar -xzf AuraVT-Linux.tar.gz`
3. Run: `./AuraVT.x86_64`

### macOS
1. Download `AuraVT-macOS.dmg` from Releases
2. Open DMG and drag AuraVT to Applications
3. Run from Applications

---

## 🎮 Usage

| Action | Control |
|--------|---------|
| **Load avatar** | Drag & drop VRM/GLB file onto window |
| **Move avatar** | Middle-click + drag |
| **Rotate avatar** | Right-click + drag |
| **Scale avatar** | Mouse scroll wheel |
| **Toggle settings** | `ESC` |
| **Toggle click-through** | `Ctrl + T` |
| **Reset position** | `Ctrl + R` |

---

## 🏗️ Building from Source

### Requirements
- **Godot 4.3+** (with export templates)
- **Git**

### Setup

```bash
# Clone repository
git clone https://github.com/pfn000/AuraVT.git
cd AuraVT

# Download VRM addon (required)
git clone https://github.com/V-Sekai/godot-vrm.git temp_vrm
cp -r temp_vrm/addons/vrm addons/
cp -r temp_vrm/addons/Godot-MToon-Shader addons/
rm -rf temp_vrm

# Open in Godot
# Project > Import > Select this folder
```

### Export

**From Godot Editor:**
1. Project → Export
2. Select platform (Windows/Linux/macOS)
3. Click "Export Project"

**From Command Line:**
```bash
# Windows
godot --headless --export-release "Windows" export/windows/AuraVT.exe

# Linux
godot --headless --export-release "Linux" export/linux/AuraVT.x86_64

# macOS
godot --headless --export-release "macOS" export/macos/AuraVT.dmg
```

---

## 📁 Project Structure

```
AuraVT/
├── project.godot          ← Godot project file
├── export_presets.cfg     ← Export configurations
├── addons/
│   ├── vrm/               ← V-Sekai VRM addon
│   └── Godot-MToon-Shader/← MToon shader
├── scenes/
│   └── main.tscn          ← Main scene
├── scripts/
│   ├── core/              ← Core systems (globals, settings)
│   ├── avatar/            ← Avatar loading & control
│   ├── window/            ← Transparent window handling
│   └── ui/                ← UI controllers
├── assets/
│   ├── icons/             ← App icons
│   └── fonts/             ← UI fonts
└── export/                ← Build output (gitignored)
```

---

## ⚡ Performance

Godot 4's GL Compatibility renderer ensures excellent performance on low-end hardware:

- **Target:** 60 FPS on Intel i3 + 4GB RAM
- **Vulkan** renderer available for modern GPUs
- **OpenGL 3.3** fallback for older hardware
- **Minimal memory footprint** (~100-200MB)

---

## 🔧 Creating Installers

### Windows (.msi / .msix)
Use your exported `export/windows/` folder with:
- **Advanced Installer** (what you have)
- **WiX Toolset**
- **Inno Setup**

### Linux (.deb / .AppImage)
```bash
# AppImage (recommended)
# Use linuxdeploy or appimagetool

# .deb package
# Use dpkg-deb with proper control file
```

### macOS (.pkg)
```bash
# Use productbuild or Packages.app
productbuild --component AuraVT.app /Applications AuraVT.pkg
```

---

## 📋 Roadmap

| Phase | Feature | Status |
|-------|---------|--------|
| 1 | Transparent window overlay | ✅ |
| 2 | VRM/GLB drag-drop loading | ✅ |
| 3 | Settings UI + persistence | ✅ |
| 4 | Cross-platform export | ✅ |
| 5 | VRChat avatar import | 🔜 |
| 6 | Face tracking integration | 🔜 |

---

## 📄 License

MIT License — Built by NCOM Systems ([@Saidie000](https://github.com/Saidie000))

---

## 🙏 Credits

- **V-Sekai** — godot-vrm addon
- **Godot Engine** — Game engine
- **VRM Consortium** — VRM specification
