Browser verification uses the published site and actual desktop recordings.

Run `tools/verify-browser.sh` from a desktop session, or `xvfb-run -a tools/verify-browser.sh` on headless Linux. Prerequisites and coverage are documented in [Testing](../../docs/TESTING.md#browser-verification). The script installs the pinned npm dependencies, but browser binaries and system dependencies should be installed first with `npx playwright install --with-deps chromium firefox` from this directory.

`npm test` runs only the browser suite against an existing Release build and browser publish. `BROWSERS=chromium,firefox` selects engines. Reports, screenshots, performance samples, replay fixtures and every-step checksum comparisons are written to `TestResults/browser`.

For Electron on a real GPU, serve the published site and run `npm run test:electron -- /path/to/electron http://localhost:8000` from this directory. It opens a temporary window, checks high-precision 3D transforms and depth-only colour masks, and samples the ice during camera movement. Renderer details and results are saved with the browser reports.
