// The values the browser checks put on window inside the game page (see instrument() in verify.mjs,
// render-checks.mjs and verify-electron.mjs), declared so editors can type-check those scripts.

/** Every AudioContext the page created. */
declare var __audio: AudioContext[];
/** Sum of squared samples read from the analyser taps, to prove the game makes sound. */
declare var __audioEnergy: number;
/** AnalyserNodes tapping every node the game connects to an AudioContext destination. */
declare var __audioTaps: AnalyserNode[];
/** Adds the squared samples currently in every tap to __audioEnergy and returns the total. */
declare function __sampleAudio(): number;
/** The simulated standard gamepad navigator.getGamepads() returns. */
declare var __pad: {
    id: string;
    index: number;
    connected: boolean;
    mapping: GamepadMappingType;
    timestamp: number;
    axes: number[];
    buttons: { pressed: boolean; touched: boolean; value: number }[];
};
/** One entry per animation frame: its timestamp and the callback's work in ms. */
declare var __frames: { timestamp: number; work: number }[];
/** While true, animation frames are driven by the test instead of the browser. */
declare var __manualFrames: boolean | undefined;
/** Draw counts and colour masks recorded while a render check runs. */
declare var __renderCheck: { draws: number; lowPrecisionDraws: number; masks: unknown[] } | undefined;
/** Frames, dark frames and camera matrices recorded while the Electron motion check runs. */
declare var __motion: { frames: number; dark: number; cameras: number[][] } | undefined;
/** The game's BrowserHost exports (Start, Frame, Input, State, ...). */
declare var __testHost: any;
/** The .NET WebAssembly runtime the launcher started. */
declare function getDotnetRuntime(id: number): any;
