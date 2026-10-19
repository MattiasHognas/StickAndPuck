-- Turns the HUD PNGs in art/ui/ into editable .aseprite files, or exports them back.
-- mode=make (default): each PNG from make_ui.py becomes an .aseprite indexed to the Stick and
--   Puck dusk palette (the font stays RGBA: it is white ink the game tints), with slices for the
--   scorebug slots, the panels' 9-slice centre, every crest cell and every icon cell.
-- mode=export: saves every art/ui/*.aseprite back to its PNG after hand edits. If the font
--   changed, run `python3 art/scripts/make_ui.py fnt` afterwards to refresh font.fnt.
-- only=<name>: handles just that one file, e.g. only=title after `make_ui.py title`.
-- Run: aseprite -b --script-param ui=<art/ui> --script-param pal=<.gpl> [--script-param mode=export] [--script-param only=<name>] --script make_ui.lua
local p = app.params
local ui = p["ui"]
local mode = p["mode"] or "make"
local names = { "font", "scorebug", "panel", "panel_gold", "panel_red", "icons", "crests", "title" }
if p["only"] then names = { p["only"] } end

local function path(name, ext) return app.fs.joinPath(ui, name .. "." .. ext) end

if mode == "export" then
  for _, n in ipairs(names) do
    local spr = app.open(path(n, "aseprite"))
    spr:saveCopyAs(path(n, "png"))
    spr:close()
  end
  return
end

local f = io.open(path("ui", "json"), "r")
local meta = json.decode(f:read("a"))
f:close()

local function slice(spr, name, r)
  local s = spr:newSlice(Rectangle(r[1], r[2], r[3], r[4]))
  s.name = name
  return s
end

for _, n in ipairs(names) do
  local spr = app.open(path(n, "png"))
  spr.layers[1].name = n
  if n ~= "font" then
    spr:setPalette(Palette{ fromFile = p["pal"] })
    app.command.ChangePixelFormat{ format = "indexed", dithering = "none" }
  end
  if n == "scorebug" then
    for k, r in pairs(meta.scorebug.slots) do slice(spr, k, r) end
  elseif n == "icons" or n == "crests" then
    for k, r in pairs(meta[n].cells) do slice(spr, k, r) end
  elseif meta.panels[n] then
    local s = slice(spr, n, { 0, 0, spr.width, spr.height })
    local b = meta.panels[n].border
    s.center = Rectangle(b[1], b[2], spr.width - b[1] - b[3], spr.height - b[2] - b[4])
  end
  spr:saveAs(path(n, "aseprite"))
  spr:close()
end
