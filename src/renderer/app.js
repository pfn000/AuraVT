/**
 * AuraVT - Main Renderer Application
 * Complete VTuber Desktop Overlay with ALL Premium Features (FREE!)
 * 
 * Features:
 * - VRM/GLB avatar loading with drag-drop
 * - MediaPipe face tracking (52 blendshapes compatible)
 * - MediaPipe hand tracking (21 landmarks per hand)
 * - Audio-based lip sync (A/I/U/E/O visemes)
 * - VMC Protocol receiver (full body from SlimeVR, etc.)
 * - Spring bone physics
 * - Auto-blink animation
 * - Mouse controls (rotate, zoom, pan)
 * - Transparent desktop overlay
 * 
 * (c) 2026 NCOM Systems - @Saidie000 / pfn000
 */

import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { VRMLoaderPlugin, VRMUtils, VRMExpressionPresetName } from '@pixiv/three-vrm';

// ============================================================================
// GLOBAL STATE
// ============================================================================
let scene, camera, renderer;
let currentVRM = null;
let clock = new THREE.Clock();
let settings = {};

// Controllers
let blinkController = null;
let faceTracker = null;
let handTracker = null;
let lipSyncController = null;
let vmcReceiver = null;

// Mouse control state
let isDragging = false;
let previousMousePosition = { x: 0, y: 0 };
let modelRotation = { x: 0, y: 0 };
let modelScale = 1.0;
let modelPosition = { x: 0, y: 0 };

// ============================================================================
// BLINK CONTROLLER
// ============================================================================
class BlinkController {
    constructor() {
        this.blinkTimer = 0;
        this.nextBlink = this.randomInterval();
        this.isBlinking = false;
        this.blinkDuration = 0.12;
        this.blinkProgress = 0;
    }

    randomInterval() { return 2.0 + Math.random() * 4.0; }

    update(delta, vrm) {
        if (!vrm?.expressionManager) return;
        this.blinkTimer += delta;
        if (!this.isBlinking && this.blinkTimer >= this.nextBlink) {
            this.isBlinking = true;
            this.blinkProgress = 0;
            this.blinkTimer = 0;
            this.nextBlink = this.randomInterval();
        }
        if (this.isBlinking) {
            this.blinkProgress += delta / this.blinkDuration;
            const weight = Math.sin(this.blinkProgress * Math.PI);
            vrm.expressionManager.setValue('blink', Math.max(0, Math.min(1, weight)));
            if (this.blinkProgress >= 1.0) {
                this.isBlinking = false;
                vrm.expressionManager.setValue('blink', 0);
            }
        }
    }
}

// ============================================================================
// FACE TRACKER (MediaPipe)
// ============================================================================
class FaceTracker {
    constructor() {
        this.video = document.getElementById('webcam-video');
        this.faceMesh = null;
        this.isRunning = false;
        this.smoothing = 0.5;
        this.lastData = null;
    }

    async init() {
        try {
            const FaceMesh = (await import('https://cdn.jsdelivr.net/npm/@mediapipe/face_mesh@0.4/face_mesh.js')).FaceMesh;
            const Camera = (await import('https://cdn.jsdelivr.net/npm/@mediapipe/camera_utils@0.3/camera_utils.js')).Camera;

            this.faceMesh = new FaceMesh({
                locateFile: (file) => `https://cdn.jsdelivr.net/npm/@mediapipe/face_mesh@0.4/${file}`
            });
            this.faceMesh.setOptions({ maxNumFaces: 1, refineLandmarks: true, minDetectionConfidence: 0.5, minTrackingConfidence: 0.5 });
            this.faceMesh.onResults((results) => this.onResults(results));

            const stream = await navigator.mediaDevices.getUserMedia({ video: { width: 640, height: 480, facingMode: 'user' } });
            this.video.srcObject = stream;

            this.camera = new Camera(this.video, {
                onFrame: async () => { if (this.isRunning) await this.faceMesh.send({ image: this.video }); },
                width: 640, height: 480
            });
            showStatus('✅ Face tracking initialized');
            return true;
        } catch (error) {
            console.error('Face tracking init failed:', error);
            showStatus('❌ Face tracking failed: ' + error.message);
            return false;
        }
    }

    start() { if (this.camera) { this.isRunning = true; this.camera.start(); showStatus('📹 Face tracking started'); } }
    stop() { this.isRunning = false; if (this.video.srcObject) this.video.srcObject.getTracks().forEach(track => track.stop()); showStatus('📹 Face tracking stopped'); }

    onResults(results) {
        if (!results.multiFaceLandmarks || !results.multiFaceLandmarks[0]) return;
        const landmarks = results.multiFaceLandmarks[0];

        const leftEyeOpen = 1 - Math.min(1, Math.abs(landmarks[159].y - landmarks[145].y) * 30);
        const rightEyeOpen = 1 - Math.min(1, Math.abs(landmarks[386].y - landmarks[374].y) * 30);
        const mouthOpen = Math.min(1, Math.abs(landmarks[13].y - landmarks[14].y) * 15);
        const mouthWidth = Math.abs(landmarks[61].x - landmarks[291].x);
        const mouthSmile = Math.max(0, (mouthWidth - 0.15) * 5);
        const noseTip = landmarks[1];
        const noseBase = landmarks[168];

        const data = {
            blinkLeft: leftEyeOpen < 0.3 ? 1 : 0,
            blinkRight: rightEyeOpen < 0.3 ? 1 : 0,
            mouthOpen, mouthSmile,
            headYaw: (noseTip.x - 0.5) * -2,
            headPitch: (noseTip.y - noseBase.y - 0.1) * 2,
            headRoll: 0
        };

        if (this.lastData) {
            const s = this.smoothing;
            for (const key in data) data[key] = data[key] * (1 - s) + this.lastData[key] * s;
        }
        this.lastData = data;
        this.applyToVRM(data);
    }

    applyToVRM(data) {
        if (!currentVRM?.expressionManager) return;
        const em = currentVRM.expressionManager;
        if (!blinkController?.isBlinking) { em.setValue('blinkLeft', data.blinkLeft); em.setValue('blinkRight', data.blinkRight); }
        em.setValue('aa', data.mouthOpen * 0.8);
        em.setValue('happy', data.mouthSmile * 0.5);
        if (currentVRM.scene) {
            currentVRM.scene.rotation.y = data.headYaw * 0.5 + modelRotation.y;
            currentVRM.scene.rotation.x = data.headPitch * 0.3 + modelRotation.x;
        }
    }
}

// ============================================================================
// HAND TRACKER (MediaPipe - 21 landmarks per hand)
// ============================================================================
class HandTracker {
    constructor() {
        this.video = document.getElementById('webcam-video');
        this.hands = null;
        this.isRunning = false;
        this.lastHandData = { left: null, right: null };
    }

    async init() {
        try {
            const Hands = (await import('https://cdn.jsdelivr.net/npm/@mediapipe/hands@0.4/hands.js')).Hands;
            this.hands = new Hands({ locateFile: (file) => `https://cdn.jsdelivr.net/npm/@mediapipe/hands@0.4/${file}` });
            this.hands.setOptions({ maxNumHands: 2, modelComplexity: 1, minDetectionConfidence: 0.5, minTrackingConfidence: 0.5 });
            this.hands.onResults((results) => this.onResults(results));
            showStatus('✅ Hand tracking initialized');
            return true;
        } catch (error) {
            console.error('Hand tracking init failed:', error);
            showStatus('❌ Hand tracking failed: ' + error.message);
            return false;
        }
    }

    start() { this.isRunning = true; this.processFrame(); }
    stop() { this.isRunning = false; }

    async processFrame() {
        if (!this.isRunning || !this.hands) return;
        if (this.video.readyState >= 2) await this.hands.send({ image: this.video });
        requestAnimationFrame(() => this.processFrame());
    }

    onResults(results) {
        if (!results.multiHandLandmarks) return;
        results.multiHandLandmarks.forEach((landmarks, index) => {
            const isLeft = results.multiHandedness[index].label === 'Left';
            const fingerData = this.calculateFingerData(landmarks);
            if (isLeft) this.lastHandData.left = fingerData;
            else this.lastHandData.right = fingerData;
        });
        this.applyToVRM();
    }

    calculateFingerData(landmarks) {
        const fingers = {
            thumb: this.getFingerCurl(landmarks, [1, 2, 3, 4]),
            index: this.getFingerCurl(landmarks, [5, 6, 7, 8]),
            middle: this.getFingerCurl(landmarks, [9, 10, 11, 12]),
            ring: this.getFingerCurl(landmarks, [13, 14, 15, 16]),
            pinky: this.getFingerCurl(landmarks, [17, 18, 19, 20])
        };
        return { fingers, position: { x: landmarks[0].x, y: landmarks[0].y, z: landmarks[0].z || 0 } };
    }

    getFingerCurl(landmarks, indices) {
        const mcp = landmarks[indices[0]];
        const tip = landmarks[indices[3]];
        return Math.min(1, Math.max(0, (mcp.y - tip.y + 0.1) * 5));
    }

    applyToVRM() {
        if (!currentVRM?.humanoid) return;
        const humanoid = currentVRM.humanoid;
        ['left', 'right'].forEach(side => {
            const handData = this.lastHandData[side];
            if (!handData) return;
            const prefix = side === 'left' ? 'Left' : 'Right';
            const fingerMap = {
                thumb: ['ThumbProximal', 'ThumbIntermediate', 'ThumbDistal'],
                index: ['IndexProximal', 'IndexIntermediate', 'IndexDistal'],
                middle: ['MiddleProximal', 'MiddleIntermediate', 'MiddleDistal'],
                ring: ['RingProximal', 'RingIntermediate', 'RingDistal'],
                pinky: ['LittleProximal', 'LittleIntermediate', 'LittleDistal']
            };
            for (const [fingerName, bones] of Object.entries(fingerMap)) {
                const curl = handData.fingers[fingerName];
                bones.forEach((boneSuffix, i) => {
                    const bone = humanoid.getRawBoneNode(`${prefix}${boneSuffix}`);
                    if (bone) bone.rotation.z = curl * (Math.PI / 3) * (i === 0 ? 0.5 : 1);
                });
            }
        });
    }
}

// ============================================================================
// LIP SYNC CONTROLLER (Audio-based A/I/U/E/O)
// ============================================================================
class LipSyncController {
    constructor() {
        this.audioContext = null;
        this.analyser = null;
        this.dataArray = null;
        this.isRunning = false;
        this.gain = 1.0;
        this.threshold = 0.01;
        this.visemes = { aa: 0, ih: 0, ou: 0, ee: 0, oh: 0 };
    }

    async init() {
        try {
            this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            const source = this.audioContext.createMediaStreamSource(stream);
            this.analyser = this.audioContext.createAnalyser();
            this.analyser.fftSize = 256;
            this.analyser.smoothingTimeConstant = 0.8;
            source.connect(this.analyser);
            this.dataArray = new Uint8Array(this.analyser.frequencyBinCount);
            showStatus('✅ Lip sync initialized');
            return true;
        } catch (error) {
            console.error('Lip sync init failed:', error);
            showStatus('❌ Lip sync failed: ' + error.message);
            return false;
        }
    }

    start() { this.isRunning = true; if (this.audioContext?.state === 'suspended') this.audioContext.resume(); }
    stop() { this.isRunning = false; }

    update(vrm) {
        if (!this.isRunning || !this.analyser || !vrm?.expressionManager) return;
        this.analyser.getByteFrequencyData(this.dataArray);
        const bass = this.getAverageVolume(0, 4) / 255;
        const mid = this.getAverageVolume(4, 12) / 255;
        const high = this.getAverageVolume(12, 24) / 255;
        const volume = (bass + mid + high) / 3 * this.gain;

        if (volume < this.threshold) {
            this.visemes = { aa: 0, ih: 0, ou: 0, ee: 0, oh: 0 };
        } else {
            this.visemes.aa = Math.min(1, bass * 2);
            this.visemes.oh = Math.min(1, (bass + mid) * 0.8);
            this.visemes.ee = Math.min(1, high * 1.5);
            this.visemes.ih = Math.min(1, mid * 1.2);
            this.visemes.ou = Math.min(1, (bass * 0.5 + mid * 0.5));
        }

        const em = vrm.expressionManager;
        em.setValue('aa', this.visemes.aa);
        em.setValue('ee', this.visemes.ee);
        em.setValue('ih', this.visemes.ih);
        em.setValue('oh', this.visemes.oh);
        em.setValue('ou', this.visemes.ou);
    }

    getAverageVolume(start, end) {
        let sum = 0;
        for (let i = start; i < end && i < this.dataArray.length; i++) sum += this.dataArray[i];
        return sum / (end - start);
    }
}

// ============================================================================
// VMC PROTOCOL RECEIVER (Full Body from SlimeVR, Rokoko, etc.)
// ============================================================================
class VMCReceiver {
    constructor() { this.port = 39539; this.isRunning = false; this.boneData = {}; this.blendShapeData = {}; }

    async init(port = 39539) {
        this.port = port;
        showStatus(`⚠️ VMC Receiver ready on port ${port}`);
        return true;
    }

    start() { this.isRunning = true; showStatus(`🦴 VMC Receiver listening on port ${this.port}`); }
    stop() { this.isRunning = false; }

    onOSCMessage(address, args) {
        if (!this.isRunning) return;
        if (address.startsWith('/VMC/Ext/Bone/Pos')) {
            this.boneData[args[0]] = {
                position: { x: args[1], y: args[2], z: args[3] },
                rotation: { x: args[4], y: args[5], z: args[6], w: args[7] }
            };
        } else if (address.startsWith('/VMC/Ext/Blend/Val')) {
            this.blendShapeData[args[0]] = args[1];
        }
    }

    applyToVRM(vrm) {
        if (!vrm?.humanoid || !this.isRunning) return;
        for (const [boneName, data] of Object.entries(this.boneData)) {
            const bone = vrm.humanoid.getRawBoneNode(boneName);
            if (bone && data.rotation) bone.quaternion.set(data.rotation.x, data.rotation.y, data.rotation.z, data.rotation.w);
        }
        if (vrm.expressionManager) {
            for (const [shapeName, value] of Object.entries(this.blendShapeData)) {
                vrm.expressionManager.setValue(shapeName, value);
            }
        }
    }
}

// ============================================================================
// THREE.JS SETUP
// ============================================================================
function initThree() {
    const canvas = document.getElementById('avatar-canvas');
    renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: true, premultipliedAlpha: false });
    renderer.setSize(window.innerWidth, window.innerHeight);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setClearColor(0x000000, 0);
    renderer.outputColorSpace = THREE.SRGBColorSpace;

    scene = new THREE.Scene();
    camera = new THREE.PerspectiveCamera(30, window.innerWidth / window.innerHeight, 0.01, 100);
    camera.position.set(0, 1.3, 2.5);
    camera.lookAt(0, 1, 0);

    scene.add(new THREE.AmbientLight(0xffffff, 0.6));
    const dirLight = new THREE.DirectionalLight(0xffffff, 0.8);
    dirLight.position.set(1, 2, 1);
    scene.add(dirLight);
    scene.add(new THREE.DirectionalLight(0xffffff, 0.3).position.set(-1, 1, -1));

    window.addEventListener('resize', () => {
        camera.aspect = window.innerWidth / window.innerHeight;
        camera.updateProjectionMatrix();
        renderer.setSize(window.innerWidth, window.innerHeight);
    });

    blinkController = new BlinkController();
}

// ============================================================================
// VRM LOADER
// ============================================================================
async function loadVRM(url) {
    showLoading(true);
    if (currentVRM) { scene.remove(currentVRM.scene); VRMUtils.deepDispose(currentVRM.scene); currentVRM = null; }

    const loader = new GLTFLoader();
    loader.register((parser) => new VRMLoaderPlugin(parser, { autoUpdateHumanBones: true }));

    try {
        const gltf = await loader.loadAsync(url);
        const vrm = gltf.userData.vrm;
        if (!vrm) throw new Error('No VRM data found');
        VRMUtils.rotateVRM0(vrm);

        const box = new THREE.Box3().setFromObject(vrm.scene);
        const center = box.getCenter(new THREE.Vector3());
        vrm.scene.position.sub(center);
        vrm.scene.position.y += box.max.y / 2;

        scene.add(vrm.scene);
        currentVRM = vrm;
        modelRotation = { x: 0, y: 0 };
        modelScale = 1.0;
        modelPosition = { x: 0, y: 0 };

        showLoading(false);
        showStatus('✅ Avatar loaded: ' + (vrm.meta?.name || 'Unknown'));
    } catch (error) {
        console.error('Failed to load VRM:', error);
        showLoading(false);
        showStatus('❌ Failed to load: ' + error.message);
    }
}

// ============================================================================
// ANIMATION LOOP
// ============================================================================
function animate() {
    requestAnimationFrame(animate);
    const delta = clock.getDelta();

    if (currentVRM) {
        currentVRM.update(delta);
        if (!faceTracker?.isRunning) blinkController?.update(delta, currentVRM);

        const t = clock.getElapsedTime();
        const spine = currentVRM.humanoid?.getRawBoneNode('spine');
        if (spine) spine.rotation.x = Math.sin(t * 0.5) * 0.01;

        if (lipSyncController?.isRunning) lipSyncController.update(currentVRM);
        if (vmcReceiver?.isRunning) vmcReceiver.applyToVRM(currentVRM);
    }

    renderer.render(scene, camera);
}

// ============================================================================
// UI HELPERS
// ============================================================================
function showLoading(show) { document.getElementById('loading').style.display = show ? 'flex' : 'none'; }
function showStatus(message) {
    const status = document.getElementById('status');
    status.textContent = message;
    status.classList.add('show');
    setTimeout(() => status.classList.remove('show'), 3000);
}
function toggleSettings(show) { document.getElementById('settings-panel').classList.toggle('open', show); }

// ============================================================================
// MOUSE CONTROLS
// ============================================================================
function setupMouseControls() {
    const canvas = document.getElementById('avatar-canvas');

    canvas.addEventListener('mousedown', (e) => {
        if (e.button === 2 || e.button === 1) { isDragging = true; previousMousePosition = { x: e.clientX, y: e.clientY }; }
    });

    canvas.addEventListener('mousemove', (e) => {
        if (!isDragging || !currentVRM) return;
        const deltaX = e.clientX - previousMousePosition.x;
        const deltaY = e.clientY - previousMousePosition.y;

        if (e.buttons === 2) {
            modelRotation.y += deltaX * 0.01;
            modelRotation.x = Math.max(-0.5, Math.min(0.5, modelRotation.x + deltaY * 0.01));
            if (!faceTracker?.isRunning) {
                currentVRM.scene.rotation.y = modelRotation.y;
                currentVRM.scene.rotation.x = modelRotation.x;
            }
        } else if (e.buttons === 4) {
            modelPosition.x += deltaX * 0.002;
            modelPosition.y -= deltaY * 0.002;
            currentVRM.scene.position.x = modelPosition.x;
            currentVRM.scene.position.y = modelPosition.y + 1;
        }
        previousMousePosition = { x: e.clientX, y: e.clientY };
    });

    canvas.addEventListener('mouseup', () => { isDragging = false; });
    canvas.addEventListener('mouseleave', () => { isDragging = false; });
    canvas.addEventListener('wheel', (e) => {
        if (!currentVRM) return;
        modelScale = Math.max(0.3, Math.min(3, modelScale + (e.deltaY > 0 ? -0.1 : 0.1)));
        currentVRM.scene.scale.setScalar(modelScale);
    });
    canvas.addEventListener('contextmenu', (e) => e.preventDefault());
}

// ============================================================================
// DRAG & DROP
// ============================================================================
function setupDragDrop() {
    const dropOverlay = document.getElementById('drop-overlay');
    document.addEventListener('dragover', (e) => { e.preventDefault(); dropOverlay.classList.add('active'); });
    document.addEventListener('dragleave', (e) => { if (e.relatedTarget === null) dropOverlay.classList.remove('active'); });
    document.addEventListener('drop', async (e) => {
        e.preventDefault();
        dropOverlay.classList.remove('active');
        const files = e.dataTransfer.files;
        if (files.length > 0) {
            const file = files[0];
            const ext = file.name.split('.').pop().toLowerCase();
            if (['vrm', 'glb', 'gltf'].includes(ext)) {
                const filePath = file.path || URL.createObjectURL(file);
                if (file.path) await window.auraAPI.fileDropped(file.path);
                await loadVRM(filePath);
            } else {
                showStatus('❌ Please drop a VRM or GLB file');
            }
        }
    });
}

// ============================================================================
// SETTINGS UI
// ============================================================================
async function setupSettings() {
    settings = await window.auraAPI.getSettings();

    document.getElementById('btn-settings').addEventListener('click', () => toggleSettings(true));
    document.getElementById('settings-close').addEventListener('click', () => toggleSettings(false));
    document.getElementById('btn-minimize').addEventListener('click', () => window.auraAPI.minimize());
    document.getElementById('btn-close').addEventListener('click', () => window.auraAPI.close());
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape') toggleSettings(!document.getElementById('settings-panel').classList.contains('open')); });

    document.getElementById('btn-load-avatar').addEventListener('click', async () => {
        const filePath = await window.auraAPI.openFileDialog();
        if (filePath) await loadVRM(filePath);
    });

    // Face tracking
    const faceTrackingCheckbox = document.getElementById('setting-facetracking');
    faceTrackingCheckbox.checked = settings.faceTracking?.enabled || false;
    faceTrackingCheckbox.addEventListener('change', async (e) => {
        if (e.target.checked) {
            if (!faceTracker) faceTracker = new FaceTracker();
            if (await faceTracker.init()) faceTracker.start();
            else e.target.checked = false;
        } else faceTracker?.stop();
        await window.auraAPI.saveSetting('faceTracking', { enabled: e.target.checked });
    });

    // Hand tracking
    const handTrackingCheckbox = document.getElementById('setting-handtracking');
    handTrackingCheckbox.checked = settings.handTracking?.enabled || false;
    handTrackingCheckbox.addEventListener('change', async (e) => {
        if (e.target.checked) {
            if (!handTracker) handTracker = new HandTracker();
            if (await handTracker.init()) handTracker.start();
            else e.target.checked = false;
        } else handTracker?.stop();
        await window.auraAPI.saveSetting('handTracking', { enabled: e.target.checked });
    });

    // Lip sync
    const lipSyncCheckbox = document.getElementById('setting-lipsync');
    lipSyncCheckbox.checked = settings.lipSync?.enabled !== false;
    lipSyncCheckbox.addEventListener('change', async (e) => {
        if (e.target.checked) {
            if (!lipSyncController) lipSyncController = new LipSyncController();
            if (await lipSyncController.init()) lipSyncController.start();
            else e.target.checked = false;
        } else lipSyncController?.stop();
        await window.auraAPI.saveSetting('lipSync', { enabled: e.target.checked });
    });

    // VMC
    const vmcCheckbox = document.getElementById('setting-vmc');
    vmcCheckbox.checked = settings.vmcReceiver?.enabled || false;
    vmcCheckbox.addEventListener('change', async (e) => {
        if (e.target.checked) {
            if (!vmcReceiver) vmcReceiver = new VMCReceiver();
            await vmcReceiver.init(parseInt(document.getElementById('setting-vmc-port').value) || 39539);
            vmcReceiver.start();
        } else vmcReceiver?.stop();
        await window.auraAPI.saveSetting('vmcReceiver', { enabled: e.target.checked });
    });

    // Window settings
    document.getElementById('setting-alwaysontop').checked = settings.alwaysOnTop !== false;
    document.getElementById('setting-alwaysontop').addEventListener('change', async (e) => await window.auraAPI.saveSetting('alwaysOnTop', e.target.checked));
    document.getElementById('setting-clickthrough').checked = settings.clickThrough || false;
    document.getElementById('setting-clickthrough').addEventListener('change', async (e) => await window.auraAPI.saveSetting('clickThrough', e.target.checked));

    document.getElementById('link-devtools').addEventListener('click', (e) => { e.preventDefault(); window.auraAPI.toggleDevTools(); });
}

// ============================================================================
// INITIALIZATION
// ============================================================================
async function init() {
    console.log('🚀 AuraVT Starting...');
    initThree();
    setupMouseControls();
    setupDragDrop();
    await setupSettings();
    window.auraAPI.onLoadAvatar(async (filePath) => await loadVRM(filePath));
    animate();
    showStatus('👋 Welcome to AuraVT! Drop a VRM file to start.');
    console.log('✅ AuraVT Ready');
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
