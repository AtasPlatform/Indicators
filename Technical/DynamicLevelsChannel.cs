namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Properties;

	[Category("Clusters, Profiles, Levels")]
	[DisplayName("Dynamic Levels Channel")]
	public class DynamicLevelsChannel : Indicator
	{
		#region Nested types

		private class VolumeInfo
		{
			#region Properties

			public decimal Price { get; set; }

			public int Bar { get; set; }

			public decimal Volume { get; set; }

			public decimal Bid { get; set; }

			public decimal Ask { get; set; }

			public int Time { get; set; }

			#endregion
		}

		private class Signal
		{
			#region Properties

			public TradeDirection Direction { get; set; }

			public decimal Price { get; set; }

			public decimal PocTicks { get; set; }

			#endregion
		}

		public enum CalculationMode
		{
			[Display(ResourceType = typeof(Resources), Name = "Volume")]
			Volume,

			[Display(ResourceType = typeof(Resources), Name = "PositiveDelta")]
			PosDelta,

			[Display(ResourceType = typeof(Resources), Name = "NegativeDelta")]
			NegDelta,

			[Display(ResourceType = typeof(Resources), Name = "Delta")]
			Delta,

			[Display(ResourceType = typeof(Resources), Name = "Time")]
			Time
		}

		#endregion

		#region Static and constants

		private const decimal _percent = 70m;
		private const int _priceInterval = 2;

		#endregion

		#region Fields

		private readonly RangeDataSeries _areaSeries = new RangeDataSeries("Range");
		private readonly ValueDataSeries _buySeries = new ValueDataSeries(Resources.Buys);
		private readonly ValueDataSeries _downSeries = new ValueDataSeries("VAL");
		private readonly ValueDataSeries _pocSeries = new ValueDataSeries("POC");
		private readonly List<VolumeInfo> _priceInfo = new List<VolumeInfo>();
		private readonly ValueDataSeries _sellSeries = new ValueDataSeries(Resources.Sells);
		private readonly List<Signal> _signals = new List<Signal>();
		private readonly ValueDataSeries _upSeries = new ValueDataSeries("VAH");
		private CalculationMode _calculationMode;
		private int _lastBar;
		private decimal _lastVah;
		private decimal _lastVal;
		private decimal _lastVol;
		private decimal _maxPrice;

		private int _period;
		private decimal _tickSize;
		private List<VolumeInfo> _volumeGroup = new List<VolumeInfo>();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "CalculationMode", GroupName = "Common", Order = 100)]
		public CalculationMode CalcMode
		{
			get => _calculationMode;
			set
			{
				_calculationMode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common", Order = 110)]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "AreaColor", GroupName = "Drawing")]
		public Color AreaColor
		{
			get => _areaSeries.RangeColor;
			set => _areaSeries.RangeColor = value;
		}

		#endregion

		#region ctor

		public DynamicLevelsChannel()
			: base(true)
		{
			DenyToChangePanel = true;
			_period = 40;
			_lastBar = -1;

			_areaSeries.RangeColor = Color.FromArgb(100, 255, 100, 100);
			_areaSeries.IsHidden = true;
			DataSeries[0] = _areaSeries;
			_upSeries.ShowZeroValue = _downSeries.ShowZeroValue = _pocSeries.ShowZeroValue = false;
			_upSeries.Width = _downSeries.Width = _pocSeries.Width = 2;
			_pocSeries.Color = Colors.Aqua;

			_buySeries.VisualType = VisualMode.UpArrow;
			_buySeries.Color = Colors.Green;
			_sellSeries.VisualType = VisualMode.DownArrow;
			_sellSeries.Color = Colors.Red;
			_buySeries.ShowZeroValue = _sellSeries.ShowZeroValue = false;

			DataSeries.Add(_upSeries);
			DataSeries.Add(_downSeries);
			DataSeries.Add(_pocSeries);
			DataSeries.Add(_buySeries);
			DataSeries.Add(_sellSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				_lastVah = 0;
				_lastVal = 0;
				_lastVol = 0;
				_tickSize = InstrumentInfo.TickSize;
				_priceInfo.Clear();
				_upSeries.SetPointOfEndLine(_period - 1);
				_downSeries.SetPointOfEndLine(_period - 1);
				_pocSeries.SetPointOfEndLine(_period - 1);
				_signals.Clear();
				return;
			}

			var candle = GetCandle(bar - 1);

			if (bar == _lastBar)
				_priceInfo.RemoveAll(x => x.Bar == bar);

			for (var i = candle.Low; i <= candle.High; i += InstrumentInfo.TickSize)
			{
				var priceInfo = candle.GetPriceVolumeInfo(i);

				if (priceInfo != null)
				{
					_priceInfo.Add(new VolumeInfo
					{
						Price = i,
						Volume = priceInfo.Volume,
						Bar = bar,
						Ask = priceInfo.Ask,
						Bid = priceInfo.Bid,
						Time = priceInfo.Time
					});
				}
			}

			_lastBar = bar;

			if (bar < _period)
				return;

			_priceInfo.RemoveAll(x => x.Bar == bar - Period);

			_volumeGroup = _priceInfo
				.GroupBy(x => x.Price)
				.Select(p => new VolumeInfo
				{
					Price = p.First().Price,
					Volume = p.Sum(v => v.Volume),
					Time = p.Sum(t => t.Time),
					Ask = p.Sum(a => a.Ask),
					Bid = p.Sum(b => b.Bid)
				})
				.OrderByDescending(x => x.Volume)
				.ToList();

			var maxPriceInfo = _volumeGroup
				.FirstOrDefault();

			if (maxPriceInfo != null)
				_maxPrice = maxPriceInfo.Price;

			VolumeInfo pocValue;

			switch (_calculationMode)
			{
				case CalculationMode.Volume:
					_pocSeries[bar] = _maxPrice;
					break;
				case CalculationMode.PosDelta:
					pocValue = _volumeGroup
						.OrderByDescending(x => x.Ask - x.Bid)
						.FirstOrDefault();

					if (pocValue != null && pocValue.Ask - pocValue.Bid > 0)
						_pocSeries[bar] = pocValue.Price;
					break;
				case CalculationMode.NegDelta:
					pocValue = _volumeGroup
						.OrderBy(x => x.Ask - x.Bid)
						.FirstOrDefault();

					if (pocValue != null && pocValue.Ask - pocValue.Bid < 0)
						_pocSeries[bar] = pocValue.Price;
					break;
				case CalculationMode.Delta:
					pocValue = _volumeGroup
						.OrderByDescending(x => Math.Abs(x.Ask - x.Bid))
						.FirstOrDefault();

					if (pocValue != null)
						_pocSeries[bar] = pocValue.Price;
					break;
				case CalculationMode.Time:
					pocValue = _volumeGroup
						.OrderByDescending(x => x.Time)
						.FirstOrDefault();

					if (pocValue != null)
						_pocSeries[bar] = pocValue.Price;
					break;
			}

			GetArea();

			_areaSeries[bar] = new RangeValue
			{ Lower = _lastVal, Upper = _lastVah };
			_upSeries[bar] = _lastVah;
			_downSeries[bar] = _lastVal;

			if (candle.High > _upSeries[bar] && candle.Low <= _upSeries[bar]
				||
				candle.Low < _downSeries[bar] && candle.High >= _downSeries[bar])
			{
				var signal = new Signal
				{
					Direction = Math.Abs(candle.High - _upSeries[bar]) < Math.Abs(candle.Low - _downSeries[bar])
					? TradeDirection.Buy
					: TradeDirection.Sell
				};

				signal.Price = signal.Direction == TradeDirection.Buy
					? _upSeries[bar]
					: _downSeries[bar];

				signal.PocTicks = Math.Abs(signal.Price - _pocSeries[bar]) / _tickSize;

				_signals.Add(signal);
			}

			if (candle.High > _upSeries[bar])
				_signals.RemoveAll(x => x.Direction == TradeDirection.Sell);

			if (candle.Low < _downSeries[bar])
				_signals.RemoveAll(x => x.Direction == TradeDirection.Buy);

			for (var i = _signals.Count - 1; i >= 0; i--)
			{
				var signal = _signals[i];

				if (signal.Direction == TradeDirection.Buy)
				{
					if (Math.Abs(candle.High - _upSeries[bar]) / _tickSize >= signal.PocTicks && _buySeries[bar] == 0)
					{
						_buySeries[bar] = candle.Low - _tickSize * 2;
						_signals.RemoveAt(i);
					}
				}

				if (signal.Direction == TradeDirection.Sell)
				{
					if (Math.Abs(_downSeries[bar] - candle.Low) / _tickSize >= signal.PocTicks && _sellSeries[bar] == 0)
					{
						_sellSeries[bar] = candle.High + _tickSize * 2;
						_signals.RemoveAt(i);
					}
				}
			}
		}

		#endregion

		#region Private methods

		private void GetArea()
		{
			var totalVolume = _volumeGroup.Sum(x => x.Volume);

			if (totalVolume == _lastVol)
				return;

			var vah = 0m;
			var val = 0m;
			var high = _volumeGroup.Max(x => x.Price);
			var low = _volumeGroup.Min(x => x.Price);

			if (high != 0 && low != 0)
			{
				vah = val = _maxPrice;

				var vol = _volumeGroup
					.Where(x => x.Price == _maxPrice)
					.Sum(x => x.Volume);

				var valueAreaVolume = totalVolume * _percent * 0.01m;

				while (vol <= valueAreaVolume)
				{
					if (vah >= high && val <= low)
						break;

					var upperVol = 0m;
					var lowerVol = 0m;
					var upperPrice = vah;
					var lowerPrice = val;

					for (var i = 0; i <= _priceInterval; i++)
					{
						if (high > upperPrice + _tickSize)
						{
							upperPrice += _tickSize;

							upperVol += _volumeGroup
								.Where(x => x.Price == upperPrice)
								.Sum(x => x.Volume);
						}

						if (low > lowerPrice - _tickSize)
							continue;

						lowerPrice -= _tickSize;

						lowerVol += _volumeGroup
							.Where(x => x.Price == lowerPrice)
							.Sum(x => x.Volume);
					}

					if (lowerVol == 0 && upperVol == 0)
					{
						vah = Math.Min(upperPrice, high);
						val = Math.Max(lowerPrice, low);
						break;
					}

					if (upperVol >= lowerVol)
					{
						vah = upperPrice;
						vol += upperVol;
					}
					else
					{
						val = lowerPrice;
						vol += lowerVol;
					}

					if (vol >= valueAreaVolume)
						break;
				}
			}

			_lastVol = totalVolume;
			_lastVah = vah;
			_lastVal = val;
		}

		#endregion
	}
}