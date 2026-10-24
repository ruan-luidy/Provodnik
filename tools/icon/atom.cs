#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property PublishAot=false
#:property PublishTrimmed=false
// Ícone do Проводник: o átomo do GAMMA (três órbitas, elétrons, núcleo) desenhado em vetor no verde do modpack_icon, nítido até
// em 16 px (o PNG original do GAMMA é um brilho difuso que vira borrão pequeno). Duas versões:
//   solto: só o átomo, com um brilho leve nos tamanhos grandes;
//   tile:  o átomo num quadrado escuro arredondado.
//   dotnet run tools/icon/atom.cs -- ico solto|tile src/Provodnik.Wpf/Assets/provodnik.ico   (grava também o .png de 256)
//   dotnet run tools/icon/atom.cs -- preview prévia.png
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

var green = Color.FromRgb(0x8C, 0xF0, 0x3C);
int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];

var thread = new Thread(() =>
{
  if (args[0] == "preview")
  {
    var sheet = new DrawingVisual();
    using (var dc = sheet.RenderOpen())
    {
      dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x24)), null, new Rect(0, 0, 900, 330));
      var y = 10.0;
      foreach (var variant in new[] { "solto", "tile" })
      {
        var x = 10.0;
        foreach (var size in new[] { 16, 24, 32, 48 })
        {
          dc.DrawImage(Render(variant, size), new Rect(x, y, size, size));
          dc.DrawImage(Render(variant, size), new Rect(x, y + 56, size * 2, size * 2));
          x += size * 2 + 24;
        }
        dc.DrawImage(Render(variant, 128), new Rect(x, y, 128, 128));
        y += 160;
      }
    }
    var bmp = new RenderTargetBitmap(900, 330, 96, 96, PixelFormats.Pbgra32);
    bmp.Render(sheet);
    File.WriteAllBytes(args[1], Png(bmp));
    return;
  }

  var images = sizes.Select(s => Png(Render(args[1], s))).ToList();
  using var ico = new MemoryStream();
  using var w = new BinaryWriter(ico);
  w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length);
  var offset = 6 + 16 * sizes.Length;
  for (var i = 0; i < sizes.Length; i++)
  {
    var side = sizes[i] >= 256 ? 0 : sizes[i];
    w.Write((byte)side); w.Write((byte)side); w.Write((byte)0); w.Write((byte)0);
    w.Write((ushort)1); w.Write((ushort)32); w.Write(images[i].Length); w.Write(offset);
    offset += images[i].Length;
  }
  foreach (var image in images)
    w.Write(image);
  File.WriteAllBytes(args[2], ico.ToArray());
  File.WriteAllBytes(Path.ChangeExtension(args[2], ".png"), Png(Render(args[1], 256)));
  Console.WriteLine($"ok {args[2]}");
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();

BitmapSource Render(string variant, int size)
{
  var visual = new DrawingVisual();
  var tile = variant == "tile";
  var host = new ContainerVisual();
  using (var dc = visual.RenderOpen())
  {
    if (tile)
    {
      var radius = size * 0.22;
      var back = new LinearGradientBrush(Color.FromRgb(0x23, 0x30, 0x1A), Color.FromRgb(0x10, 0x16, 0x0C), 90);
      dc.DrawRoundedRectangle(back, null, new Rect(0, 0, size, size), radius, radius);
    }

    // o átomo do GAMMA: três órbitas (uma quase em pé, duas deitadas cruzando), elétrons nas pontas e o núcleo
    var area = tile ? size * 0.70 : size * (size <= 24 ? 1.0 : 0.94);
    var c = new Point(size / 2.0, size / 2.0);
    var rx = area * 0.47;
    var ry = area * 0.17;
    var stroke = size <= 24 ? Math.Max(1.15, size * 0.07) : size * 0.055;
    var pen = new Pen(new SolidColorBrush(green), stroke);
    double[] angles = [-75, 25, -30];
    foreach (var angle in angles)
    {
      dc.PushTransform(new RotateTransform(angle, c.X, c.Y));
      dc.DrawEllipse(null, pen, c, rx - stroke / 2, ry);
      dc.Pop();
    }

    // elétrons nas pontas das órbitas (como no do GAMMA); pequeno demais, só órbitas e núcleo pra não embolar
    if (size > 24)
    {
      var electron = size * 0.065;
      double[] tips = [1, 1, -1];
      for (var i = 0; i < angles.Length; i++)
      {
        var tip = new RotateTransform(angles[i], c.X, c.Y).Transform(new Point(c.X + tips[i] * (rx - stroke / 2), c.Y));
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xB8, 0xFF, 0x6A)), null, tip, electron, electron);
      }
    }
    var nucleus = size <= 24 ? Math.Max(1.7, size * 0.12) : size * 0.1;
    var glow = new RadialGradientBrush(Color.FromRgb(0xD8, 0xFF, 0x9A), green) { GradientOrigin = new Point(0.4, 0.35) };
    dc.DrawEllipse(glow, null, c, nucleus, nucleus);
  }
  host.Children.Add(visual);
  // brilho verde leve nos tamanhos grandes (o do GAMMA é todo brilho)
  if (size >= 48)
    visual.Effect = new DropShadowEffect { Color = green, BlurRadius = size * 0.09, ShadowDepth = 0, Opacity = 0.6 };
  var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
  bmp.Render(host);
  return bmp;
}
static byte[] Png(BitmapSource image)
{
  var encoder = new PngBitmapEncoder();
  encoder.Frames.Add(BitmapFrame.Create(image));
  using var memory = new MemoryStream();
  encoder.Save(memory);
  return memory.ToArray();
}
