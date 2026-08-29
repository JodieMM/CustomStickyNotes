using System;
using System.Collections.Generic;
using System.Drawing;
using CustomStickyNotes.Models;

namespace CustomStickyNotes.Services;

/// <summary>
/// Snaps notes to a consistent gutter distance from nearby note edges and the monitor's
/// working-area edges — proximity-based, independent per axis — rather than a fixed grid pitch,
/// so notes of any size line up with even padding between them.
/// </summary>
public class SnapService
{
    private readonly AppSettings _settings;

    public SnapService(AppSettings settings)
    {
        _settings = settings;
    }

    private double Gutter => _settings.NoteGutter;

    private double Threshold => _settings.SnapThreshold;

    public System.Windows.Point OffsetToScreenPoint(Rectangle workingArea, double offsetX, double offsetY)
        => new(workingArea.X + offsetX, workingArea.Y + offsetY);

    public (double OffsetX, double OffsetY) ScreenPointToOffset(Rectangle workingArea, double screenX, double screenY)
        => (screenX - workingArea.X, screenY - workingArea.Y);

    /// <summary>
    /// Snaps a proposed note rect to a consistent gutter distance from nearby note edges and the
    /// monitor's working-area edges, independently per axis. An axis with nothing nearby to snap
    /// to is left at the proposed position (free placement, no fixed grid fallback).
    /// </summary>
    public System.Windows.Rect ComputeSnappedRect(System.Windows.Rect proposed, Rectangle workingArea, IReadOnlyList<System.Windows.Rect> obstacles)
    {
        var x = SnapX(proposed, workingArea, obstacles);
        var y = SnapY(proposed, workingArea, obstacles);
        return new System.Windows.Rect(x, y, proposed.Width, proposed.Height);
    }

    private double SnapX(System.Windows.Rect proposed, Rectangle wa, IReadOnlyList<System.Windows.Rect> obstacles)
    {
        var candidates = new List<double> { wa.Left + Gutter, wa.Right - Gutter - proposed.Width };
        foreach (var o in obstacles)
        {
            if (proposed.Top >= o.Bottom || proposed.Bottom <= o.Top) continue; // not vertically aligned -> not a horizontal neighbor
            candidates.Add(o.Right + Gutter);
            candidates.Add(o.Left - Gutter - proposed.Width);
        }
        return PickClosest(proposed.X, candidates);
    }

    private double SnapY(System.Windows.Rect proposed, Rectangle wa, IReadOnlyList<System.Windows.Rect> obstacles)
    {
        var candidates = new List<double> { wa.Top + Gutter, wa.Bottom - Gutter - proposed.Height };
        foreach (var o in obstacles)
        {
            if (proposed.Left >= o.Right || proposed.Right <= o.Left) continue; // not horizontally aligned -> not a vertical neighbor
            candidates.Add(o.Bottom + Gutter);
            candidates.Add(o.Top - Gutter - proposed.Height);
        }
        return PickClosest(proposed.Y, candidates);
    }

    private double PickClosest(double current, List<double> candidates)
    {
        var best = current;
        var bestDistance = Threshold;
        foreach (var c in candidates)
        {
            var d = Math.Abs(c - current);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = c;
            }
        }
        return best;
    }

    /// <summary>Finds a free spot for a new note: packs left-to-right, wrapping into gutter-spaced rows.</summary>
    public System.Windows.Point FindOpenPosition(Rectangle workingArea, double width, double height, IReadOnlyList<System.Windows.Rect> obstacles)
    {
        var y = workingArea.Top + Gutter;
        for (var row = 0; row < 200; row++)
        {
            var x = workingArea.Left + Gutter;
            while (x + width <= workingArea.Right - Gutter)
            {
                var candidate = new System.Windows.Rect(x, y, width, height);
                var blocker = FindBlocker(candidate, obstacles);
                if (blocker is null) return new System.Windows.Point(x, y);
                x = blocker.Value.Right + Gutter;
            }
            y += height + Gutter;
        }
        return new System.Windows.Point(workingArea.Left + Gutter, workingArea.Top + Gutter);
    }

    private System.Windows.Rect? FindBlocker(System.Windows.Rect candidate, IReadOnlyList<System.Windows.Rect> obstacles)
    {
        foreach (var o in obstacles)
        {
            var inflated = new System.Windows.Rect(o.X - Gutter / 2, o.Y - Gutter / 2, o.Width + Gutter, o.Height + Gutter);
            if (inflated.IntersectsWith(candidate)) return o;
        }
        return null;
    }
}
