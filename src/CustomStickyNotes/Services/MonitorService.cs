using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CustomStickyNotes.Models;

namespace CustomStickyNotes.Services;

public class MonitorService
{
    /// <summary>
    /// Enumerates connected monitors. Identity is derived from resolution + virtual-desktop
    /// position rather than OS index/device name, since those can shift on reconnect.
    /// </summary>
    public List<MonitorRecord> GetMonitors()
    {
        var list = new List<MonitorRecord>();
        foreach (var screen in Screen.AllScreens)
        {
            var b = screen.Bounds;
            var stableId = $"{b.Width}x{b.Height}@{b.X},{b.Y}";
            list.Add(new MonitorRecord
            {
                StableId = stableId,
                Bounds = b,
                WorkingArea = screen.WorkingArea,
                IsPrimary = screen.Primary,
            });
        }
        return list;
    }

    public MonitorRecord? FindByStableId(List<MonitorRecord> monitors, string? stableId)
    {
        if (stableId is null) return null;
        return monitors.FirstOrDefault(m => m.StableId == stableId);
    }

    public MonitorRecord GetPrimary(List<MonitorRecord> monitors)
    {
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }
}
