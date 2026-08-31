namespace ATAS.Indicators.Technical.Extensions;

using System;

/// <summary>
/// Date-aware custom-session start detection shared by indicators that carry their own
/// session settings (PLAT-907). Comparing times of day alone misses a session start that
/// falls into an overnight or weekend gap of the data (start 09:00, first bar 09:30 after
/// yesterday's 15:00 close): the previous bar's time of day is then LATER than the start,
/// so the session would never open and the indicator would stay empty.
/// </summary>
public static class CustomSessionExtensions
{
	/// <summary>
	/// Whether the session-start instant (the start time of day on the bar's date, or the
	/// day before when the bar ends before it) lies after the previous bar's end and not
	/// later than this bar's end - i.e. the session opened inside this bar or in the gap
	/// before it. Without a previous bar the bar itself must contain the instant.
	/// All times are in the indicator's anchor clock.
	/// </summary>
	public static bool IsSessionStartCrossed(DateTime? previousBarEnd, DateTime barStart, DateTime barEnd, TimeSpan sessionStart)
	{
		var start = barEnd.Date + sessionStart;

		if (start > barEnd)
			start = start.AddDays(-1);

		return previousBarEnd is { } prevEnd ? start > prevEnd : start >= barStart;
	}
}
