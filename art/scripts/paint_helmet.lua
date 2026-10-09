-- Repaints the helmet strip (x 32-63, y 24-31) of a skater atlas as a hockey helmet:
-- dark rim band, shell colour, vent ridges front and back, a small sticker in front and on
-- each side. Strip order across: right side, front, left side, back; top row = crown.
-- Run: aseprite -b --script-param file=skater_home.aseprite --script-param shell=#2f5f8f
--      --script-param rim=#2a2c40 --script-param accent=#f2c14e --script paint_helmet.lua
local p = app.params
local spr = app.open(p["file"])
local pal = spr.palettes[1]
local function idx(hex)
  local r, g, b = tonumber(hex:sub(2, 3), 16), tonumber(hex:sub(4, 5), 16), tonumber(hex:sub(6, 7), 16)
  for i = 0, #pal - 1 do
    local c = pal:getColor(i)
    if c.red == r and c.green == g and c.blue == b then return i end
  end
  error("colour not in palette: " .. hex)
end
local SHELL, RIM, ACC = idx(p["shell"]), idx(p["rim"]), idx(p["accent"])
local layer
for _, l in ipairs(spr.layers) do if l.name == "kit" then layer = l end end
local cel = layer:cel(1)
local img = cel.image:clone()
local X0, Y0 = 32 - cel.position.x, 24 - cel.position.y
local function px(x, y, c) img:drawPixel(X0 + x, Y0 + y, c) end
for y = 0, 7 do for x = 0, 31 do px(x, y, SHELL) end end
for x = 0, 31 do px(x, 7, RIM) end                              -- rim band
for y = 0, 3 do px(5, y, RIM); px(10, y, RIM) end               -- vent ridges over the front
for y = 0, 4 do px(21, y, RIM); px(26, y, RIM) end              -- and over the back
px(7, 4, ACC); px(8, 4, ACC)                                    -- front sticker
px(15, 3, ACC); px(0, 3, ACC)                                  -- side stickers
px(23, 1, RIM); px(24, 1, RIM)                                  -- back vent
cel.image = img
spr:saveAs(p["file"])
