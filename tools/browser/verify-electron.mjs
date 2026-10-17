import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { _electron as electron } from 'playwright';
import { instrumentRendering, verifyIceSurface } from './render-checks.mjs';

const executablePath = process.argv[2];
assert(executablePath, 'Usage: npm run test:electron -- /path/to/electron [http://localhost:8000]');
const origin = process.argv[3] || 'http://127.0.0.1:8000';
const artifacts = fileURLToPath(new URL('../../TestResults/browser/', import.meta.url));
await mkdir(artifacts, { recursive: true });
const temporary = await mkdtemp(resolve(tmpdir(), 'stickandpuck-electron-'));
const main = resolve(temporary, 'main.cjs');
await writeFile(main, `const { app, BrowserWindow } = require('electron');
app.whenReady().then(() => {
    const window = new BrowserWindow({ width: 1500, height: 1100, show: false,
        webPreferences: { backgroundThrottling: false, nodeIntegration: false, contextIsolation: true } });
    window.loadURL('about:blank');
});
`);
let application;
const errors = [], logs = [];
try {
    application = await electron.launch({ executablePath, args: ['--no-sandbox', main] });
    const page = await application.firstWindow();
    await instrumentRendering(page);
    page.on('pageerror', error => errors.push(String(error)));
    page.on('console', message => {
        logs.push(message.text());
        if (message.type() === 'error' || /WebGL: INVALID_|WARNING: SHADER/.test(message.text())) errors.push(message.text());
    });
    await page.addInitScript(() => {
        const raf = requestAnimationFrame.bind(window);
        const draw = WebGLRenderingContext.prototype.drawElements;
        WebGLRenderingContext.prototype.drawElements = function(...args) {
            // The practice rink's ice is an identity-transform mesh (150 indices).
            // Its MVP changes only when the camera changes, unlike player models.
            if (window.__motion && args[1] === 150 && this.isEnabled(this.DEPTH_TEST)) {
                const program = this.getParameter(this.CURRENT_PROGRAM);
                const matrix = this.getUniform(program, this.getUniformLocation(program, 'mvp'));
                window.__motion.cameras.push(Array.from(matrix));
            }
            return draw.apply(this, args);
        };
        window.requestAnimationFrame = callback => raf(timestamp => {
            callback(timestamp);
            if (!window.__motion) return;
            const canvas = document.querySelector('canvas'), gl = canvas.getContext('webgl');
            const pixel = new Uint8Array(4);
            let brightest = 0;
            for (const x of [0.35, 0.4, 0.45]) {
                gl.readPixels(Math.floor(canvas.width * x), Math.floor(canvas.height * 0.4), 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixel);
                brightest = Math.max(brightest, pixel[0] + pixel[1] + pixel[2]);
            }
            window.__motion.frames++;
            if (brightest < 128) window.__motion.dark++;
        });
    });
    await page.goto(`${origin}/?scene=practice`);
    await page.waitForFunction(() => !document.querySelector('#start').disabled, { timeout: 60000 });
    // A visible, unfocused window is needed: hidden Electron windows throttle rendering.
    await application.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows()[0].showInactive());
    await page.click('#start');
    await page.waitForTimeout(500);
    const ice = await verifyIceSurface(page);
    const renderer = await page.evaluate(() => {
        const gl = document.querySelector('canvas').getContext('webgl');
        const extension = gl.getExtension('WEBGL_debug_renderer_info');
        return extension ? gl.getParameter(extension.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
    });
    await page.keyboard.down('ArrowRight'); await page.waitForTimeout(1000);
    await page.evaluate(() => { window.__motion = { frames: 0, dark: 0, cameras: [] }; });
    await page.waitForTimeout(4000);
    await page.keyboard.up('ArrowRight'); await page.keyboard.down('ArrowDown');
    await page.waitForTimeout(2500); await page.keyboard.up('ArrowDown');
    const motion = await page.evaluate(() => {
        const result = window.__motion;
        delete window.__motion;
        return { frames: result.frames, dark: result.dark, cameraMatrices: new Set(result.cameras.map(matrix => JSON.stringify(matrix))).size };
    });
    assert(motion.frames >= 60, 'Motion check renders enough frames.');
    assert(motion.cameraMatrices >= 10, 'The camera actually pans during the check.');
    assert(motion.dark / motion.frames < 0.1, `Ice does not disappear while panning: ${JSON.stringify(motion)}`);
    assert.equal(errors.length, 0, errors.join('\n'));
    await page.screenshot({ path: resolve(artifacts, 'electron-gpu.png') });
    const result = { renderer, motion, ice, errors };
    await writeFile(resolve(artifacts, 'electron-gpu.json'), JSON.stringify(result, null, 2));
    console.log(JSON.stringify(result, null, 2));
} finally {
    await writeFile(resolve(artifacts, 'electron-gpu.log'), logs.join('\n'));
    if (application) await application.close();
    await rm(temporary, { recursive: true, force: true });
}
