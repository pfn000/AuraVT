/**
 * AuraVT - Main Process
 * The FREE VTuber Desktop Overlay
 * (c) 2026 NCOM Systems - @Saidie000 / pfn000
 */

const { app, BrowserWindow, ipcMain, Tray, Menu, dialog, nativeImage } = require('electron');
const path = require('path');
const fs = require('fs');
const Store = require('electron-store');

const store = new Store({
    defaults: {
        windowBounds: { width: 800, height: 600, x: undefined, y: undefined },
        alwaysOnTop: true,
        clickThrough: false,
        lastAvatarPath: null,
        faceTracking: { enabled: false, smoothing: 0.5 },
        handTracking: { enabled: false },
        lipSync: { enabled: true, gain: 1.0, threshold: 0.01 },
        vmcReceiver: { enabled: false, port: 39539 },
        springBones: { enabled: true, stiffness: 1.0 },
        transparency: 1.0,
        backgroundColor: '#00000000'
    }
});

let mainWindow = null;
let tray = null;

const gotTheLock = app.requestSingleInstanceLock();
if (!gotTheLock) {
    app.quit();
} else {
    app.on('second-instance', () => {
        if (mainWindow) {
            if (mainWindow.isMinimized()) mainWindow.restore();
            mainWindow.focus();
        }
    });
}

function createWindow() {
    const bounds = store.get('windowBounds');

    mainWindow = new BrowserWindow({
        width: bounds.width,
        height: bounds.height,
        x: bounds.x,
        y: bounds.y,
        frame: false,
        transparent: true,
        alwaysOnTop: store.get('alwaysOnTop'),
        skipTaskbar: false,
        hasShadow: false,
        backgroundColor: '#00000000',
        webPreferences: {
            preload: path.join(__dirname, 'preload.js'),
            contextIsolation: true,
            nodeIntegration: false,
            webSecurity: false
        }
    });

    mainWindow.loadFile(path.join(__dirname, 'renderer', 'index.html'));
    mainWindow.on('move', () => saveWindowBounds());
    mainWindow.on('resize', () => saveWindowBounds());
    mainWindow.on('closed', () => { mainWindow = null; });

    mainWindow.webContents.on('did-finish-load', () => {
        const lastAvatar = store.get('lastAvatarPath');
        if (lastAvatar && fs.existsSync(lastAvatar)) {
            mainWindow.webContents.send('load-avatar', lastAvatar);
        }
    });

    createTray();
}

function saveWindowBounds() {
    if (!mainWindow) return;
    store.set('windowBounds', mainWindow.getBounds());
}

function createTray() {
    const iconPath = path.join(__dirname, '..', 'assets', 'icon.png');
    let trayIcon = fs.existsSync(iconPath) 
        ? nativeImage.createFromPath(iconPath).resize({ width: 16, height: 16 })
        : nativeImage.createEmpty();

    tray = new Tray(trayIcon);

    const contextMenu = Menu.buildFromTemplate([
        { label: 'Show AuraVT', click: () => { mainWindow?.show(); mainWindow?.focus(); }},
        { type: 'separator' },
        { label: 'Always on Top', type: 'checkbox', checked: store.get('alwaysOnTop'),
          click: (m) => { store.set('alwaysOnTop', m.checked); mainWindow?.setAlwaysOnTop(m.checked); }},
        { label: 'Click Through', type: 'checkbox', checked: store.get('clickThrough'),
          click: (m) => { store.set('clickThrough', m.checked); mainWindow?.setIgnoreMouseEvents(m.checked, { forward: true }); }},
        { type: 'separator' },
        { label: 'Load Avatar...', click: async () => {
            const result = await dialog.showOpenDialog(mainWindow, {
                properties: ['openFile'],
                filters: [{ name: 'VRM/GLB Files', extensions: ['vrm', 'glb', 'gltf'] }]
            });
            if (!result.canceled && result.filePaths.length > 0) {
                store.set('lastAvatarPath', result.filePaths[0]);
                mainWindow?.webContents.send('load-avatar', result.filePaths[0]);
            }
        }},
        { type: 'separator' },
        { label: 'Quit', click: () => app.quit() }
    ]);

    tray.setToolTip('AuraVT - VTuber Desktop Overlay');
    tray.setContextMenu(contextMenu);
    tray.on('click', () => { mainWindow?.show(); mainWindow?.focus(); });
}

ipcMain.handle('get-settings', () => store.store);
ipcMain.handle('save-setting', (e, key, value) => { 
    store.set(key, value); 
    if (key === 'alwaysOnTop') mainWindow?.setAlwaysOnTop(value);
    else if (key === 'clickThrough') mainWindow?.setIgnoreMouseEvents(value, { forward: true });
    return true;
});
ipcMain.handle('file-dropped', (e, filePath) => { 
    if (fs.existsSync(filePath)) { store.set('lastAvatarPath', filePath); return filePath; } 
    return null; 
});
ipcMain.handle('open-file-dialog', async () => {
    const result = await dialog.showOpenDialog(mainWindow, {
        properties: ['openFile'],
        filters: [{ name: 'VRM/GLB Files', extensions: ['vrm', 'glb', 'gltf'] }]
    });
    if (!result.canceled && result.filePaths.length > 0) {
        store.set('lastAvatarPath', result.filePaths[0]);
        return result.filePaths[0];
    }
    return null;
});
ipcMain.handle('minimize-window', () => mainWindow?.minimize());
ipcMain.handle('close-window', () => mainWindow?.close());
ipcMain.handle('toggle-devtools', () => mainWindow?.webContents.toggleDevTools());

app.whenReady().then(createWindow);
app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
process.on('uncaughtException', (error) => console.error('Uncaught Exception:', error));
