using System.Globalization;
using CieloHud.Core.Guidance;

namespace CieloHud.App.Hud;

/// <summary>
/// Tactical HUD: reticle at the center, target marker offset by angular distance, edge arrow when out of view,
/// plain-words hint at the bottom. Draw only; all numbers come from <see cref="HudFrame"/>.
/// </summary>
public sealed class HudDrawable : IDrawable
{
    private static readonly Color Mint = Color.FromArgb("#7CFFB2");
    private static readonly Color Cyan = Color.FromArgb("#4DD2FF");
    private static readonly Color Amber = Color.FromArgb("#FFC42E");
    private static readonly Color Alert = Color.FromArgb("#FF5C5C");
    private static readonly Color Text = Color.FromArgb("#E8EEF4");
    private static readonly Color Muted = Color.FromArgb("#8FA0B0");
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private const string FontRegular = "ChakraPetch";
    private const string FontBold = "ChakraPetchBold";

    public HudFrame Frame { get; set; } = new();

    public void Draw(ICanvas canvas, RectF r)
    {
        canvas.SaveState();
        canvas.Antialias = true;

        DrawReticle(canvas, r, Frame.Guidance?.IsOnTarget == true);

        if (!Frame.Ready)
        {
            DrawCenteredText(canvas, r, StatusText(), r.Center.Y + 70, 15, Muted, FontRegular);
        }
        else if (Frame.TargetBelowHorizon)
        {
            DrawCenteredText(canvas, r, $"{Frame.TargetName} está bajo el horizonte", r.Center.Y + 70, 16, Amber, FontBold);
            DrawCenteredText(canvas, r, $"altura {Frame.Target!.Value.AltitudeDegrees.ToString("F0", Culture)}°", r.Center.Y + 94, 13, Muted, FontRegular);
        }
        else
        {
            var g = Frame.Guidance!.Value;
            var p = HudProjection.Project(g, Frame.Pointing!.Value.AltitudeDegrees, r.Width, r.Height);
            if (p.InView)
                DrawMarker(canvas, (float)p.X, (float)p.Y, g.IsOnTarget);
            else
                DrawEdgeArrow(canvas, (float)p.X, (float)p.Y, (float)p.DirectionDegrees);
            DrawHint(canvas, r, g);
        }

        canvas.RestoreState();
    }

    private string StatusText()
    {
        if (!Frame.HasLocation) return "Buscando tu posición…";
        if (Frame.Pointing is null) return "Esperando sensores…";
        if (Frame.Target is null) return Frame.Unavailable ?? "Objetivo no disponible";
        return "";
    }

    private void DrawReticle(ICanvas canvas, RectF r, bool locked)
    {
        var cx = r.Center.X;
        var cy = r.Center.Y;
        var color = locked ? Mint : Cyan;
        var radius = 34f;

        if (locked)
        {
            var pulse = (float)Frame.Pulse;
            canvas.StrokeSize = 2 + 6 * (1 - pulse);
            canvas.StrokeColor = color.WithAlpha(0.25f + 0.35f * (1 - pulse));
            canvas.DrawCircle(cx, cy, radius + 10 + 18 * pulse);
        }

        canvas.StrokeSize = locked ? 3 : 1.5f;
        canvas.StrokeColor = color.WithAlpha(locked ? 1f : 0.8f);
        canvas.DrawCircle(cx, cy, radius);

        // Four ticks, leaving the center open.
        var gap = radius + 6;
        var len = 16;
        canvas.DrawLine(cx - gap - len, cy, cx - gap, cy);
        canvas.DrawLine(cx + gap, cy, cx + gap + len, cy);
        canvas.DrawLine(cx, cy - gap - len, cx, cy - gap);
        canvas.DrawLine(cx, cy + gap, cx, cy + gap + len);

        canvas.FillColor = color.WithAlpha(0.9f);
        canvas.FillCircle(cx, cy, 2);
    }

    private void DrawMarker(ICanvas canvas, float x, float y, bool locked)
    {
        var color = locked ? Mint : Amber;
        var d = 18f;

        var path = new PathF();
        path.MoveTo(x, y - d);
        path.LineTo(x + d * 0.72f, y);
        path.LineTo(x, y + d);
        path.LineTo(x - d * 0.72f, y);
        path.Close();

        canvas.FillColor = color.WithAlpha(locked ? 0.5f : 0.25f);
        canvas.FillPath(path);
        canvas.StrokeSize = 2;
        canvas.StrokeColor = color;
        canvas.DrawPath(path);

        canvas.FillColor = Colors.White;
        canvas.FillCircle(x, y, 3);

        // Bracket corners.
        var b = d * 1.5f;
        var l = d * 0.5f;
        canvas.StrokeColor = Colors.White.WithAlpha(0.7f);
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(x - b, y - b, x - b + l, y - b); canvas.DrawLine(x - b, y - b, x - b, y - b + l);
        canvas.DrawLine(x + b, y - b, x + b - l, y - b); canvas.DrawLine(x + b, y - b, x + b, y - b + l);
        canvas.DrawLine(x - b, y + b, x - b + l, y + b); canvas.DrawLine(x - b, y + b, x - b, y + b - l);
        canvas.DrawLine(x + b, y + b, x + b - l, y + b); canvas.DrawLine(x + b, y + b, x + b, y + b - l);

        canvas.FontColor = color;
        canvas.FontSize = 14;
        canvas.Font = new Microsoft.Maui.Graphics.Font(FontBold);
        canvas.DrawString(Frame.TargetName.ToUpperInvariant(), x - 80, y + b + 6, 160, 20, HorizontalAlignment.Center, VerticalAlignment.Top);
    }

    private void DrawEdgeArrow(ICanvas canvas, float x, float y, float directionDegrees)
    {
        // Screen y grows downwards, so a positive (up) direction becomes a negative rotation.
        var angle = -directionDegrees;
        canvas.SaveState();
        canvas.Translate(x, y);
        canvas.Rotate(angle);

        var path = new PathF();
        path.MoveTo(22, 0);
        path.LineTo(-10, -14);
        path.LineTo(-4, 0);
        path.LineTo(-10, 14);
        path.Close();

        canvas.FillColor = Amber.WithAlpha(0.35f + 0.4f * (float)Frame.Pulse);
        canvas.FillPath(path);
        canvas.StrokeColor = Amber;
        canvas.StrokeSize = 2;
        canvas.DrawPath(path);
        canvas.RestoreState();

        canvas.FontColor = Amber;
        canvas.FontSize = 13;
        canvas.Font = new Microsoft.Maui.Graphics.Font(FontBold);
        var label = Frame.TargetName.ToUpperInvariant();
        var lx = Math.Clamp(x - 60, 8, 1e6);
        canvas.DrawString(label, (float)lx, y + 26, 120, 18, HorizontalAlignment.Center, VerticalAlignment.Top);
    }

    private void DrawHint(ICanvas canvas, RectF r, Guidance g)
    {
        string main, sub;
        Color color;
        if (g.IsOnTarget)
        {
            main = "AQUÍ";
            sub = "Baja el móvil y mira justo ahí";
            color = Mint;
        }
        else
        {
            var turn = Math.Abs(g.AzimuthDeltaDegrees) < 1.5 ? null : (g.AzimuthDeltaDegrees > 0 ? "derecha" : "izquierda") + " " + Math.Abs(g.AzimuthDeltaDegrees).ToString("F0", Culture) + "°";
            var tilt = Math.Abs(g.AltitudeDeltaDegrees) < 1.5 ? null : (g.AltitudeDeltaDegrees > 0 ? "sube" : "baja") + " " + Math.Abs(g.AltitudeDeltaDegrees).ToString("F0", Culture) + "°";
            main = string.Join("  ·  ", new[] { turn, tilt }.Where(s => s is not null));
            sub = $"{Frame.TargetName}: acimut {Frame.Target!.Value.AzimuthDegrees.ToString("F0", Culture)}°, altura {Frame.Target.Value.AltitudeDegrees.ToString("F0", Culture)}°";
            color = g.AngularDistanceDegrees < 15 ? Amber : Text;
        }

        var y = r.Bottom - 150;
        DrawCenteredText(canvas, r, main, y, g.IsOnTarget ? 34 : 24, color, FontBold);
        DrawCenteredText(canvas, r, sub, y + 44, 13, Muted, FontRegular);
    }

    private static void DrawCenteredText(ICanvas canvas, RectF r, string text, float y, float size, Color color, string font)
    {
        canvas.FontColor = color;
        canvas.FontSize = size;
        canvas.Font = new Microsoft.Maui.Graphics.Font(font);
        canvas.DrawString(text, r.Left + 16, y, r.Width - 32, size + 12, HorizontalAlignment.Center, VerticalAlignment.Top);
    }
}
