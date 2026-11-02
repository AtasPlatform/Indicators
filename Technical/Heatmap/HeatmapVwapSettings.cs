#nullable enable

namespace ATAS.Indicators.Technical.Heatmap;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using OFT.Localization;
using OFT.Rendering.Settings;
using static ATAS.Indicators.Heatmap.HeatmapIndicatorColors;

/// <summary>Timeframe of the candles the heatmap VWAP/TWAP is calculated from.</summary>
public enum HeatmapVwapBarsTimeFrame
{
	M1,
	M2,
	M3,
	M5,
	M10,
	M15,
	M30,
	H1,
	H4
}

/// <summary>
/// Settings of the heatmap VWAP/TWAP indicator. The calculation settings have the names, defaults and
/// strings of the chart <see cref="VWAP"/> indicator; the line settings replace the chart's data series
/// settings (colour, width, line style). Not ported: the hidden custom start point (hotkeys).
/// </summary>
public sealed class HeatmapVwapSettings
{
	#region Const fields

	private const string BandColorHex = "#FF00BCD4";
	private const string UpperFillColorHex = "#40C30101";
	private const string MiddleFillColorHex = "#40808080";
	private const string LowerFillColorHex = "#4000FF00";

	#endregion

	#region Static fields

	private static readonly TimeSpan _defaultSessionEnd = new(23, 59, 59);

	#endregion

	#region Fields

	private VWAP.VWAPPeriodType _type = VWAP.VWAPPeriodType.Daily;
	private FilterTimeSpan _customSessionStartFilter = CreateSessionFilter(TimeSpan.Zero, enabled: false);
	private FilterTimeSpan _customSessionEndFilter = CreateSessionFilter(_defaultSessionEnd, enabled: false);

	#endregion

	#region Auto properties

	/// <summary>Schema version. Increment when changing field semantics.</summary>
	[Browsable(false)]
	public int Version { get; set; } = 1;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TimeFrame), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.HeatmapVwapBarsTimeFrameDescription), Order = 10)]
	public HeatmapVwapBarsTimeFrame BarsTimeFrame { get; set; } = HeatmapVwapBarsTimeFrame.M1;

	/// <summary>
	/// Sessions to calculate, counted back from the last candle. Unlike the chart (0..1000, 0 = all bars) the
	/// range is 0..30 and 0 means 30 days: the candle series of the heatmap load at most 30 days of history
	/// (<c>CandleSeriesRequest.DaysToLoad</c> is clamped to 1..30).
	/// </summary>
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.DaysLookBack), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.HeatmapVwapDaysLookBackDescription), Order = 20)]
	[Range(0, 30)]
	public int Days { get; set; } = 20;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Mode), GroupName = nameof(Strings.Settings), Description = nameof(Strings.CalculationModeDescription), Order = 20)]
	public VWAP.VWAPMode TWAPMode { get; set; } = VWAP.VWAPMode.VWAP;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.VolumeType), GroupName = nameof(Strings.Settings), Description = nameof(Strings.VolumeTypeDescription), Order = 25)]
	public VWAP.VolumeType VolumeMode { get; set; } = VWAP.VolumeType.Total;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FirstDev), GroupName = nameof(Strings.Settings), Description = nameof(Strings.StdDevPeriodDescription), Order = 40)]
	[Range(0.0000001, 10000)]
	public decimal StDev { get; set; } = 1;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SecondDev), GroupName = nameof(Strings.Settings), Description = nameof(Strings.StdDevPeriodDescription), Order = 50)]
	[Range(0.0000001, 10000)]
	public decimal StDev1 { get; set; } = 2;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ThirdDev), GroupName = nameof(Strings.Settings), Description = nameof(Strings.StdDevPeriodDescription), Order = 60)]
	[Range(0.0000001, 10000)]
	public decimal StDev2 { get; set; } = 3;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowFirstPartialPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.ShowFirstPeriodDescription), Order = 90)]
	public bool ShowFirstPeriod { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.VWAPOnly), GroupName = nameof(Strings.Settings), Description = nameof(Strings.ShowBaseElementOnlyDescription), Order = 100)]
	public bool VWAPOnly { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ColoredDirection), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.ColoredDirectionDescription), Order = 200)]
	public bool ColoredDirection { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BullishColorDescription), Order = 210)]
	public CrossColor BullishColor { get; set; } = ParseHex("#FF2196F3");

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearlishColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BearishColorDescription), Order = 220)]
	public CrossColor BearishColor { get; set; } = ParseHex("#FFB22222");

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.VWAP), Description = nameof(Strings.BaseLineSettingsDescription), Order = 300)]
	public CrossColor VwapColor { get; set; } = ParseHex("#FFFF5252");

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.VWAP), Order = 310)]
	[Range(1, 10)]
	public int VwapWidth { get; set; } = 3;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineDashStyle), GroupName = nameof(Strings.VWAP), Order = 320)]
	public LineDashStyle VwapLineStyle { get; set; } = LineDashStyle.Solid;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.UpperStd3), Order = 400)]
	public CrossColor UpperStd3Color { get; set; } = ParseHex(BandColorHex);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.UpperStd3), Order = 410)]
	[Range(1, 10)]
	public int UpperStd3Width { get; set; } = 1;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineDashStyle), GroupName = nameof(Strings.UpperStd3), Order = 420)]
	public LineDashStyle UpperStd3LineStyle { get; set; } = LineDashStyle.Solid;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.UpperStd2), Order = 430)]
	public CrossColor UpperStd2Color { get; set; } = ParseHex(BandColorHex);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.UpperStd2), Order = 440)]
	[Range(1, 10)]
	public int UpperStd2Width { get; set; } = 1;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineDashStyle), GroupName = nameof(Strings.UpperStd2), Order = 450)]
	public LineDashStyle UpperStd2LineStyle { get; set; } = LineDashStyle.Solid;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.UpperStd1), Order = 460)]
	public CrossColor UpperStd1Color { get; set; } = ParseHex(BandColorHex);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.UpperStd1), Order = 470)]
	[Range(1, 10)]
	public int UpperStd1Width { get; set; } = 1;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineDashStyle), GroupName = nameof(Strings.UpperStd1), Order = 480)]
	public LineDashStyle UpperStd1LineStyle { get; set; } = LineDashStyle.Solid;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.LowerStd1), Order = 500)]
	public CrossColor LowerStd1Color { get; set; } = ParseHex(BandColorHex);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.LowerStd1), Order = 510)]
	[Range(1, 10)]
	public int LowerStd1Width { get; set; } = 1;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineDashStyle), GroupName = nameof(Strings.LowerStd1), Order = 520)]
	public LineDashStyle LowerStd1LineStyle { get; set; } = LineDashStyle.Solid;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.LowerStd2), Order = 530)]
	public CrossColor LowerStd2Color { get; set; } = ParseHex(BandColorHex);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.LowerStd2), Order = 540)]
	[Range(1, 10)]
	public int LowerStd2Width { get; set; } = 1;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineDashStyle), GroupName = nameof(Strings.LowerStd2), Order = 550)]
	public LineDashStyle LowerStd2LineStyle { get; set; } = LineDashStyle.Solid;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.LowerStd3), Order = 560)]
	public CrossColor LowerStd3Color { get; set; } = ParseHex(BandColorHex);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.LowerStd3), Order = 570)]
	[Range(1, 10)]
	public int LowerStd3Width { get; set; } = 1;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineDashStyle), GroupName = nameof(Strings.LowerStd3), Order = 580)]
	public LineDashStyle LowerStd3LineStyle { get; set; } = LineDashStyle.Solid;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.UpperFill2), GroupName = nameof(Strings.HeatmapDrawingFill), Order = 600)]
	public CrossColor UpperFill2Color { get; set; } = ParseHex(UpperFillColorHex);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.UpperFill), GroupName = nameof(Strings.HeatmapDrawingFill), Order = 610)]
	public CrossColor UpperFillColor { get; set; } = ParseHex(UpperFillColorHex);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MiddleFillUp), GroupName = nameof(Strings.HeatmapDrawingFill), Order = 620)]
	public CrossColor MiddleFillUpColor { get; set; } = ParseHex(MiddleFillColorHex);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MiddleFillDown), GroupName = nameof(Strings.HeatmapDrawingFill), Order = 630)]
	public CrossColor MiddleFillDownColor { get; set; } = ParseHex(MiddleFillColorHex);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LowerFill), GroupName = nameof(Strings.HeatmapDrawingFill), Order = 640)]
	public CrossColor LowerFillColor { get; set; } = ParseHex(LowerFillColorHex);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LowerFill2), GroupName = nameof(Strings.HeatmapDrawingFill), Order = 650)]
	public CrossColor LowerFill2Color { get; set; } = ParseHex(LowerFillColorHex);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HeatmapVwapShowPreviousValues), GroupName = nameof(Strings.HeatmapVwapPreviousPeriodValues), Description = nameof(Strings.HeatmapVwapShowPreviousValuesDescription), Order = 700)]
	public bool ShowPreviousValues { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HeatmapVwapPreviousUpperValue), GroupName = nameof(Strings.HeatmapVwapPreviousPeriodValues), Order = 710)]
	public CrossColor PreviousUpperColor { get; set; } = ParseHex("#FF4CAF50");

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HeatmapVwapPreviousLowerValue), GroupName = nameof(Strings.HeatmapVwapPreviousPeriodValues), Order = 720)]
	public CrossColor PreviousLowerColor { get; set; } = ParseHex("#FFCD5C5C");

	/// <summary>
	/// The chart draws the previous-period values as crosses of width 5; the heatmap draws dotted lines,
	/// which read best at width 2.
	/// </summary>
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.HeatmapVwapPreviousPeriodValues), Order = 730)]
	[Range(1, 10)]
	public int PreviousWidth { get; set; } = 2;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodTypeDescription), Order = 10)]
	public VWAP.VWAPPeriodType Type
	{
		get => _type;
		set
		{
			_type = value;
			SyncSessionFilters();
		}
	}

	/// <summary>Custom session begin, in the instrument time zone; editable only for the Custom period (as in the chart).</summary>
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SessionBegin), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SessionBeginDescription), Order = 70)]
	public FilterTimeSpan CustomSessionStartFilter
	{
		get => _customSessionStartFilter;
		set
		{
			_customSessionStartFilter = value ?? CreateSessionFilter(TimeSpan.Zero, enabled: false);
			SyncSessionFilters();
		}
	}

	/// <summary>Custom session end, in the instrument time zone; editable only for the Custom period (as in the chart).</summary>
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SessionEnd), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SessionEndDescription), Order = 80)]
	public FilterTimeSpan CustomSessionEndFilter
	{
		get => _customSessionEndFilter;
		set
		{
			_customSessionEndFilter = value ?? CreateSessionFilter(_defaultSessionEnd, enabled: false);
			SyncSessionFilters();
		}
	}

	#endregion

	#region Public methods

	/// <summary>The candle timeframe of <see cref="BarsTimeFrame"/>.</summary>
	public CandleSeriesTimeFrame GetCandleTimeFrame() => BarsTimeFrame switch
	{
		HeatmapVwapBarsTimeFrame.M1 => CandleSeriesTimeFrame.Minutes(1),
		HeatmapVwapBarsTimeFrame.M2 => CandleSeriesTimeFrame.Minutes(2),
		HeatmapVwapBarsTimeFrame.M3 => CandleSeriesTimeFrame.Minutes(3),
		HeatmapVwapBarsTimeFrame.M5 => CandleSeriesTimeFrame.Minutes(5),
		HeatmapVwapBarsTimeFrame.M10 => CandleSeriesTimeFrame.Minutes(10),
		HeatmapVwapBarsTimeFrame.M15 => CandleSeriesTimeFrame.Minutes(15),
		HeatmapVwapBarsTimeFrame.M30 => CandleSeriesTimeFrame.Minutes(30),
		HeatmapVwapBarsTimeFrame.H1 => CandleSeriesTimeFrame.Hours(1),
		HeatmapVwapBarsTimeFrame.H4 => CandleSeriesTimeFrame.Hours(4),
		_ => CandleSeriesTimeFrame.Minutes(1)
	};

	/// <summary>
	/// Whether any deviation band line or fill is drawn: <see cref="VWAPOnly"/> is off and at least one of their
	/// colours is not fully transparent (the heatmap settings have no per-band visibility switch).
	/// </summary>
	internal bool AreBandsShown() =>
		!VWAPOnly
		&& (UpperStd3Color.A > 0 || UpperStd2Color.A > 0 || UpperStd1Color.A > 0
			|| LowerStd1Color.A > 0 || LowerStd2Color.A > 0 || LowerStd3Color.A > 0
			|| UpperFill2Color.A > 0 || UpperFillColor.A > 0 || MiddleFillUpColor.A > 0
			|| MiddleFillDownColor.A > 0 || LowerFillColor.A > 0 || LowerFill2Color.A > 0);

	/// <summary>
	/// Immutable calculation snapshot for <see cref="HeatmapVwapBarEngine"/>. Days 0 becomes "all bars", which
	/// the indicator loads as 30 days of candles. The bands are calculated only while they are shown.
	/// </summary>
	internal HeatmapVwapEngineSettings ToEngineSettings() => new()
	{
		Period = Type,
		Mode = TWAPMode,
		VolumeType = VolumeMode,
		StDev = StDev,
		StDev1 = StDev1,
		StDev2 = StDev2,
		CustomSessionStart = CustomSessionStartFilter.Value,
		CustomSessionEnd = CustomSessionEndFilter.Value,
		Days = Math.Clamp(Days, 0, 30),
		ShowFirstPeriod = ShowFirstPeriod,
		ComputeBands = AreBandsShown()
	};

	#endregion

	#region Private methods

	// As VWAP.Type: the session times are editable only for the Custom period.
	private void SyncSessionFilters()
	{
		var enabled = _type == VWAP.VWAPPeriodType.Custom;
		_customSessionStartFilter.Enabled = enabled;
		_customSessionEndFilter.Enabled = enabled;
	}

	#endregion

	#region Private static methods

	private static FilterTimeSpan CreateSessionFilter(TimeSpan value, bool enabled) =>
		new(false) { Value = value, Enabled = enabled };

	#endregion
}
