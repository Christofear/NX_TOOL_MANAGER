using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NX_TOOL_MANAGER.Models;

namespace NX_TOOL_MANAGER.Services
{
    public interface IToolDrawer
    {
        bool Draw(Canvas surface, DatRow toolData);
    }

    // --- DRAWER IMPLEMENTATIONS (One class per UG Subtype) ---
    internal static class ToolDraw
    {
        // ---------- shared drawing context ----------
        internal sealed class Ctx
        {
            public Canvas Surface { get; init; }
            public DatRow Row { get; init; }

            // scale & canvas box (AFTER fit-to-view with shank)
            public double S, Left, Top, BoxW, BoxH;

            // body frame (below shank band)
            public double BodyTop, BodyBottom, BodyHeight;

            // shank info
            public bool HasShank;
            public double TsDia, TsLen, TsTLen;

            /// <summary>Draw the green shank starting from the ACTUAL body top you drew.</summary>
            public void DrawShankFromBodyTop(double bodyLeftX, double bodyRightX)
            {
                if (!HasShank) return;
                ToolShankRenderer.DrawFromBodyTop(
                    Surface, S,
                    bodyLeftX, bodyRightX, BodyTop,
                    TsDia, TsLen, TsTLen);
            }
        }

        /// <summary>
        /// Measure shank (TSDIA/TSLEN), fit-to-view using (bodyHei + shank),
        /// compute scale and body frame (BodyTop/Bottom/Height). Returns null on failure.
        /// </summary>
        public static Ctx Begin(Canvas surface, DatRow row, double bodyDia, double bodyHei)
        {
            var ctx = new Ctx { Surface = surface, Row = row };

            // measure shank first so we can fit-to-view
            ctx.HasShank = ToolShankRenderer.TryMeasureExtraHeight(row, out ctx.TsLen, out ctx.TsDia, out ctx.TsTLen);
            double effectiveHei = bodyHei + (ctx.HasShank ? ctx.TsLen : 0.0);

            var (s, boxW, boxH, left, top) = ToolDrawerHelpers.CalculateScaling(surface, bodyDia, effectiveHei);
            if (s <= 0) return null;

            ctx.S = s; ctx.BoxW = boxW; ctx.BoxH = boxH; ctx.Left = left; ctx.Top = top;

            // reserve top band for shank
            double shankH = ctx.HasShank ? ctx.TsLen * s : 0.0;
            ctx.BodyTop = top + shankH;
            ctx.BodyBottom = top + boxH;
            ctx.BodyHeight = ctx.BodyBottom - ctx.BodyTop;

            return ctx;
        }

        /// <summary>
        /// Gray→Yellow body fill based on flute/relief heights relative to BODY height.
        /// </summary>
        public static Brush MakeBodyFill(double bodyHeight, double scaledFlen, double scaledReliefLen = 0)
        {
            var shankGray = Color.FromRgb(0xAD, 0xB5, 0xBD);
            var fluteYel = Color.FromRgb(0xFF, 0xC1, 0x07);

            double pFlute = bodyHeight <= 0 ? 0 : 1.0 - Math.Clamp(scaledFlen / bodyHeight, 0.0, 1.0);
            double pRelief = bodyHeight <= 0 ? 0 : 1.0 - Math.Clamp((scaledFlen + scaledReliefLen) / bodyHeight, 0.0, 1.0);

            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            g.GradientStops.Add(new GradientStop(shankGray, 0.0));
            if (scaledReliefLen > 0)
            {
                g.GradientStops.Add(new GradientStop(shankGray, pRelief));
                g.GradientStops.Add(new GradientStop(shankGray, pFlute));
            }
            else
            {
                g.GradientStops.Add(new GradientStop(shankGray, pFlute));
            }
            g.GradientStops.Add(new GradientStop(fluteYel, pFlute + 0.001));
            g.GradientStops.Add(new GradientStop(fluteYel, 1.0));
            return g;
        }

        public static Pen Outline(double px = 1) =>
            new Pen(new SolidColorBrush(Color.FromRgb(0x34, 0x3A, 0x40)), px);

        /// <summary>Add a closed path to the canvas.</summary>
        public static void AddPath(Canvas c, PathFigure fig, Brush fill, Pen pen)
        {
            fig.IsClosed = true;
            c.Children.Add(new Path
            {
                Data = new PathGeometry(new[] { fig }),
                Fill = fill,
                Stroke = pen.Brush,
                StrokeThickness = pen.Thickness
            });
        }

        // common math helpers
        public static double ChamferRunFromAxis(double scaledChamferLen, double deg) =>
            scaledChamferLen * Math.Tan(deg * Math.PI / 180.0);

        public static double Safe(double v, double eps = 1e-6) =>
            Math.Abs(v) < eps ? (v >= 0 ? eps : -eps) : v;
    }

    public class FiveParameterDrawer : IToolDrawer
    {
        public bool Draw(Canvas surface, DatRow row)
        {
            if (!ToolDrawerHelpers.TryParseWithAliases(row, out double dia, "DIA", "DIAMETER") ||
                !ToolDrawerHelpers.TryParseWithAliases(row, out double hei, "HEI", "LENGTH") ||
                !ToolDrawerHelpers.TryParseWithAliases(row, out double flen, "FLEN", "CUT_LENGTH"))
                return false;

            // start shared context
            var ctx = ToolDraw.Begin(surface, row, dia, hei);
            if (ctx == null) return false;

            double s = ctx.S;
            double scaledFlen = flen * s;

            var fill = ToolDraw.MakeBodyFill(ctx.BodyHeight, scaledFlen);
            var pen = ToolDraw.Outline();

            // build your body outline using ctx.Left / ctx.Left+ctx.BoxW and ctx.BodyTop/Bottom
            var fig = new PathFigure { StartPoint = new Point(ctx.Left, ctx.BodyTop) };
            fig.Segments.Add(new LineSegment(new Point(ctx.Left + ctx.BoxW, ctx.BodyTop), true));
            fig.Segments.Add(new LineSegment(new Point(ctx.Left + ctx.BoxW, ctx.BodyBottom), true));
            fig.Segments.Add(new LineSegment(new Point(ctx.Left, ctx.BodyBottom), true));
            // ... add your tool-specific geometry here ...
            ToolDraw.AddPath(surface, fig, fill, pen);

            // draw shank from what you actually drew at body top
            ctx.DrawShankFromBodyTop(ctx.Left, ctx.Left + ctx.BoxW);

            return true;
        }
    }
    public class BallDrawer : IToolDrawer
    {
        public bool Draw(Canvas surface, DatRow row)
        {
            // ---- Required ----
            if (!ToolDrawerHelpers.TryParseWithAliases(row, out double dia, "DIA", "DIAMETER") ||
                !ToolDrawerHelpers.TryParseWithAliases(row, out double hei, "HEI", "LENGTH") ||
                !ToolDrawerHelpers.TryParseWithAliases(row, out double flen, "FLEN", "CUT_LENGTH"))
                return false;

            // ---- Optional ----
            ToolDrawerHelpers.TryParseWithAliases(row, out double tapa, "TAPA", "TAPER_ANGLE");
            ToolDrawerHelpers.TryParseWithAliases(row, out double reliefLen, "RL");
            ToolDrawerHelpers.TryParseWithAliases(row, out double reliefDia, "RD");

            // ---- Validate core ----
            double radius = dia / 2.0;
            if (dia <= 0 || hei <= 0 || flen < radius) return false;
            if ((flen + reliefLen) > hei) return false;
            if (reliefDia > dia) return false;

            bool hasRelief = reliefLen > 0 && reliefDia > 0;

            // ---- Shared begin: fit-to-view (adds shank), scale, body frame ----
            var ctx = ToolDraw.Begin(surface, row, dia, hei);
            if (ctx == null) return false;

            double s = ctx.S;
            double left = ctx.Left;
            double right = ctx.Left + ctx.BoxW;
            double bodyTop = ctx.BodyTop;
            double bodyBottom = ctx.BodyBottom;
            double bodyHeight = ctx.BodyHeight;

            // ---- Scale body params ----
            double scaledFlen = flen * s;
            double scaledRadius = radius * s;

            double taperAngleRad = tapa * Math.PI / 180.0;
            double taperHeight = Math.Max(0.0, scaledFlen - scaledRadius); // vertical length of tapered flank above ball
            double taperOffset = taperHeight * Math.Tan(taperAngleRad);    // horizontal squeeze at ball start

            double scaledReliefLen = reliefLen * s;
            double scaledReliefDia = reliefDia * s;
            double reliefDiaInset = hasRelief ? (ctx.BoxW - scaledReliefDia) / 2.0 : 0.0;

            // ---- Fill & outline (BODY only) ----
            var fill = ToolDraw.MakeBodyFill(bodyHeight, scaledFlen, hasRelief ? scaledReliefLen : 0.0);
            var pen = ToolDraw.Outline();

            // ---- Geometry (all Y relative to bodyTop/bodyBottom) ----
            var fig = new PathFigure { StartPoint = new Point(left, bodyTop) };

            // Right side: top -> shank-end of body (above relief+flute)
            fig.Segments.Add(new LineSegment(new Point(right, bodyTop), true));

            double shankEndY = bodyBottom - scaledFlen - (hasRelief ? scaledReliefLen : 0.0);
            fig.Segments.Add(new LineSegment(new Point(right, shankEndY), true));

            // Relief step (right)
            if (hasRelief)
            {
                fig.Segments.Add(new LineSegment(new Point(right - reliefDiaInset, shankEndY), true));                 // in to RD
                fig.Segments.Add(new LineSegment(new Point(right - reliefDiaInset, shankEndY + scaledReliefLen), true)); // relief wall down
                fig.Segments.Add(new LineSegment(new Point(right, shankEndY + scaledReliefLen), true));                 // out to flute dia
            }

            // Right tapered flank down to ball start
            double ballY = bodyBottom - scaledRadius;
            var rightTaperEnd = new Point(right - taperOffset, ballY);
            fig.Segments.Add(new LineSegment(rightTaperEnd, true));

            // 180° ball arc to left side start of taper
            var leftTaperStart = new Point(left + taperOffset, ballY);
            fig.Segments.Add(new ArcSegment(
                leftTaperStart,
                new Size(scaledRadius, scaledRadius),
                180, false, SweepDirection.Clockwise, true));

            // Left flank up
            if (taperOffset > 0)
            {
                var leftTaperTop = new Point(left, ballY - taperHeight);
                fig.Segments.Add(new LineSegment(leftTaperTop, true));
            }
            else
            {
                // No taper: straight up from ball start to flute top
                fig.Segments.Add(new LineSegment(new Point(left, bodyBottom - scaledFlen), true));
            }

            // Relief step (left)
            if (hasRelief)
            {
                fig.Segments.Add(new LineSegment(new Point(left + reliefDiaInset, shankEndY + scaledReliefLen), true)); // in to RD
                fig.Segments.Add(new LineSegment(new Point(left + reliefDiaInset, shankEndY), true));                   // relief up
                fig.Segments.Add(new LineSegment(new Point(left, shankEndY), true));                                    // out to body wall
            }

            // Close to top-left of body
            fig.Segments.Add(new LineSegment(new Point(left, bodyTop), true));

            // Draw body
            ToolDraw.AddPath(surface, fig, fill, pen);

            // ---- SHANK drawn from what was actually drawn at the body top ----
            ctx.DrawShankFromBodyTop(left, right); // ball’s body top spans full box width

            return true;
        }
    }

    public class ChamferMillDrawer : IToolDrawer
    {
        public bool Draw(Canvas surface, DatRow row)
        {
            // Core
            if (!ToolDrawerHelpers.TryParseWithAliases(row, out double dia, "D", "DIAMETER", "DIA") ||
                !ToolDrawerHelpers.TryParseWithAliases(row, out double hei, "L", "LENGTH", "HEI") ||
                !ToolDrawerHelpers.TryParseWithAliases(row, out double flen, "FL", "FLEN", "CUT_LENGTH"))
                return false;

            // Optional
            ToolDrawerHelpers.TryParseWithAliases(row, out double chamferLen, "C", "CHAMFERLEN"); // axial length of chamfer
            ToolDrawerHelpers.TryParseWithAliases(row, out double radius, "R1", "COR1");       // corner radius
            ToolDrawerHelpers.TryParseWithAliases(row, out double chamferDeg, "B", "TAPA");       // angle from tool axis

            if (chamferDeg <= 0.0)
                return new FiveParameterDrawer().Draw(surface, row); // fallback to standard endmill

            // Validate
            if (dia <= 0 || hei <= 0 || flen <= 0 || chamferLen <= 0) return false;
            if (flen < chamferLen) return false;
            if (radius < 0 || radius > dia / 2.0) return false;

            // ---- Begin shared context (fit-to-view with shank, scaling, body frame) ----
            var ctx = ToolDraw.Begin(surface, row, dia, hei);
            if (ctx == null) return false;

            double s = ctx.S;
            double xLeft = ctx.Left;
            double xRight = ctx.Left + ctx.BoxW;
            double bodyTop = ctx.BodyTop;
            double bodyBottom = ctx.BodyBottom;
            double bodyHeight = ctx.BodyHeight;

            // Scaled params
            double scaledFlen = flen * s;
            double scaledCham = chamferLen * s;
            double scaledR = radius * s;

            // Horizontal run of chamfer (angle measured FROM AXIS)
            double dx = ToolDraw.ChamferRunFromAxis(scaledCham, chamferDeg);
            dx = Math.Min(dx, ctx.BoxW / 2.0); // guard against overrun

            // Fill & outline (BODY only)
            var fill = ToolDraw.MakeBodyFill(bodyHeight, scaledFlen);
            var pen = ToolDraw.Outline();

            // Key Y levels
            double yChamTop = bodyBottom - scaledCham; // where chamfer begins
            double yFluteTop = bodyBottom - scaledFlen; // top of flute region

            // If radius at bottom, trim chamfer endpoints along its direction so arcs fit cleanly
            double angleRad = chamferDeg * Math.PI / 180.0;
            double rTrim = (scaledR > 0.0) ? (scaledR / Math.Sin(angleRad)) : 0.0;
            rTrim = Math.Min(rTrim, scaledCham);

            // Right side (unit along chamfer up-left)
            double uxR = -Math.Sin(angleRad), uy = Math.Cos(angleRad);
            Point pTopRWall = new(xRight, bodyTop);
            Point pRightWallEnd = new(xRight, Math.Max(yFluteTop, yChamTop));
            Point pChamTopRight = new(xRight, yChamTop);
            Point pChamBotRight = new(xRight - dx, bodyBottom);
            if (scaledR > 0.0)
            {
                pChamBotRight = new Point(pChamBotRight.X - uxR * rTrim, pChamBotRight.Y - uy * rTrim);
                pChamTopRight = new Point(pChamTopRight.X + uxR * rTrim, pChamTopRight.Y + uy * rTrim);
            }

            // Left side (mirror: unit along chamfer up-right)
            double uxL = +Math.Sin(angleRad);
            Point pTopLWall = new(xLeft, bodyTop);
            Point pLeftWallEnd = new(xLeft, Math.Max(yFluteTop, yChamTop));
            Point pChamTopLeft = new(xLeft, yChamTop);
            Point pChamBotLeft = new(xLeft + dx, bodyBottom);
            if (scaledR > 0.0)
            {
                pChamBotLeft = new Point(pChamBotLeft.X + uxL * rTrim, pChamBotLeft.Y - uy * rTrim);
                pChamTopLeft = new Point(pChamTopLeft.X - uxL * rTrim, pChamTopLeft.Y + uy * rTrim);
            }

            // Build symmetric outline
            var fig = new PathFigure { StartPoint = pTopLWall };
            // top edge
            fig.Segments.Add(new LineSegment(pTopRWall, true));
            // right vertical to chamfer start
            fig.Segments.Add(new LineSegment(pRightWallEnd, true));
            // right chamfer edge to trimmed top
            fig.Segments.Add(new LineSegment(pChamTopRight, true));
            // bottom right (arc or straight)
            if (scaledR > 0.01)
                fig.Segments.Add(new ArcSegment(pChamBotRight, new Size(scaledR, scaledR), 0, false, SweepDirection.Clockwise, true));
            else
                fig.Segments.Add(new LineSegment(new Point(xRight - dx, bodyBottom), true));
            // bottom across to left (via trimmed point if arc used)
            fig.Segments.Add(new LineSegment(scaledR > 0.01 ? pChamBotLeft : new Point(xLeft + dx, bodyBottom), true));
            // bottom left (arc or straight) back up onto chamfer
            if (scaledR > 0.01)
                fig.Segments.Add(new ArcSegment(pChamTopLeft, new Size(scaledR, scaledR), 0, false, SweepDirection.Clockwise, true));
            else
                fig.Segments.Add(new LineSegment(pChamTopLeft, true));
            // up the left vertical and close
            fig.Segments.Add(new LineSegment(pLeftWallEnd, true));
            fig.Segments.Add(new LineSegment(pTopLWall, true));

            ToolDraw.AddPath(surface, fig, fill, pen);

            // Draw shank from the ACTUAL body top (full width for chamfer tools)
            ctx.DrawShankFromBodyTop(xLeft, xRight);

            return true;
        }
    }



    public class SphericalMillDrawer : IToolDrawer
    {
        public bool Draw(Canvas surface, DatRow row)
        {
            // Required
            if (!ToolDrawerHelpers.TryParseWithAliases(row, out double dia, "DIA", "DIAMETER") ||
                !ToolDrawerHelpers.TryParseWithAliases(row, out double hei, "HEI", "LENGTH") ||
                !ToolDrawerHelpers.TryParseWithAliases(row, out double flen, "FLEN", "CUT_LENGTH") ||
                !ToolDrawerHelpers.TryParseWithAliases(row, out double sdia, "SDIA", "NECK_DIAMETER"))
                return false;

            if (dia <= 0 || hei <= 0 || sdia <= 0 || sdia > dia) return false;

            // Shared context: fit-to-view (adds shank height), scale, body frame
            var ctx = ToolDraw.Begin(surface, row, dia, hei);
            if (ctx == null) return false;

            double s = ctx.S;
            double bodyTop = ctx.BodyTop;
            double bodyBottom = ctx.BodyBottom;
            double bodyH = ctx.BodyHeight;

            // Scaled geometry
            double R = dia / 2.0;
            double rn = sdia / 2.0;
            double scaledR = R * s;
            double scaledRn = rn * s;

            // Neck centered in tool width
            double neckInset = (ctx.BoxW - (sdia * s)) / 2.0;
            double leftNeckX = ctx.Left + neckInset;
            double rightNeckX = ctx.Left + ctx.BoxW - neckInset;

            // Sphere center and intersection with neck
            double centerY = bodyBottom - scaledR;
            double y0 = Math.Sqrt(Math.Max(0.0, scaledR * scaledR - scaledRn * scaledRn)); // above center
            double intersectionY = centerY - y0;

            // Fill & outline (BODY only). flen controls the yellow portion height.
            double scaledFlen = flen * s;
            var fill = ToolDraw.MakeBodyFill(bodyH, scaledFlen);
            var pen = ToolDraw.Outline();

            // Outline: neck top -> right wall -> big sphere arc -> left wall -> close
            var fig = new PathFigure { StartPoint = new Point(leftNeckX, bodyTop) };
            fig.Segments.Add(new LineSegment(new Point(rightNeckX, bodyTop), true));                 // neck top
            fig.Segments.Add(new LineSegment(new Point(rightNeckX, intersectionY), true));           // right neck wall
            fig.Segments.Add(new ArcSegment(                                                          // >180° sphere
                new Point(leftNeckX, intersectionY),
                new Size(scaledR, scaledR),
                0,
                true, // IsLargeArc
                SweepDirection.Clockwise,
                true));
            fig.Segments.Add(new LineSegment(new Point(leftNeckX, bodyTop), true));                  // left neck wall up

            ToolDraw.AddPath(surface, fig, fill, pen);

            // Shank from what we actually drew at the body top (neck width)
            ctx.DrawShankFromBodyTop(leftNeckX, rightNeckX);

            return true;
        }
    }
    public class DovetailMillDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class TSlotCutterDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class BarrelCutterDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class SevenParameterDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class TenParameterDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class MillFormDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class ThreadMillDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class StandardDrillDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class CoreDrillDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class StepDrillDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class SpotFaceDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class SpotDrillDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class CenterBellDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class BoreDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class DrillReamDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class CounterBoreDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class CounterSinkDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class TapDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class BackCounterSinkDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class BoringBarDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }
    public class ChamferBoringBarDrawer : IToolDrawer { public bool Draw(Canvas surface, DatRow toolData) => ToolDrawerHelpers.DrawCylindricalTool(surface, toolData, "DIAMETER", "LENGTH"); }

    // --- SHARED HELPER METHODS ---
    internal static class ToolDrawerHelpers
    {
        internal static bool DrawCylindricalTool(Canvas surface, DatRow toolData, string diamKey, string lengthKey)
        {
            if (!TryParseWithAliases(toolData, out double diam, diamKey, "DIA") || !TryParseWithAliases(toolData, out double length, lengthKey, "HEI")) return false;
            var (s, boxW, boxH, left, top) = CalculateScaling(surface, diam, length);
            if (s <= 0) return false;
            var fill = new SolidColorBrush(Color.FromRgb(0xAD, 0xB5, 0xBD));
            var stroke = new SolidColorBrush(Color.FromRgb(0x34, 0x3A, 0x40));
            var rect = new Rectangle { Width = boxW, Height = boxH, Fill = fill, Stroke = stroke, StrokeThickness = 1 };
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top);
            surface.Children.Add(rect);
            return true;
        }

        internal static bool TryParseWithAliases(DatRow toolData, out double result, params string[] aliases)
        {
            foreach (var alias in aliases)
            {
                string valueStr = toolData.Get(alias);
                if (!string.IsNullOrEmpty(valueStr) && double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                {
                    return true;
                }
            }
            result = 0;
            return false;
        }

        internal static (double scale, double boxW, double boxH, double left, double top) CalculateScaling(Canvas surface, double realWidth, double realHeight)
        {
            const double pad = 40;
            var w = Math.Max(0, surface.ActualWidth - 2 * pad);
            var h = Math.Max(0, surface.ActualHeight - 2 * pad);
            if (w <= 0 || h <= 0 || realWidth <= 0 || realHeight <= 0) return (0, 0, 0, 0, 0);
            var sx = w / realWidth;
            var sy = h / realHeight;
            var s = Math.Min(sx, sy);
            var boxW = realWidth * s;
            var boxH = realHeight * s;
            var left = (surface.ActualWidth - boxW) / 2.0;
            var top = (surface.ActualHeight - boxH) / 2.0;
            return (s, boxW, boxH, left, top);
        }
    }

internal static class ToolShankRenderer
    {
        /// <summary>
        /// Read shank parameters from the row.
        /// REQUIRED: TSDIA, TSLEN. OPTIONAL: TSTLEN.
        /// </summary>
        public static bool TryMeasureExtraHeight(
            NX_TOOL_MANAGER.Models.DatRow toolData,
            out double tsLen, out double tsDia, out double tsTLen)
        {
            tsLen = tsDia = tsTLen = 0;

            if (!ToolDrawerHelpers.TryParseWithAliases(toolData, out tsDia, "TSDIA", "SHANK_DIA")) return false;
            if (!ToolDrawerHelpers.TryParseWithAliases(toolData, out tsLen, "TSLEN", "SHANK_LEN")) return false;
            ToolDrawerHelpers.TryParseWithAliases(toolData, out tsTLen, "TSTLEN", "SHANK_TAPER_LEN");

            if (tsDia <= 0 || tsLen <= 0) return false;
            tsTLen = Math.Clamp(tsTLen, 0.0, tsLen);
            return true;
        }

        /// <summary>
        /// Draw the shank ABOVE the tool body, using what was actually drawn at the top.
        /// Pass the body's top edge (left X, right X, Y) so we can:
        /// - taper from that width to TSDIA over TSTLEN, then
        /// - continue as a cylinder at TSDIA for the remaining length.
        ///
        /// Coordinates are in canvas space (already scaled); 's' is your tool scale.
        /// </summary>
        public static bool DrawFromBodyTop(
            Canvas surface, double s,
            double bodyLeftX, double bodyRightX, double bodyTopY,
            double tsDia, double tsLen, double tsTLen)
        {
            if (surface == null || s <= 0) return false;
            if (tsDia <= 0 || tsLen <= 0) return false;

            double scaledLen = tsLen * s;
            double scaledTaper = Math.Min(Math.Max(0.0, tsTLen * s), scaledLen);
            double topWidth = tsDia * s;

            // Vertical positions (Y grows downward)
            double yTop = bodyTopY - scaledLen;     // very top of shank
            double yTaperTop = bodyTopY - scaledTaper;   // top of taper / bottom of cylinder
            double yCylTop = yTop;                     // cylinder top (if any)
            double yCylBottom = (scaledTaper > 0.0) ? yTaperTop : bodyTopY;

            // Horizontal positions
            double bodyWidth = bodyRightX - bodyLeftX;
            double cx = (bodyLeftX + bodyRightX) / 2.0;
            double cylLeftX = cx - topWidth / 2.0;
            double cylRightX = cx + topWidth / 2.0;

            var fill = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A)); // lighter green
            var stroke = new SolidColorBrush(Color.FromRgb(0x34, 0x3A, 0x40));

            // Build one clean outline that wraps cylinder (if any) and taper (if any)
            var fig = new PathFigure { IsClosed = true, StartPoint = new Point(cylLeftX, yCylTop) };

            // Top edge of cylinder
            fig.Segments.Add(new LineSegment(new Point(cylRightX, yCylTop), true));

            // Right side of cylinder down to its bottom
            fig.Segments.Add(new LineSegment(new Point(cylRightX, yCylBottom), true));

            if (scaledTaper > 0.0)
            {
                // Right taper edge down to the body's top-right corner
                fig.Segments.Add(new LineSegment(new Point(bodyRightX, bodyTopY), true));

                // Bottom edge across the body's top to the left corner
                fig.Segments.Add(new LineSegment(new Point(bodyLeftX, bodyTopY), true));

                // Left taper edge up to the cylinder bottom on the left
                fig.Segments.Add(new LineSegment(new Point(cylLeftX, yCylBottom), true));
            }
            else
            {
                // No taper: simple cylinder standing on its own base centered over the body
                fig.Segments.Add(new LineSegment(new Point(cylLeftX, yCylBottom), true));
            }

            // Close implicitly back to (cylLeftX, yCylTop)

            var geom = new PathGeometry(new[] { fig });
            surface.Children.Add(new Path
            {
                Data = geom,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = 1
            });

            return true;
        }
    }


}

