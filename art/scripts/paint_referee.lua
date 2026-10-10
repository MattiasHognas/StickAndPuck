-- Repaints the referee's sweater stripes (torso x 0-31, y 2-18 of the atlas) two texels wide,
-- the same as the sleeves, so a black and a white stripe both face the camera at rink distance;
-- four-texel stripes left one black band in front that merged with the outline (A4). Also adds a
-- cream glint on each side of the helmet strip (x 32-63, y 24-31), like the skaters' helmets.
-- Run: aseprite -b --script-param file=art/player/referee_ref.aseprite --script art/scripts/paint_referee.lua
--      then export: aseprite -b art/player/referee_ref.aseprite --save-as art/player/referee_ref_64.png
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
local BLACK, CREAM = idx("#211b28"), idx("#efe0c2")
local layer
for _, l in ipairs(spr.layers) do if l.name == "kit" then layer = l end end
local cel = layer:cel(1)
local img = cel.image:clone()
local function px(x, y, c) img:drawPixel(x - cel.position.x, y - cel.position.y, c) end
for y = 2, 18 do
  for x = 0, 31 do px(x, y, (x // 2) % 2 == 1 and BLACK or CREAM) end
end
px(34, 26, CREAM); px(35, 26, CREAM); px(45, 26, CREAM); px(46, 26, CREAM)   -- helmet glints
cel.image = img
spr:saveAs(p["file"])
