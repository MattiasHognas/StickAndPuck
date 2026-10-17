import { dotnet } from './_framework/dotnet.js';
import { createSaveStore } from './storage.js';

const button = document.querySelector('#start');
const stopButton = document.querySelector('#stop');
const downloadButton = document.querySelector('#download');
const selectedDownloadButton = document.querySelector('#download-selected');
const playButton = document.querySelector('#play');
const verifyButton = document.querySelector('#verify');
const upload = document.querySelector('#upload');
const replays = document.querySelector('#replays');
const status = document.querySelector('#status');
const saveStatus = document.querySelector('#save-status');
const canvas = document.querySelector('#canvas');
let host;
let running = false;
let frameId;

function listReplays() {
    const names = host.Replays().split('\n').filter(Boolean);
    replays.replaceChildren(...names.map(name => new Option(name, name)));
    playButton.disabled = names.length === 0;
    verifyButton.disabled = names.length === 0;
    downloadButton.disabled = !running;
    selectedDownloadButton.disabled = names.length === 0;
}

function stop() {
    running = false;
    cancelAnimationFrame(frameId);
    host?.Stop();
    button.disabled = !host;
    stopButton.disabled = true;
    if (host) listReplays();
}

function fail(error) {
    try { stop(); } catch (stopError) { console.error(stopError); }
    console.error(error);
    status.textContent = `Game failed: ${error.message || error}`;
}

function frame(timestamp) {
    if (!running) return;
    try {
        if (!host.Frame(timestamp)) {
            stop();
            status.textContent = host.ReplayResult() || 'Game stopped.';
            return;
        }
        frameId = requestAnimationFrame(frame);
    } catch (error) { fail(error); }
}

function start(action) {
    try {
        cancelAnimationFrame(frameId);
        action();
        canvas.focus();
        running = true;
        button.disabled = true;
        stopButton.disabled = false;
        downloadButton.disabled = false;
        status.textContent = 'Running';
        frameId = requestAnimationFrame(frame);
    } catch (error) { fail(error); }
}

// Loading first lets initialization and Web Audio run synchronously inside the Start gesture.
try {
    const runtime = await dotnet.withModuleConfig({ canvas }).create();
    runtime.setModuleImports('browser', {
        saves: createSaveStore(localStorage, message => { saveStatus.textContent = message; })
    });
    host = (await runtime.getAssemblyExports('StickAndPuck.dll')).StickAndPuck.App.Core.BrowserHost;
    button.disabled = false;
    button.textContent = 'Start game';
    upload.disabled = false;
    listReplays();
    status.textContent = 'Ready.';
    button.onclick = () => start(() => host.Start(new URLSearchParams(location.search).get('scene') || 'start'));
    stopButton.onclick = () => { stop(); status.textContent = 'Game stopped.'; };
    playButton.onclick = () => start(() => host.PlayReplay(replays.value, false));
    verifyButton.onclick = () => start(() => host.PlayReplay(replays.value, true));
    const downloadReplay = name => {
        try {
            const base64 = host.DownloadReplay(name);
            if (!base64) { status.textContent = 'Play a match before downloading its replay.'; return; }
            const bytes = Uint8Array.from(atob(base64), char => char.charCodeAt(0));
            const url = URL.createObjectURL(new Blob([bytes], { type: 'application/octet-stream' }));
            const link = document.createElement('a');
            link.href = url;
            link.download = name || 'StickAndPuck.sapreplay';
            link.click();
            setTimeout(() => URL.revokeObjectURL(url), 1000);
            listReplays();
        } catch (error) { status.textContent = `Replay download failed: ${error.message || error}`; }
    };
    downloadButton.onclick = () => downloadReplay('');
    selectedDownloadButton.onclick = () => downloadReplay(replays.value);
    upload.onchange = async () => {
        const file = upload.files[0];
        if (!file) return;
        try {
            const bytes = new Uint8Array(await file.arrayBuffer());
            let binary = '';
            for (let i = 0; i < bytes.length; i += 8192) binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
            const name = host.UploadReplay(file.name, btoa(binary));
            listReplays();
            replays.value = name;
            status.textContent = 'Replay uploaded. Click Play replay to watch it.';
            selectedDownloadButton.disabled = false;
        } catch (error) { status.textContent = `Replay upload failed: ${error.message || error}`; }
        finally { upload.value = ''; }
    };
} catch (error) { fail(error); }

// The browser releases the departing page's GL/audio/runtime objects. Calling native
// audio shutdown during navigation starts promises against an inactive document.
let resumeAfterPageShow = false;
window.addEventListener('pagehide', () => {
    resumeAfterPageShow = running;
    running = false;
    cancelAnimationFrame(frameId);
});
window.addEventListener('pageshow', event => {
    if (event.persisted && resumeAfterPageShow) {
        running = true;
        frameId = requestAnimationFrame(frame);
    }
});
canvas.addEventListener('keydown', event => {
    if (['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Space', 'Tab'].includes(event.code)) event.preventDefault();
});
canvas.addEventListener('contextmenu', event => event.preventDefault());
canvas.addEventListener('pointerdown', event => {
    canvas.focus();
    if (running) host.MousePressed(event.button === 1 ? 2 : event.button === 2 ? 1 : 0);
});
