import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { resolve, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { gunzipSync } from 'node:zlib';
import { chromium, firefox, webkit } from 'playwright';
import { instrumentRendering, verifyIceSurface } from './render-checks.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const site = resolve(root, process.env.SITE_ROOT || 'publish/browser-wasm/wwwroot');
const artifacts = resolve(root, 'TestResults/browser');
await mkdir(artifacts, { recursive: true });
const failures = [];
const results = [];
const desktop = resolve(root, 'src/StickAndPuck.App/bin/Release/net11.0/StickAndPuck.dll');
assert(existsSync(desktop), 'Build the desktop app in Release before browser verification.');
assert(existsSync(resolve(site, '_framework/dotnet.js')), 'Publish browser-wasm before verification.');

// Use real desktop recordings, with independent seeds and match logic, as browser fixtures.
const fixtures = [
    { name: 'desktop-ai', scene: 'aimatch', seed: 31, steps: 1800 },
    { name: 'desktop-match', scene: 'matchdemo', seed: 71, steps: 6000 }
];
const environment = { ...Object.fromEntries(Object.entries(process.env).filter(([key]) => !key.startsWith('SAP_'))), LIBGL_ALWAYS_SOFTWARE: '1' };
for (const fixture of fixtures) {
    console.log(`Recording desktop fixture ${fixture.name} (${fixture.steps} steps)`);
    fixture.path = resolve(artifacts, `${fixture.name}.sapreplay`);
    const run = spawnSync('dotnet', [desktop], {
        cwd: artifacts, encoding: 'utf8', timeout: 120000,
        env: { ...environment, SAP_SCENE: fixture.scene, SAP_SEED: String(fixture.seed), SAP_STEPS: String(fixture.steps),
            SAP_REPLAY_OUT: fixture.path, SAP_CHECKSUMS: resolve(artifacts, `${fixture.name}.checksums`),
            SAP_SETTINGS: resolve(artifacts, 'fixture-settings.json'), SAP_SAVE: resolve(artifacts, 'fixture-season.json'),
            SAP_ARCADE_SAVE: resolve(artifacts, 'fixture-arcade.json') }
    });
    await writeFile(resolve(artifacts, `${fixture.name}.log`), run.stdout + run.stderr);
    assert.equal(run.status, 0, `Desktop fixture ${fixture.name} failed: ${run.stderr}`);
    fixture.checksums = (await readFile(resolve(artifacts, `${fixture.name}.checksums`), 'utf8')).trim().split(/\r?\n/);
    assert.equal(fixture.checksums.length, fixture.steps);
}

const mime = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm' };
const server = createServer(async (request, response) => {
    try {
        const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
        const path = resolve(site, `.${pathname === '/' ? '/index.html' : pathname}`);
        if (!path.startsWith(site + sep)) throw new Error('Invalid path');
        const bytes = await readFile(path);
        response.writeHead(200, { 'Content-Type': mime[extname(path)] || 'application/octet-stream', 'Cache-Control': 'no-store' });
        response.end(bytes);
    } catch { response.writeHead(404); response.end(); }
});
await new Promise(resolveListen => server.listen(0, '127.0.0.1', resolveListen));
const origin = `http://127.0.0.1:${server.address().port}`;

async function instrument(page) {
    await instrumentRendering(page);
    await page.addInitScript(() => {
        // Tap whatever the game connects to an AudioContext's destination with an AnalyserNode,
        // so the check reads the PCM that actually reaches the speakers.
        window.__audio = [];
        window.__audioTaps = [];
        window.__audioEnergy = 0;
        const NativeAudio = window.AudioContext;
        window.AudioContext = class extends NativeAudio {
            constructor(...args) { super(...args); window.__audio.push(this); }
        };
        const connect = AudioNode.prototype.connect;
        AudioNode.prototype.connect = function (destination, ...rest) {
            const result = connect.call(this, destination, ...rest);
            if (destination instanceof AudioDestinationNode) {
                const analyser = this.context.createAnalyser();
                analyser.fftSize = 2048;
                connect.call(this, analyser);
                window.__audioTaps.push(analyser);
            }
            return result;
        };
        // Adds the squared samples currently in every tap's window; silence keeps it at zero.
        window.__sampleAudio = () => {
            for (const analyser of window.__audioTaps) {
                const samples = new Float32Array(analyser.fftSize);
                analyser.getFloatTimeDomainData(samples);
                for (const value of samples) window.__audioEnergy += value * value;
            }
            return window.__audioEnergy;
        };
        window.__pad = { id: 'Browser Standard Gamepad', index: 0, connected: true, mapping: 'standard', timestamp: 1,
            axes: [0, 0, 0, 0], buttons: Array.from({ length: 17 }, () => ({ pressed: false, touched: false, value: 0 })) };
        navigator.getGamepads = () => [window.__pad, null, null, null];
        window.__frames = [];
        const raf = window.requestAnimationFrame.bind(window);
        window.requestAnimationFrame = callback => raf(timestamp => {
            if (window.__manualFrames) return;
            const begin = performance.now();
            callback(timestamp);
            window.__frames.push({ timestamp, work: performance.now() - begin });
        });
    });
}

async function host(page, method, ...args) {
    return page.evaluate(async ({ method, args }) => {
        const api = (await getDotnetRuntime(0).getAssemblyExports('StickAndPuck.dll')).StickAndPuck.App.Core.BrowserHost;
        return api[method](...args);
    }, { method, args });
}
async function waitForInput(page, pattern) {
    await page.evaluate(async () => {
        window.__testHost = (await getDotnetRuntime(0).getAssemblyExports('StickAndPuck.dll')).StickAndPuck.App.Core.BrowserHost;
    });
    const observed = await page.waitForFunction(source => {
        const input = __testHost.Input();
        return new RegExp(source).test(input) ? input : false;
    }, pattern.source, { timeout: 10000, polling: 50 });
    return observed.jsonValue();
}
async function open(page, scene = 'start') {
    await page.goto(`${origin}/?scene=${scene}`);
    await page.waitForFunction(() => !document.querySelector('#start').disabled || document.querySelector('#status').textContent.includes('failed'), { timeout: 60000 });
    assert.equal(await page.locator('#start').isEnabled(), true, await page.locator('#status').textContent());
}
async function start(page) {
    await page.locator('#start').click();
    await page.waitForTimeout(300);
    assert.equal(await page.locator('#status').textContent(), 'Running');
}
async function press(page, key) {
    await page.keyboard.press(key);
    await page.waitForTimeout(120);
}
const saves = page => page.evaluate(() => Object.fromEntries(Object.keys(localStorage).filter(key => key.startsWith('stickandpuck:')).map(key => [key, localStorage.getItem(key)])));

async function verifyBrowser(name) {
    console.log(`Verifying ${name}`);
    const browser = await ({ chromium, firefox, webkit })[name].launch(name === 'chromium'
        ? { args: ['--no-sandbox', '--enable-unsafe-swiftshader'] }
        : name === 'firefox' ? { firefoxUserPrefs: { 'webgl.force-enabled': true, 'gfx.webrender.software': true, 'gfx.x11-egl.force-enabled': true } } : {});
    const context = await browser.newContext({ viewport: { width: 1500, height: 1100 }, acceptDownloads: true });
    const page = await context.newPage();
    await instrument(page);
    const errors = [], logs = [];
    page.on('pageerror', error => { const message = error.stack || String(error); errors.push(message); logs.push(`PAGEERROR ${message}`); });
    page.on('console', message => {
        logs.push(message.text());
        if (message.type() === 'error' || /WebGL: INVALID_|WARNING: SHADER|Failed to load/.test(message.text())) { const detail = `${message.text()} at ${JSON.stringify(message.location())}`; errors.push(detail); logs.push(`BROWSER ERROR ${detail}`); }
    });
    try {
        await open(page);
        assert.equal(await page.evaluate(() => __audio.length), 0, 'Audio starts only after Start.');
        await start(page);
        await page.waitForFunction(() => __audio.some(audio => audio.state === 'running') && __sampleAudio() > 0, null, { timeout: 10000 });
        assert(logs.some(line => line.includes('3 music tracks loaded')), 'All music loads from shared virtual files.');
        assert(logs.some(line => line.includes('font.fnt] Font loaded successfully')), 'BMFont resolves its adjacent PNG.');
        assert(logs.some(line => line.includes('Loaded animation: skater_')), 'Skater animation clips load.');

        // Navigate the actual menus using short taps, save a changed control setting, then reload.
        for (const key of ['ArrowUp', 'ArrowUp', 'Enter', 'ArrowDown', 'ArrowRight', 'Escape']) await press(page, key);
        const settings = (await saves(page))['stickandpuck:v1:/saves/settings.json'];
        assert(settings, 'Settings menu writes browser storage.');
        await open(page); await start(page);
        assert.equal((await saves(page))['stickandpuck:v1:/saves/settings.json'], settings);

        await open(page, 'seasontable'); await start(page);
        const season = (await saves(page))['stickandpuck:v1:/saves/season.json'];
        assert(JSON.parse(season).games.length > 0, 'Season schedule is saved.');
        await open(page, 'seasontable'); await start(page);
        assert.equal((await saves(page))['stickandpuck:v1:/saves/season.json'], season, 'Season continues after reload.');
        await open(page, 'arcadesummary'); await start(page);
        const arcade = (await saves(page))['stickandpuck:v1:/saves/arcade.json'];
        assert(JSON.parse(arcade).opponents.length > 0, 'Arcade progress is saved.');
        await open(page, 'arcadesummary'); await start(page);
        assert.equal((await saves(page))['stickandpuck:v1:/saves/arcade.json'], arcade, 'Arcade continues after reload.');

        await open(page, 'practice'); await start(page);
        await verifyIceSurface(page);
        const before = await host(page, 'State');
        await page.keyboard.down('ArrowRight'); await page.waitForTimeout(400);
        assert.match(await host(page, 'Input'), /k:262/);
        await page.keyboard.up('ArrowRight');
        assert.notEqual((await host(page, 'State')).match(/STATE player[^\n]+/)[0], before.match(/STATE player[^\n]+/)[0], 'Keyboard moves the skater.');
        await page.mouse.move(750, 650); await page.mouse.down({ button: 'right' }); await page.waitForTimeout(100);
        assert.match(await host(page, 'Input'), /m:2/, 'Right mouse button passes without a context menu.');
        await page.mouse.up({ button: 'right' });
        await page.evaluate(() => {
            const connected = new Event('gamepadconnected');
            Object.defineProperty(connected, 'gamepad', { value: __pad }); dispatchEvent(connected);
            __pad.axes[0] = 0.75; __pad.buttons[0] = { pressed: true, touched: true, value: 1 }; __pad.timestamp++;
        });
        const gamepadInput = await waitForInput(page, /g0:80\/[\da-f]+\/3072,/);
        assert.match(gamepadInput, /g0:80\/[\da-f]+\/3072,/, 'Browser gamepad button and axis reach the simulation.');
        await page.evaluate(() => {
            __pad.connected = false;
            const disconnected = new Event('gamepaddisconnected');
            Object.defineProperty(disconnected, 'gamepad', { value: __pad }); dispatchEvent(disconnected);
            navigator.getGamepads = () => [null, null, null, null];
        });
        await page.waitForFunction(() => !/g0:/.test(__testHost.Input()), null, { timeout: 10000 });
        assert(!/g0:/.test(await host(page, 'Input')), 'Disconnected pad is removed.');

        const measurement = await page.evaluate(() => {
            const result = { tick: Number(__testHost.State().split(':', 1)[0]), time: performance.now(), timestamp: __frames.at(-1).timestamp };
            __frames.length = 0;
            return result;
        });
        await page.waitForTimeout(3000);
        const end = await page.evaluate(() => ({ tick: Number(__testHost.State().split(':', 1)[0]), time: performance.now(), samples: __frames }));
        const samples = end.samples;
        // SimClock discards time beyond 250 ms per callback after a stall. Software
        // rendering on CI can hit this limit; verify fixed steps against admitted
        // frame time while reporting the actual wall-clock rate separately.
        let previousTimestamp = measurement.timestamp, simulationSeconds = 0, clampedFrames = 0;
        for (const sample of samples) {
            const elapsed = Math.max(0, (sample.timestamp - previousTimestamp) / 1000);
            simulationSeconds += Math.min(elapsed, 0.25);
            if (elapsed > 0.25) clampedFrames++;
            previousTimestamp = sample.timestamp;
        }
        const work = samples.map(sample => sample.work).sort((a, b) => a - b);
        const performanceResult = { browser: name, version: browser.version(), frames: samples.length,
            observedFps: samples.length * 1000 / (end.time - measurement.time), simulationHz: (end.tick - measurement.tick) * 1000 / (end.time - measurement.time),
            simulationSteps: end.tick - measurement.tick, expectedSimulationSteps: simulationSeconds * 60, clampedFrames,
            frameWorkMeanMs: work.reduce((a, b) => a + b, 0) / work.length,
            frameWorkP95Ms: work[Math.floor(work.length * 0.95)], frameWorkMaxMs: work.at(-1) };
        await writeFile(resolve(artifacts, `${name}-performance.json`), JSON.stringify(performanceResult, null, 2));
        assert(samples.length > 0 && simulationSeconds > 0, 'The browser executes animation frames.');
        assert(Math.abs(performanceResult.simulationSteps - performanceResult.expectedSimulationSteps) <= 1,
            `Fixed simulation advances at 60 Hz with bounded stall catch-up: ${JSON.stringify(performanceResult)}`);
        await page.screenshot({ path: resolve(artifacts, `${name}-practice.png`) });

        const [download] = await Promise.all([page.waitForEvent('download'), page.locator('#download').click()]);
        const downloaded = resolve(artifacts, `${name}-recording.sapreplay`);
        await download.saveAs(downloaded);
        assert.match(gunzipSync(await readFile(downloaded)).toString(), /^SAPREPLAY /, 'Replay downloads as a real gzip replay.');
        await page.locator('#stop').click();
        await page.locator('#upload').setInputFiles(downloaded);
        await page.waitForFunction(() => document.querySelector('#status').textContent.startsWith('Replay uploaded'));
        await page.locator('#verify').click();
        await page.waitForFunction(() => document.querySelector('#status').textContent.startsWith('Replay verified') || document.querySelector('#status').textContent.includes('differs') || document.querySelector('#status').textContent.includes('failed'), { timeout: 60000 });
        assert.match(await page.locator('#status').textContent(), /^Replay verified/);
        const desktopPlayback = spawnSync('dotnet', [desktop], { cwd: artifacts, encoding: 'utf8', timeout: 120000,
            env: { ...environment, SAP_REPLAY: downloaded, SAP_SCREENSHOT: '1', SAP_SETTINGS: resolve(artifacts, 'fixture-settings.json') } });
        await writeFile(resolve(artifacts, `${name}-recording-desktop.log`), desktopPlayback.stdout + desktopPlayback.stderr);
        assert.equal(desktopPlayback.status, 0, 'Browser recording verifies on desktop.');

        const savedBeforeReplay = await saves(page);
        for (const fixture of fixtures) {
            await page.locator('#upload').setInputFiles(fixture.path);
            await page.waitForFunction(() => document.querySelector('#status').textContent.startsWith('Replay uploaded'));
            await page.locator('#verify').click();
            await page.waitForFunction(() => document.querySelector('#status').textContent.startsWith('Replay verified') || document.querySelector('#status').textContent.includes('differs') || document.querySelector('#status').textContent.includes('failed'), { timeout: 60000 });
            const replayStatus = await page.locator('#status').textContent();
            const actual = (await host(page, 'Checksums')).trim().split(/\r?\n/);
            await writeFile(resolve(artifacts, `${name}-${fixture.name}.checksums`), actual.join('\n') + '\n');
            assert.match(replayStatus, /^Replay verified/, `${fixture.name}: ${replayStatus}`);
            assert.deepEqual(actual, fixture.checksums, `${name} matches desktop at every one of ${fixture.steps} steps.`);
        }
        assert.deepEqual(await saves(page), savedBeforeReplay, 'Replays never modify settings or progress.');
        await page.locator('#upload').setInputFiles({ name: 'invalid.sapreplay', mimeType: 'application/octet-stream', buffer: Buffer.from('invalid') });
        await page.waitForFunction(() => document.querySelector('#status').textContent.startsWith('Replay upload failed'));
        // Freeze automatic scheduling, then draw at 1 ms intervals. A changed canvas while
        // State() stays identical proves interpolation is wired into real WebGL draw passes.
        await open(page, 'skating'); await start(page);
        await page.waitForTimeout(1500);
        const interpolation = await page.evaluate(async () => {
            window.__manualFrames = true;
            const api = (await getDotnetRuntime(0).getAssemblyExports('StickAndPuck.dll')).StickAndPuck.App.Core.BrowserHost;
            const canvas = document.querySelector('canvas');
            const timestamp = __frames.at(-1).timestamp;
            let previousState = '', previousImage = '', changedBetweenTicks = 0, image = '';
            for (let offset = 1; offset <= 40; offset++) {
                api.Frame(timestamp + offset);
                const state = api.State();
                image = canvas.toDataURL('image/png');
                if (state === previousState && image !== previousImage) changedBetweenTicks++;
                previousState = state; previousImage = image;
            }
            return { changedBetweenTicks, image };
        });
        assert(interpolation.changedBetweenTicks > 0, 'Rendered skaters move between simulation ticks.');
        await writeFile(resolve(artifacts, `${name}-interpolation.png`), Buffer.from(interpolation.image.split(',')[1], 'base64'));
        await host(page, 'Stop');
        assert.equal(errors.length, 0, errors.join('\n'));
        results.push({ ...performanceResult, checksumsCompared: fixtures.reduce((sum, fixture) => sum + fixture.steps, 0), audioPcmVerified: true, interpolatedFramesVerified: interpolation.changedBetweenTicks, input: ['keyboard', 'mouse', 'simulated standard gamepad'], persistence: ['settings', 'season', 'arcade'], errors });
        console.log(`PASS ${name}: storage, inputs, audio, replay transfer and ${results.at(-1).checksumsCompared} desktop checksums`);
        console.log(JSON.stringify(performanceResult));
    } catch (error) {
        const diagnostics = await page.evaluate(async () => {
            const api = (await getDotnetRuntime(0).getAssemblyExports('StickAndPuck.dll')).StickAndPuck.App.Core.BrowserHost;
            return { input: api.Input(), audio: __audio.map(audio => audio.state), audioTaps: __audioTaps.length, audioEnergy: __sampleAudio(),
                frames: __frames.length, pad: __pad };
        }).catch(error => ({ error: String(error) }));
        await writeFile(resolve(artifacts, `${name}-failure.json`), JSON.stringify(diagnostics, null, 2));
        failures.push({ browser: name, error: error.stack });
        console.error(`FAIL ${name}: ${error.stack}`);
        await page.screenshot({ path: resolve(artifacts, `${name}-failure.png`) }).catch(() => {});
    } finally {
        await writeFile(resolve(artifacts, `${name}.log`), logs.join('\n'));
        await writeFile(resolve(artifacts, 'results.json'), JSON.stringify({ results, failures }, null, 2));
        await browser.close();
    }
}

try {
    for (const name of (process.env.BROWSERS || 'chromium,firefox').split(',')) await verifyBrowser(name);
} finally { server.close(); }
if (failures.length) process.exitCode = 1;
