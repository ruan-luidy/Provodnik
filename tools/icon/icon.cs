#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property PublishAot=false
#:property PublishTrimmed=false
#:package MahApps.Metro.IconPacks.PhosphorIcons@6.2.1
// Ícone do Проводник igual ao da Связь: quadrado arredondado no degradê azul da casa (#4A8CD6 -> #2A5F9E) com o
// símbolo Phosphor branco no meio (a Связь usa o abacate; aqui é a bússola do guia, CompassFill).
//   dotnet run tools/icon/icon.cs -- ico <Glifo> src/Provodnik.Wpf/Assets/provodnik.ico   (também grava o provodnik.png de 512)
//   dotnet run tools/icon/icon.cs -- preview prévia.png <Glifo> <Glifo> ...          (opções lado a lado)
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MahApps.Metro.IconPacks;

var thread = new Thread(() =>
{
  if (args[0] == "preview")
  {
    var glyphs = args.Skip(2).ToArray();
    var sheet = new DrawingVisual();
    using (var dc = sheet.RenderOpen())
    {
      dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x24)), null, new Rect(0, 0, glyphs.Length * 200, 200));
      dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3)), null, new Rect(0, 200, glyphs.Length * 200, 60));
      for (var i = 0; i < glyphs.Length; i++)
      {
        var x = i * 200;
        dc.DrawImage(Render(glyphs[i], 128), new Rect(x + 36, 16, 128, 128));
        dc.DrawImage(Render(glyphs[i], 32), new Rect(x + 60, 160, 32, 32));
        dc.DrawImage(Render(glyphs[i], 16), new Rect(x + 120, 168, 16, 16));
        var label = new FormattedText(glyphs[i], System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, Brushes.White, 1);
        dc.DrawText(label, new Point(x + 100 - label.Width / 2, 196 - 20));
        dc.DrawImage(Render(glyphs[i], 32), new Rect(x + 60, 214, 32, 32));
        dc.DrawImage(Render(glyphs[i], 16), new Rect(x + 120, 222, 16, 16));
      }
    }
    var bmp = new RenderTargetBitmap(glyphs.Length * 200, 260, 96, 96, PixelFormats.Pbgra32);
    bmp.Render(sheet);
    File.WriteAllBytes(args[1], Png(bmp));
    return;
  }

  var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
  var pngs = sizes.Select(s => Png(Render(args[1], s))).ToList();
  using var fs = File.Create(args[2]);
  using var w = new BinaryWriter(fs);
  w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
  var offset = 6 + 16 * sizes.Length;
  for (var i = 0; i < sizes.Length; i++)
  {
    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
    w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
    w.Write(pngs[i].Length); w.Write(offset);
    offset += pngs[i].Length;
  }
  foreach (var png in pngs)
    w.Write(png);
  File.WriteAllBytes(Path.ChangeExtension(args[2], ".png"), Png(Render(args[1], 512)));
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();
Console.WriteLine("ok");

static BitmapSource Render(string glyph, int size)
{
  var visual = new DrawingVisual();
  using (var dc = visual.RenderOpen())
  {
    // igual ao Otmetka: quadrado com ~22% de raio no degradê azul de cima pra baixo e o símbolo branco no meio
    var inset = size * 0.04;
    var box = new Rect(inset, inset, size - 2 * inset, size - 2 * inset);
    var radius = box.Width * 0.22;
    dc.DrawRoundedRectangle(new LinearGradientBrush(Hex("#4A8CD6"), Hex("#2A5F9E"), 90), null, box, radius, radius);

    var kind = Enum.Parse<PackIconPhosphorIconsKind>(glyph);
    var geometry = Geometry.Parse(new PackIconPhosphorIcons { Kind = kind }.Data);
    var bounds = geometry.Bounds;
    var target = box.Width * 0.62;
    var scale = target / Math.Max(bounds.Width, bounds.Height);
    var group = new TransformGroup();
    group.Children.Add(new TranslateTransform(-bounds.X - bounds.Width / 2, -bounds.Y - bounds.Height / 2));
    group.Children.Add(new ScaleTransform(scale, scale));
    group.Children.Add(new TranslateTransform(size / 2.0, size / 2.0));
    dc.PushTransform(group);
    dc.DrawGeometry(Brushes.White, null, geometry);
    dc.Pop();
  }
  var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
  bmp.Render(visual);
  return bmp;
}

static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

static byte[] Png(BitmapSource bmp)
{
  var enc = new PngBitmapEncoder();
  enc.Frames.Add(BitmapFrame.Create(bmp));
  using var ms = new MemoryStream();
  enc.Save(ms);
  return ms.ToArray();
}
