-- Repaints the helmet strip (x 32-63, y 24-31) of a skater atlas as a hockey helmet that reads
-- as a hard shell at rink distance (A4): a dark two-row rim over the brows and ears, a light
-- ridge stripe from the front over the crown to the back, and a glint on each side. Strip order
-- across: right side, front, left side, back; top row = crown. make_kits.py paints club helmets
-- the same way, so keep the two in step.
-- Run: aseprite -b --script-param file=skater_home.aseprite --script-param shell=#2f5f8f
--      --script-param rim=#2a2c40 --script-param ridge=#efe0c2 --script paint_helmet.lua
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
local SHELL, RIM, RIDGE = idx(p["shell"]), idx(p["rim"]), idx(p["ridge"] or "#efe0c2")
local layer
for _, l in ipairs(spr.layers) do if l.name == "kit" then layer = l end end
local cel = layer:cel(1)
local img = cel.image:clone()
local X0, Y0 = 32 - cel.position.x, 24 - cel.position.y
local function px(x, y, c) img:drawPixel(X0 + x, Y0 + y, c) end
for y = 0, 7 do for x = 0, 31 do px(x, y, SHELL) end end
for x = 0, 31 do px(x, 6, RIM); px(x, 7, RIM) end                -- rim band
for y = 0, 4 do                                                 -- ridge over the front, crown and back
  for _, x in ipairs({6, 7, 8, 22, 23, 24}) do px(x, y, RIDGE) end
end
px(2, 2, RIDGE); px(3, 2, RIDGE); px(13, 2, RIDGE); px(14, 2, RIDGE)   -- side glints
cel.image = img
spr:saveAs(p["file"])
