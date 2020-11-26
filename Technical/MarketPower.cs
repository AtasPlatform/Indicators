namespace ATAS.Indicators.Technical
{
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[Category("Order Flow")]
	[DisplayName("Market Power")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/382-market-power")]
	public class MarketPower : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _barDelta = new ValueDataSeries("BarDelta");
		private readonly ValueDataSeries _cumulativeDelta = new ValueDataSeries("HiLo");
		private readonly ValueDataSeries _higher = new ValueDataSeries("Higher");
		private readonly object _locker = new object();
		private readonly ValueDataSeries _lower = new ValueDataSeries("Lower");

		private readonly SMA _sma = new SMA();
		private readonly ValueDataSeries _smaSeries = new ValueDataSeries("SMA");
		private bool _bigTradesIsReceived;
		private decimal _delta;
		private int _lastBar;
		private decimal _lastDelta;
		private decimal _lastMaxValue;
		private decimal _lastMinValue;
		private decimal _maxValue;
		private int _maxVolume;
		private decimal _minValue;
		private int _minVolume;
		private int _sessionBegin;
		private bool _showCumulative;
		private bool _showHiLo;
		private bool _showSma;
		private List<CumulativeTrade> _trades = new List<CumulativeTrade>();
		private int _width;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ShowSMA", GroupName = "Visualization", Order = 100)]
		public bool ShowSma
		{
			get => _showSma;
			set
			{
				_showSma = value;
				_smaSeries.VisualType = value ? VisualMode.Histogram : VisualMode.Hide;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShowHighLow", GroupName = "Visualization", Order = 110)]
		public bool ShowHighLow
		{
			get => _showHiLo;
			set

			{
				_showHiLo = value;
				_higher.VisualType = value && !_showCumulative ? VisualMode.Histogram : VisualMode.Hide;

				_lower.VisualType = value
					? _showCumulative
						? VisualMode.Line
						: VisualMode.Histogram
					: VisualMode.Hide;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShowCumulative", GroupName = "Visualization", Order = 120)]
		public bool ShowCumulative
		{
			get => _showCumulative;
			set
			{
				_showCumulative = value;

				if (value)
				{
					_lower.VisualType = VisualMode.Line;
					_cumulativeDelta.VisualType = VisualMode.Line;
					_higher.VisualType = VisualMode.Hide;
					_barDelta.VisualType = VisualMode.Hide;
					_smaSeries.VisualType = _showSma ? VisualMode.Line : VisualMode.Hide;
				}
				else
				{
					_lower.VisualType = VisualMode.Histogram;
					_higher.VisualType = VisualMode.Histogram;
					_barDelta.VisualType = VisualMode.Histogram;
					_cumulativeDelta.VisualType = VisualMode.Hide;
					_smaSeries.VisualType = VisualMode.Hide;
				}

				RecalculateValues();
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumVolume", GroupName = "VolumeFilter", Order = 200)]
		public int MinimumVolume
		{
			get => _minVolume;
			set
			{
				if (value < 0)
					return;

				_minVolume = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "VolumeFilter", Order = 210)]
		public int MaximumVolume
		{
			get => _maxVolume;
			set
			{
				if (value < 0)
					return;

				_maxVolume = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "HighLowColor", GroupName = "Settings", Order = 300)]
		public Color HighLowColor
		{
			get => _lower.Color;
			set => _lower.Color = _higher.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Settings", Order = 310)]
		public Color LineColor
		{
			get => _cumulativeDelta.Color;
			set => _cumulativeDelta.Color = _barDelta.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAColor", GroupName = "Settings", Order = 320)]
		public Color SmaColor
		{
			get => _smaSeries.Color;
			set => _smaSeries.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "Width", GroupName = "Settings", Order = 330)]
		public int Width
		{
			get => _width;
			set
			{
				if (value <= 0)
					return;

				_width = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod", GroupName = "Settings", Order = 340)]
		public int SmaPeriod
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MarketPower()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			HighLowColor = Color.FromRgb(135, 206, 235);
			LineColor = Color.FromRgb(100, 149, 237);
			SmaColor = Color.FromRgb(128, 128, 128);
			ShowSma = true;
			ShowCumulative = true;
			ShowHighLow = true;
			Width = 2;
			SmaPeriod = 14;

			_lastBar = -1;
			_barDelta.VisualType = VisualMode.Hide;
			_higher.VisualType = VisualMode.Hide;
			_lower.VisualType = VisualMode.Line;

			_lower.IsHidden = _smaSeries.IsHidden = _cumulativeDelta.IsHidden
				= _barDelta.IsHidden = _higher.IsHidden = true;
			_cumulativeDelta.Width = 2;
			_smaSeries.ShowZeroValue = _cumulativeDelta.ShowZeroValue = false;

			DataSeries[0] = _lower;
			DataSeries.Add(_smaSeries);
			DataSeries.Add(_cumulativeDelta);
			DataSeries.Add(_higher);
			DataSeries.Add(_barDelta);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_bigTradesIsReceived = false;
				_trades.Clear();
				DataSeries.ForEach(x => x.Clear());

				_maxValue = _minValue = _lastMaxValue = _lastMinValue = 0;
				_delta = 0;
				_barDelta.Clear();

				var totalBars = ChartInfo.PriceChartContainer.TotalBars;
				_sessionBegin = totalBars;
				_lastBar = totalBars;

				for (var i = totalBars; i >= 0; i--)
				{
					if (!IsNewSession(i))
						continue;

					_sessionBegin = i;
					break;
				}

				for (var i = 0; i < _sessionBegin; i++)
					_sma.Calculate(i, 0);

				RequestForCumulativeTrades(new CumulativeTradesRequest(GetCandle(_sessionBegin).Time));
			}

			if (bar == ChartInfo.PriceChartContainer.TotalBars)
				_smaSeries[bar] = _sma.Calculate(bar, _cumulativeDelta[bar]);
		}

		protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
		{
			var trades = cumulativeTrades.ToList();
			CalculateHistory(trades);

			_trades.AddRange(trades.Where(x => x.Time >= GetCandle(ChartInfo.PriceChartContainer.TotalBars - 2).Time));
			_bigTradesIsReceived = true;
		}

		#endregion

		#region Private methods

		private void CalculateHistory(List<CumulativeTrade> trades)
		{
			for (var i = _sessionBegin; i <= ChartInfo.PriceChartContainer.TotalBars; i++)
			{
				CalculateBarTrades(trades, i);

				if (_cumulativeDelta[i] == 0 && i <= 0)
					_cumulativeDelta[i] = _cumulativeDelta[i - 1];

				_smaSeries[i] = _sma.Calculate(i, _cumulativeDelta[i]);
			}

			RedrawChart();
		}

		private void CalculateBarTrades(List<CumulativeTrade> trades, int bar, bool realTime = false, bool newBar = false)
		{
			if (newBar)
				CalculateBarTrades(trades, bar - 1, true);

			var candle = GetCandle(bar);

			var candleTrades = trades
				.Where(x => x.Time >= candle.Time && x.Time <= candle.LastTime && x.Direction != TradeDirection.Between)
				.ToList();

			var filterTrades = candleTrades
				.Where(x => x.Volume >= _minVolume && (x.Volume <= _maxVolume || _maxVolume == 0))
				.ToList();

			if (realTime && !newBar)
			{
				_delta -= _lastDelta;
				_minValue -= _lastMinValue;
				_maxValue -= _lastMaxValue;
			}

			var sum = ShowCumulative ? _delta : 0;

			_lastMinValue = _lastMaxValue = 0;

			foreach (var trade in filterTrades)
			{
				sum += trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

				if (sum > _lastMaxValue || _lastMaxValue == 0)
					_lastMaxValue = sum;

				if (sum < _lastMinValue || _lastMinValue == 0)
					_lastMinValue = sum;
			}

			_maxValue = _lastMaxValue;
			_minValue = _lastMinValue;

			_lastDelta =
				filterTrades
					.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1)
					);
			_delta += _lastDelta;

			_cumulativeDelta[bar] = _delta;

			_barDelta[bar] = _lastDelta;

			if (ShowCumulative)
			{
				_higher[bar] = _maxValue;

				_lower[bar] = _minValue;
			}
			else
			{
				if (_barDelta[bar] > _lastMaxValue || _lastMaxValue == 0)
					_lastMaxValue = _barDelta[bar];

				if (_barDelta[bar] < _lastMinValue || _lastMinValue == 0)
					_lastMinValue = _barDelta[bar];

				_higher[bar] = _lastMaxValue;
				_lower[bar] = _lastMinValue;
			}
		}

		#endregion

		#region Overrides of ExtendedIndicator

		protected override void OnCumulativeTrade(CumulativeTrade trade)
		{
			if (!_bigTradesIsReceived)
				return;

			var totalBars = ChartInfo.PriceChartContainer.TotalBars;

			lock (_trades)
			{
				_trades.Add(trade);

				var newBar = _lastBar < ChartInfo.PriceChartContainer.TotalBars;

				if (newBar)
				{
					_lastBar = ChartInfo.PriceChartContainer.TotalBars;

					_trades = _trades
						.Where(x => x.Time > GetCandle(totalBars - 2).Time)
						.ToList();
				}

				CalculateBarTrades(_trades, ChartInfo.PriceChartContainer.TotalBars, true, newBar);
			}
		}

		protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
		{
			if (!_bigTradesIsReceived)
				return;

			lock (_trades)
			{
				_trades.RemoveAll(x => x.IsEqual(trade));
				_trades.Add(trade);

				CalculateBarTrades(_trades, ChartInfo.PriceChartContainer.TotalBars, true);
			}
		}

		#endregion
	}
}