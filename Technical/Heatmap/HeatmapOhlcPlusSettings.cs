#nullable enable

namespace ATAS.Indicators.Technical.Heatmap;

using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using ATAS.Indicators;
using OFT.Localization;
using OFT.Rendering.Heatmap;
using OFT.Rendering.Settings;

public sealed class HeatmapOhlcPlusSettings
{
	#region Auto properties

	/// <summary>Schema version. Increment when changing field semantics.</summary>
	[Browsable(false)]
	public int Version { get; set; } = 1;

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.BarOpen), Order = 10)]
	public LevelSettings DayOpenLevel { get; set; } = CreateOpen(enabled: true);

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.BarHigh), Order = 20)]
	public LevelSettings DayHighLevel { get; set; } = CreateHigh();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.BarLow), Order = 30)]
	public LevelSettings DayLowLevel { get; set; } = CreateLow();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.BarClose), Order = 40)]
	public LevelSettings DayCloseLevel { get; set; } = CreateClose();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.Equilibrium), Order = 50)]
	public LevelSettings DayEquilibriumLevel { get; set; } = CreateEquilibrium();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.POC), Order = 60)]
	public LevelSettings DayPOCLevel { get; set; } = CreatePoc();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.VWAP), Order = 65)]
	public LevelSettings DayVWAPLevel { get; set; } = CreateVwap();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.VAH), Order = 70)]
	public LevelSettings DayVAHLevel { get; set; } = CreateValueArea();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.VAL), Order = 80)]
	public LevelSettings DayVALLevel { get; set; } = CreateValueArea();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.BarOpen), Order = 10)]
	public LevelSettings PrevDayOpenLevel { get; set; } = CreateOpen();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.BarHigh), Order = 20)]
	public LevelSettings PrevDayHighLevel { get; set; } = CreateHigh();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.BarLow), Order = 30)]
	public LevelSettings PrevDayLowLevel { get; set; } = CreateLow();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.BarClose), Order = 40)]
	public LevelSettings PrevDayCloseLevel { get; set; } = CreateClose();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.Equilibrium), Order = 50)]
	public LevelSettings PrevDayEquilibriumLevel { get; set; } = CreateEquilibrium();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.POC), Order = 60)]
	public LevelSettings PrevDayPOCLevel { get; set; } = CreatePoc(enabled: true);

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.VWAP), Order = 65)]
	public LevelSettings PrevDayVWAPLevel { get; set; } = CreateVwap();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.VAH), Order = 70)]
	public LevelSettings PrevDayVAHLevel { get; set; } = CreateValueArea();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.VAL), Order = 80)]
	public LevelSettings PrevDayVALLevel { get; set; } = CreateValueArea();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.BarOpen), Order = 10)]
	public LevelSettings WeekOpenLevel { get; set; } = CreateOpen();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.BarHigh), Order = 20)]
	public LevelSettings WeekHighLevel { get; set; } = CreateHigh();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.BarLow), Order = 30)]
	public LevelSettings WeekLowLevel { get; set; } = CreateLow();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.BarClose), Order = 40)]
	public LevelSettings WeekCloseLevel { get; set; } = CreateClose();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.Equilibrium), Order = 50)]
	public LevelSettings WeekEquilibriumLevel { get; set; } = CreateEquilibrium();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.POC), Order = 60)]
	public LevelSettings WeekPOCLevel { get; set; } = CreatePoc();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.VWAP), Order = 65)]
	public LevelSettings WeekVWAPLevel { get; set; } = CreateVwap();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.VAH), Order = 70)]
	public LevelSettings WeekVAHLevel { get; set; } = CreateValueArea();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.VAL), Order = 80)]
	public LevelSettings WeekVALLevel { get; set; } = CreateValueArea();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.BarOpen), Order = 10)]
	public LevelSettings PrevWeekOpenLevel { get; set; } = CreateOpen();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.BarHigh), Order = 20)]
	public LevelSettings PrevWeekHighLevel { get; set; } = CreateHigh();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.BarLow), Order = 30)]
	public LevelSettings PrevWeekLowLevel { get; set; } = CreateLow();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.BarClose), Order = 40)]
	public LevelSettings PrevWeekCloseLevel { get; set; } = CreateClose();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.Equilibrium), Order = 50)]
	public LevelSettings PrevWeekEquilibriumLevel { get; set; } = CreateEquilibrium();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.POC), Order = 60)]
	public LevelSettings PrevWeekPOCLevel { get; set; } = CreatePoc();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.VWAP), Order = 65)]
	public LevelSettings PrevWeekVWAPLevel { get; set; } = CreateVwap();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.VAH), Order = 70)]
	public LevelSettings PrevWeekVAHLevel { get; set; } = CreateValueArea();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.VAL), Order = 80)]
	public LevelSettings PrevWeekVALLevel { get; set; } = CreateValueArea();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.BarOpen), Order = 10)]
	public LevelSettings MonthOpenLevel { get; set; } = CreateOpen();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.BarHigh), Order = 20)]
	public LevelSettings MonthHighLevel { get; set; } = CreateHigh();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.BarLow), Order = 30)]
	public LevelSettings MonthLowLevel { get; set; } = CreateLow();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.BarClose), Order = 40)]
	public LevelSettings MonthCloseLevel { get; set; } = CreateClose();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.Equilibrium), Order = 50)]
	public LevelSettings MonthEquilibriumLevel { get; set; } = CreateEquilibrium();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.POC), Order = 60)]
	public LevelSettings MonthPOCLevel { get; set; } = CreatePoc();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.VWAP), Order = 65)]
	public LevelSettings MonthVWAPLevel { get; set; } = CreateVwap();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.VAH), Order = 70)]
	public LevelSettings MonthVAHLevel { get; set; } = CreateValueArea();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.VAL), Order = 80)]
	public LevelSettings MonthVALLevel { get; set; } = CreateValueArea();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.BarOpen), Order = 10)]
	public LevelSettings PrevMonthOpenLevel { get; set; } = CreateOpen();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.BarHigh), Order = 20)]
	public LevelSettings PrevMonthHighLevel { get; set; } = CreateHigh();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.BarLow), Order = 30)]
	public LevelSettings PrevMonthLowLevel { get; set; } = CreateLow();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.BarClose), Order = 40)]
	public LevelSettings PrevMonthCloseLevel { get; set; } = CreateClose();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.Equilibrium), Order = 50)]
	public LevelSettings PrevMonthEquilibriumLevel { get; set; } = CreateEquilibrium();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.POC), Order = 60)]
	public LevelSettings PrevMonthPOCLevel { get; set; } = CreatePoc();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.VWAP), Order = 65)]
	public LevelSettings PrevMonthVWAPLevel { get; set; } = CreateVwap();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.VAH), Order = 70)]
	public LevelSettings PrevMonthVAHLevel { get; set; } = CreateValueArea();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.VAL), Order = 80)]
	public LevelSettings PrevMonthVALLevel { get; set; } = CreateValueArea();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.BarOpen), Order = 10)]
	public LevelSettings ContractOpenLevel { get; set; } = CreateOpen();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.BarHigh), Order = 20)]
	public LevelSettings ContractHighLevel { get; set; } = CreateHigh();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.BarLow), Order = 30)]
	public LevelSettings ContractLowLevel { get; set; } = CreateLow();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.BarClose), Order = 40)]
	public LevelSettings ContractCloseLevel { get; set; } = CreateClose();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.Equilibrium), Order = 50)]
	public LevelSettings ContractEquilibriumLevel { get; set; } = CreateEquilibrium();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.POC), Order = 60)]
	public LevelSettings ContractPOCLevel { get; set; } = CreatePoc();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.VWAP), Order = 65)]
	public LevelSettings ContractVWAPLevel { get; set; } = CreateVwap();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.VAH), Order = 70)]
	public LevelSettings ContractVAHLevel { get; set; } = CreateValueArea();

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.VAL), Order = 80)]
	public LevelSettings ContractVALLevel { get; set; } = CreateValueArea();

	#endregion

	#region Public methods

	public IEnumerable<HeatmapOhlcPlusLevelSettings> GetLevels()
	{
		foreach (var level in GetPeriodLevels(HeatmapProfilePeriod.CurrentDay, "d", DayOpenLevel, DayHighLevel, DayLowLevel, DayCloseLevel, DayEquilibriumLevel, DayPOCLevel, DayVWAPLevel, DayVAHLevel, DayVALLevel))
			yield return level;

		foreach (var level in GetPeriodLevels(HeatmapProfilePeriod.LastDay, "p", PrevDayOpenLevel, PrevDayHighLevel, PrevDayLowLevel, PrevDayCloseLevel, PrevDayEquilibriumLevel, PrevDayPOCLevel, PrevDayVWAPLevel, PrevDayVAHLevel, PrevDayVALLevel))
			yield return level;

		foreach (var level in GetPeriodLevels(HeatmapProfilePeriod.CurrentWeek, "w", WeekOpenLevel, WeekHighLevel, WeekLowLevel, WeekCloseLevel, WeekEquilibriumLevel, WeekPOCLevel, WeekVWAPLevel, WeekVAHLevel, WeekVALLevel))
			yield return level;

		foreach (var level in GetPeriodLevels(HeatmapProfilePeriod.LastWeek, "pw", PrevWeekOpenLevel, PrevWeekHighLevel, PrevWeekLowLevel, PrevWeekCloseLevel, PrevWeekEquilibriumLevel, PrevWeekPOCLevel, PrevWeekVWAPLevel, PrevWeekVAHLevel, PrevWeekVALLevel))
			yield return level;

		foreach (var level in GetPeriodLevels(HeatmapProfilePeriod.CurrentMonth, "m", MonthOpenLevel, MonthHighLevel, MonthLowLevel, MonthCloseLevel, MonthEquilibriumLevel, MonthPOCLevel, MonthVWAPLevel, MonthVAHLevel, MonthVALLevel))
			yield return level;

		foreach (var level in GetPeriodLevels(HeatmapProfilePeriod.LastMonth, "pm", PrevMonthOpenLevel, PrevMonthHighLevel, PrevMonthLowLevel, PrevMonthCloseLevel, PrevMonthEquilibriumLevel, PrevMonthPOCLevel, PrevMonthVWAPLevel, PrevMonthVAHLevel, PrevMonthVALLevel))
			yield return level;

		foreach (var level in GetPeriodLevels(HeatmapProfilePeriod.Contract, "c", ContractOpenLevel, ContractHighLevel, ContractLowLevel, ContractCloseLevel, ContractEquilibriumLevel, ContractPOCLevel, ContractVWAPLevel, ContractVAHLevel, ContractVALLevel))
			yield return level;
	}

	#endregion

	#region Private methods

	private static IEnumerable<HeatmapOhlcPlusLevelSettings> GetPeriodLevels(
		HeatmapProfilePeriod period,
		string prefix,
		LevelSettings open,
		LevelSettings high,
		LevelSettings low,
		LevelSettings close,
		LevelSettings equilibrium,
		LevelSettings poc,
		LevelSettings vwap,
		LevelSettings vah,
		LevelSettings val)
	{
		yield return CreateEntry(period, prefix, HeatmapOhlcPlusLevelKind.Open, open);
		yield return CreateEntry(period, prefix, HeatmapOhlcPlusLevelKind.High, high);
		yield return CreateEntry(period, prefix, HeatmapOhlcPlusLevelKind.Low, low);
		yield return CreateEntry(period, prefix, HeatmapOhlcPlusLevelKind.Close, close);
		yield return CreateEntry(period, prefix, HeatmapOhlcPlusLevelKind.Equilibrium, equilibrium);
		yield return CreateEntry(period, prefix, HeatmapOhlcPlusLevelKind.Poc, poc);
		yield return CreateEntry(period, prefix, HeatmapOhlcPlusLevelKind.Vwap, vwap);
		yield return CreateEntry(period, prefix, HeatmapOhlcPlusLevelKind.ValueAreaHigh, vah);
		yield return CreateEntry(period, prefix, HeatmapOhlcPlusLevelKind.ValueAreaLow, val);
	}

	private static HeatmapOhlcPlusLevelSettings CreateEntry(
		HeatmapProfilePeriod period,
		string prefix,
		HeatmapOhlcPlusLevelKind kind,
		LevelSettings settings)
	{
		var label = $"{prefix}{GetLabelSuffix(kind)}";
		return new HeatmapOhlcPlusLevelSettings($"{prefix}{kind}", period, kind, label, settings);
	}

	private static LevelSettings CreateOpen(bool enabled = false) => CreateLevel(enabled, Color.Orange, LineDashStyle.Solid, width: 1);

	private static LevelSettings CreateHigh(bool enabled = false) => CreateLevel(enabled, Color.Green, LineDashStyle.Solid, width: 1);

	private static LevelSettings CreateLow(bool enabled = false) => CreateLevel(enabled, Color.Red, LineDashStyle.Solid, width: 1);

	private static LevelSettings CreateClose(bool enabled = false) => CreateLevel(enabled, Color.Gray, LineDashStyle.Solid, width: 1);

	private static LevelSettings CreateEquilibrium(bool enabled = false) => CreateLevel(enabled, Color.Yellow, LineDashStyle.Dash, width: 1);

	private static LevelSettings CreatePoc(bool enabled = false) => CreateLevel(enabled, Color.Orange, LineDashStyle.Solid, width: 2);

	private static LevelSettings CreateVwap(bool enabled = false) => CreateLevel(enabled, Color.SteelBlue, LineDashStyle.Solid, width: 2);

	private static LevelSettings CreateValueArea(bool enabled = false) => CreateLevel(enabled, Color.Purple, LineDashStyle.Dot, width: 1);

	private static LevelSettings CreateLevel(bool enabled, Color color, LineDashStyle lineStyle, int width) =>
		new(
			enabled: enabled,
			color: color.Convert(),
			width: width,
			lineStyle: lineStyle,
			showPrice: true,
			labelPosition: LabelPosition.Bar,
			lineType: LineType.Bar)
		{
			IsTextOnLevelColor = true
		};

	private static string GetLabelSuffix(HeatmapOhlcPlusLevelKind kind) => kind switch
	{
		HeatmapOhlcPlusLevelKind.Open => "Open",
		HeatmapOhlcPlusLevelKind.High => "High",
		HeatmapOhlcPlusLevelKind.Low => "Low",
		HeatmapOhlcPlusLevelKind.Close => "Close",
		HeatmapOhlcPlusLevelKind.Equilibrium => "EQ",
		HeatmapOhlcPlusLevelKind.Poc => "POC",
		HeatmapOhlcPlusLevelKind.Vwap => "VWAP",
		HeatmapOhlcPlusLevelKind.ValueAreaHigh => "VAH",
		HeatmapOhlcPlusLevelKind.ValueAreaLow => "VAL",
		_ => kind.ToString()
	};

	#endregion
}
