-- Turns the dressing room PNGs from make_dressing_room.py into editable .aseprite files, or exports them back.
-- mode=make (default): the backdrop, its floor print overlay and the coach portraits become .aseprite
--   files indexed to the Stick and Puck dusk palette (the overlay is indexed like the backdrop, so its
--   pixels export exactly as the backdrop's do), with a slice per mood on coach_moods; the two jersey masks stay
--   RGBA (white ink the game tints with the club's kit colours).
-- mode=export: saves every dressing room .aseprite back to its PNG after hand edits.
-- Run: aseprite -b --script-param ui=<art/ui> --script-param pal=<.gpl> [--script-param mode=export] --script make_dressing_room.lua
local p = app.params
local ui = p["ui"]
local mode = p["mode"] or "make"
local names = { "dressing_room", "dressing_room_jersey", "dressing_room_trim", "dressing_room_floor", "coach_portrait", "coach_moods" }
local masks = { dressing_room_jersey = true, dressing_room_trim = true }
local moods = { "neutral", "pleased", "angry" }

local function path(name, ext) return app.fs.joinPath(ui, name .. "." .. ext) end

if mode == "export" then
  for _, n in ipairs(names) do
    local spr = app.open(path(n, "aseprite"))
    spr:saveCopyAs(path(n, "png"))
    spr:close()
  end
  return
end

for _, n in ipairs(names) do
  local spr = app.open(path(n, "png"))
  spr.layers[1].name = n
  if not masks[n] then
    spr:setPalette(Palette{ fromFile = p["pal"] })
    app.command.ChangePixelFormat{ format = "indexed", dithering = "none" }
  end
  if n == "coach_moods" then
    for i, m in ipairs(moods) do
      local s = spr:newSlice(Rectangle((i - 1) * 64, 0, 64, 80))
      s.name = m
    end
  end
  spr:saveAs(path(n, "aseprite"))
  spr:close()
end
