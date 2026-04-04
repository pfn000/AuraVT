/**
 * AuraVT - Preload Script
 * Secure bridge between main and renderer processes
 */

const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('auraAPI', {
    // Settings
    getSettings: () => ipcRenderer.invoke('get-settings'),
    saveSetting: (key, value) => ipcRenderer.invoke('save-setting', key, value),

    // File handling
    fileDropped: (filePath) => ipcRenderer.invoke('file-dropped', filePath),
    openFileDialog: () => ipcRenderer.invoke('open-file-dialog'),

    // Window controls
    minimize: () => ipcRenderer.invoke('minimize-window'),
    close: () => ipcRenderer.invoke('close-window'),
    toggleDevTools: () => ipcRenderer.invoke('toggle-devtools'),

    // Events from main process
    onLoadAvatar: (callback) => {
        ipcRenderer.on('load-avatar', (event, filePath) => callback(filePath));
    },

    // Platform info
    platform: process.platform
});
