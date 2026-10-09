using System.Security.Cryptography;
using SkiaSharp;

namespace AdminIdentity.Infra;

internal sealed class CaptchaImageGenerator
{
    private const int Width = 160;
    private const int Height = 60;
    private const int CharCount = 4;
    private const string Chars = "ACDEFHJKLMNPQRSTUVWXYZ345679";

    public (string Code, byte[] PngBytes) Generate()
    {
        var code = GenerateCode();
        using var bitmap = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(new SKColor(248, 248, 248));

        DrawNoiseLines(canvas);
        DrawNoiseDots(canvas);
        DrawText(canvas, code);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return (code, data.ToArray());
    }

    private static string GenerateCode()
    {
        var chars = new char[CharCount];
        for (var i = 0; i < CharCount; i++)
        {
            chars[i] = Chars[RandomNumberGenerator.GetInt32(Chars.Length)];
        }
        return new string(chars);
    }

    private static void DrawNoiseLines(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            StrokeWidth = 1,
            IsAntialias = true
        };

        for (var i = 0; i < 3; i++)
        {
            paint.Color = new SKColor(
                (byte)RandomNumberGenerator.GetInt32(180, 230),
                (byte)RandomNumberGenerator.GetInt32(180, 230),
                (byte)RandomNumberGenerator.GetInt32(180, 230));

            var x1 = RandomNumberGenerator.GetInt32(Width);
            var y1 = RandomNumberGenerator.GetInt32(Height);
            var x2 = RandomNumberGenerator.GetInt32(Width);
            var y2 = RandomNumberGenerator.GetInt32(Height);
            canvas.DrawLine(x1, y1, x2, y2, paint);
        }
    }

    private static void DrawNoiseDots(SKCanvas canvas)
    {
        using var paint = new SKPaint();
        for (var i = 0; i < 30; i++)
        {
            paint.Color = new SKColor(
                (byte)RandomNumberGenerator.GetInt32(160, 220),
                (byte)RandomNumberGenerator.GetInt32(160, 220),
                (byte)RandomNumberGenerator.GetInt32(160, 220));
            var x = RandomNumberGenerator.GetInt32(Width);
            var y = RandomNumberGenerator.GetInt32(Height);
            canvas.DrawPoint(x, y, paint);
        }
    }

    private static void DrawText(SKCanvas canvas, string code)
    {
        var totalCharWidth = Width - 20;
        var charSlot = totalCharWidth / code.Length;
        using var textPaint = new SKPaint { IsAntialias = true };

        for (var i = 0; i < code.Length; i++)
        {
            var fontSize = RandomNumberGenerator.GetInt32(28, 34);

            using var typeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                                 ?? SKTypeface.Default;
            using var font = new SKFont(typeface, fontSize);

            var r = (byte)RandomNumberGenerator.GetInt32(10, 80);
            var g = (byte)RandomNumberGenerator.GetInt32(10, 80);
            var b = (byte)RandomNumberGenerator.GetInt32(10, 80);
            textPaint.Color = new SKColor(r, g, b);

            var ch = code[i].ToString();
            var charWidthMeasured = font.MeasureText(ch, textPaint);

            var x = 10 + i * charSlot + (charSlot - (int)charWidthMeasured) / 2;
            var y = Height / 2f + fontSize * 0.35f + RandomNumberGenerator.GetInt32(-3, 4);

            canvas.Save();
            var angle = RandomNumberGenerator.GetInt32(-10, 11);
            canvas.RotateDegrees(angle, x + charWidthMeasured / 2f, y);
            canvas.DrawText(ch, x, y, SKTextAlign.Left, font, textPaint);
            canvas.Restore();
        }
    }
}
