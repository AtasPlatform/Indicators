#nullable enable

namespace ATAS.Indicators.Technical.Heatmap;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ATAS.Indicators.Heatmap;
using OFT.Rendering.Heatmap;

// Reference indicator: training-period gated sub-panel scalar (Price Change).
// Patterns: IsTraining gate that withholds normalisation until enough data
//           has accumulated, adaptive max calibration after training,
//           AutoVisible scalar scale mode, mode-aware (StdDev / RoC) value
//           tracking from a per-tick price-history buffer, persistent
//           State.BeginUpdate lease pattern.
// Copy as a starting point for any indicator that needs to warm up its own
// statistics before publishing meaningful output.
//
// Live-path cost is O(1) amortised per tick: the StdDev window keeps running
// price/weight sums maintained by a lazily-advanced [start..end) index pair,
// and the RoC period anchor advances through the history with a monotonic
// index. Full scans survive only in the historical training-max sampler,
// where they are bounded by binary search and run O(100) times per warm-up.
// (A per-tick full-history scan got the Market Pressure runner killed by the
// controller's per-call timeout on high-rate crypto feeds — PLAT-4651; this
// indicator shared the same pattern.)
[HeatmapIndicator(id: "heatmap.price-change", DisplayName = "Price Change")]
public sealed class HeatmapPriceChangeIndicator
	: HeatmapIndicator<HeatmapPriceChangeSettings>
	, IHeatmapWarmupIndicator
	, IHeatmapTradeTickConsumer
{
	#region Nested types

	private readonly record struct PricePoint(DateTime Time, long TimestampNanos, decimal Price, int TickCount);

	#endregion

	#region Const fields

	private const decimal MinimumMaxValue = 0.01m;

	#endregion

	#region Static fields

	private static readonly HeatmapIndicatorDescriptor _descriptor;
	private static readonly HeatmapIndicatorVisualHandle _panel;
	private static readonly HeatmapIndicatorSeriesHandle<HeatmapPriceChangeSample> _value;

	#endregion

	#region Static constructors

	static HeatmapPriceChangeIndicator()
	{
		var build = Describe<HeatmapPriceChangeIndicator>();
		_panel = build.SubPanelScalar("price-change.panel", "Price Change");
		_value = _panel.Series<HeatmapPriceChangeSample>(
			"price-change.value", HeatmapIndicatorSeriesRole.Scalar, HeatmapIndicatorValueKind.Scalar,
			sample => (decimal)sample.Value);
		_descriptor = build.Done();
	}

	#endregion

	#region Readonly initialized fields

	private readonly List<PricePoint> _priceHistory = new();

	#endregion

	#region Fields

	private DateTime _trainingStartTime = DateTime.MinValue;
	private DateTime _lastTickTime = DateTime.MinValue;
	private DateTime _virtualCurrentTime = DateTime.MinValue;
	private DateTime _lastCleanupTime = DateTime.MinValue;
	private decimal _maxStandardDeviation = MinimumMaxValue;
	private decimal _maxRateOfChange = MinimumMaxValue;
	private decimal _currentValue;
	private decimal _currentPrice;
	// volatile: read from external threads via IsTraining; controller serialises writes.
	private volatile bool _isTrainingComplete;
	private decimal _currentPeriodStartPrice;
	private DateTime _currentPeriodStartTime = DateTime.MinValue;
	private HeatmapPriceChangePeriod _currentPeriod = HeatmapPriceChangePeriod.OneMinute;
	private int _currentPeriodSearchIndex;
	private bool _hasConfigured;
	private HeatmapPriceChangeMode _configuredMode = HeatmapPriceChangeMode.StandardDeviation;
	private HeatmapPriceChangePeriod _configuredPeriod = HeatmapPriceChangePeriod.OneMinute;
	private HeatmapTrainingPeriod _configuredTrainingPeriod = HeatmapTrainingPeriod.FifteenMinutes;

	// Sliding StdDev window over _priceHistory: points [_windowStartIndex.._windowEndIndex)
	// are included in the running sums. Both indices only move forward on the
	// live path; a period change or history mutation invalidates the window
	// and the next calculation rebuilds it.
	private int _windowStartIndex;
	private int _windowEndIndex;
	private decimal _windowPriceSum;
	private long _windowWeight;
	private HeatmapPriceChangePeriod? _windowPeriod;

	#endregion

	#region Auto properties

	public float CurrentValue { get; private set; }

	#endregion

	#region Properties

	public override HeatmapIndicatorDescriptor Descriptor => _descriptor;

	public bool IsTraining => !_isTrainingComplete;

	#endregion

	#region Public methods

	public override async ValueTask ConfigureAsync(HeatmapPriceChangeSettings settings, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(settings);

		// TrainingPeriod is normalised lazily at every read site via
		// ValidateTrainingPeriod — no need to reconstruct settings here.
		var calculationChanged = _hasConfigured
		                         && (_configuredMode != settings.Mode
		                             || _configuredPeriod != settings.Period
		                             || _configuredTrainingPeriod != settings.TrainingPeriod);

		_hasConfigured = true;
		_configuredMode = settings.Mode;
		_configuredPeriod = settings.Period;
		_configuredTrainingPeriod = settings.TrainingPeriod;

		if (_currentPeriod != settings.Period)
		{
			_currentPeriod = settings.Period;
			_currentPeriodStartPrice = 0;
			_currentPeriodStartTime = DateTime.MinValue;
			_currentPeriodSearchIndex = 0;

			if (_virtualCurrentTime != DateTime.MinValue && _currentPrice > 0)
				UpdateCurrentPeriodWindow(_virtualCurrentTime, _currentPrice, settings.Period);
		}

		if (_lastTickTime != DateTime.MinValue)
		{
			_currentValue = CalculateValue(_virtualCurrentTime, settings.Mode, settings.Period);
			CurrentValue = (float)NormalizeValue(_currentValue, settings.Mode);
		}

		ApplyPresentation();

		if (calculationChanged && Runtime is { } runtime)
		{
			await runtime
				.RequestStateResetAsync("price-change: calculation parameters changed", cancellationToken)
				.ConfigureAwait(false);
		}
	}

	public async ValueTask WarmUpAsync(
		HeatmapIndicatorWarmupRequest request,
		IHeatmapIndicatorDataSources dataSources,
		CancellationToken cancellationToken)
	{
		var ticks = await dataSources.Trades.GetTradeTicksAsync(request, cancellationToken).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();
		ProcessHistoricalTicks(ticks);
	}

	public ValueTask ProcessTicksAsync(
		HeatmapTickBatch ticks,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var span = ticks.AsSpan();
		if (span.Length == 0)
			return ValueTask.CompletedTask;

		// Live ticks arrive batched through the controller. Route each through
		// the per-tick path that maintains incremental training / cleanup state
		// rather than the bulk historical recompute used by warm-up.
		using var lease = State.BeginUpdate();
		var visualLease = lease.Visual(_panel);
		ApplyPresentation(visualLease);
		var seriesLease = visualLease.Series(_value);

		for (var i = 0; i < span.Length; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ProcessTick(span[i], seriesLease);
		}

		return ValueTask.CompletedTask;
	}

	public void ProcessHistoricalTicks(IEnumerable<HeatmapTradeTick> ticks)
	{
		var trainingPeriod = ValidateTrainingPeriod(Settings.Period, Settings.TrainingPeriod);
		var requiredSeconds = (int)trainingPeriod + (int)Settings.Period;
		var bufferSeconds = requiredSeconds * 0.1;
		var maxRetention = TimeSpan.FromSeconds(requiredSeconds + bufferSeconds);
		var tempHistory = new List<PricePoint>();
		var lastTickTime = DateTime.MinValue;

		foreach (var tick in ticks)
		{
			if (tick.Price <= 0)
				continue;

			lastTickTime = tick.Time;

			if (tempHistory.Count == 0 || tempHistory[^1].Price != tick.Price)
			{
				tempHistory.Add(new PricePoint(tick.Time, tick.TimestampNanos, tick.Price, 1));
			}
			else
			{
				var lastIndex = tempHistory.Count - 1;
				var point = tempHistory[lastIndex];
				tempHistory[lastIndex] = point with { Time = tick.Time, TimestampNanos = tick.TimestampNanos, TickCount = point.TickCount + 1 };
			}
		}

		if (tempHistory.Count == 0)
			return;

		var cutoffTime = lastTickTime - maxRetention;
		var startIndex = 0;
		for (var i = 0; i < tempHistory.Count; i++)
		{
			if (tempHistory[i].Time >= cutoffTime)
			{
				startIndex = i;
				break;
			}
		}

		using var lease = State.BeginUpdate();
		var visualLease = lease.Visual(_panel);
		ApplyPresentation(visualLease);
		var seriesLease = visualLease.Series(_value);
		seriesLease.Clear();

		_priceHistory.Clear();
		for (var i = startIndex; i < tempHistory.Count; i++)
			_priceHistory.Add(tempHistory[i]);

		InvalidateWindow();
		_currentPeriodSearchIndex = 0;

		_lastTickTime = lastTickTime;
		_virtualCurrentTime = lastTickTime;
		_trainingStartTime = lastTickTime;

		CalculateTrainingMaximums(lastTickTime, Settings.Mode, Settings.Period, trainingPeriod);

		// Replay the history point-by-point so the sliding window and the RoC
		// period anchor advance monotonically — the whole rebuild stays O(n)
		// and leaves both positioned at the newest point, ready for live ticks.
		for (var i = 0; i < _priceHistory.Count; i++)
		{
			var point = _priceHistory[i];
			UpdateCurrentPeriodWindow(point.Time, point.Price, Settings.Period);
			_currentValue = CalculateValueCore(point.Time, Settings.Mode, Settings.Period, point.Price);
			_currentPrice = point.Price;
			RecordCurrentValue(point.TimestampNanos, Settings.Mode, seriesLease);
		}
	}

	public void ProcessTick(HeatmapTradeTick tick, IHeatmapSeriesLease<HeatmapPriceChangeSample>? seriesLease = null)
	{
		if (tick.Price <= 0)
			return;

		var settings = Settings;
		var trainingPeriod = ValidateTrainingPeriod(settings.Period, settings.TrainingPeriod);

		IHeatmapVisualStateLease? ownedLease = null;
		if (seriesLease == null)
		{
			ownedLease = State.BeginUpdate();
			var visualLease = ownedLease.Visual(_panel);
			ApplyPresentation(visualLease);
			seriesLease = visualLease.Series(_value);
		}

		try
		{
			if (_trainingStartTime == DateTime.MinValue)
				_trainingStartTime = tick.Time;

			_lastTickTime = tick.Time;
			_virtualCurrentTime = tick.Time;
			_currentPrice = tick.Price;

			var lastIndex = _priceHistory.Count - 1;
			if (lastIndex < 0 || _priceHistory[lastIndex].Price != tick.Price)
			{
				_priceHistory.Add(new PricePoint(tick.Time, tick.TimestampNanos, tick.Price, 1));
			}
			else
			{
				var point = _priceHistory[lastIndex];
				_priceHistory[lastIndex] = point with { Time = tick.Time, TimestampNanos = tick.TimestampNanos, TickCount = point.TickCount + 1 };

				// The merged point may already be inside the window sums; keep
				// them in sync with its grown weight.
				if (_windowPeriod != null && _windowEndIndex == _priceHistory.Count)
				{
					_windowPriceSum += tick.Price;
					_windowWeight += 1;
				}
			}

			UpdateCurrentPeriodWindow(tick.Time, tick.Price, settings.Period);

			if (tick.Time - _lastCleanupTime > TimeSpan.FromMinutes(1))
			{
				CleanupOldData(tick.Time, trainingPeriod);
				_lastCleanupTime = tick.Time;
			}

			if (!_isTrainingComplete && tick.Time - _trainingStartTime >= TimeSpan.FromSeconds((int)trainingPeriod))
			{
				CalculateTrainingMaximums(tick.Time, settings.Mode, settings.Period, trainingPeriod);
				_isTrainingComplete = true;
			}
			else if (!_isTrainingComplete)
			{
				UpdateTrainingMaximums(tick.Time, settings.Mode, settings.Period);
			}

			_currentValue = CalculateValue(tick.Time, settings.Mode, settings.Period);

			if (_isTrainingComplete)
				AdaptMaximums(_currentValue, settings.Mode);

			RecordCurrentValue(tick.TimestampNanos, settings.Mode, seriesLease);
		}
		finally
		{
			ownedLease?.Dispose();
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

		_priceHistory.Clear();
		ResetState();

		return ValueTask.CompletedTask;
	}

	#endregion

	#region Private methods

	private void RecordCurrentValue(long timestampNanos, HeatmapPriceChangeMode mode, IHeatmapSeriesLease<HeatmapPriceChangeSample> seriesLease, decimal? rawValue = null)
	{
		var value = NormalizeValue(rawValue ?? _currentValue, mode);
		CurrentValue = (float)value;

		if (timestampNanos > 0)
			seriesLease.Append(timestampNanos, new HeatmapPriceChangeSample { Value = CurrentValue });
	}

	private void ApplyPresentation(IHeatmapVisualLease visualLease)
	{
		visualLease.Presentation = new HeatmapIndicatorVisualPresentation(
			PanelHeight: Settings.PanelHeight,
			ScalarScaleMode: HeatmapIndicatorScalarScaleMode.AutoVisible);
	}

	private void ApplyPresentation()
	{
		using var lease = State.BeginUpdate();
		ApplyPresentation(lease.Visual(_panel));
	}

	private void CleanupOldData(DateTime currentTime, HeatmapTrainingPeriod trainingPeriod)
	{
		var requiredSeconds = (int)trainingPeriod + 300;
		var bufferSeconds = requiredSeconds * 0.1;
		var maxRetention = TimeSpan.FromSeconds(requiredSeconds + bufferSeconds);
		var cutoffTime = currentTime - maxRetention;

		// History is time-ordered, so expired points form a prefix — count and
		// remove them in one shot, then shift the forward-only indices.
		var removeCount = 0;
		while (removeCount < _priceHistory.Count && _priceHistory[removeCount].Time < cutoffTime)
			removeCount++;

		if (removeCount == 0)
			return;

		_priceHistory.RemoveRange(0, removeCount);
		_currentPeriodSearchIndex = Math.Max(0, _currentPeriodSearchIndex - removeCount);

		if (_windowStartIndex >= removeCount)
		{
			// Retention always exceeds the StdDev period, so expired points sit
			// strictly before the window; the sums stay valid.
			_windowStartIndex -= removeCount;
			_windowEndIndex -= removeCount;
		}
		else
		{
			InvalidateWindow();
		}
	}

	/// <summary>
	/// Brings the sliding window up to <paramref name="referenceTime"/> for
	/// <paramref name="period"/> and returns the weighted SMA of the covered
	/// points, or 0 when the window is empty. Amortised O(1) per call on the
	/// live path: each history point enters and leaves the sums exactly once.
	/// </summary>
	private decimal GetWindowSma(DateTime referenceTime, HeatmapPriceChangePeriod period)
	{
		if (_windowPeriod != period)
		{
			InvalidateWindow();
			_windowPeriod = period;
			_windowStartIndex = FindFirstIndexAtOrAfter(_priceHistory, referenceTime.AddSeconds(-(int)period));
			_windowEndIndex = _windowStartIndex;
		}

		var periodStart = referenceTime.AddSeconds(-(int)period);

		while (_windowEndIndex < _priceHistory.Count && _priceHistory[_windowEndIndex].Time <= referenceTime)
		{
			var point = _priceHistory[_windowEndIndex];
			_windowPriceSum += point.Price * point.TickCount;
			_windowWeight += point.TickCount;
			_windowEndIndex++;
		}

		while (_windowStartIndex < _windowEndIndex && _priceHistory[_windowStartIndex].Time < periodStart)
		{
			var point = _priceHistory[_windowStartIndex];
			_windowPriceSum -= point.Price * point.TickCount;
			_windowWeight -= point.TickCount;
			_windowStartIndex++;
		}

		return _windowWeight > 0 ? _windowPriceSum / _windowWeight : 0;
	}

	private void InvalidateWindow()
	{
		_windowStartIndex = 0;
		_windowEndIndex = 0;
		_windowPriceSum = 0;
		_windowWeight = 0;
		_windowPeriod = null;
	}

	private void CalculateTrainingMaximums(DateTime referenceTime, HeatmapPriceChangeMode mode, HeatmapPriceChangePeriod period, HeatmapTrainingPeriod trainingPeriod)
	{
		var trainingStart = referenceTime.AddSeconds(-(int)trainingPeriod);
		var startIndex = FindFirstIndexAtOrAfter(_priceHistory, trainingStart);
		var endIndex = _priceHistory.Count - 1;

		while (endIndex >= startIndex && _priceHistory[endIndex].Time > referenceTime)
			endIndex--;

		var trainingDataCount = endIndex - startIndex + 1;
		if (startIndex > endIndex || trainingDataCount < 10)
			return;

		var maxValue = MinimumMaxValue;
		var step = Math.Max(1, trainingDataCount / 100);

		for (var i = startIndex; i <= endIndex; i += step)
		{
			var value = Math.Abs(CalculateValueAtHistorical(_priceHistory[i].Time, mode, period));
			maxValue = Math.Max(maxValue, value);
		}

		if ((endIndex - startIndex) % step != 0)
		{
			var value = Math.Abs(CalculateValueAtHistorical(_priceHistory[endIndex].Time, mode, period));
			maxValue = Math.Max(maxValue, value);
		}

		switch (mode)
		{
			case HeatmapPriceChangeMode.StandardDeviation:
				_maxStandardDeviation = Math.Max(_maxStandardDeviation, maxValue);
				break;
			case HeatmapPriceChangeMode.RateOfChange:
				_maxRateOfChange = Math.Max(_maxRateOfChange, maxValue);
				break;
		}
	}

	private void UpdateTrainingMaximums(DateTime currentTime, HeatmapPriceChangeMode mode, HeatmapPriceChangePeriod period)
	{
		var value = Math.Abs(CalculateValue(currentTime, mode, period));

		switch (mode)
		{
			case HeatmapPriceChangeMode.StandardDeviation:
				_maxStandardDeviation = Math.Max(_maxStandardDeviation, value);
				break;
			case HeatmapPriceChangeMode.RateOfChange:
				_maxRateOfChange = Math.Max(_maxRateOfChange, value);
				break;
		}
	}

	private void AdaptMaximums(decimal currentValue, HeatmapPriceChangeMode mode)
	{
		const decimal adaptationRate = 0.001m;
		var absValue = Math.Abs(currentValue);

		switch (mode)
		{
			case HeatmapPriceChangeMode.StandardDeviation:
				if (absValue > _maxStandardDeviation)
					_maxStandardDeviation = absValue;
				else if (absValue < _maxStandardDeviation * 0.5m)
					_maxStandardDeviation = _maxStandardDeviation * (1 - adaptationRate) + absValue * adaptationRate * 2;
				break;
			case HeatmapPriceChangeMode.RateOfChange:
				if (absValue > _maxRateOfChange)
					_maxRateOfChange = absValue;
				else if (absValue < _maxRateOfChange * 0.5m)
					_maxRateOfChange = _maxRateOfChange * (1 - adaptationRate) + absValue * adaptationRate * 2;
				break;
		}
	}

	/// <summary>
	/// Live-path value at the newest reference time. Reference times must be
	/// non-decreasing between calls (they follow the tick stream); historical
	/// sampling goes through <see cref="CalculateValueAtHistorical"/> instead.
	/// </summary>
	private decimal CalculateValue(DateTime referenceTime, HeatmapPriceChangeMode mode, HeatmapPriceChangePeriod period) =>
		CalculateValueCore(referenceTime, mode, period, GetPriceAtOrBeforeTime(referenceTime, _priceHistory));

	private decimal CalculateValueCore(DateTime referenceTime, HeatmapPriceChangeMode mode, HeatmapPriceChangePeriod period, decimal currentPrice)
	{
		if (currentPrice == 0)
			return 0;

		return mode switch
		{
			HeatmapPriceChangeMode.StandardDeviation => GetWindowSma(referenceTime, period) is var sma && sma != 0
				? currentPrice - sma
				: 0,
			HeatmapPriceChangeMode.RateOfChange => CalculateRateOfChange(_priceHistory, currentPrice, referenceTime, (int)period),
			_ => 0
		};
	}

	/// <summary>
	/// Random-access value at an arbitrary historical time. Bounded by binary
	/// search to the period window; used only by the training-max sampler
	/// (~100 calls per warm-up / training completion).
	/// </summary>
	private decimal CalculateValueAtHistorical(DateTime referenceTime, HeatmapPriceChangeMode mode, HeatmapPriceChangePeriod period)
	{
		var periodSeconds = (int)period;
		var periodStart = referenceTime.AddSeconds(-periodSeconds);
		var currentPrice = GetPriceAtOrBeforeTime(referenceTime, _priceHistory);

		if (currentPrice == 0)
			return 0;

		switch (mode)
		{
			case HeatmapPriceChangeMode.StandardDeviation:
				return CalculateStandardDeviationInRange(_priceHistory, currentPrice, periodStart, referenceTime);
			case HeatmapPriceChangeMode.RateOfChange:
				var referenceOldPrice = GetPriceAtOrBeforeTime(periodStart, _priceHistory);
				return referenceOldPrice == 0
					? 0
					: ((currentPrice - referenceOldPrice) / referenceOldPrice) * 100;
			default:
				return 0;
		}
	}

	private decimal CalculateRateOfChange(List<PricePoint> data, decimal currentPrice, DateTime referenceTime, int periodSeconds)
	{
		var period = (HeatmapPriceChangePeriod)periodSeconds;

		if (_currentPeriod == period && _currentPeriodStartPrice > 0)
			return ((currentPrice - _currentPeriodStartPrice) / _currentPeriodStartPrice) * 100;

		var periodAgo = referenceTime.AddSeconds(-periodSeconds);
		var referenceOldPrice = GetPriceAtOrBeforeTime(periodAgo, data);

		return referenceOldPrice == 0
			? 0
			: ((currentPrice - referenceOldPrice) / referenceOldPrice) * 100;
	}

	private decimal NormalizeValue(decimal value, HeatmapPriceChangeMode mode)
	{
		var maxValue = mode switch
		{
			HeatmapPriceChangeMode.StandardDeviation => _maxStandardDeviation,
			HeatmapPriceChangeMode.RateOfChange => _maxRateOfChange,
			_ => MinimumMaxValue
		};

		if (maxValue == 0)
			return 0;

		var normalized = value / maxValue * 100;
		return Math.Max(-100, Math.Min(100, normalized));
	}

	private void UpdateCurrentPeriodWindow(DateTime currentTime, decimal currentPrice, HeatmapPriceChangePeriod period)
	{
		if (_currentPeriod != period)
		{
			_currentPeriod = period;
			_currentPeriodStartPrice = 0;
			_currentPeriodStartTime = DateTime.MinValue;
			_currentPeriodSearchIndex = 0;
		}

		var periodSeconds = (int)period;
		var windowStart = currentTime.AddSeconds(-periodSeconds);

		if (_currentPeriodStartTime != DateTime.MinValue && _currentPeriodStartTime >= windowStart)
			return;

		// windowStart only moves forward on the live path, so the anchor is
		// found by advancing a persistent index instead of scanning from zero.
		while (_currentPeriodSearchIndex < _priceHistory.Count && _priceHistory[_currentPeriodSearchIndex].Time < windowStart)
			_currentPeriodSearchIndex++;

		if (_currentPeriodSearchIndex < _priceHistory.Count)
		{
			var startPoint = _priceHistory[_currentPeriodSearchIndex];
			_currentPeriodStartPrice = startPoint.Price;
			_currentPeriodStartTime = startPoint.Time;
		}
	}

	private void ResetState()
	{
		_trainingStartTime = DateTime.MinValue;
		_lastTickTime = DateTime.MinValue;
		_virtualCurrentTime = DateTime.MinValue;
		_lastCleanupTime = DateTime.MinValue;
		_maxStandardDeviation = MinimumMaxValue;
		_maxRateOfChange = MinimumMaxValue;
		_currentValue = 0;
		_currentPrice = 0;
		_isTrainingComplete = false;
		_currentPeriodStartPrice = 0;
		_currentPeriodStartTime = DateTime.MinValue;
		_currentPeriodSearchIndex = 0;
		InvalidateWindow();
		CurrentValue = 0;
	}

	#endregion

	#region Private static methods

	private static HeatmapTrainingPeriod ValidateTrainingPeriod(HeatmapPriceChangePeriod period, HeatmapTrainingPeriod trainingPeriod)
	{
		if ((int)period <= (int)trainingPeriod)
			return trainingPeriod;

		if ((int)period <= 300)
			return HeatmapTrainingPeriod.FiveMinutes;

		if ((int)period <= 900)
			return HeatmapTrainingPeriod.FifteenMinutes;

		return HeatmapTrainingPeriod.OneHour;
	}

	private static decimal CalculateStandardDeviationInRange(List<PricePoint> data, decimal currentPrice, DateTime periodStart, DateTime periodEnd)
	{
		decimal weightedSum = 0;
		var totalWeight = 0;

		for (var i = FindFirstIndexAtOrAfter(data, periodStart); i < data.Count; i++)
		{
			var point = data[i];
			if (point.Time > periodEnd)
				break;

			weightedSum += point.Price * point.TickCount;
			totalWeight += point.TickCount;
		}

		if (totalWeight == 0)
			return 0;

		var sma = weightedSum / totalWeight;
		return currentPrice - sma;
	}

	private static decimal GetPriceAtOrBeforeTime(DateTime targetTime, List<PricePoint> data)
	{
		for (var i = data.Count - 1; i >= 0; i--)
		{
			if (data[i].Time <= targetTime)
				return data[i].Price;
		}

		return 0;
	}

	/// <summary>Binary search over the time-ordered history: index of the first point with Time &gt;= <paramref name="targetTime"/> (or Count when none).</summary>
	private static int FindFirstIndexAtOrAfter(List<PricePoint> data, DateTime targetTime)
	{
		var lo = 0;
		var hi = data.Count;
		while (lo < hi)
		{
			var mid = lo + (hi - lo) / 2;
			if (data[mid].Time < targetTime)
				lo = mid + 1;
			else
				hi = mid;
		}

		return lo;
	}

	#endregion
}
