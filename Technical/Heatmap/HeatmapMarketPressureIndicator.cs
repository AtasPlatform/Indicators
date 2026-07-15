#nullable enable

namespace ATAS.Indicators.Technical.Heatmap;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ATAS.Indicators.Heatmap;
using OFT.Rendering.Heatmap;

// Reference indicator: paired buy/sell sub-panel scalar (Market Pressure).
// Patterns: SubPanelPair visual with two HeatmapIndicatorSeriesHandle<T>
//           (Buy + Sell) and per-series projection delegates, O(1)-per-tick
//           incremental exponential-decay accumulators, persistent
//           State.BeginUpdate lease, presentation override carrying a
//           Threshold for the renderer.
// Copy as a starting point for any indicator that publishes a paired
// buyer-vs-seller scalar.
//
// The pressure model is a decayed sum over trade events:
//   P(t) = Σ w_i × e^(−(t − t_i) / τ)
// which is maintained incrementally — P(t2) = P(t1) × e^(−(t2 − t1)/τ) + w —
// so live processing costs O(1) per tick regardless of tick rate. The
// previous implementation recomputed the sum over a bounded event buffer on
// every tick, which could not keep up with high-rate crypto feeds and got
// the runner killed by the controller's per-call timeout (PLAT-4651).
[HeatmapIndicator(id: "heatmap.market-pressure", DisplayName = "Market Pressure")]
public sealed class HeatmapMarketPressureIndicator
	: HeatmapIndicator<HeatmapPressureSettings>
	, IHeatmapWarmupIndicator
	, IHeatmapTradeTickConsumer
{
	#region Const fields

	private const decimal MinimumMaxPressure = 0.01m;

	#endregion

	#region Static fields

	private static readonly HeatmapIndicatorDescriptor _descriptor;
	private static readonly HeatmapIndicatorVisualHandle _panel;
	private static readonly HeatmapIndicatorSeriesHandle<HeatmapPressureSample> _buy;
	private static readonly HeatmapIndicatorSeriesHandle<HeatmapPressureSample> _sell;

	#endregion

	#region Static constructors

	static HeatmapMarketPressureIndicator()
	{
		var build = Describe<HeatmapMarketPressureIndicator>();
		_panel = build.SubPanelPair("market-pressure.panel", "Market Pressure");
		_buy = _panel.Series<HeatmapPressureSample>(
			"market-pressure.buy", HeatmapIndicatorSeriesRole.Buy, HeatmapIndicatorValueKind.Scalar,
			sample => (decimal)sample.BuyNormalized);
		_sell = _panel.Series<HeatmapPressureSample>(
			"market-pressure.sell", HeatmapIndicatorSeriesRole.Sell, HeatmapIndicatorValueKind.Scalar,
			sample => (decimal)sample.SellNormalized);
		_descriptor = build.Done();
	}

	#endregion

	#region Fields

	private decimal _buyPressure;
	private decimal _sellPressure;
	private DateTime _pressureTime = DateTime.MinValue;
	private DateTime _trainingStartTime = DateTime.MinValue;
	private DateTime _lastTickTime = DateTime.MinValue;
	private DateTime _virtualCurrentTime = DateTime.MinValue;
	private DateTime _lastMaxUpdateTime = DateTime.MinValue;
	private decimal _maxPressure = MinimumMaxPressure;
	// volatile: read from external threads via IsTraining; controller serialises writes.
	private volatile bool _isTraining = true;
	private bool _hasConfigured;
	private HeatmapPressureMode _configuredMode = HeatmapPressureMode.Volume;
	private HeatmapPressurePreset _configuredPreset = HeatmapPressurePreset.Medium;

	#endregion

	#region Auto properties

	public float CurrentBuyNormalized { get; private set; }

	public float CurrentSellNormalized { get; private set; }

	#endregion

	#region Properties

	public override HeatmapIndicatorDescriptor Descriptor => _descriptor;

	public bool IsTraining => _isTraining;

	#endregion

	#region Public methods

	public override async ValueTask ConfigureAsync(HeatmapPressureSettings settings, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(settings);

		var calculationChanged = _hasConfigured
		                         && (_configuredMode != settings.Mode
		                             || _configuredPreset != settings.Preset);

		_hasConfigured = true;
		_configuredMode = settings.Mode;
		_configuredPreset = settings.Preset;

		UpdateVisual(_panel, ApplyPresentation);

		if (calculationChanged && Runtime is { } runtime)
		{
			await runtime
				.RequestStateResetAsync("market-pressure: calculation parameters changed", cancellationToken)
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

		using var lease = State.BeginUpdate();
		var visualLease = lease.Visual(_panel);
		ApplyPresentation(visualLease);
		var buyLease = visualLease.Series(_buy);
		var sellLease = visualLease.Series(_sell);

		for (var i = 0; i < span.Length; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ProcessTick(span[i], buyLease, sellLease);
		}

		return ValueTask.CompletedTask;
	}

	public void ProcessHistoricalTicks(IEnumerable<HeatmapTradeTick> ticks)
	{
		var mode = Settings.Mode;
		var preset = Settings.Preset;
		var halfLife = GetHalfLifePeriod(preset);
		var trainingPeriod = GetTrainingPeriod(preset);
		var sortedTicks = ticks
			.Where(IsEligibleTick)
			.OrderBy(t => t.Time)
			.ToList();

		if (sortedTicks.Count == 0)
			return;

		var lastTickTime = sortedTicks[^1].Time;

		// Pass 1: replay the history to find the true pressure maximum inside
		// the training window. Seeding the normalisation max with the real
		// peak (instead of the old "end state × 1.3" heuristic) keeps the
		// normalised lines off the 100 ceiling right after warm-up.
		ResetPressure();
		var searchStartTime = lastTickTime - trainingPeriod;
		var maxSeen = MinimumMaxPressure;

		foreach (var tick in sortedTicks)
		{
			ApplyTickToPressure(tick, mode, halfLife);

			if (tick.Time >= searchStartTime)
				maxSeen = Math.Max(maxSeen, Math.Max(_buyPressure, _sellPressure));
		}

		_maxPressure = maxSeen;
		_lastMaxUpdateTime = lastTickTime;
		_lastTickTime = lastTickTime;
		_virtualCurrentTime = lastTickTime;
		_trainingStartTime = lastTickTime;
		_isTraining = true;

		// Pass 2: replay again, this time publishing a sample per tick so the
		// sub-panel shows history. UpdateMaximumValue cannot ratchet past the
		// pass-1 peak, so the published normalisation is stable.
		ResetPressure();

		using var lease = State.BeginUpdate();
		var visualLease = lease.Visual(_panel);
		ApplyPresentation(visualLease);
		var buyLease = visualLease.Series(_buy);
		buyLease.Clear();
		var sellLease = visualLease.Series(_sell);
		sellLease.Clear();

		foreach (var tick in sortedTicks)
		{
			ApplyTickToPressure(tick, mode, halfLife);
			CalculateAndRecord(tick.Time, tick.TimestampNanos, buyLease, sellLease);
		}
	}

	public void ProcessTick(
		HeatmapTradeTick tick,
		IHeatmapSeriesLease<HeatmapPressureSample>? buyLease = null,
		IHeatmapSeriesLease<HeatmapPressureSample>? sellLease = null)
	{
		if (!IsEligibleTick(tick))
			return;

		if (_trainingStartTime == DateTime.MinValue)
			_trainingStartTime = tick.Time;

		_lastTickTime = tick.Time;
		_virtualCurrentTime = tick.Time;

		ApplyTickToPressure(tick, Settings.Mode, GetHalfLifePeriod(Settings.Preset));

		IHeatmapVisualStateLease? ownedLease = null;
		if (buyLease == null || sellLease == null)
		{
			ownedLease = State.BeginUpdate();
			var visualLease = ownedLease.Visual(_panel);
			ApplyPresentation(visualLease);
			buyLease = visualLease.Series(_buy);
			sellLease = visualLease.Series(_sell);
		}

		try
		{
			CalculateAndRecord(tick.Time, tick.TimestampNanos, buyLease, sellLease);
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

		ResetPressure();
		_trainingStartTime = DateTime.MinValue;
		_lastTickTime = DateTime.MinValue;
		_virtualCurrentTime = DateTime.MinValue;
		_lastMaxUpdateTime = DateTime.MinValue;
		_maxPressure = MinimumMaxPressure;
		_isTraining = true;
		CurrentBuyNormalized = 0;
		CurrentSellNormalized = 0;

		return ValueTask.CompletedTask;
	}

	#endregion

	#region Private methods

	/// <summary>
	/// Decays both running pressures to <paramref name="tick"/>'s time and adds
	/// the tick's weight to its side. Out-of-order ticks are applied without
	/// decay (the clock never moves backwards), matching the previous model
	/// which ignored negative deltas.
	/// </summary>
	private void ApplyTickToPressure(HeatmapTradeTick tick, HeatmapPressureMode mode, TimeSpan halfLife)
	{
		AdvancePressureTo(tick.Time, halfLife);

		var weight = GetWeight(tick, mode);
		if (tick.Direction == HeatmapTradeDirection.Buy)
			_buyPressure += weight;
		else
			_sellPressure += weight;
	}

	private void AdvancePressureTo(DateTime time, TimeSpan halfLife)
	{
		if (_pressureTime == DateTime.MinValue)
		{
			_pressureTime = time;
			return;
		}

		var deltaSeconds = (time - _pressureTime).TotalSeconds;
		if (deltaSeconds <= 0)
			return;

		// e^(−dt/τ) < 1e−28 underflows the decimal cast to exactly 0 — the
		// correct fixed point after a long quiet gap.
		var decay = (decimal)Math.Exp(-deltaSeconds / halfLife.TotalSeconds);
		_buyPressure *= decay;
		_sellPressure *= decay;
		_pressureTime = time;
	}

	private void ResetPressure()
	{
		_buyPressure = 0;
		_sellPressure = 0;
		_pressureTime = DateTime.MinValue;
	}

	private void CalculateAndRecord(
		DateTime referenceTime,
		long timestampNanos,
		IHeatmapSeriesLease<HeatmapPressureSample> buyLease,
		IHeatmapSeriesLease<HeatmapPressureSample> sellLease)
	{
		UpdateMaximumValue(_buyPressure, _sellPressure, referenceTime, Settings.Preset);

		CurrentBuyNormalized = (float)Math.Min(100m, _buyPressure / _maxPressure * 100m);
		CurrentSellNormalized = (float)Math.Min(100m, _sellPressure / _maxPressure * 100m);

		if (timestampNanos > 0)
		{
			var sample = new HeatmapPressureSample
			{
				BuyNormalized = CurrentBuyNormalized,
				SellNormalized = CurrentSellNormalized,
			};
			buyLease.Append(timestampNanos, sample);
			sellLease.Append(timestampNanos, sample);
		}
	}

	private void ApplyPresentation(IHeatmapVisualLease visualLease)
	{
		visualLease.Presentation = new HeatmapIndicatorVisualPresentation(
			PanelHeight: Settings.PanelHeight,
			Threshold: Settings.Threshold);
		visualLease.Style = new HeatmapIndicatorVisualStyle(
			ColorScheme: new HeatmapIndicatorSplitColors(
				Positive: HeatmapIndicatorColors.ToHex(Settings.BuyColor),
				Negative: HeatmapIndicatorColors.ToHex(Settings.SellColor)));
	}

	private void UpdateMaximumValue(decimal buyersPressure, decimal sellersPressure, DateTime referenceTime, HeatmapPressurePreset preset)
	{
		var currentMaxPressure = Math.Max(buyersPressure, sellersPressure);

		if (_isTraining && _trainingStartTime != DateTime.MinValue)
		{
			var trainingPeriod = GetTrainingPeriod(preset);
			if (_virtualCurrentTime - _trainingStartTime >= trainingPeriod)
				_isTraining = false;
		}

		if (_isTraining)
		{
			if (currentMaxPressure > _maxPressure)
			{
				_maxPressure = currentMaxPressure;
				_lastMaxUpdateTime = referenceTime;
			}

			return;
		}

		if (currentMaxPressure > _maxPressure)
		{
			_maxPressure = currentMaxPressure;
			_lastMaxUpdateTime = referenceTime;
			return;
		}

		var timeSinceLastMax = referenceTime - _lastMaxUpdateTime;
		var maxDecayPeriod = GetMaxDecayPeriod(preset);
		if (timeSinceLastMax <= maxDecayPeriod)
			return;

		var newMax = _maxPressure * 0.95m;
		newMax = Math.Max(newMax, Math.Max(currentMaxPressure, MinimumMaxPressure));

		if (newMax < _maxPressure)
		{
			_maxPressure = newMax;
			_lastMaxUpdateTime = referenceTime;
		}
	}

	#endregion

	#region Private static methods

	private static bool IsEligibleTick(HeatmapTradeTick tick) =>
		tick.Volume > 0 &&
		(tick.Direction == HeatmapTradeDirection.Buy || tick.Direction == HeatmapTradeDirection.Sell);

	private static decimal GetWeight(HeatmapTradeTick tick, HeatmapPressureMode mode) =>
		mode == HeatmapPressureMode.Pace ? 1m : tick.Volume;

	private static TimeSpan GetMaxDecayPeriod(HeatmapPressurePreset preset) =>
		preset switch
		{
			HeatmapPressurePreset.Short => TimeSpan.FromSeconds(20),
			HeatmapPressurePreset.Medium => TimeSpan.FromMinutes(1),
			HeatmapPressurePreset.Long => TimeSpan.FromMinutes(2),
			_ => TimeSpan.FromMinutes(1)
		};

	private static TimeSpan GetHalfLifePeriod(HeatmapPressurePreset preset) =>
		preset switch
		{
			HeatmapPressurePreset.Short => TimeSpan.FromSeconds(10),
			HeatmapPressurePreset.Medium => TimeSpan.FromSeconds(30),
			HeatmapPressurePreset.Long => TimeSpan.FromMinutes(1),
			_ => TimeSpan.FromSeconds(30)
		};

	private static TimeSpan GetTrainingPeriod(HeatmapPressurePreset preset) =>
		preset switch
		{
			HeatmapPressurePreset.Short => TimeSpan.FromMinutes(5),
			HeatmapPressurePreset.Medium => TimeSpan.FromMinutes(15),
			HeatmapPressurePreset.Long => TimeSpan.FromHours(1),
			_ => TimeSpan.FromMinutes(15)
		};

	#endregion
}
