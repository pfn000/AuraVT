# 🎭 AuraVT

**The FREE open-source VTuber desktop overlay with ALL premium features.**

> Why pay $50+ for VTuber software when you can have everything for free?

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-brightgreen.svg)](#installation)
[![Made with Electron](https://img.shields.io/badge/Made%20with-Electron-47848F.svg)](https://www.electronjs.org/)

---

## 🔥 Why AuraVT?

**Premium VTuber software charges you $50-100+ for features that should be free.**

AuraVT gives you EVERYTHING — face tracking, hand tracking, lip sync, full body mocap support — completely FREE and open source.

| Feature | VSeeFace | VTube Studio | Warudo | **AuraVT** |
|---------|----------|--------------|--------|------------|
| Price | Free | $25+ | $50+ | **FREE** |
| Face Tracking | ✅ | ✅ | ✅ | ✅ |
| Hand Tracking | ❌ | $25 | ✅ | ✅ **FREE** |
| Lip Sync | ✅ | ✅ | ✅ | ✅ |
| Full Body (VMC) | ✅ | ❌ | ✅ | ✅ **FREE** |
| Transparent Overlay | ✅ | ✅ | ✅ | ✅ |
| Open Source | ❌ | ❌ | ❌ | ✅ **YES** |

---

## ✨ Features

### 🎭 Avatar System
- **VRM 0.x & 1.0 support** — Load any VRM or GLB avatar
- **Drag & drop loading** — Just drop your avatar file
- **Spring bone physics** — Hair, clothes, accessories move naturally
- **MToon shader support** — Anime-style rendering

### 📹 Face Tracking (MediaPipe)
- **52 blendshape compatible** — Perfect Sync quality tracking
- **Real-time webcam tracking** — No special hardware needed
- **Head rotation** — Yaw, pitch, and roll
- **Eye tracking** — Blink detection, gaze direction
- **Adjustable smoothing** — Reduce jitter without lag

### ✋ Hand Tracking (MediaPipe)
- **21 landmarks per hand** — Full finger articulation
- **Both hands simultaneously** — Natural gestures
- **Finger curl detection** — Peace signs, thumbs up, pointing
- **No extra hardware** — Uses your webcam

### 🎤 Lip Sync
- **Audio-based visemes** — A/I/U/E/O mouth shapes
- **Real-time microphone input** — Instant response
- **Adjustable sensitivity** — Fine-tune for your voice
- **Frequency-band analysis** — Natural mouth movement

### 🦴 Full Body Tracking (VMC Protocol)
- **VMC receiver built-in** — Connect any VMC sender
- **SlimeVR compatible** — $75 DIY full body
- **Rokoko compatible** — Professional mocap
- **HaritoraX compatible** — Consumer full body
- **Custom port configuration** — Default 39539

### 🪟 Desktop Overlay
- **Transparent background** — Avatar floats on screen
- **Always on top** — Stay visible while streaming
- **Click-through mode** — Interact with apps behind
- **Resizable & movable** — Put avatar anywhere
- **System tray** — Quick access controls

---

## 📦 Installation

### Download Release (Recommended)
1. Go to [Releases](https://github.com/pfn000/AuraVT/releases)
2. Download for your platform:
   - Windows: `AuraVT-x.x.x-win-x64.exe`
   - macOS: `AuraVT-x.x.x-mac.dmg`
   - Linux: `AuraVT-x.x.x-linux.AppImage`
3. Install and run

### Build from Source
```bash
# Clone the repository
git clone https://github.com/pfn000/AuraVT.git
cd AuraVT

# Install dependencies
npm install

# Run in development
npm start

# Build for production
npm run build        # All platforms
npm run build:win    # Windows only
npm run build:mac    # macOS only
npm run build:linux  # Linux only
```

---

## 🚀 Quick Start

1. **Launch AuraVT**
2. **Load your avatar** — Drag & drop a VRM/GLB file or click "Load Avatar" in settings
3. **Enable features** — Press ESC to open settings panel
4. **Position your avatar** — Right-drag to rotate, scroll to zoom, middle-drag to pan
5. **Start streaming** — Use OBS window capture or game capture

### Keyboard Shortcuts
| Key | Action |
|-----|--------|
| ESC | Toggle settings panel |
| Right-drag | Rotate avatar |
| Scroll | Zoom in/out |
| Middle-drag | Pan avatar |

---

## 🎯 OBS Setup

### Window Capture
1. Add "Window Capture" source
2. Select `[AuraVT.exe]: AuraVT`
3. Check "Capture cursor" off

### For Transparent Background
1. Add "Color Key" filter to the source
2. Key Color Type: Custom
3. Key Color: Black (#000000)
4. Similarity: 1
5. Smoothness: 80

Or use "Game Capture" with "Allow transparency" checked.

---

## 🛠️ Tech Stack

- **[Electron](https://www.electronjs.org/)** — Cross-platform desktop app
- **[Three.js](https://threejs.org/)** — 3D rendering
- **[@pixiv/three-vrm](https://github.com/pixiv/three-vrm)** — VRM avatar support
- **[MediaPipe](https://mediapipe.dev/)** — Face & hand tracking
- **[Web Audio API](https://developer.mozilla.org/en-US/docs/Web/API/Web_Audio_API)** — Lip sync
- **[OSC Protocol](http://opensoundcontrol.org/)** — VMC communication

---

## 🤝 Contributing

Contributions are welcome! This is an open-source project because **premium VTuber features shouldn't cost money**.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📄 License

MIT License — Do whatever you want with it. Just don't charge people for premium features that should be free.

---

## 🙏 Credits

**Created by [NCOM Systems](https://github.com/pfn000)**

- Developer: Saidie (@Saidie000)
- Contact: SaidieQN@ncomsystems.co

Special thanks to:
- [Pixiv](https://github.com/pixiv) for three-vrm
- [Google](https://mediapipe.dev/) for MediaPipe
- The VTuber community for inspiration

---

## 💬 Support

- **Issues**: [GitHub Issues](https://github.com/pfn000/AuraVT/issues)
- **Discord**: [NCOM Systems](https://discord.gg/7sFeAEq2)

---

<p align="center">
  <strong>Stop paying for premium features. Start using AuraVT.</strong><br>
  <em>Made with 💜 by NCOM Systems</em>
</p>
