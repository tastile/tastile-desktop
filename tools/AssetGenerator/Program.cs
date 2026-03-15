using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

var assetsDir = args.Length > 0 ? args[0] : @"..\..\src\TastileDesktop\Assets";
assetsDir = Path.GetFullPath(assetsDir);
Directory.CreateDirectory(assetsDir);

var brandColor = Color.FromArgb(0, 120, 212); // #0078D4 Tastile Blue

Console.WriteLine($"Generating assets to: {assetsDir}");

// Generate PNG assets
GeneratePng(Path.Combine(assetsDir, "Square44x44Logo.png"), 44, brandColor);
GeneratePng(Path.Combine(assetsDir, "Square44x44Logo.targetsize-256.png"), 256, brandColor);
GeneratePng(Path.Combine(assetsDir, "Square150x150Logo.png"), 150, brandColor);
GeneratePng2(Path.Combine(assetsDir, "Wide310x150Logo.png"), 310, 150, brandColor);
GeneratePng(Path.Combine(assetsDir, "StoreLogo.png"), 50, brandColor);
GeneratePng2(Path.Combine(assetsDir, "SplashScreen.png"), 620, 300, brandColor);
GeneratePng(Path.Combine(assetsDir, "LockScreenLogo.png"), 24, brandColor);

// Generate ICO file
GenerateIco(Path.Combine(assetsDir, "tastile-tray.ico"), brandColor);

Console.WriteLine("Assets generated successfully!");

void GeneratePng(string path, int size, Color bgColor)
{
    using var bmp = new Bitmap(size, size);
    using var g = Graphics.FromImage(bmp);
    
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.TextRenderingHint = TextRenderingHint.AntiAlias;
    
    // Background
    using (var brush = new SolidBrush(bgColor))
    {
        g.FillRectangle(brush, 0, 0, size, size);
    }
    
    // Draw "T" text
    var fontSize = size * 0.6f;
    using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
    using var textBrush = new SolidBrush(Color.White);
    
    var text = "T";
    var textSize = g.MeasureString(text, font);
    var x = (size - textSize.Width) / 2;
    var y = (size - textSize.Height) / 2;
    
    g.DrawString(text, font, textBrush, x, y);
    
    // Save
    bmp.Save(path, ImageFormat.Png);
    Console.WriteLine($"Generated: {path}");
}

void GeneratePng2(string path, int width, int height, Color bgColor)
{
    using var bmp = new Bitmap(width, height);
    using var g = Graphics.FromImage(bmp);
    
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.TextRenderingHint = TextRenderingHint.AntiAlias;
    
    // Background
    using (var brush = new SolidBrush(bgColor))
    {
        g.FillRectangle(brush, 0, 0, width, height);
    }
    
    // Draw "T" text
    var fontSize = Math.Min(width, height) * 0.6f;
    using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
    using var textBrush = new SolidBrush(Color.White);
    
    var text = "T";
    var textSize = g.MeasureString(text, font);
    var x = (width - textSize.Width) / 2;
    var y = (height - textSize.Height) / 2;
    
    g.DrawString(text, font, textBrush, x, y);
    
    // Save
    bmp.Save(path, ImageFormat.Png);
    Console.WriteLine($"Generated: {path}");
}

void GenerateIco(string path, Color bgColor)
{
    var sizes = new[] { 16, 32, 48, 256 };
    
    // ICO file format
    using var ms = new MemoryStream();
    using var writer = new BinaryWriter(ms);
    
    // ICO header
    writer.Write((short)0); // Reserved
    writer.Write((short)1); // Type (1 = icon)
    writer.Write((short)sizes.Length); // Count
    
    var imageDataList = new List<byte[]>();
    var offset = 6 + sizes.Length * 16; // Header + ICONDIRENTRY
    
    foreach (var size in sizes)
    {
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        
        // Background
        using (var brush = new SolidBrush(bgColor))
        {
            g.FillRectangle(brush, 0, 0, size, size);
        }
        
        // Draw "T"
        var fontSize = size * 0.6f;
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        
        var text = "T";
        var textSize = g.MeasureString(text, font);
        var x = (size - textSize.Width) / 2;
        var y = (size - textSize.Height) / 2;
        
        g.DrawString(text, font, textBrush, x, y);
        
        // Save to PNG bytes
        using var pngMs = new MemoryStream();
        bmp.Save(pngMs, ImageFormat.Png);
        var pngBytes = pngMs.ToArray();
        imageDataList.Add(pngBytes);
        
        // ICONDIRENTRY
        writer.Write((byte)size); // Width
        writer.Write((byte)size); // Height
        writer.Write((byte)0); // Colors (0 = >256)
        writer.Write((byte)0); // Reserved
        writer.Write((short)1); // Color planes
        writer.Write((short)32); // Bits per pixel
        writer.Write(pngBytes.Length); // Size
        writer.Write(offset); // Offset
        
        offset += pngBytes.Length;
    }
    
    // Write image data
    foreach (var data in imageDataList)
    {
        writer.Write(data);
    }
    
    File.WriteAllBytes(path, ms.ToArray());
    Console.WriteLine($"Generated: {path}");
}
