#nullable enable

namespace ATAS.Indicators.Technical.Heatmap;

using System;

/// <summary>
/// One input bar of <see cref="HeatmapVwapBarEngine"/>: the candle fields the chart
/// <see cref="VWAP"/> indicator reads. Times are in the same time base as the chart candles
/// (<c>IndicatorCandle.Time</c> / <c>LastTime</c>).
/// </summary>
internal readonly record struct HeatmapVwapBar(
	DateTime Time,
	DateTime LastTime,
	decimal Open,
	decimal High,
	decimal Low,
	decimal Close,
	decimal Volume,
	decimal Bid,
	decimal Ask);

/// <summary>
/// The trading calendar the engine asks for period boundaries. Same semantics and time base as
/// <c>IIndicatorDataProvider.IsNewSession/IsNewWeek/IsNewMonth</c>, which the chart indicator calls with
/// the begin times of the previous and the current candle, and <c>InstrumentInfo.TimeZoneOffset</c>.
/// </summary>
internal interface IHeatmapVwapCalendar
{
	#region Properties

	/// <summary>The instrument time zone offset applied to custom session times.</summary>
	TimeSpan TimeZoneOffset { get; }

	#endregion

	#region Public methods

	bool IsNewSession(DateTime prevTime, DateTime newTime);

	bool IsNewWeek(DateTime prevTime, DateTime newTime);

	bool IsNewMonth(DateTime prevTime, DateTime newTime);

	#endregion
}

/// <summary>What the engine did with a bar.</summary>
internal enum HeatmapVwapBarState : byte
{
	/// <summary>Before the Days look back start: no value.</summary>
	BeforeLookBack,

	/// <summary>Custom period, the bar is outside the custom session: no value.</summary>
	OutsideSession,

	/// <summary>Weekly/Monthly/Custom without the first partial period, the first full period has not started yet: no value.</summary>
	NotStarted,

	/// <summary>The bar starts a period (or the calculation): the line breaks before it.</summary>
	PeriodStart,

	/// <summary>The bar continues the current period.</summary>
	Continuation
}

/// <summary>
/// The values of one bar, equal to what the chart <see cref="VWAP"/> stores for the same bar.
/// The chart's bands <c>Upper/Lower</c>, <c>Upper1/Lower1</c>, <c>Upper2/Lower2</c> are
/// <see cref="UpperStd1"/>/<see cref="LowerStd1"/> ... <see cref="UpperStd3"/>/<see cref="LowerStd3"/>.
/// Bars without a value hold zeros, as the chart series do.
/// </summary>
/// <param name="State">What the engine did with the bar.</param>
/// <param name="Value">VWAP or TWAP.</param>
/// <param name="UpperStd1">Value + first deviation.</param>
/// <param name="LowerStd1">Value - first deviation.</param>
/// <param name="UpperStd2">Value + second deviation.</param>
/// <param name="LowerStd2">Value - second deviation.</param>
/// <param name="UpperStd3">Value + third deviation.</param>
/// <param name="LowerStd3">Value - third deviation.</param>
/// <param name="PreviousUpper">The previous period value when price is above it (chart "Previous upper value"); 0 = none.</param>
/// <param name="PreviousLower">The previous period value when price is below it (chart "Previous lower value"); 0 = none.</param>
/// <param name="Direction">Direction colour of the chart line at this bar: 1 bullish, -1 bearish, 0 not coloured
/// (the first two bars of a period).</param>
internal readonly record struct HeatmapVwapBarResult(
	HeatmapVwapBarState State,
	decimal Value,
	decimal UpperStd1,
	decimal LowerStd1,
	decimal UpperStd2,
	decimal LowerStd2,
	decimal UpperStd3,
	decimal LowerStd3,
	decimal PreviousUpper,
	decimal PreviousLower,
	sbyte Direction)
{
	#region Properties

	/// <summary>True when the bar has a value (period start or continuation).</summary>
	public bool HasValue => State is HeatmapVwapBarState.PeriodStart or HeatmapVwapBarState.Continuation;

	#endregion
}

/// <summary>
/// Immutable snapshot of the calculation settings of <see cref="HeatmapVwapBarEngine"/>; the same
/// meaning and defaults as the chart <see cref="VWAP"/> properties of the same names.
/// </summary>
internal sealed record HeatmapVwapEngineSettings
{
	#region Auto properties

	public VWAP.VWAPPeriodType Period { get; init; } = VWAP.VWAPPeriodType.Daily;

	public VWAP.VWAPMode Mode { get; init; } = VWAP.VWAPMode.VWAP;

	public VWAP.VolumeType VolumeType { get; init; } = VWAP.VolumeType.Total;

	public decimal StDev { get; init; } = 1;

	public decimal StDev1 { get; init; } = 2;

	public decimal StDev2 { get; init; } = 3;

	public TimeSpan CustomSessionStart { get; init; } = TimeSpan.Zero;

	public TimeSpan CustomSessionEnd { get; init; } = new(23, 59, 59);

	/// <summary>Number of sessions to calculate, counted back from the last bar; 0 = all bars.</summary>
	public int Days { get; init; } = 20;

	public bool ShowFirstPeriod { get; init; }

	/// <summary>
	/// Whether to calculate the deviation bands. The deviation feeds only the bands (and the fills between them),
	/// never the VWAP/TWAP line, the previous period values or the direction, and each bar's deviation is
	/// independent of the earlier ones; false skips its cost (the TWAP window loop is most of a TWAP reload) and
	/// leaves the band values 0.
	/// </summary>
	public bool ComputeBands { get; init; } = true;

	#endregion
}
