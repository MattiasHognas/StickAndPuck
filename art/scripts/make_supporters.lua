-- Turns the supporter sheets from make_supporters.py (art/crowd/supporters/{league}/{id}.png) into
-- editable .aseprite files beside them, or exports them back.
-- mode=make (default): each sheet becomes an .aseprite indexed to the Stick and Puck dusk palette, with a
--   slice for every frame in art/crowd/supporters.json (tifo_flat, scarf_wave0, flag_wave2, banner_sag, ...).
-- mode=export: saves every sheet's .aseprite back to its PNG after hand edits.
-- only=<id>: handles just that club, e.g. only=frolunda after `make_supporters.py frolunda`.
-- Run: aseprite -b --script-param crowd=<art/crowd> --script-param pal=<.gpl> [--script-param mode=export] [--script-param only=<id>] --script make_supporters.lua
local p = app.params
local crowd = p["crowd"]
local mode = p["mode"] or "make"
local only = p["only"]

local f = io.open(app.fs.joinPath(crowd, "supporters.json"), "r")
local meta = json.decode(f:read("a"))
f:close()

for _, league in ipairs({ "shl", "nhl" }) do
  local folder = app.fs.joinPath(crowd, "supporters", league)
  for _, file in ipairs(app.fs.listFiles(folder)) do
    local id = app.fs.fileTitle(file)
    if app.fs.fileExtension(file) == "png" and (not only or only == id) then
      local png = app.fs.joinPath(folder, id .. ".png")
      local ase = app.fs.joinPath(folder, id .. ".aseprite")
      if mode == "export" then
        local spr = app.open(ase)
        spr:saveCopyAs(png)
        spr:close()
      else
        local spr = app.open(png)
        spr.layers[1].name = id
        spr:setPalette(Palette{ fromFile = p["pal"] })
        app.command.ChangePixelFormat{ format = "indexed", dithering = "none" }
        for piece, info in pairs(meta.pieces) do
          for i, r in ipairs(info.rects) do
            local s = spr:newSlice(Rectangle(r[1], r[2], r[3], r[4]))
            s.name = piece .. "_" .. info.frames[i]
          end
        end
        spr:saveAs(ase)
        spr:close()
      end
    end
  end
end
