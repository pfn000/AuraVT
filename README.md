# AuraVT 👻

**A next-generation open-source VTuber desktop overlay application.**  
Built to be dramatically faster, lighter, and more compatible than VSeeFace.

<div align="center">

<img src="https://github.com/pfn000/AuraVT/blob/main/.github/Images/pngimg.com%20-%20under_construction_PNG3.png" alt="Under Construction" width="800">

</div>

---
## ✨ Features

| Feature | Status |
|---|---|
| Runs on 4 GB RAM / Intel i3 | ✅ Phase 0 |
| Transparent desktop overlay (Windows) | ✅ Phase 1 |
| Drag & drop VRM / GLB avatar loading | ✅ Phase 2 |
| VRChat avatar importer (Poiyomi, lilToon) | ✅ Phase 3 |
| VRChat Avatar Importer (.unitypackage stripper + VRC component shim) |✅ Phase 4 |
| Face tracking (MediaPipe / OpenSeeFace) | ✅ Phase 5 |
| NATIVE FaceTracking - no not April fools 🃏 | ✅ Phase 5B |
| DirectX 11/12 native plugin (Windows) | 🔜 Phase 6 |
| Vulkan native plugin (Linux) | 🔜 Phase 6 |
| --- Possible future updates --- | 💪😏 |

---
## Dev Notes 🚧 
> [!NOTE] 
> these are developer notes to myself and for anyone with a naked eye looks at this

- this shouldn't have to be loaded into unity. 
- this should have unity crashhandler 
- We'll hope to use AdvancedInstaller for windows
- Windows Development is Active
- Linux Development is Active
- MacOS Development is on hold 🥺. | MacOS System is not available to develop on | 

## 🚀 Getting Started
> [!CAUTION]
> Ignore this set up. this method is stupid and won't work anyway. so just hang tight

### Requirements
- Unity 2022.3 LTS (URP)
- Windows 10/11 (for transparent overlay) or Linux with compositor
- Git

### Setup
```bash
git clone https://github.com/pfn000/AuraVT.git
cd AuraVT/UnityProject
# Open in Unity Hub → Add project
```

### Build Native Plugin (Windows)
> [!TIP] 
> Don't sweat, I'm building a Windows Installer and for Linux I'll just build an AppImage file. 

```bash
cd NativePlugins/Windows/TransparentWindow
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release
```
The `.dll` is automatically copied to `UnityProject/Assets/AuraVT/Plugins/Windows/`.

---

## 📋 Roadmap

See [DEVELOPMENT_ROADMAP.md](./DEVELOPMENT_ROADMAP.md)

---

## 📄 License

MIT — built under NCOM Systems by [@Saidie000](https://github.com/Saidie000)
