using System.Globalization;
using CieloHud.Core.Guidance;

namespace CieloHud.App.Hud;

/// <summary>
/// Tactical HUD: reticle at the center, target marker offset by angular distance, edge arrow when out of view,
/// plain-words hint at the bottom. Draw only; all numbers come from <see cref="HudFrame"/>.
/// </summary>
public sealed class HudDrawable : IDrawable
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private const string FontRegular = "ChakraPetch";
    private const string FontBold = "ChakraPetchBold";

    /// <summary>Stars brighter than this are labelled on screen (the 20 or so brightest).</summary>
    private const double LabelMagnitudeLimit = 1.7;

    public HudFrame Frame { get; set; } = new();

    public HudPalette Palette { get; set; } = HudPalette.Normal;

    public void Draw(ICanvas canvas, RectF r)
    {
        canvas.SaveState();
        canvas.Antialias = true;
        NameZone = null;
        DetailZone = null;

        if (Frame.Pointing is { } pointing)
        {
            DrawHorizonAndCompass(canvas, r, pointing);
            DrawConstellationFigure(canvas, r, pointing);
            DrawReferences(canvas, r, pointing);
            DrawAltitudeLabels(canvas, r, pointing);
        }

        var locked = Frame.IdentifyMode ? Frame.Identified?.IsMatch == true : Frame.Guidance?.IsOnTarget == true;
        DrawReticle(canvas, r, locked);

        if (Frame.NeedsCalibration)
            DrawCalibrationBanner(canvas, r);

        if (Frame.IdentifyMode)
        {
            DrawIdentify(canvas, r);
        }
        else if (!Frame.Ready)
        {
            DrawCenteredText(canvas, r, StatusText(), r.Center.Y + 70, 15, Palette.TextMuted, FontRegular);
        }
        else if (Frame.TargetBelowHorizon)
        {
            DrawCenteredText(canvas, r, $"{Frame.TargetName} está bajo el horizonte", r.Center.Y + 70, 16, Palette.Marker, FontBold);
            DrawCenteredText(canvas, r, $"altura {Frame.Target!.Value.AltitudeDegrees.ToString("F0", Culture)}°", r.Center.Y + 94, 13, Palette.TextMuted, FontRegular);
        }
        else
        {
            var g = Frame.Guidance!.Value;
            var target = Frame.Target!.Value;
            var p = HudProjection.Project(Frame.Pointing!.Value, target.AzimuthDegrees, target.AltitudeDegrees, r.Width, r.Height);
            if (p.InView)
                DrawMarker(canvas, (float)p.X, (float)p.Y, g.IsOnTarget);
            else
                DrawEdgeArrow(canvas, (float)p.X, (float)p.Y, (float)p.DirectionDegrees);
            DrawHint(canvas, r, g);
        }

        canvas.RestoreState();
    }

    /// <summary>
    /// The world drawn as the phone sees it, through the same camera-like projection as everything else: the horizon
    /// (a great circle, so a straight line) slides down as you tilt up and the cardinal letters slide sideways as you turn.
    /// Altitude ticks every 10° on the left; azimuth ticks every 10° along the horizon.
    /// </summary>
    private void DrawHorizonAndCompass(ICanvas canvas, RectF r, PointingDirection pointing)
    {
        var dim = Palette.TextMuted.WithAlpha(0.55f);
        var faint = Palette.TextMuted.WithAlpha(0.25f);
        ScreenPoint? At(double az, double alt) => HudProjection.ToScreen(pointing, az, alt, r.Width, r.Height);

        // Altitude ladder along the vertical through the reticle: lines here, labels later (DrawAltitudeLabels) so
        // star names never cover them.
        foreach (var (alt, y) in AltitudeLadder(pointing, r))
        {
            var isHorizon = alt == 0;
            canvas.StrokeColor = isHorizon ? Palette.TextMuted.WithAlpha(0.8f) : faint;
            canvas.StrokeSize = isHorizon ? 1.5f : 1;
            if (isHorizon)
                DrawHorizonLine(canvas, pointing, r);
            else
                canvas.DrawLine(r.Left + 12, y, r.Left + 32, y);
        }

        // Compass along the horizon; pinned near an edge when the horizon is off screen, and kept above the bottom
        // text panel so the cardinal letters are not hidden behind it.
        var horizonY = At(pointing.AzimuthDegrees, 0)?.Y ?? (pointing.AltitudeDegrees > 0 ? r.Bottom + 1000 : r.Top - 1000);
        var compassY = (float)Math.Clamp(horizonY, r.Top + 60, Math.Min(r.Bottom - 60, TextPanelTop(r) - 12));
        var pinned = Math.Abs(compassY - horizonY) > 0.5;
        for (var az = 0; az < 360; az += 10)
        {
            if (At(az, 0) is not { } p)
                continue;
            var x = (float)p.X;
            if (x < r.Left + 8 || x > r.Right - 8)
                continue;
            var cardinal = az % 45 == 0 ? CardinalLabel(az) : null;
            canvas.StrokeColor = cardinal is null ? faint : dim;
            canvas.StrokeSize = 1;
            var tick = cardinal is null ? 5 : 10;
            canvas.DrawLine(x, compassY - tick, x, compassY + tick);
            if (cardinal is not null)
            {
                canvas.FontColor = az == 0 ? Palette.Alert.WithAlpha(0.9f) : Palette.TextMuted;
                canvas.FontSize = 13;
                canvas.Font = new Microsoft.Maui.Graphics.Font(FontBold);
                canvas.DrawString(cardinal, x - 20, compassY + (pinned ? -34 : 14), 40, 18, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }
    }

    /// <summary>Every 10° altitude mark that falls on screen, with its pixel row.</summary>
    private static IEnumerable<(int Altitude, float Y)> AltitudeLadder(PointingDirection pointing, RectF r)
    {
        for (var alt = -80; alt <= 90; alt += 10)
        {
            if (HudProjection.ToScreen(pointing, pointing.AzimuthDegrees, alt, r.Width, r.Height) is { } p
                && p.Y >= r.Top + 10 && p.Y <= r.Bottom - 10)
                yield return (alt, (float)p.Y);
        }
    }

    /// <summary>Ladder labels on a dark backing, drawn after stars and planets so their names cannot cover them.</summary>
    private void DrawAltitudeLabels(ICanvas canvas, RectF r, PointingDirection pointing)
    {
        canvas.FontSize = 11;
        canvas.Font = new Microsoft.Maui.Graphics.Font(FontRegular);
        foreach (var (alt, y) in AltitudeLadder(pointing, r))
        {
            var isHorizon = alt == 0;
            var label = isHorizon ? "HORIZONTE" : $"{alt}°";
            var width = isHorizon ? 74f : 34f;
            canvas.FillColor = Palette.Background.WithAlpha(0.8f);
            canvas.FillRoundedRectangle(r.Left + 34, y - 8, width, 16, 3);
            canvas.FontColor = isHorizon ? Palette.TextMuted : Palette.TextMuted.WithAlpha(0.55f);
            canvas.DrawString(label, r.Left + 36, y - 8, 90, 16, HorizontalAlignment.Left, VerticalAlignment.Center);
        }
    }

    /// <summary>Top of the dark panel behind the bottom text (see <see cref="DrawTextPanel"/>).</summary>
    private static float TextPanelTop(RectF r) => r.Bottom - 150 - 14;

    /// <summary>The horizon is a great circle: under a gnomonic projection, a straight line through two of its points.</summary>
    private static void DrawHorizonLine(ICanvas canvas, PointingDirection pointing, RectF r)
    {
        var left = HudProjection.ToScreen(pointing, pointing.AzimuthDegrees - 80, 0, r.Width, r.Height);
        var right = HudProjection.ToScreen(pointing, pointing.AzimuthDegrees + 80, 0, r.Width, r.Height);
        if (left is { } a && right is { } b)
            canvas.DrawLine((float)a.X, (float)a.Y, (float)b.X, (float)b.Y);
    }

    private static string CardinalLabel(int azimuth) => azimuth switch
    {
        0 => "N", 45 => "NE", 90 => "E", 135 => "SE", 180 => "S", 225 => "SO", 270 => "O", 315 => "NO", _ => "",
    };

    /// <summary>
    /// The stick figure of the constellation under the reticle, faint, with its name by the highest vertex on screen.
    /// Only that one: drawing them all would turn the HUD into a star chart.
    /// </summary>
    private void DrawConstellationFigure(ICanvas canvas, RectF r, PointingDirection pointing)
    {
        if (Frame.ConstellationFigure.Count == 0)
            return;

        canvas.StrokeColor = Palette.Guide.WithAlpha(0.3f);
        canvas.StrokeSize = 1.5f;
        ScreenPoint? top = null;
        foreach (var line in Frame.ConstellationFigure)
        {
            ScreenPoint? previous = null;
            foreach (var vertex in line)
            {
                var p = HudProjection.ToScreen(pointing, vertex.AzimuthDegrees, vertex.AltitudeDegrees, r.Width, r.Height);
                if (p is { } a && previous is { } b)
                    canvas.DrawLine((float)b.X, (float)b.Y, (float)a.X, (float)a.Y);
                if (p is { } q && r.Contains((float)q.X, (float)q.Y) && (top is null || q.Y < top.Value.Y))
                    top = q;
                previous = p;
            }
        }

        if (top is { } t && Frame.ConstellationFigureName is { } name)
        {
            canvas.FontColor = Palette.Guide.WithAlpha(0.55f);
            canvas.FontSize = 12;
            canvas.Font = new Microsoft.Maui.Graphics.Font(FontBold);
            canvas.DrawString(name.ToUpperInvariant(), (float)t.X - 90, (float)t.Y - 26, 180, 18, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    /// <summary>Other objects as faint dots, so you know what else is around.</summary>
    private void DrawReferences(ICanvas canvas, RectF r, PointingDirection pointing)
    {
        foreach (var reference in Frame.References)
        {
            if (HudProjection.ToScreen(pointing, reference.Position.AzimuthDegrees, reference.Position.AltitudeDegrees, r.Width, r.Height) is not { } p
                || !r.Contains((float)p.X, (float)p.Y))
                continue;

            var x = (float)p.X;
            var y = (float)p.Y;
            var below = reference.Position.AltitudeDegrees < 0;
            // Stars sized by brightness (Sirius ~5 px, Polaris ~2 px); Moon, planets and ISS in the guide color.
            var isStar = reference.Magnitude is not null;
            var color = below ? Palette.TextMuted.WithAlpha(0.3f) : isStar ? Palette.Star.WithAlpha(0.6f) : Palette.Guide.WithAlpha(0.7f);
            var radius = below ? 2.5f : isStar ? (float)Math.Clamp(3.5 - reference.Magnitude!.Value, 1.5, 5) : 4;
            canvas.FillColor = color;
            canvas.FillCircle(x, y, radius);
            canvas.FontColor = color;
            canvas.FontSize = 11;
            canvas.Font = new Microsoft.Maui.Graphics.Font(FontRegular);
            // Only the brightest stars get a label; the rest are dots, or the screen turns into a star chart.
            if (!isStar || reference.Magnitude < LabelMagnitudeLimit)
                canvas.DrawString(reference.Name, x - 60, y + 7, 120, 16, HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }

    /// <summary>
    /// Where the names that open a card were drawn in identify mode (decision 048): the big name, and the line under it. Null when
    /// they are not on screen or do not open anything. Each is a full-width band of the text panel, at least 48 dp high, whatever
    /// the length of the text.
    /// </summary>
    public RectF? NameZone { get; private set; }

    /// <inheritdoc cref="NameZone"/>
    public RectF? DetailZone { get; private set; }

    /// <summary>
    /// "What is that?": the name of what sits under the reticle, or a nudge towards the nearest known object. The names that open a
    /// card carry an ⓘ: the recognized object, and its constellation on the line under it; with nothing recognized, "Hacia Orión".
    /// </summary>
    private void DrawIdentify(ICanvas canvas, RectF r)
    {
        var y = r.Bottom - 150;
        if (!Frame.HasLocation || Frame.Pointing is null)
        {
            DrawCenteredText(canvas, r, StatusText(), r.Center.Y + 70, 15, Palette.TextMuted, FontRegular);
            return;
        }

        DrawTextPanel(canvas, r, y);
        // The text panel (DrawTextPanel), split under the name.
        var nameZone = new RectF(r.Left + 12, y - 14, r.Width - 24, 54);
        var detailZone = new RectF(r.Left + 12, y + 40, r.Width - 24, 58);

        if (Frame.Shown is { } shown)
        {
            DrawWithInfo(canvas, r, shown.Name.ToUpperInvariant(), y - 6, 30, 18, Palette.Locked, FontBold);
            var sub = $"{shown.Kind} · altura {shown.Position.AltitudeDegrees.ToString("F0", Culture)}° · en {shown.Constellation}";
            DrawWithInfo(canvas, r, sub, y + 44, 13, 10, Palette.TextMuted, FontRegular);
            (NameZone, DetailZone) = (nameZone, detailZone);
            return;
        }

        // Where the reticle is, in words: always available, even in empty sky or below the horizon. Only this line opens a card
        // here: the one under it names something else ("cerca: Bellatrix").
        var belowHorizon = Frame.Pointing.Value.AltitudeDegrees < 0 ? "bajo el horizonte · " : "";
        if (Frame.PointingConstellation is { } c)
        {
            DrawWithInfo(canvas, r, $"Hacia {c}", y, 22, 14, Palette.Text, FontBold);
            NameZone = nameZone;
        }

        if (Frame.Identified is not { } found)
        {
            DrawCenteredText(canvas, r, belowHorizon + "nada conocido sobre el horizonte", y + 44, 13, Palette.TextMuted, FontRegular, lines: 2);
            return;
        }

        // Nothing under the reticle: say what is closest and which way.
        var pointing = Frame.Pointing.Value;
        var g = new Guidance(
            GuidanceCalculator.WrapToHalfTurn(found.Position.AzimuthDegrees - pointing.AzimuthDegrees),
            found.Position.AltitudeDegrees - pointing.AltitudeDegrees,
            found.AngularDistanceDegrees, false);
        var horizontal = Math.Abs(g.AzimuthDeltaDegrees) < 1.5 ? null : g.AzimuthDeltaDegrees > 0 ? "derecha" : "izquierda";
        var vertical = Math.Abs(g.AltitudeDeltaDegrees) < 1.5 ? null : g.AltitudeDeltaDegrees > 0 ? "arriba" : "abajo";
        var where = string.Join(" y ", new[] { horizontal, vertical }.Where(s => s is not null));
        DrawCenteredText(canvas, r, $"{belowHorizon}cerca: {found.Name}, {found.AngularDistanceDegrees.ToString("F0", Culture)}° {where}", y + 44, 13, Palette.TextMuted, FontRegular, lines: 2);
    }

    /// <summary>
    /// One line, centered, followed by an ⓘ that says it opens a card. The text shrinks, down to <paramref name="minSize"/>, until the
    /// two fit in the width ("Hacia la Cabellera de Berenice"); if they still do not, the line is cut at the edges, never wrapped.
    /// The ⓘ is drawn, not a character: the HUD's font may not have it, and the system's could be a blue emoji, at night too.
    /// </summary>
    private void DrawWithInfo(ICanvas canvas, RectF r, string text, float y, float size, float minSize, Color color, string fontName)
    {
        var font = new Microsoft.Maui.Graphics.Font(fontName);
        var available = r.Width - 40;
        float Radius(float s) => Math.Max(7, s * 0.36f);
        float Gap(float s) => s * 0.3f;

        var textWidth = canvas.GetStringSize(text, font, size).Width;
        while (size > minSize && textWidth + Gap(size) + 2 * Radius(size) > available)
        {
            size -= 1;
            textWidth = canvas.GetStringSize(text, font, size).Width;
        }

        var radius = Radius(size);
        var total = textWidth + Gap(size) + 2 * radius;
        var left = r.Center.X - total / 2;
        canvas.FontColor = color;
        canvas.FontSize = size;
        canvas.Font = font;
        // A little wider than measured, so rounding never wraps the last word.
        canvas.DrawString(text, left, y, textWidth + 8, size + 12, HorizontalAlignment.Left, VerticalAlignment.Top);

        // Centered on the lowercase letters' height, roughly half the size below the top of the line box.
        var cx = left + textWidth + Gap(size) + radius;
        var cy = y + size * 0.62f;
        canvas.StrokeColor = color;
        canvas.StrokeSize = Math.Max(1.2f, radius * 0.16f);
        canvas.DrawCircle(cx, cy, radius);
        canvas.FillColor = color;
        canvas.FillCircle(cx, cy - radius * 0.45f, radius * 0.14f);
        canvas.StrokeSize = radius * 0.22f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(cx, cy - radius * 0.12f, cx, cy + radius * 0.5f);
        canvas.StrokeLineCap = LineCap.Butt;
    }


    /// <summary>Dark backing for the bottom text block, so the altitude ladder and star labels never run through it.</summary>
    private void DrawTextPanel(ICanvas canvas, RectF r, float y)
    {
        canvas.FillColor = Palette.Background.WithAlpha(0.88f);
        canvas.FillRoundedRectangle(r.Left + 12, y - 14, r.Width - 24, 112, 10);
    }

    private void DrawCalibrationBanner(ICanvas canvas, RectF r)
    {
        var alpha = 0.6f + 0.4f * (float)Math.Abs(Math.Sin(Frame.Pulse * Math.PI));
        var y = r.Top + 16;
        canvas.FillColor = Palette.Background.WithAlpha(0.85f);
        canvas.FillRoundedRectangle(r.Left + 16, y, r.Width - 32, 52, 8);
        canvas.StrokeColor = Palette.Alert.WithAlpha(alpha);
        canvas.StrokeSize = 1.5f;
        canvas.DrawRoundedRectangle(r.Left + 16, y, r.Width - 32, 52, 8);
        DrawCenteredText(canvas, r, "BRÚJULA SIN CALIBRAR", y + 6, 14, Palette.Alert.WithAlpha(alpha), FontBold);
        DrawCenteredText(canvas, r, "Mueve el móvil dibujando un 8 en el aire", y + 26, 12, Palette.TextMuted, FontRegular);
    }

    private string StatusText()
    {
        if (!Frame.HasLocation) return Frame.LocationFailed ? "Sin ubicación · activa el GPS y da permiso" : "Buscando tu posición…";
        if (Frame.Pointing is null) return "Esperando sensores…";
        if (Frame.Target is null) return Frame.Unavailable ?? "Objetivo no disponible";
        return "";
    }

    private void DrawReticle(ICanvas canvas, RectF r, bool locked)
    {
        var cx = r.Center.X;
        var cy = r.Center.Y;
        var color = locked ? Palette.Locked : Palette.Guide;
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
        var color = locked ? Palette.Locked : Palette.Marker;
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

        canvas.FillColor = Palette.MarkerCore;
        canvas.FillCircle(x, y, 3);

        // Bracket corners.
        var b = d * 1.5f;
        var l = d * 0.5f;
        canvas.StrokeColor = Palette.MarkerCore.WithAlpha(0.7f);
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(x - b, y - b, x - b + l, y - b); canvas.DrawLine(x - b, y - b, x - b, y - b + l);
        canvas.DrawLine(x + b, y - b, x + b - l, y - b); canvas.DrawLine(x + b, y - b, x + b, y - b + l);
        canvas.DrawLine(x - b, y + b, x - b + l, y + b); canvas.DrawLine(x - b, y + b, x - b, y + b - l);
        canvas.DrawLine(x + b, y + b, x + b - l, y + b); canvas.DrawLine(x + b, y + b, x + b, y + b - l);

        canvas.FontColor = color;
        canvas.FontSize = 14;
        canvas.Font = new Microsoft.Maui.Graphics.Font(FontBold);
        // When locked the pulse ring surrounds the marker; push the label below it.
        var labelY = locked ? y + 78 : y + b + 6;
        canvas.DrawString(Frame.TargetName.ToUpperInvariant(), x - 80, labelY, 160, 20, HorizontalAlignment.Center, VerticalAlignment.Top);
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

        canvas.FillColor = Palette.Marker.WithAlpha(0.35f + 0.4f * (float)Frame.Pulse);
        canvas.FillPath(path);
        canvas.StrokeColor = Palette.Marker;
        canvas.StrokeSize = 2;
        canvas.DrawPath(path);
        canvas.RestoreState();

        canvas.FontColor = Palette.Marker;
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
            color = Palette.Locked;
        }
        else
        {
            var turn = Math.Abs(g.AzimuthDeltaDegrees) < 1.5 ? null : (g.AzimuthDeltaDegrees > 0 ? "derecha" : "izquierda") + " " + Math.Abs(g.AzimuthDeltaDegrees).ToString("F0", Culture) + "°";
            var tilt = Math.Abs(g.AltitudeDeltaDegrees) < 1.5 ? null : (g.AltitudeDeltaDegrees > 0 ? "sube" : "baja") + " " + Math.Abs(g.AltitudeDeltaDegrees).ToString("F0", Culture) + "°";
            main = string.Join("  ·  ", new[] { turn, tilt }.Where(s => s is not null));
            var where = Frame.TargetConstellation is { } tc ? $", en {tc}" : "";
            sub = $"{Frame.TargetName}{where} · acimut {Frame.Target!.Value.AzimuthDegrees.ToString("F0", Culture)}°, altura {Frame.Target.Value.AltitudeDegrees.ToString("F0", Culture)}°";
            color = g.AngularDistanceDegrees < 15 ? Palette.Marker : Palette.Text;
        }

        var y = r.Bottom - 150;
        DrawTextPanel(canvas, r, y);
        DrawCenteredText(canvas, r, main, y, g.IsOnTarget ? 34 : 24, color, FontBold);
        DrawCenteredText(canvas, r, sub, y + 44, 13, Palette.TextMuted, FontRegular, lines: 2);
    }

    /// <param name="lines">Height reserved for wrapping; place the next text below accordingly.</param>
    private static void DrawCenteredText(ICanvas canvas, RectF r, string text, float y, float size, Color color, string font, int lines = 1)
    {
        canvas.FontColor = color;
        canvas.FontSize = size;
        canvas.Font = new Microsoft.Maui.Graphics.Font(font);
        canvas.DrawString(text, r.Left + 20, y, r.Width - 40, lines * (size + 8) + 4, HorizontalAlignment.Center, VerticalAlignment.Top);
    }
}
