# AuraVT 👻

**A next-generation open-source VTuber desktop overlay application.**  
Built to be dramatically faster, lighter, and more compatible than VSeeFace.

---

<div align="center">

<img src="https://pngimg.com/d/under_construction_PNG3.png" alt="Under Construction" width="800">

</div>

---
## ✨ Features

| Feature | Status |
|---|---|
| Transparent desktop overlay (Windows) | ✅ Phase 1 |
| Drag & drop VRM / GLB avatar loading | ✅ Phase 2 |
| VRChat avatar importer (Poiyomi, lilToon) | ✅ Phase 3 |
| VRChat Avatar Importer (.unitypackage stripper + VRC component shim) | 🔜 Phase 4 |
| Face tracking (MediaPipe / OpenSeeFace) | 🔜 Phase 5 |
| DirectX 11/12 native plugin (Windows) | 🔜 Phase 6 |
| Vulkan native plugin (Linux) | 🔜 Phase 6 |
| Runs on 4 GB RAM / Intel i3 | ✅ Phase 1 |

---

## 🚀 Getting Started

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
