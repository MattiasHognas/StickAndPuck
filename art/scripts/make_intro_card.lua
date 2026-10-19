-- Turns the stats card PNGs from make_intro_card.py (A22) into editable .aseprite files, or exports them back.
-- mode=make (default): intro_card and intro_icons become .aseprite files indexed to the Stick and Puck
--   dusk palette, with a slice per card slot (crest wells, name plates, stripes, VS) and per icon cell
--   (stat_<name>, form_<name>, style_<name>), all named as in intro_card.json.
-- mode=export: saves both .aseprite files back to their PNGs after hand edits. The row rects in
--   intro_card.json come from make_intro_card.py; move a row there, not only in Aseprite.
-- Run: aseprite -b --script-param ui=<art/ui> --script-param pal=<.gpl> [--script-param mode=export] --script make_intro_card.lua
local p = app.params
local ui = p["ui"]
local mode = p["mode"] or "make"
local names = { "intro_card", "intro_icons" }

local function path(name, ext) return app.fs.joinPath(ui, name .. "." .. ext) end

if mode == "export" then
  for _, n in ipairs(names) do
    local spr = app.open(path(n, "aseprite"))
    spr:saveCopyAs(path(n, "png"))
    spr:close()
  end
  return
end

local f = io.open(path("intro_card", "json"), "r")
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
  spr:setPalette(Palette{ fromFile = p["pal"] })
  app.command.ChangePixelFormat{ format = "indexed", dithering = "none" }
  if n == "intro_card" then
    for k, r in pairs(meta.card.slots) do slice(spr, k, r) end
  else
    for _, group in ipairs({ "stat", "form", "style" }) do
      for k, r in pairs(meta.icons[group]) do
        if not (group == "style" and k == "forecheck_balanced") then -- an alias of balanced
          slice(spr, group .. "_" .. k, r)
        end
      end
    end
  end
  spr:saveAs(path(n, "aseprite"))
  spr:close()
end
