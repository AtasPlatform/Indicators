#nullable enable

namespace ATAS.Indicators.Technical.Heatmap;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ATAS.Indicators.Heatmap;
using OFT.Rendering.Heatmap;

// Reference indicator: market-profile derived three-series visual (Value Area).
// Patterns: multiple HeatmapIndicatorSeriesHandle<T> on a single visual with
//           per-series projection delegates, persistent State.BeginUpdate
//           lease pattern, conditional series emission gated by settings.
// Copy as a starting point for any indicator that publishes several
// related series (POC + VAH + VAL, bid/ask/mid, etc.) on one visual.
//
// POC/VAH/VAL come from the platform's market-profile source (the same one the
// chart's volume profile reads), NOT from a profile accumulated over the ticks the
// heatmap happens to hold. Accumulating locally made the values depend on how much
// history the heatmap had recorded and on the UTC calendar day rather than the
// instrument's trading session, so they never matched the chart.
[HeatmapIndicator(id: "heatmap.value-area", DisplayName = "Value Area")]
public sealed class HeatmapValueAreaIndicator
	: HeatmapIndicator<HeatmapValueAreaSettings>
	, IHeatmapWarmupIndicator
	, IHeatmapProfileConsumer
{
	#region Static fields

	private static readonly HeatmapIndicatorDescriptor _descriptor;
	private static readonly HeatmapIndicatorVisualHandle _lines;
	private static readonly HeatmapIndicatorSeriesHandle<HeatmapValueAreaSample> _poc;
	private static readonly HeatmapIndicatorSeriesHandle<HeatmapValueAreaSample> _high;
	private static readonly HeatmapIndicatorSeriesHandle<HeatmapValueAreaSample> _low;

	#endregion

	#region Static constructors

	static HeatmapValueAreaIndicator()
	{
		var build = Describe<HeatmapValueAreaIndicator>();
		_lines = build.ValueArea("value-area.lines", "Value Area");
		_poc = _lines.Series<HeatmapValueAreaSample>(
			"value-area.poc", HeatmapIndicatorSeriesRole.Poc, HeatmapIndicatorValueKind.Price,
			sample => sample.Poc);
		_high = _lines.Series<HeatmapValueAreaSample>(
			"value-area.high", HeatmapIndicatorSeriesRole.ValueAreaHigh, HeatmapIndicatorValueKind.Price,
			sample => sample.ValueAreaHigh);
		_low = _lines.Series<HeatmapValueAreaSample>(
			"value-area.low", HeatmapIndicatorSeriesRole.ValueAreaLow, HeatmapIndicatorValueKind.Price,
			sample => sample.ValueAreaLow);
		_descriptor = build.Done();
	}

	#endregion

	#region Fields

	private bool _hasConfigured;
	private HeatmapProfileScope _configuredScope = HeatmapProfileScope.CurrentDay;
	private bool _configuredShowValueArea;

	// Read by the host's profile pump on its own thread — see the
	// IHeatmapProfileConsumer.GetRequiredProfilePeriods threading contract.
	private volatile HeatmapProfilePeriod[] _requiredPeriods = [ToProfilePeriod(HeatmapProfileScope.CurrentDay)];

	private long _lastPublishedTimestampNanos;
	private HeatmapValueAreaSample _lastPublishedSample;
	private bool _hasPublished;

	#endregion

	#region Properties

	public override HeatmapIndicatorDescriptor Descriptor => _descriptor;

	#endregion

	#region Public methods

	public override async ValueTask ConfigureAsync(HeatmapValueAreaSettings settings, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(settings);

		var calculationChanged = _hasConfigured
		                         && (_configuredScope != settings.Scope
		                             || _configuredShowValueArea != settings.ShowValueArea);

		_hasConfigured = true;
		_configuredScope = settings.Scope;
		_configuredShowValueArea = settings.ShowValueArea;

		// Publish the needed period before refreshing visuals so the host's profile
		// pump starts pumping the right one on its next tick.
		_requiredPeriods = [ToProfilePeriod(settings.Scope)];

		ApplyStyles();

		if (calculationChanged && Runtime is { } runtime)
		{
			await runtime
				.RequestStateResetAsync("value-area: calculation parameters changed", cancellationToken)
				.ConfigureAwait(false);
		}
	}

	#endregion

	#region Protected methods

	protected override ValueTask OnStateResetCoreAsync(
		IHeatmapIndicatorContext context,
		IHeatmapIndicatorRuntime runtime,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		_lastPublishedTimestampNanos = 0;
		_lastPublishedSample = default;
		_hasPublished = false;

		return ValueTask.CompletedTask;
	}

	public async ValueTask WarmUpAsync(
		HeatmapIndicatorWarmupRequest request,
		IHeatmapIndicatorDataSources dataSources,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var period = ToProfilePeriod(Settings.Scope);
		var profiles = await dataSources.Profiles
			.GetProfilesAsync(
				new HeatmapIndicatorProfileRangeRequest(
					period,
					request.BeginTimeNanos,
					request.EndTimeNanos,
					request.EndTimeNanos),
				cancellationToken)
			.ConfigureAwait(false);

		cancellationToken.ThrowIfCancellationRequested();

		_lastPublishedTimestampNanos = 0;
		_lastPublishedSample = default;
		_hasPublished = false;

		using var lease = State.BeginUpdate();
		var visualLease = lease.Visual(_lines);
		ApplyStyles(visualLease);
		ClearSeries(visualLease, _poc, _high, _low);

		foreach (var profile in profiles)
		{
			if (profile.Period == period)
				Publish(visualLease, profile);
		}
	}

	public IReadOnlyCollection<HeatmapProfilePeriod> GetRequiredProfilePeriods() => _requiredPeriods;

	public ValueTask ProcessProfileAsync(
		HeatmapMarketProfileSnapshot profile,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (profile.Period != ToProfilePeriod(Settings.Scope))
			return ValueTask.CompletedTask;

		using var lease = State.BeginUpdate();
		var visualLease = lease.Visual(_lines);
		ApplyStyles(visualLease);
		Publish(visualLease, profile);

		return ValueTask.CompletedTask;
	}

	#endregion

	#region Private methods

	/// <summary>
	/// Appends one profile snapshot to the POC/VAH/VAL series. The pump re-delivers the profile
	/// on every tick, so unchanged snapshots are dropped: the renderer already extends the last
	/// sample to the right edge, and appending duplicates would grow the series without end while
	/// the market is quiet. A changed profile whose timestamp did not move forward (the source
	/// timestamps a snapshot with its last trade) is nudged by a nanosecond to keep samples ordered.
	/// </summary>
	private void Publish(IHeatmapVisualLease visualLease, HeatmapMarketProfileSnapshot profile)
	{
		if (profile.Poc <= 0)
			return;

		var sample = new HeatmapValueAreaSample
		{
			Poc = profile.Poc,
			ValueAreaHigh = profile.ValueAreaHigh,
			ValueAreaLow = profile.ValueAreaLow,
		};

		if (_hasPublished && sample == _lastPublishedSample)
			return;

		var timestampNanos = profile.TimestampNanos <= _lastPublishedTimestampNanos
			? _lastPublishedTimestampNanos + 1
			: profile.TimestampNanos;

		visualLease.Series(_poc).Append(timestampNanos, sample);

		if (Settings.ShowValueArea)
		{
			visualLease.Series(_high).Append(timestampNanos, sample);
			visualLease.Series(_low).Append(timestampNanos, sample);
		}

		_lastPublishedTimestampNanos = timestampNanos;
		_lastPublishedSample = sample;
		_hasPublished = true;
	}

	private void ApplyStyles(IHeatmapVisualLease visualLease)
	{
		var pocStyle = new HeatmapIndicatorVisualStyle(
			Color: HeatmapIndicatorColors.ToHex(Settings.PocColor),
			Thickness: Settings.PocThickness);
		var valueAreaStyle = new HeatmapIndicatorVisualStyle(
			Color: HeatmapIndicatorColors.ToHex(Settings.ValueAreaColor),
			Thickness: Settings.PocThickness);

		visualLease.Style = pocStyle;
		visualLease.Series(_poc).Style = pocStyle;
		visualLease.Series(_high).Style = valueAreaStyle;
		visualLease.Series(_low).Style = valueAreaStyle;
	}

	private void ApplyStyles()
	{
		using var lease = State.BeginUpdate();
		ApplyStyles(lease.Visual(_lines));
	}

	#endregion

	#region Private static methods

	/// <summary>
	/// Maps the indicator's scope onto a market-profile period. The profile source has no
	/// "data start" period — the widest profile it can build is the whole contract, which is
	/// also the closest match for "everything we have".
	/// </summary>
	private static HeatmapProfilePeriod ToProfilePeriod(HeatmapProfileScope scope) => scope switch
	{
		HeatmapProfileScope.CurrentDay => HeatmapProfilePeriod.CurrentDay,
		HeatmapProfileScope.LastDay => HeatmapProfilePeriod.LastDay,
		HeatmapProfileScope.CurrentWeek => HeatmapProfilePeriod.CurrentWeek,
		HeatmapProfileScope.LastWeek => HeatmapProfilePeriod.LastWeek,
		HeatmapProfileScope.CurrentMonth => HeatmapProfilePeriod.CurrentMonth,
		HeatmapProfileScope.LastMonth => HeatmapProfilePeriod.LastMonth,
		_ => HeatmapProfilePeriod.Contract
	};

	#endregion
}

internal readonly record struct HeatmapPeriodKey(int Year, int Period, HeatmapProfileScope Scope)
{
	public static HeatmapPeriodKey Empty { get; } = new(0, 0, HeatmapProfileScope.DataStart);
}

internal static class HeatmapPeriodResolver
{
	public static HeatmapPeriodKey Resolve(
		DateTime timestamp,
		IHeatmapIndicatorContext? context,
		HeatmapProfileScope scope)
	{
		var timeZone = context?.TimeZone ?? TimeZoneInfo.Utc;
		var local = timestamp.Kind == DateTimeKind.Unspecified
			? timestamp
			: TimeZoneInfo.ConvertTime(timestamp, timeZone);

		return scope switch
		{
			HeatmapProfileScope.CurrentDay or HeatmapProfileScope.LastDay =>
				new HeatmapPeriodKey(local.Year, local.DayOfYear, scope),
			HeatmapProfileScope.CurrentWeek or HeatmapProfileScope.LastWeek =>
				new HeatmapPeriodKey(IsoWeekYear(local), IsoWeek(local), scope),
			HeatmapProfileScope.CurrentMonth or HeatmapProfileScope.LastMonth =>
				new HeatmapPeriodKey(local.Year, local.Month, scope),
			_ => new HeatmapPeriodKey(0, 0, scope)
		};
	}

	private static int IsoWeek(DateTime value)
	{
		var day = (int)value.DayOfWeek;
		if (day == 0)
			day = 7;

		var thursday = value.AddDays(4 - day);
		return (thursday.DayOfYear - 1) / 7 + 1;
	}

	private static int IsoWeekYear(DateTime value)
	{
		var day = (int)value.DayOfWeek;
		if (day == 0)
			day = 7;

		return value.AddDays(4 - day).Year;
	}
}
