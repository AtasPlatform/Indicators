#nullable enable

namespace ATAS.Indicators.Technical.Heatmap;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// Pure port of the chart <see cref="VWAP"/> calculation (<c>VWAP.OnCalculate</c>) over a list of bars.
/// For the same bars, settings and calendar it produces exactly the values the chart indicator stores,
/// quirks included (the TWAP deviation window, the hour-of-day test of the Hourly period, the custom
/// session rules, the previous-period markers). Keep it in step with <c>VWAP.cs</c>; the parity tests in
/// <c>Tests/Indicators.Tests/HeatmapVwapParityTests.cs</c> run both on the same candles.
/// <para>
/// Not ported: the hidden custom start point (hotkeys / saved start date) of the chart indicator.
/// </para>
/// <para>
/// Incremental use: <see cref="Update"/> replaces the bars from an index on (the forming bar, finished
/// bars, new bars) and recalculates from there. The result is always identical to
/// <see cref="Recalculate"/> over the same bars: per-bar scalar state is checkpointed, and when the
/// Days look back start moves (a new session arrived) the whole list is recalculated. The live chart
/// keeps its look back start until it recalculates; the heatmap follows the recalculated chart.
/// </para>
/// Not thread-safe: use it from one thread (the indicator runner).
/// </summary>
internal sealed class HeatmapVwapBarEngine
{
	#region Nested types

	/// <summary>Running sums of a bar and the scalar state after it (the chart's fields).</summary>
	private readonly record struct BarState(
		decimal TotalVolume,
		decimal TotalVolToClose,
		decimal SumSrcSrcVol,
		int ZeroBarAfter,
		bool CalcStartedAfter);

	#endregion

	#region Const fields

	// VWAP.Period (obsolete, hidden, always 300) caps the TWAP deviation window.
	private const int TwapDeviationPeriod = 300;

	// How often a long calculation checks its cancellation token (a power of two).
	private const int CancellationCheckInterval = 256;

	#endregion

	#region Static fields

	private static readonly TimeSpan _m15 = TimeSpan.FromMinutes(15);
	private static readonly TimeSpan _m30 = TimeSpan.FromMinutes(30);
	private static readonly TimeSpan _h4 = TimeSpan.FromHours(4);

	#endregion

	#region Readonly initialized fields

	private readonly List<HeatmapVwapBar> _bars = new();
	private readonly List<bool> _sessionStarts = new();
	private readonly List<BarState> _states = new();
	private readonly List<HeatmapVwapBarResult> _results = new();

	#endregion

	#region Readonly fields

	private readonly HeatmapVwapEngineSettings _settings;
	private readonly IHeatmapVwapCalendar _calendar;

	#endregion

	#region Fields

	private int _targetBar;
	private int _zeroBar;
	private bool _calcStarted;

	// A session start was added or removed since the look back start was computed.
	private bool _sessionStartsChanged;

	#endregion

	#region Properties

	/// <summary>The settings the engine calculates with.</summary>
	public HeatmapVwapEngineSettings Settings => _settings;

	/// <summary>The bars the engine holds.</summary>
	public IReadOnlyList<HeatmapVwapBar> Bars => _bars;

	/// <summary>One result per bar of <see cref="Bars"/>.</summary>
	public IReadOnlyList<HeatmapVwapBarResult> Results => _results;

	/// <summary>
	/// Per bar of <see cref="Bars"/>: whether it opens a trading session (<see cref="IHeatmapVwapCalendar.IsNewSession"/>
	/// against the previous bar; true for the first bar, as in the chart).
	/// </summary>
	public IReadOnlyList<bool> SessionStarts => _sessionStarts;

	/// <summary>The first bar of the Days look back window (0 when Days is 0 or the history is shorter).</summary>
	public int TargetBar => _targetBar;

	/// <summary>
	/// Whether every bar has its result. False after a calculation was cancelled: the results then end
	/// before the bars, and the next call must be <see cref="Recalculate"/>.
	/// </summary>
	public bool IsComplete => _results.Count == _bars.Count;

	#endregion

	#region Constructors

	public HeatmapVwapBarEngine(HeatmapVwapEngineSettings settings, IHeatmapVwapCalendar calendar)
	{
		_settings = settings ?? throw new ArgumentNullException(nameof(settings));
		_calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
	}

	#endregion

	#region Public methods

	/// <summary>Replaces all bars and recalculates them.</summary>
	/// <param name="bars">The bars in time order.</param>
	/// <param name="cancellationToken">Checked every few hundred bars; a cancelled calculation leaves
	/// <see cref="IsComplete"/> false.</param>
	public void Recalculate(IReadOnlyList<HeatmapVwapBar> bars, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(bars);
		Update(0, bars, cancellationToken);
	}

	/// <summary>
	/// Replaces the bars from <paramref name="firstIndex"/> on with <paramref name="bars"/> (the list after
	/// the call is the old bars before <paramref name="firstIndex"/> followed by <paramref name="bars"/>)
	/// and recalculates.
	/// </summary>
	/// <param name="firstIndex">The first replaced bar; at most <c>Bars.Count</c> (append).</param>
	/// <param name="bars">The new bars from <paramref name="firstIndex"/> on.</param>
	/// <param name="cancellationToken">Checked every few hundred bars; a cancelled calculation leaves
	/// <see cref="IsComplete"/> false.</param>
	/// <returns>The first bar whose result was recalculated: <paramref name="firstIndex"/>, or 0 when the
	/// look back start moved and everything was recalculated.</returns>
	public int Update(int firstIndex, IReadOnlyList<HeatmapVwapBar> bars, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(bars);

		if ((uint)firstIndex > (uint)_bars.Count)
			throw new ArgumentOutOfRangeException(nameof(firstIndex), firstIndex, "The first index must be within the bar list or right after it.");

		TruncateBars(firstIndex);

		for (var i = 0; i < bars.Count; i++)
			AddBar(bars[i]);

		return Calculate(firstIndex, cancellationToken);
	}

	/// <summary>Appends one bar and calculates it.</summary>
	/// <param name="bar">The new bar.</param>
	/// <returns>The first recalculated bar (see <see cref="Update"/>).</returns>
	public int Append(in HeatmapVwapBar bar)
	{
		var index = _bars.Count;
		AddBar(bar);
		return Calculate(index, CancellationToken.None);
	}

	/// <summary>Replaces the last bar (the forming candle) and recalculates it.</summary>
	/// <param name="bar">The new state of the last bar.</param>
	/// <returns>The first recalculated bar (see <see cref="Update"/>).</returns>
	public int ReplaceLast(in HeatmapVwapBar bar)
	{
		if (_bars.Count == 0)
			throw new InvalidOperationException("There is no bar to replace.");

		var index = _bars.Count - 1;
		TruncateBars(index);
		AddBar(bar);
		return Calculate(index, CancellationToken.None);
	}

	/// <summary>Removes all bars and results.</summary>
	public void Clear()
	{
		_bars.Clear();
		_sessionStarts.Clear();
		_states.Clear();
		_results.Clear();
		_targetBar = 0;
		_zeroBar = 0;
		_calcStarted = false;
		_sessionStartsChanged = false;
	}

	#endregion

	#region Private methods

	private void AddBar(in HeatmapVwapBar bar)
	{
		var index = _bars.Count;
		_bars.Add(bar);

		// IsNewSession(0) is true in the chart (Extensions.IsNewIndicatorSession: no previous bar).
		var isSessionStart = index == 0 || _calendar.IsNewSession(_bars[index - 1].Time, bar.Time);
		_sessionStarts.Add(isSessionStart);
		_sessionStartsChanged |= isSessionStart;
	}

	private int Calculate(int firstIndex, CancellationToken cancellationToken)
	{
		// A cancelled calculation left results only for a prefix of the bars: continue from there.
		firstIndex = Math.Min(firstIndex, _results.Count);

		// The look back start depends only on where the sessions start: recount them only when that changed.
		var target = firstIndex == 0 || _sessionStartsChanged ? ComputeTargetBar() : _targetBar;
		_sessionStartsChanged = false;
		var from = firstIndex;

		if (target != _targetBar || from == 0)
		{
			_targetBar = target;
			from = 0;
		}

		Truncate(_states, from);
		Truncate(_results, from);

		if (from == 0)
		{
			// VWAP.OnCalculate(0) resets _calcStarted; _zeroBar is assigned by the first calculated bar.
			_calcStarted = false;
			_zeroBar = 0;
		}
		else
		{
			var before = _states[from - 1];
			_calcStarted = before.CalcStartedAfter;
			_zeroBar = before.ZeroBarAfter;
		}

		for (var bar = from; bar < _bars.Count; bar++)
		{
			if ((bar & (CancellationCheckInterval - 1)) == 0)
				cancellationToken.ThrowIfCancellationRequested();

			CalculateBar(bar);
		}

		return from;
	}

	private void TruncateBars(int count)
	{
		for (var i = count; i < _sessionStarts.Count; i++)
			_sessionStartsChanged |= _sessionStarts[i];

		Truncate(_bars, count);
		Truncate(_sessionStarts, count);
	}

	// VWAP.OnCalculate(0): counts sessions back from the last bar.
	private int ComputeTargetBar()
	{
		var targetBar = 0;

		if (_settings.Days <= 0)
			return targetBar;

		var days = 0;

		for (var i = _bars.Count - 1; i >= 0; i--)
		{
			targetBar = i;

			if (!_sessionStarts[i])
				continue;

			days++;

			if (days == _settings.Days)
				break;
		}

		return targetBar;
	}

	// Mirrors VWAP.OnCalculate(bar, value) statement by statement.
	private void CalculateBar(int bar)
	{
		if (bar < _targetBar)
		{
			StoreEmpty(HeatmapVwapBarState.BeforeLookBack);
			return;
		}

		var period = _settings.Period;

		if (period is VWAP.VWAPPeriodType.Custom && !InsideSession(bar))
		{
			StoreEmpty(HeatmapVwapBarState.OutsideSession);
			return;
		}

		if (!_settings.ShowFirstPeriod && !_calcStarted
			&& period is VWAP.VWAPPeriodType.Weekly or VWAP.VWAPPeriodType.Monthly or VWAP.VWAPPeriodType.Custom)
		{
			if (bar == 0 && period is not VWAP.VWAPPeriodType.Custom)
			{
				StoreEmpty(HeatmapVwapBarState.NotStarted);
				return;
			}

			_calcStarted = period switch
			{
				VWAP.VWAPPeriodType.Weekly => IsNewWeek(bar),
				VWAP.VWAPPeriodType.Monthly => IsNewMonth(bar),
				VWAP.VWAPPeriodType.Custom => IsNewCustomSession(bar),
				_ => _calcStarted
			};

			if (!_calcStarted)
			{
				StoreEmpty(HeatmapVwapBarState.NotStarted);
				return;
			}
		}

		var candle = _bars[bar];
		var isTwap = _settings.Mode == VWAP.VWAPMode.TWAP;

		var volume = _settings.VolumeType switch
		{
			VWAP.VolumeType.Total => candle.Volume,
			VWAP.VolumeType.Bid => candle.Bid,
			VWAP.VolumeType.Ask => candle.Ask,
			_ => candle.Volume
		};

		// DataSeriesType.HLC3 of the chart source (CandlePartSeries).
		var typical = (candle.High + candle.Low + candle.Close) / 3;

		var computeBands = _settings.ComputeBands;

		if (bar == _targetBar)
		{
			_zeroBar = bar;
			var sumSrcSrcVol = volume * typical * typical;

			if (isTwap)
			{
				var band = computeBands ? typical : 0;

				Store(new BarState(volume, typical, sumSrcSrcVol, _zeroBar, _calcStarted),
					new HeatmapVwapBarResult(HeatmapVwapBarState.PeriodStart, typical, band, band, band, band, band, band, 0, 0, 0));
			}
			else
			{
				var value = volume == 0 ? 0 : typical;
				var band = computeBands ? value : 0;

				Store(new BarState(volume, typical * volume, sumSrcSrcVol, _zeroBar, _calcStarted),
					new HeatmapVwapBarResult(HeatmapVwapBarState.PeriodStart, value, band, band, band, band, band, band, 0, 0, 0));
			}

			return;
		}

		var prevCandle = _bars[bar - 1];

		var needReset = period switch
		{
			VWAP.VWAPPeriodType.M15 => PeriodNumber(prevCandle.Time, _m15) != PeriodNumber(candle.Time, _m15),
			VWAP.VWAPPeriodType.M30 => PeriodNumber(prevCandle.Time, _m30) != PeriodNumber(candle.Time, _m30),
			VWAP.VWAPPeriodType.Hourly => prevCandle.Time.Hour != candle.Time.Hour,
			VWAP.VWAPPeriodType.H4 => PeriodNumber(prevCandle.Time, _h4) != PeriodNumber(candle.Time, _h4),
			VWAP.VWAPPeriodType.Daily => _sessionStarts[bar],
			VWAP.VWAPPeriodType.Weekly => IsNewWeek(bar),
			VWAP.VWAPPeriodType.Monthly => IsNewMonth(bar),
			VWAP.VWAPPeriodType.Custom => IsNewCustomSession(bar),
			_ => false
		};

		decimal totalVolume, totalVolToClose, sumSrcSrc;

		if (needReset)
		{
			_zeroBar = bar;
			totalVolume = volume;
			totalVolToClose = isTwap ? typical : typical * volume;
			sumSrcSrc = volume * typical * typical;
		}
		else
		{
			var prevState = _states[bar - 1];
			totalVolume = prevState.TotalVolume + volume;
			totalVolToClose = prevState.TotalVolToClose + (isTwap ? typical : typical * volume);

			// The chart leaves _sumSrcSrcVol untouched in TWAP mode; TWAP never reads it.
			sumSrcSrc = isTwap ? 0 : prevState.SumSrcSrcVol + volume * typical * typical;
		}

		decimal stdDev = 0m, currentValue, lastValue;
		lastValue = _results[bar - 1].Value;

		if (isTwap)
		{
			currentValue = totalVolToClose / (bar - _zeroBar + 1);

			if (computeBands && bar != _zeroBar)
			{
				var window = Math.Min(bar - _zeroBar, TwapDeviationPeriod);
				var average = CalcAverage(window, bar, currentValue);
				var results = CollectionsMarshal.AsSpan(_results);
				var sqrSum = 0m;

				// Hot loop of a TWAP reload (up to 301 bars per bar): read the results by reference.
				for (var i = bar - window; i <= bar; i++)
				{
					var diff = average - (i == bar ? currentValue : results[i].Value);
					sqrSum += diff * diff;
				}

				stdDev = (decimal)Math.Sqrt((double)sqrSum / window);
			}
		}
		else
		{
			// Replay can open a period before any volume has arrived.
			currentValue = totalVolume == 0 ? 0 : totalVolToClose / totalVolume;

			if (computeBands)
			{
				var variance = totalVolume == 0 ? 0 : sumSrcSrc / totalVolume - currentValue * currentValue;
				variance = variance < 0 ? 0 : variance;
				stdDev = (decimal)Math.Sqrt((double)variance);
			}
		}

		sbyte direction = 0;

		// The chart colours a bar from the third bar of a period on ("colored series point bug").
		if (bar - _zeroBar > 1 && bar != 0)
			direction = currentValue > lastValue ? (sbyte)1 : (sbyte)-1;

		var std = stdDev * _settings.StDev;
		var std1 = stdDev * _settings.StDev1;
		var std2 = stdDev * _settings.StDev2;

		// Without bands the band values stay 0 (currentValue +- 0 would look like computed bands).
		var bandBase = computeBands ? currentValue : 0;

		decimal previousUpper = 0m, previousLower = 0m;

		if (bar != 0)
		{
			if (needReset)
			{
				if (lastValue < currentValue)
					previousUpper = lastValue;
				else
					previousLower = lastValue;
			}
			else
			{
				var prevResult = _results[bar - 1];

				var prevValue = prevResult.PreviousUpper != 0
					? prevResult.PreviousUpper
					: prevResult.PreviousLower;

				if (candle.Close >= prevValue)
					previousUpper = prevValue;
				else
					previousLower = prevValue;
			}
		}

		Store(new BarState(totalVolume, totalVolToClose, sumSrcSrc, _zeroBar, _calcStarted),
			new HeatmapVwapBarResult(
				needReset ? HeatmapVwapBarState.PeriodStart : HeatmapVwapBarState.Continuation,
				currentValue,
				bandBase + std,
				bandBase - std,
				bandBase + std1,
				bandBase - std1,
				bandBase + std2,
				bandBase - std2,
				previousUpper,
				previousLower,
				direction));
	}

	// Extensions.CalcAverage(IDataSeries<decimal>, period, bar) over the VWAP/TWAP values.
	private decimal CalcAverage(int period, int bar, decimal currentValue)
	{
		var start = Math.Max(0, bar - period + 1);
		var count = Math.Min(bar + 1, period);
		var results = CollectionsMarshal.AsSpan(_results);

		var sum = 0m;

		for (var i = start; i < start + count; i++)
			sum += i == bar ? currentValue : results[i].Value;

		return sum / count;
	}

	private bool IsNewWeek(int bar) =>
		bar == 0 || _calendar.IsNewWeek(_bars[bar - 1].Time, _bars[bar].Time);

	private bool IsNewMonth(int bar) =>
		bar == 0 || _calendar.IsNewMonth(_bars[bar - 1].Time, _bars[bar].Time);

	// VWAP.IsNewCustomSession.
	private bool IsNewCustomSession(int bar)
	{
		var currentBar = _bars[bar];
		var offset = _calendar.TimeZoneOffset;
		var endTime = currentBar.LastTime.Add(offset);
		var sessionStart = endTime.Date.Add(_settings.CustomSessionStart);

		if (sessionStart > endTime)
			sessionStart = sessionStart.AddDays(-1);

		// The first loaded candle must contain the boundary to be a complete period.
		// Later candles reset when the boundary was crossed, including data gaps.
		if (bar == 0)
			return currentBar.Time.Add(offset) <= sessionStart;

		// A boundary already reached by the previous candle belongs to that candle.
		return _bars[bar - 1].LastTime.Add(offset) < sessionStart;
	}

	// VWAP.InsideSession.
	private bool InsideSession(int bar)
	{
		var currentBar = _bars[bar];
		var offset = _calendar.TimeZoneOffset;

		var startTime = currentBar.Time.Add(offset);
		var endTime = currentBar.LastTime.Add(offset);

		var customSessionStart = _settings.CustomSessionStart;
		var customSessionEnd = _settings.CustomSessionEnd;
		var sessionCrossesMidnight = customSessionStart > customSessionEnd;

		if (sessionCrossesMidnight)
		{
			return (startTime.TimeOfDay >= customSessionStart || startTime.TimeOfDay <= customSessionEnd) ||
				(endTime.TimeOfDay >= customSessionStart || endTime.TimeOfDay <= customSessionEnd) ||
				(startTime.TimeOfDay <= customSessionEnd && endTime.TimeOfDay >= customSessionStart);
		}

		return (startTime.TimeOfDay >= customSessionStart && startTime.TimeOfDay <= customSessionEnd) ||
			(endTime.TimeOfDay >= customSessionStart && endTime.TimeOfDay <= customSessionEnd) ||
			(startTime.TimeOfDay <= customSessionEnd && endTime.TimeOfDay >= customSessionStart);
	}

	private void StoreEmpty(HeatmapVwapBarState state) =>
		Store(new BarState(0, 0, 0, _zeroBar, _calcStarted), new HeatmapVwapBarResult { State = state });

	private void Store(in BarState state, in HeatmapVwapBarResult result)
	{
		_states.Add(state);
		_results.Add(result);
	}

	#endregion

	#region Private static methods

	private static long PeriodNumber(DateTime time, TimeSpan period) => time.Ticks / period.Ticks;

	private static void Truncate<T>(List<T> list, int count)
	{
		if (list.Count > count)
			list.RemoveRange(count, list.Count - count);
	}

	#endregion
}
