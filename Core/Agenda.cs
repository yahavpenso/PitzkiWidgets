using Windows.ApplicationModel.Appointments;

using System.Globalization;

namespace PitzkiWidgets.Core;

/// <summary>
/// What is in the calendar of this PC: whatever the Windows Mail and Calendar app knows about, which
/// is the same thing the clock on the taskbar shows. Windows is asked for it through its own
/// appointments store, so nothing is read out of any file and nothing is sent anywhere. It is read
/// only: Pitzki never writes an event, moves one, or deletes one.
/// </summary>
public static class Agenda
{
    /// <summary>One thing in the calendar, as the card shows it.</summary>
    public sealed record Entry(string What, DateTime Starts, TimeSpan Lasts, bool AllDay, string Where)
    {
        /// <summary>When it ends, or the end of its day when it lasts all day.</summary>
        public DateTime Ends => AllDay ? Starts.Date.AddDays(1) : Starts + Lasts;

        /// <summary>True while it is going on right now.</summary>
        public bool Now(DateTime at) => Starts <= at && at < Ends;
    }

    /// <summary>Everything from now to the end of <paramref name="days"/> days, soonest first.</summary>
    public static async Task<List<Entry>> SoonAsync(DateTime now, int days = 7, int most = 20)
    {
        var store = await AppointmentManager.RequestStoreAsync(AppointmentStoreAccessType.AllCalendarsReadOnly);
        // from the start of today, so something that began an hour ago and is still going still shows
        var from = new DateTimeOffset(now.Date);
        var found = await store.FindAppointmentsAsync(from, TimeSpan.FromDays(days + 1));
        return Tidy(found.Select(a => new Entry(
            (a.Subject ?? "").Trim(), a.StartTime.LocalDateTime, a.Duration, a.AllDay, (a.Location ?? "").Trim())), now, days, most);
    }

    /// <summary>
    /// The arithmetic on its own, so the test can try it without a calendar: what has finished is
    /// dropped, what is too far ahead is dropped, the same event in two calendars is kept once, and
    /// the soonest comes first.
    /// </summary>
    internal static List<Entry> Tidy(IEnumerable<Entry> entries, DateTime now, int days = 7, int most = 20)
    {
        var until = now.Date.AddDays(days + 1);
        var seen = new HashSet<string>();
        var kept = new List<Entry>();
        foreach (var entry in entries.OrderBy(e => e.Starts).ThenBy(e => e.What, StringComparer.OrdinalIgnoreCase))
        {
            if (entry.What.Length == 0 || entry.Ends <= now || entry.Starts >= until) continue;
            // the same event often sits in two calendars at once (one account, two views of it)
            if (!seen.Add($"{entry.Starts:yyyyMMddHHmm}|{entry.What.ToLowerInvariant()}")) continue;
            kept.Add(entry);
            if (kept.Count >= most) break;
        }
        return kept;
    }

    /// <summary>
    /// "Now", "in 20 min", "14:30", "tomorrow 09:00", "Sun 09:00": how the card says when. With the
    /// day already written above the line, the clock alone is enough.
    /// </summary>
    internal static string When(Entry entry, DateTime now, bool dayShown = false)
    {
        if (entry.Now(now)) return "Now";
        var away = entry.Starts - now;
        if (away < TimeSpan.FromMinutes(60)) return $"in {Math.Max(1, (int)Math.Round(away.TotalMinutes))} min";
        int days = (entry.Starts.Date - now.Date).Days;
        // the days are named in English like the rest of the card, whatever language Windows is set to
        var plain = CultureInfo.InvariantCulture;
        string clock = entry.AllDay ? "all day" : entry.Starts.ToString("HH:mm", plain);
        if (dayShown) return clock;
        return days switch
        {
            0 => clock,
            1 => entry.AllDay ? "tomorrow" : "tomorrow " + clock,
            < 7 => entry.Starts.ToString("ddd", plain) + (entry.AllDay ? "" : " " + clock),
            _ => entry.Starts.ToString("d MMM", plain) + (entry.AllDay ? "" : " " + clock),
        };
    }
}
