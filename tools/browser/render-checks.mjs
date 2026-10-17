import assert from 'node:assert/strict';

// Observe the real WebGL calls without changing shader sources or draw state.
export async function instrumentRendering(page) {
    await page.addInitScript(() => {
        const sources = new WeakMap(), programs = new WeakMap();
        const prototype = WebGLRenderingContext.prototype;
        const source = prototype.shaderSource;
        prototype.shaderSource = function(shader, code) {
            sources.set(shader, code);
            return source.call(this, shader, code);
        };
        const link = prototype.linkProgram;
        prototype.linkProgram = function(program) {
            const vertex = this.getAttachedShaders(program).find(shader => this.getShaderParameter(shader, this.SHADER_TYPE) === this.VERTEX_SHADER);
            programs.set(program, sources.get(vertex) || '');
            return link.call(this, program);
        };
        for (const name of ['drawArrays', 'drawElements']) {
            const draw = prototype[name];
            prototype[name] = function(...args) {
                if (window.__renderCheck && this.isEnabled(this.DEPTH_TEST)) {
                    const vertex = programs.get(this.getParameter(this.CURRENT_PROGRAM)) || '';
                    if (!vertex.includes('precision highp float')) window.__renderCheck.lowPrecisionDraws++;
                    window.__renderCheck.draws++;
                }
                return draw.apply(this, args);
            };
        }
        const mask = prototype.colorMask;
        prototype.colorMask = function(...args) {
            if (window.__renderCheck) {
                window.__renderCheck.masks.push(args);
            }
            return mask.apply(this, args);
        };
    });
}

// raylib's uncoloured meshes use a context-wide generic vertex colour, which a
// VAO does not save. Exercise the WebGL default (black) instead of inheriting
// whichever value UploadMesh happened to leave behind.
export async function verifyIceSurface(page) {
    const result = await page.evaluate(async () => {
        const api = (await getDotnetRuntime(0).getAssemblyExports('StickAndPuck.dll')).StickAndPuck.App.Core.BrowserHost;
        const canvas = document.querySelector('canvas');
        const gl = canvas.getContext('webgl');
        const original = gl.getVertexAttrib(3, gl.CURRENT_VERTEX_ATTRIB);
        const samples = [];
        window.__renderCheck = { draws: 0, lowPrecisionDraws: 0, masks: [] };
        try {
            gl.vertexAttrib4f(3, 0, 0, 0, 1);
            api.Frame(performance.now());
            for (const y of [0.4, 0.5]) {
                for (const x of [0.2, 0.35, 0.65, 0.8]) {
                    const pixel = new Uint8Array(4);
                    gl.readPixels(Math.floor(canvas.width * x), Math.floor(canvas.height * y), 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixel);
                    samples.push(Array.from(pixel));
                }
            }
        } finally { gl.vertexAttrib4fv(3, original); }
        const checks = window.__renderCheck;
        delete window.__renderCheck;
        return { samples, checks };
    });
    const { samples: pixels, checks } = result;
    assert(checks.draws > 0, 'The rendering probe exercises 3D draw calls.');
    assert.equal(checks.lowPrecisionDraws, 0, 'Every 3D pass uses a high-precision vertex shader.');
    assert(checks.masks.filter(mask => mask.every(value => !value)).length >= 2, 'Both depth-only passes disable all colour channels.');
    assert(checks.masks.every(mask => mask.every(Boolean) || mask.every(value => !value)), 'Colour masks have no mis-marshalled channels.');
    const bright = pixels.filter(([r, g, b]) => r > 128 && g > 128 && b > 128).length;
    assert(bright >= 5, `Ice remains bright after resetting the generic vertex colour: ${JSON.stringify(pixels)}`);
    return pixels;
}
