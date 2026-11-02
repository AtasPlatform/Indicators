#nullable enable

namespace ATAS.Indicators.Technical.Heatmap;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ATAS.Indicators.Heatmap;
using OFT.Localization;
using OFT.Rendering.Heatmap;
using OFT.Rendering.Settings;
using Utils.Common.Logging;
using static ATAS.Indicators.Heatmap.HeatmapIndicatorColors;

// Reference indicator: candle-driven price overlay (VWAP/TWAP of the chart).
// Patterns: IHeatmapCandleConsumer with a volatile request array refreshed in
//           ConfigureAsync, a pure calculation engine fed with the delivered
//           candles, cheap per-tick publishing (replace the forming bar's point,
//           append new bars) with a full Clear + republish in one lease when
//           earlier bars changed, gaps at period resets, PriceLine direction
//           colours and PriceBand fills.
/// <summary>
/// Heatmap VWAP/TWAP: the chart <see cref="VWAP"/> indicator calculated from time-based candles of the heatmap
/// instrument (<see cref="HeatmapVwapBarEngine"/>) and drawn over the heatmap.
/// </summary>
[HeatmapIndicator(
	id: "heatmap.vwap-twap",
	DisplayName = "VWAP/TWAP",
	ResourceType = typeof(Strings),
	DescriptionKey = nameof(Strings.HeatmapVwapDescription))]
public sealed class HeatmapVwapIndicator
	: HeatmapIndicator<HeatmapVwapSettings>
	, IHeatmapCandleConsumer
{
	#region Nested types

	/// <summary>One published series: which visual it belongs to and which engine value it carries.</summary>
	private readonly record struct Channel(
		VisualSlot Slot,
		BarValue Value,
		HeatmapIndicatorSeriesHandle<decimal> Series);

	/// <summary>The engine's calendar over the heatmap trading calendar.</summary>
	private sealed class CalendarAdapter : IHeatmapVwapCalendar
	{
		#region Readonly fields

		private readonly IHeatmapTradingCalendar _calendar;

		#endregion

		#region Properties

		// Read at the time of use: the user can change the offset while the heatmap is open.
		public TimeSpan TimeZoneOffset => _calendar.TimeZoneOffset;

		#endregion

		#region Constructors

		public CalendarAdapter(IHeatmapTradingCalendar calendar)
		{
			_calendar = calendar;
		}

		#endregion

		#region Public methods

		public bool IsNewSession(DateTime prevTime, DateTime newTime) => _calendar.IsNewSession(prevTime, newTime);

		public bool IsNewWeek(DateTime prevTime, DateTime newTime) => _calendar.IsNewWeek(prevTime, newTime);

		public bool IsNewMonth(DateTime prevTime, DateTime newTime) => _calendar.IsNewMonth(prevTime, newTime);

		#endregion
	}

	/// <summary>A read-only view of delivered candles as engine bars, without copying them.</summary>
	private sealed class BarList : IReadOnlyList<HeatmapVwapBar>
	{
		#region Readonly fields

		private readonly IReadOnlyList<HeatmapCandle> _candles;

		#endregion

		#region Properties

		public int Count => _candles.Count;

		#endregion

		#region Indexers

		public HeatmapVwapBar this[int index] => ToBar(_candles[index]);

		#endregion

		#region Constructors

		public BarList(IReadOnlyList<HeatmapCandle> candles)
		{
			_candles = candles;
		}

		#endregion

		#region Public methods

		public IEnumerator<HeatmapVwapBar> GetEnumerator()
		{
			for (var i = 0; i < _candles.Count; i++)
				yield return ToBar(_candles[i]);
		}

		#endregion

		#region Private methods

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		#endregion

		#region Private static methods

		private static HeatmapVwapBar ToBar(in HeatmapCandle candle) =>
			new(candle.Time, candle.LastTime, candle.Open, candle.High, candle.Low, candle.Close, candle.Volume, candle.Bid, candle.Ask);

		#endregion
	}

	#endregion

	#region Enum definitions

	/// <summary>The visuals in declaration order, which is also the drawing order (the VWAP line on top).</summary>
	private enum VisualSlot
	{
		UpperFill2,
		UpperFill,
		MiddleFillUp,
		MiddleFillDown,
		LowerFill,
		LowerFill2,
		UpperStd3,
		UpperStd2,
		UpperStd1,
		LowerStd1,
		LowerStd2,
		LowerStd3,
		PreviousUpper,
		PreviousLower,
		Vwap
	}

	/// <summary>An engine value of a bar.</summary>
	private enum BarValue
	{
		Vwap,
		UpperStd1,
		LowerStd1,
		UpperStd2,
		LowerStd2,
		UpperStd3,
		LowerStd3,
		PreviousUpper,
		PreviousLower
	}

	#endregion

	#region Const fields

	private const string VisualIdPrefix = "vwap-twap.";
	private const int VisualSlotCount = (int)VisualSlot.Vwap + 1;

	// _publishedBars value meaning "the series hold something that does not match the engine": republish all.
	private const int PublishedUnknown = -1;

	private const string NoCalendarMessage =
		"VWAP/TWAP: the trading session of the heatmap instrument is unknown, the indicator is not calculated.";

	#endregion

	#region Static fields

	private static readonly HeatmapIndicatorDescriptor _descriptor;
	private static readonly HeatmapIndicatorVisualHandle[] _visuals;
	private static readonly Channel[] _channels;

	#endregion

	#region Readonly initialized fields

	private readonly List<HeatmapCandle> _candles = new();
	private readonly bool[] _visualEnabled = new bool[VisualSlotCount];

	#endregion

	#region Fields

	// The declaration read by the platform's pump thread (GetRequiredCandleSeries): replaced wholesale in
	// ConfigureAsync, never mutated, hence volatile.
	private volatile CandleSeriesRequest[] _requests = { CreateRequest(new HeatmapVwapSettings()) };

	private HeatmapVwapBarEngine? _engine;
	private IHeatmapTradingCalendar? _engineCalendar;
	private TimeSpan _engineTimeZoneOffset;

	// How many bars of the engine are published; the last published bar is the only one whose points
	// may be replaced in place. See Publish.
	private int _publishedBars;
	private long _lastPublishedTimestamp;
	private int _lastPublishedMask;
	private bool _lastPublishedPeriodStart;

	// The visual styles must be (re)applied with the next commit: after a reset (which drops them), a settings
	// change or a new engine.
	private bool _stylesDirty = true;

	private string? _status;

	#endregion

	#region Properties

	/// <inheritdoc/>
	public override HeatmapIndicatorDescriptor Descriptor => _descriptor;

	/// <summary>
	/// Why the indicator shows nothing (failed candle series, unknown trading session); null while it calculates.
	/// The message is also written to the log once per change.
	/// </summary>
	internal string? Status => _status;

	/// <summary>The engine of the current settings and calendar (tests).</summary>
	internal HeatmapVwapBarEngine? Engine => _engine;

	#endregion

	#region Static constructors

	static HeatmapVwapIndicator()
	{
		var build = Describe<HeatmapVwapIndicator>();
		_visuals = new HeatmapIndicatorVisualHandle[VisualSlotCount];
		var channels = new List<Channel>();

		AddBand(VisualSlot.UpperFill2, "upper-fill-2", "Upper Fill 2", BarValue.UpperStd3, BarValue.UpperStd2);
		AddBand(VisualSlot.UpperFill, "upper-fill", "Upper Fill", BarValue.UpperStd2, BarValue.UpperStd1);
		AddBand(VisualSlot.MiddleFillUp, "middle-fill-up", "Middle Fill Up", BarValue.UpperStd1, BarValue.Vwap);
		AddBand(VisualSlot.MiddleFillDown, "middle-fill-down", "Middle Fill Down", BarValue.Vwap, BarValue.LowerStd1);
		AddBand(VisualSlot.LowerFill, "lower-fill", "Lower Fill", BarValue.LowerStd1, BarValue.LowerStd2);
		AddBand(VisualSlot.LowerFill2, "lower-fill-2", "Lower Fill 2", BarValue.LowerStd2, BarValue.LowerStd3);

		AddLine(VisualSlot.UpperStd3, "upper-std3", "Upper std3", BarValue.UpperStd3);
		AddLine(VisualSlot.UpperStd2, "upper-std2", "Upper std2", BarValue.UpperStd2);
		AddLine(VisualSlot.UpperStd1, "upper-std1", "Upper std1", BarValue.UpperStd1);
		AddLine(VisualSlot.LowerStd1, "lower-std1", "Lower std1", BarValue.LowerStd1);
		AddLine(VisualSlot.LowerStd2, "lower-std2", "Lower std2", BarValue.LowerStd2);
		AddLine(VisualSlot.LowerStd3, "lower-std3", "Lower std3", BarValue.LowerStd3);
		AddLine(VisualSlot.PreviousUpper, "previous-upper", "Previous upper value", BarValue.PreviousUpper);
		AddLine(VisualSlot.PreviousLower, "previous-lower", "Previous lower value", BarValue.PreviousLower);
		AddLine(VisualSlot.Vwap, "vwap", "VWAP/TWAP", BarValue.Vwap);

		_channels = channels.ToArray();
		_descriptor = build.Done();

		void AddLine(VisualSlot slot, string id, string label, BarValue value)
		{
			var visual = build.PriceLine(VisualIdPrefix + id, label);
			_visuals[(int)slot] = visual;
			channels.Add(new Channel(slot, value, visual.Series(
				VisualIdPrefix + id + ".value", HeatmapIndicatorSeriesRole.Price, HeatmapIndicatorValueKind.Price)));
		}

		// A series belongs to exactly one visual, so a fill publishes its edges in series of its own.
		void AddBand(VisualSlot slot, string id, string label, BarValue upper, BarValue lower)
		{
			var visual = build.PriceBand(VisualIdPrefix + id, label);
			_visuals[(int)slot] = visual;
			channels.Add(new Channel(slot, upper, visual.Series(
				VisualIdPrefix + id + ".upper", HeatmapIndicatorSeriesRole.BandUpper, HeatmapIndicatorValueKind.Price)));
			channels.Add(new Channel(slot, lower, visual.Series(
				VisualIdPrefix + id + ".lower", HeatmapIndicatorSeriesRole.BandLower, HeatmapIndicatorValueKind.Price)));
		}
	}

	#endregion

	#region Public methods

	/// <inheritdoc/>
	public override ValueTask ConfigureAsync(HeatmapVwapSettings settings, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var current = _requests[0];
		var next = CreateRequest(settings);

		// Keep the declared object while the series stays the same: updates carry the object we declared.
		if (!HeatmapCandleSeriesRequestComparer.Instance.Equals(current, next))
		{
			// Candles of another timeframe are of no use: wait for the Reload of the new series. A deeper or
			// shallower history of the same series still gives the right values until its Reload arrives.
			if (current.TimeFrame != next.TimeFrame)
			{
				_candles.Clear();
				_engine = null;
			}

			_requests = new[] { next };
		}

		UpdateEnabledVisuals();
		_stylesDirty = true;

		if (_candles.Count == 0 || !TryPrepareEngine(out var engine, out var rebuilt))
		{
			ClearOutput();
			return ValueTask.CompletedTask;
		}

		_publishedBars = PublishedUnknown;

		if (rebuilt || !engine.IsComplete)
			engine.Recalculate(new BarList(_candles), cancellationToken);

		// Styles and enabled visuals may have changed: republish everything in one lease.
		Publish(0);
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc/>
	public IReadOnlyCollection<CandleSeriesRequest> GetRequiredCandleSeries() => _requests;

	/// <inheritdoc/>
	public ValueTask ProcessCandlesAsync(HeatmapCandleSeriesUpdate update, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(update);
		cancellationToken.ThrowIfCancellationRequested();

		// An update of a series declared before the last settings change.
		if (!HeatmapCandleSeriesRequestComparer.Instance.Equals(update.Request, _requests[0]))
			return ValueTask.CompletedTask;

		if (update.Kind == HeatmapCandleUpdateKind.Failed)
		{
			_candles.Clear();
			_engine?.Clear();
			ClearOutput();
			ReportStatus(update.Error ?? "VWAP/TWAP: the candle series is not available.");
			return ValueTask.CompletedTask;
		}

		var engineBarsBefore = _candles.Count;

		// An update that does not fit the list (after a reset): the next Reload brings it back in sync.
		if (!update.ApplyTo(_candles))
			return ValueTask.CompletedTask;

		if (!TryPrepareEngine(out var engine, out var rebuilt))
		{
			ClearOutput();
			ReportStatus(NoCalendarMessage);
			return ValueTask.CompletedTask;
		}

		ReportStatus(null);

		// A cancelled calculation leaves the engine ahead of the series: republish everything next time.
		var publishedBars = _publishedBars;
		_publishedBars = PublishedUnknown;

		int firstChanged;

		if (rebuilt || update.Kind == HeatmapCandleUpdateKind.Reload || engine.Bars.Count != engineBarsBefore)
		{
			engine.Recalculate(new BarList(_candles), cancellationToken);
			firstChanged = 0;
		}
		else
		{
			// Continues a calculation cancelled by an earlier call (see HeatmapVwapBarEngine.Update).
			firstChanged = engine.Update(update.FirstIndex, new BarList(update.Candles), cancellationToken);
		}

		_publishedBars = rebuilt ? PublishedUnknown : publishedBars;
		Publish(firstChanged);
		return ValueTask.CompletedTask;
	}

	#endregion

	#region Protected methods

	/// <inheritdoc/>
	protected override ValueTask OnStateResetCoreAsync(
		IHeatmapIndicatorContext context,
		IHeatmapIndicatorRuntime runtime,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// The platform has cleared the series; a Reload of the candles follows.
		_candles.Clear();
		_engine = null;
		_engineCalendar = null;
		_publishedBars = 0;
		_stylesDirty = true;
		_status = null;
		return ValueTask.CompletedTask;
	}

	#endregion

	#region Private methods

	private bool TryPrepareEngine(out HeatmapVwapBarEngine engine, out bool rebuilt)
	{
		engine = null!;
		rebuilt = false;

		var calendar = Context?.TradingCalendar;

		if (calendar is null)
		{
			_engine = null;
			_engineCalendar = null;
			return false;
		}

		var settings = Settings.ToEngineSettings();

		// The custom session reads the time zone offset bar by bar: a changed offset invalidates every bar.
		var offsetChanged = settings.Period == VWAP.VWAPPeriodType.Custom && calendar.TimeZoneOffset != _engineTimeZoneOffset;

		if (_engine is null || !ReferenceEquals(calendar, _engineCalendar) || offsetChanged || _engine.Settings != settings)
		{
			_engine = new HeatmapVwapBarEngine(settings, new CalendarAdapter(calendar));
			_engineCalendar = calendar;
			_engineTimeZoneOffset = calendar.TimeZoneOffset;
			_stylesDirty = true;
			rebuilt = true;
		}

		engine = _engine;
		return true;
	}

	/// <summary>
	/// Publishes the engine results. Bars before <paramref name="firstChanged"/> are unchanged since the previous
	/// publish. When only the last published bar (the forming candle) and later bars changed, the points of the last
	/// published bar are replaced in place (Append with the same timestamp) and new bars are appended - the per-tick
	/// path. Otherwise every series is cleared and republished in the same lease.
	/// </summary>
	private void Publish(int firstChanged)
	{
		var engine = _engine!;
		var bars = engine.Bars;
		var results = engine.Results;
		var calendar = _engineCalendar!;
		var count = results.Count;

		using var lease = State.BeginUpdate();
		ApplyStylesIfDirty(lease);

		var start = _publishedBars;

		if (!CanPublishIncrementally(firstChanged, calendar))
		{
			foreach (var channel in _channels)
				lease.Series(channel.Series).Clear();

			start = 0;
		}
		else if (start > 0)
		{
			// Replace the points of the last published bar; its period state is unchanged, and only the previous
			// period markers may have switched series (see CanPublishIncrementally). A marker that left a series
			// turns its point into a gap at the same timestamp.
			var last = start - 1;
			var result = results[last];

			for (var i = 0; i < _channels.Length; i++)
			{
				var channel = _channels[i];

				if (!_visualEnabled[(int)channel.Slot])
					continue;

				if (TryGetValue(result, channel.Value, out var value))
					lease.Series(channel.Series).Append(_lastPublishedTimestamp, value);
				else if ((_lastPublishedMask & (1 << i)) != 0)
					lease.Series(channel.Series).AppendGap(_lastPublishedTimestamp);
			}
		}

		var previousTimestamp = start > 0 ? _lastPublishedTimestamp : 0;
		var barDurationNanos = GetBarDurationNanos();

		for (var bar = start; bar < count; bar++)
		{
			var timestamp = calendar.ToTimelineNanos(bars[bar].Time);
			PublishBar(lease, engine, bar, timestamp, previousTimestamp, barDurationNanos);
			previousTimestamp = timestamp;
		}

		_publishedBars = count;

		if (count > 0)
		{
			var lastResult = results[count - 1];
			_lastPublishedTimestamp = previousTimestamp;
			_lastPublishedMask = GetValueMask(lastResult);
			_lastPublishedPeriodStart = lastResult.State == HeatmapVwapBarState.PeriodStart;
		}
	}

	// Gap rule. Bars of one session are connected point to point, even across minutes without candles. The line
	// is broken after a bar with a value when the next bar has no value (outside the look back, outside the custom
	// session, before the first full period), starts a new period, or opens a new trading session (overnight
	// break, weekend) even when the period continues. The gap goes at min(next bar - 1 ns, bar + its duration):
	// the renderer holds the value of the bar up to the gap, so the value covers only the bar's own duration, not
	// the empty time of a break. The gap belongs to the earlier bar and is written when the next bar arrives; the
	// forming last bar has none (the renderer holds it to the cutoff).
	private void PublishBar(
		IHeatmapVisualStateLease lease,
		HeatmapVwapBarEngine engine,
		int bar,
		long timestamp,
		long previousTimestamp,
		long barDurationNanos)
	{
		var results = engine.Results;
		var result = results[bar];
		var hasPrevious = bar > 0;
		var previous = hasPrevious ? results[bar - 1] : default;
		var breaksLine = result.State == HeatmapVwapBarState.PeriodStart || (hasPrevious && engine.SessionStarts[bar]);
		var gapTimestamp = Math.Min(timestamp - 1, previousTimestamp + barDurationNanos);

		foreach (var channel in _channels)
		{
			if (!_visualEnabled[(int)channel.Slot])
				continue;

			var hasValue = TryGetValue(result, channel.Value, out var value);
			var series = lease.Series(channel.Series);

			if (hasPrevious && TryGetValue(previous, channel.Value, out _) && (!hasValue || breaksLine))
				series.AppendGap(gapTimestamp);

			if (hasValue)
				series.Append(timestamp, value);
		}
	}

	private bool CanPublishIncrementally(int firstChanged, IHeatmapTradingCalendar calendar)
	{
		var published = _publishedBars;

		if (published == PublishedUnknown)
			return false;

		// Nothing published yet: the series are empty, append everything.
		if (published == 0)
			return true;

		var last = published - 1;
		var results = _engine!.Results;

		if (firstChanged < last || results.Count <= last)
			return false;

		// The last published bar may be replaced in place only when it is the same candle and publishes the same
		// value kinds with the same period state: otherwise its points or the gap before it (written for the bar
		// before it) would differ. Its session start depends only on the two candle times, checked by the timestamp.
		// The exception is the previous period markers: the forming close moves them between the upper and the lower
		// series whenever it crosses the previous value, which in a range happens on most ticks. Their value is
		// constant within the period, so a point replaced by a gap, or a point after the gap written for an empty
		// bar, draws the same flat line as a republish; republishing every series on each cross would not.
		var result = results[last];
		var changedKinds = GetValueMask(result) ^ _lastPublishedMask;

		return calendar.ToTimelineNanos(_engine.Bars[last].Time) == _lastPublishedTimestamp
			&& (changedKinds & ~GetPreviousValueMask()) == 0
			&& (result.State == HeatmapVwapBarState.PeriodStart) == _lastPublishedPeriodStart;
	}

	private void ClearOutput()
	{
		if (_publishedBars == 0 && !_stylesDirty)
			return;

		using var lease = State.BeginUpdate();
		ApplyStylesIfDirty(lease);

		foreach (var channel in _channels)
			lease.Series(channel.Series).Clear();

		_publishedBars = 0;
	}

	private void UpdateEnabledVisuals()
	{
		var settings = Settings;
		var bands = !settings.VWAPOnly;

		_visualEnabled[(int)VisualSlot.Vwap] = true;
		_visualEnabled[(int)VisualSlot.UpperStd3] = bands && settings.UpperStd3Color.A > 0;
		_visualEnabled[(int)VisualSlot.UpperStd2] = bands && settings.UpperStd2Color.A > 0;
		_visualEnabled[(int)VisualSlot.UpperStd1] = bands && settings.UpperStd1Color.A > 0;
		_visualEnabled[(int)VisualSlot.LowerStd1] = bands && settings.LowerStd1Color.A > 0;
		_visualEnabled[(int)VisualSlot.LowerStd2] = bands && settings.LowerStd2Color.A > 0;
		_visualEnabled[(int)VisualSlot.LowerStd3] = bands && settings.LowerStd3Color.A > 0;
		_visualEnabled[(int)VisualSlot.UpperFill2] = bands && settings.UpperFill2Color.A > 0;
		_visualEnabled[(int)VisualSlot.UpperFill] = bands && settings.UpperFillColor.A > 0;
		_visualEnabled[(int)VisualSlot.MiddleFillUp] = bands && settings.MiddleFillUpColor.A > 0;
		_visualEnabled[(int)VisualSlot.MiddleFillDown] = bands && settings.MiddleFillDownColor.A > 0;
		_visualEnabled[(int)VisualSlot.LowerFill] = bands && settings.LowerFillColor.A > 0;
		_visualEnabled[(int)VisualSlot.LowerFill2] = bands && settings.LowerFill2Color.A > 0;
		_visualEnabled[(int)VisualSlot.PreviousUpper] = settings.ShowPreviousValues;
		_visualEnabled[(int)VisualSlot.PreviousLower] = settings.ShowPreviousValues;
	}

	private void ApplyStylesIfDirty(IHeatmapVisualStateLease lease)
	{
		if (!_stylesDirty)
			return;

		_stylesDirty = false;
		var settings = Settings;
		var label = settings.TWAPMode == VWAP.VWAPMode.TWAP ? "TWAP" : "VWAP";

		var directionColors = settings.ColoredDirection
			? new HeatmapIndicatorSplitColors(ToHex(settings.BullishColor), ToHex(settings.BearishColor))
			: null;

		// Only the VWAP line keeps its price badge at the cutoff; eight more badges would crowd the price axis.
		SetStyle(VisualSlot.Vwap, LineStyle(settings.VwapColor, settings.VwapWidth, settings.VwapLineStyle, label, showPrice: true, directionColors));
		SetStyle(VisualSlot.UpperStd3, LineStyle(settings.UpperStd3Color, settings.UpperStd3Width, settings.UpperStd3LineStyle, Strings.UpperStd3, showPrice: false));
		SetStyle(VisualSlot.UpperStd2, LineStyle(settings.UpperStd2Color, settings.UpperStd2Width, settings.UpperStd2LineStyle, Strings.UpperStd2, showPrice: false));
		SetStyle(VisualSlot.UpperStd1, LineStyle(settings.UpperStd1Color, settings.UpperStd1Width, settings.UpperStd1LineStyle, Strings.UpperStd1, showPrice: false));
		SetStyle(VisualSlot.LowerStd1, LineStyle(settings.LowerStd1Color, settings.LowerStd1Width, settings.LowerStd1LineStyle, Strings.LowerStd1, showPrice: false));
		SetStyle(VisualSlot.LowerStd2, LineStyle(settings.LowerStd2Color, settings.LowerStd2Width, settings.LowerStd2LineStyle, Strings.LowerStd2, showPrice: false));
		SetStyle(VisualSlot.LowerStd3, LineStyle(settings.LowerStd3Color, settings.LowerStd3Width, settings.LowerStd3LineStyle, Strings.LowerStd3, showPrice: false));
		SetStyle(VisualSlot.PreviousUpper, LineStyle(settings.PreviousUpperColor, settings.PreviousWidth, LineDashStyle.Dot, Strings.HeatmapVwapPreviousUpperValue, showPrice: false));
		SetStyle(VisualSlot.PreviousLower, LineStyle(settings.PreviousLowerColor, settings.PreviousWidth, LineDashStyle.Dot, Strings.HeatmapVwapPreviousLowerValue, showPrice: false));
		SetStyle(VisualSlot.UpperFill2, FillStyle(settings.UpperFill2Color, Strings.UpperFill2));
		SetStyle(VisualSlot.UpperFill, FillStyle(settings.UpperFillColor, Strings.UpperFill));
		SetStyle(VisualSlot.MiddleFillUp, FillStyle(settings.MiddleFillUpColor, Strings.MiddleFillUp));
		SetStyle(VisualSlot.MiddleFillDown, FillStyle(settings.MiddleFillDownColor, Strings.MiddleFillDown));
		SetStyle(VisualSlot.LowerFill, FillStyle(settings.LowerFillColor, Strings.LowerFill));
		SetStyle(VisualSlot.LowerFill2, FillStyle(settings.LowerFill2Color, Strings.LowerFill2));

		void SetStyle(VisualSlot slot, HeatmapIndicatorVisualStyle style) =>
			lease.Visual(_visuals[(int)slot]).Style = style;
	}

	// The duration of one candle of the declared series, in timeline nanoseconds.
	private long GetBarDurationNanos() =>
		(_requests[0].TimeFrame.TimeBasedDuration ?? TimeSpan.FromMinutes(1)).Ticks * 100;

	// Which published series have a point at this bar.
	private int GetValueMask(in HeatmapVwapBarResult result)
	{
		var mask = 0;

		for (var i = 0; i < _channels.Length; i++)
		{
			var channel = _channels[i];

			if (_visualEnabled[(int)channel.Slot] && TryGetValue(result, channel.Value, out _))
				mask |= 1 << i;
		}

		return mask;
	}

	// Which published series carry the previous period markers.
	private int GetPreviousValueMask()
	{
		var mask = 0;

		for (var i = 0; i < _channels.Length; i++)
		{
			if (_channels[i].Value is BarValue.PreviousUpper or BarValue.PreviousLower)
				mask |= 1 << i;
		}

		return mask;
	}

	private void ReportStatus(string? message)
	{
		if (message == _status)
			return;

		_status = message;

		if (message != null)
			this.LogWarn("{0}", message);
	}

	#endregion

	#region Private static methods

	private static CandleSeriesRequest CreateRequest(HeatmapVwapSettings settings)
	{
		var days = Math.Clamp(settings.Days, 0, 30);

		// Instrument left empty: the heatmap instrument.
		return new CandleSeriesRequest
		{
			TimeFrame = settings.GetCandleTimeFrame(),
			DaysToLoad = days == 0 ? 30 : days
		};
	}

	private static bool TryGetValue(in HeatmapVwapBarResult result, BarValue kind, out decimal value)
	{
		if (!result.HasValue)
		{
			value = 0;
			return false;
		}

		value = kind switch
		{
			BarValue.Vwap => result.Value,
			BarValue.UpperStd1 => result.UpperStd1,
			BarValue.LowerStd1 => result.LowerStd1,
			BarValue.UpperStd2 => result.UpperStd2,
			BarValue.LowerStd2 => result.LowerStd2,
			BarValue.UpperStd3 => result.UpperStd3,
			BarValue.LowerStd3 => result.LowerStd3,
			BarValue.PreviousUpper => result.PreviousUpper,
			BarValue.PreviousLower => result.PreviousLower,
			_ => 0
		};

		// The chart hides zero previous-period markers (ShowZeroValue = false, the line ends there).
		return kind is not (BarValue.PreviousUpper or BarValue.PreviousLower) || value != 0;
	}

	private static HeatmapIndicatorVisualStyle LineStyle(
		CrossColor color,
		int width,
		LineDashStyle dashStyle,
		string label,
		bool showPrice,
		HeatmapIndicatorColorScheme? colorScheme = null) =>
		new(
			Color: ToHex(color),
			Thickness: width,
			Label: label,
			LineDashStyle: dashStyle == LineDashStyle.Solid ? null : dashStyle.ToString(),
			ShowPrice: showPrice,
			ColorScheme: colorScheme);

	private static HeatmapIndicatorVisualStyle FillStyle(CrossColor color, string label) =>
		new(Color: ToHex(color), Label: label);

	#endregion
}
