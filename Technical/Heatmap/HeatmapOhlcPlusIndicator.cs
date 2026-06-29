#nullable enable

namespace ATAS.Indicators.Technical.Heatmap;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ATAS.Indicators.Heatmap;
using ATAS.Indicators.Technical;
using OFT.Rendering.Heatmap;
using static ATAS.Indicators.Heatmap.HeatmapIndicatorColors;

[HeatmapIndicator(id: "heatmap.ohlc-plus", DisplayName = "OHLC Plus")]
public sealed class HeatmapOhlcPlusIndicator
	: HeatmapIndicator<HeatmapOhlcPlusSettings>
	, IHeatmapWarmupIndicator
	, IHeatmapProfileConsumer
{
	#region Nested types

	private readonly record struct LevelKey(
		HeatmapProfilePeriod Period,
		HeatmapOhlcPlusLevelKind Kind);

	#endregion

	#region Const fields

	private const string LevelsVisualId = "ohlc-plus.lines";

	#endregion

	#region Static fields

	private static readonly HeatmapIndicatorDescriptor _descriptor;
	private static readonly HeatmapIndicatorVisualHandle _lines;
	private static readonly Dictionary<LevelKey, HeatmapIndicatorSeriesHandle<decimal>> _seriesByLevel;

	#endregion

	#region Static constructors

	static HeatmapOhlcPlusIndicator()
	{
		var build = Describe<HeatmapOhlcPlusIndicator>();
		_lines = build.LevelLine(LevelsVisualId, "OHLC Plus");
		_seriesByLevel = new Dictionary<LevelKey, HeatmapIndicatorSeriesHandle<decimal>>();

		foreach (var period in Enum.GetValues<HeatmapProfilePeriod>())
		{
			foreach (var kind in Enum.GetValues<HeatmapOhlcPlusLevelKind>())
			{
				var key = new LevelKey(period, kind);
				_seriesByLevel[key] = _lines.Series(
					$"ohlc-plus.{period}.{kind}",
					HeatmapIndicatorSeriesRole.Level,
					HeatmapIndicatorValueKind.Price);
			}
		}

		_descriptor = build.Done();
	}

	#endregion

	#region Readonly initialized fields

	private readonly Dictionary<HeatmapProfilePeriod, HeatmapMarketProfileSnapshot> _profiles = new();
	private readonly Dictionary<LevelKey, LevelValue> _lastResolvedLevels = new();
	private long _lastPublishTimestampNanos;

	#endregion

	#region Fields

	// Snapshot of the periods the currently enabled levels need. Refreshed on the
	// consumer task from ConfigureAsync and published for the host's profile pump,
	// which reads it via IHeatmapProfileConsumer.GetRequiredProfilePeriods from a
	// different thread — hence volatile, and only ever replaced wholesale (never
	// mutated in place).
	private volatile HeatmapProfilePeriod[] _requiredPeriods = Array.Empty<HeatmapProfilePeriod>();

	#endregion

	#region Properties

	public override HeatmapIndicatorDescriptor Descriptor => _descriptor;

	#endregion

	#region Public methods

	public override ValueTask ConfigureAsync(HeatmapOhlcPlusSettings settings, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Publish the needed periods before refreshing visuals so the host's
		// profile pump starts/stops pumping the right set on its next tick.
		_requiredPeriods = ComputeRequiredPeriods();
		
		RefreshLevels();
		return ValueTask.CompletedTask;
	}

	public async ValueTask WarmUpAsync(
		HeatmapIndicatorWarmupRequest request,
		IHeatmapIndicatorDataSources dataSources,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		_profiles.Clear();
		
		foreach (var period in _requiredPeriods)
		{
			var profiles = await dataSources.Profiles.GetProfilesAsync(
					new HeatmapIndicatorProfileRangeRequest(
						period,
						request.BeginTimeNanos,
						request.EndTimeNanos,
						request.EndTimeNanos),
					cancellationToken)
				.ConfigureAwait(false);

			foreach (var profile in profiles)
				_profiles[profile.Period] = profile;
		}

		RefreshLevels();
	}

	public ValueTask ProcessProfileAsync(
		HeatmapMarketProfileSnapshot profile,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		_profiles[profile.Period] = profile;
		RefreshLevels();

		return ValueTask.CompletedTask;
	}

	// Reads the volatile snapshot published by ConfigureAsync. Called by the host's
	// profile pump on a different thread, so it must not touch mutable indicator
	// state — see the IHeatmapProfileConsumer.GetRequiredProfilePeriods threading contract.
	public IReadOnlyCollection<HeatmapProfilePeriod> GetRequiredProfilePeriods() => _requiredPeriods;

	#endregion

	#region Protected methods

	protected override ValueTask OnStateResetCoreAsync(
		IHeatmapIndicatorContext context,
		IHeatmapIndicatorRuntime runtime,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		_profiles.Clear();
		_lastResolvedLevels.Clear();
		_lastPublishTimestampNanos = 0;
		return ValueTask.CompletedTask;
	}

	#endregion

	#region Private methods

	private void RefreshLevels()
	{
		var activeLevels = Settings.GetLevels()
			.Where(static level => level.Settings is { Enabled: true, LineType: not LineType.None })
			.ToArray();

		var publishTimestampNanos = GetNextPublishTimestampNanos();

		using var lease = State.BeginUpdate();
		var visualLease = lease.Visual(_lines);
		ClearSeries(visualLease, [.. _seriesByLevel.Values]);

		if (activeLevels.Length == 0 || publishTimestampNanos <= 0)
			return;

		foreach (var level in activeLevels)
		{
			var key = new LevelKey(level.Period, level.Kind);
			if (!_seriesByLevel.TryGetValue(key, out var seriesHandle))
				continue;

			if (!TryResolveLevelValue(key, level.Period, level.Kind, publishTimestampNanos, out var levelValue))
				continue;

			var seriesLease = visualLease.Series(seriesHandle);
			seriesLease.Style = ToVisualStyle(level.Settings, level.Label);
			seriesLease.Append(levelValue.TimestampNanos, levelValue.Price);
		}

		_lastPublishTimestampNanos = publishTimestampNanos;
	}

	private long GetNextPublishTimestampNanos()
	{
		var latestTimestampNanos = 0L;
		foreach (var profile in _profiles.Values)
		{
			if (profile.TimestampNanos > latestTimestampNanos)
				latestTimestampNanos = profile.TimestampNanos;
		}

		foreach (var value in _lastResolvedLevels.Values)
		{
			if (value.TimestampNanos > latestTimestampNanos)
				latestTimestampNanos = value.TimestampNanos;
		}

		if (latestTimestampNanos <= 0)
			return 0;

		return latestTimestampNanos <= _lastPublishTimestampNanos
			? _lastPublishTimestampNanos + 1
			: latestTimestampNanos;
	}

	private bool TryResolveLevelValue(
		LevelKey key,
		HeatmapProfilePeriod period,
		HeatmapOhlcPlusLevelKind kind,
		long fallbackTimestampNanos,
		out LevelValue value)
	{
		if (TryResolvePrice(period, kind, out var price) && price > 0)
		{
			value = new LevelValue(fallbackTimestampNanos, price);
			_lastResolvedLevels[key] = value;
			return true;
		}

		if (_lastResolvedLevels.TryGetValue(key, out var cached) && cached.Price > 0 && fallbackTimestampNanos > 0)
		{
			value = new LevelValue(fallbackTimestampNanos, cached.Price);
			_lastResolvedLevels[key] = value;
			return true;
		}

		value = default;
		return false;
	}

	private bool TryResolvePrice(
		HeatmapProfilePeriod period,
		HeatmapOhlcPlusLevelKind kind,
		out decimal price)
	{
		if (!_profiles.TryGetValue(period, out var profile))
		{
			price = 0;
			return false;
		}

		price = kind switch
		{
			HeatmapOhlcPlusLevelKind.Open => profile.Open,
			HeatmapOhlcPlusLevelKind.High => profile.High,
			HeatmapOhlcPlusLevelKind.Low => profile.Low,
			HeatmapOhlcPlusLevelKind.Close => profile.Close,
			HeatmapOhlcPlusLevelKind.Equilibrium => (profile.High + profile.Low) / 2,
			HeatmapOhlcPlusLevelKind.Poc => profile.Poc,
			HeatmapOhlcPlusLevelKind.Vwap => profile.Vwap,
			HeatmapOhlcPlusLevelKind.ValueAreaHigh => profile.ValueAreaHigh,
			HeatmapOhlcPlusLevelKind.ValueAreaLow => profile.ValueAreaLow,
			_ => 0
		};

		return price > 0;
	}

	private HeatmapProfilePeriod[] ComputeRequiredPeriods() =>
		Settings.GetLevels()
			.Where(static level => level.Settings is { Enabled: true, LineType: not LineType.None })
			.Select(static level => level.Period)
			.Distinct()
			.ToArray();

	private static HeatmapIndicatorVisualStyle ToVisualStyle(LevelSettings settings, string label)
	{
		var resolvedLabel = settings.LabelPosition == LabelPosition.None
			? null
			: label;

		return new HeatmapIndicatorVisualStyle(
			Color: ToHex(settings.Color),
			Thickness: settings.Width,
			Label: resolvedLabel,
			LineType: settings.LineType.ToString(),
			LineDashStyle: settings.LineStyle.ToString(),
			ShowPrice: settings.ShowPrice);
	}

	#endregion

	private readonly record struct LevelValue(
		long TimestampNanos,
		decimal Price);
}
