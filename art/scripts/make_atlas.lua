-- Turns a generated 64x64 skater atlas into an editable .aseprite file:
-- indexed to the Stick and Puck dusk palette, with a hidden "UV guides" layer that
-- outlines every body part's rectangle. Run: aseprite -b --script-param in=... --script-param out=... --script make_atlas.lua
local p = app.params
local spr = app.open(p["in"])
spr:setPalette(Palette{ fromFile = p["pal"] })
app.command.ChangePixelFormat{ format = "indexed", dithering = "none" }
spr.layers[1].name = "kit"
local rects = {
  {0,0,32,20},
  {32,0,32,10},
  {32,10,16,10},
  {48,10,16,14},
  {0,20,16,8},
  {16,20,16,8},
  {32,24,32,8},
  {0,28,32,14},
  {32,32,16,8},
  {48,32,16,8},
  {0,44,32,4},
  {32,40,16,8},
  {48,40,16,8},
  {0,48,32,16}
}
local guide = spr:newLayer(); guide.name = "UV guides"; guide.opacity = 160
local img = Image(spr.spec)
local idx = 22   -- light gold from the palette
for _, r in ipairs(rects) do
  local x0, y0, w, h = r[1], r[2], r[3], r[4]
  for x = x0, x0 + w - 1 do img:drawPixel(x, y0, idx); img:drawPixel(x, y0 + h - 1, idx) end
  for y = y0, y0 + h - 1 do img:drawPixel(x0, y, idx); img:drawPixel(x0 + w - 1, y, idx) end
end
spr:newCel(guide, 1, img, Point(0, 0))
guide.isVisible = false
spr:saveAs(p["out"])
