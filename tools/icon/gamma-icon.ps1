# O ícone do Проводник é o átomo verde do próprio GAMMA (G.A.M.M.A\modpack_data\modpack_icon.ico, só 256x256).
# Gera o .ico com todos os tamanhos (16 a 256, PNG dentro) pra não borrar na barra de tarefas, e o .png de 256.
#   .\tools\icon\gamma-icon.ps1 -Source "D:\GAMMA\.Grok's Modpack Installer\G.A.M.M.A\modpack_data\modpack_icon.ico"
param(
  [Parameter(Mandatory)] [string]$Source,
  [string]$Out = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'src\Provodnik.Wpf\Assets')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

$stream = [IO.File]::OpenRead($Source)
$decoder = [Windows.Media.Imaging.BitmapDecoder]::Create($stream, [Windows.Media.Imaging.BitmapCreateOptions]::None, [Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
$stream.Close()
$big = $decoder.Frames | Sort-Object PixelWidth -Descending | Select-Object -First 1

# o átomo tem muita borda vazia e um brilho fraco em volta: em 16 px ele sumia. Recorta no que tem alfa de verdade
# (acima de $Threshold), num quadrado com uma folguinha, antes de reduzir.
$Threshold = 70
$bgra = [Windows.Media.Imaging.FormatConvertedBitmap]::new($big, [Windows.Media.PixelFormats]::Bgra32, $null, 0)
$w = $bgra.PixelWidth; $h = $bgra.PixelHeight
$pixels = [byte[]]::new($w * $h * 4)
$bgra.CopyPixels($pixels, $w * 4, 0)
$left = $w; $top = $h; $right = 0; $bottom = 0
for ($y = 0; $y -lt $h; $y++) {
  for ($x = 0; $x -lt $w; $x++) {
    if ($pixels[($y * $w + $x) * 4 + 3] -gt $Threshold) {
      if ($x -lt $left) { $left = $x }; if ($x -gt $right) { $right = $x }
      if ($y -lt $top) { $top = $y }; if ($y -gt $bottom) { $bottom = $y }
    }
  }
}
$side = [Math]::Max($right - $left, $bottom - $top) + 6
$cx = ($left + $right) / 2; $cy = ($top + $bottom) / 2
$x0 = [int][Math]::Max(0, [Math]::Min($w - $side, $cx - $side / 2))
$y0 = [int][Math]::Max(0, [Math]::Min($h - $side, $cy - $side / 2))
$big = [Windows.Media.Imaging.CroppedBitmap]::new($big, [Windows.Int32Rect]::new($x0, $y0, [Math]::Min($side, $w), [Math]::Min($side, $h)))
"recorte: $x0,$y0 lado $side (de ${w}x$h)"

function Png([Windows.Media.Imaging.BitmapSource]$image) {
  $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
  $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($image))
  $memory = [IO.MemoryStream]::new()
  $encoder.Save($memory)
  return , $memory.ToArray()
}

function Scaled([int]$size) {
  # desenha em alta qualidade (Fant) pro tamanho pedido
  $visual = [Windows.Media.DrawingVisual]::new()
  $context = $visual.RenderOpen()
  [Windows.Media.RenderOptions]::SetBitmapScalingMode($visual, [Windows.Media.BitmapScalingMode]::Fant)
  # pequeno, o brilho fino sumia: desenhado mais de uma vez por cima, o átomo engrossa e fica visível na barra
  $layers = if ($size -le 24) { 3 } elseif ($size -le 48) { 2 } else { 1 }
  for ($i = 0; $i -lt $layers; $i++) { $context.DrawImage($big, [Windows.Rect]::new(0, 0, $size, $size)) }
  $context.Close()
  $target = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
  $target.Render($visual)
  return $target
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = foreach ($size in $sizes) { , (Png (Scaled $size)) }

# ICO: cabeçalho, uma entrada por tamanho (256 vira 0) e os PNGs em seguida
$ico = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($ico)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $side = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
  $writer.Write([byte]$side); $writer.Write([byte]$side); $writer.Write([byte]0); $writer.Write([byte]0)
  $writer.Write([uint16]1); $writer.Write([uint16]32)
  $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
  $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write($image) }
$writer.Flush()

[IO.File]::WriteAllBytes((Join-Path $Out 'provodnik.ico'), $ico.ToArray())
[IO.File]::WriteAllBytes((Join-Path $Out 'provodnik.png'), (Png $big))
"provodnik.ico ($($sizes -join ', ')) e provodnik.png em $Out"
