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
		private readonly List<CumulativeTrade> _trades = new List<CumulativeTrade>();
		private bool _bigTradesIsReceived;
		private decimal _delta;
		private Color _hiLoColor;
		private int _lastBar;
		private decimal _lastBarValue;
		private decimal _lastDelta;
		private decimal _lastMaxValue;
		private decimal _lastMinValue;
		private decimal _lastTotalVol;
		private Color _lineColor;
		private decimal _maxValue;
		private int _maxVolume;
		private decimal _minValue;
		private int _minVolume;
		private bool _showCumulative;
		private bool _showHiLo;
		private bool _showSma;
		private Color _smaColor;
		private decimal _totalVol;
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
				_smaSeries.Color = Color.FromArgb(value && ShowCumulative ? _smaColor.A : (byte)0, _smaColor.R, _smaColor.B, _smaColor.B);
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShowHighLow", GroupName = "Visualization", Order = 110)]
		public bool ShowHighLow
		{
			get => _showHiLo;
			set

			{
				_showHiLo = value;
				_higher.Color = _lower.Color = Color.FromArgb(value ? _hiLoColor.A : (byte)0, _hiLoColor.R, _hiLoColor.B, _hiLoColor.B);
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
					_cumulativeDelta.Color = _lineColor;
					_higher.Color = Colors.Transparent;
					_barDelta.Color = Colors.Transparent;

					if (_showSma)
						_smaSeries.Color = _smaColor;
				}
				else
				{
					_lower.VisualType = VisualMode.Histogram;
					_higher.Color = _hiLoColor;
					_barDelta.Color = _lineColor;
					_cumulativeDelta.Color = Colors.Transparent;
					_smaSeries.Color = Colors.Transparent;
				}

				RecalculateValues();
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
			get => _hiLoColor;
			set
			{
				_hiLoColor = value;
				_lower.Color = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Settings", Order = 310)]
		public Color LineColor
		{
			get => _lineColor;
			set
			{
				_lineColor = value;

				if (ShowCumulative)
					_cumulativeDelta.Color = value;
				else
					_barDelta.Color = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAColor", GroupName = "Settings", Order = 320)]
		public Color SmaColor
		{
			get => _smaColor;
			set => _smaColor = value;
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

			_barDelta.VisualType = VisualMode.Histogram;
			_barDelta.Color = Colors.Transparent;
			_higher.VisualType = VisualMode.Histogram;

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
				_trades.Clear();
				DataSeries.ForEach(x => x.Clear());
				_lastBar = -1;
				_lastBar = -1;
				_maxValue = _minValue = _lastMaxValue = _lastMinValue = 0;
				_delta = _totalVol = 0;
				_barDelta.Clear();

				var totalBars = ChartInfo.PriceChartContainer.TotalBars;
				var sessionBegin = totalBars;

				for (var i = totalBars; i >= 0; i--)
				{
					if (!IsNewSession(i))
						continue;

					sessionBegin = i;
					break;
				}

				for (var i = 0; i < sessionBegin; i++)
					_sma.Calculate(i, 0);

				RequestForCumulativeTrades(new CumulativeTradesRequest(GetCandle(sessionBegin).Time));
			}

			if (bar == ChartInfo.PriceChartContainer.TotalBars)
			{
				lock (_locker)
					_smaSeries[bar] = _sma.Calculate(bar, _cumulativeDelta[bar]);
			}
		}

		protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
		{
			lock (_locker)
				CalculateHistory(cumulativeTrades.OrderBy(x => x.Time).ToList());
		}

		#endregion

		#region Private methods

		private void CalculateHistory(List<CumulativeTrade> trades)
		{
			lock (_locker)
			{
				var curCandle = 0;

				foreach (var trade in trades)
				{
					var time = trade.Time;

					for (var i = curCandle; i < ChartInfo.PriceChartContainer.TotalBars; i++)
					{
						var candle = GetCandle(i);

						if (candle.Time <= time && candle.LastTime >= time)
						{
							curCandle = i;
							CalculateBigTrade(trade, true, curCandle, false);
							break;
						}
					}

					_bigTradesIsReceived = true;
					_smaSeries[curCandle] = _sma.Calculate(curCandle, _cumulativeDelta[curCandle]);
				}
			}
		}

		private void CalculateBigTrade(CumulativeTrade trade, bool needToAdd, int bar, bool updatingTrade)
		{
			var isNewBt = false;

			lock (_trades)
			{
				if (updatingTrade && _trades.Count != 0 && _trades.Last().IsEqual(trade))
				{
					_trades[_trades.Count - 1] = trade;
					_delta = _lastDelta;
					_totalVol = _lastTotalVol;
				}
				else
				{
					isNewBt = true;

					if (needToAdd)
						_trades.Add(trade);
				}
			}

			if (isNewBt)
			{
				_lastDelta = _delta;
				_lastTotalVol = _totalVol;
			}

			if (_lastBar != bar)
			{
				_lastBar = bar;
				_maxValue = _minValue = _lastMaxValue = _lastMinValue = 0;
			}

			if (trade.Direction != TradeDirection.Between && trade.Volume >= _minVolume && (trade.Volume <= _maxVolume || _maxVolume == 0))
			{
				_delta += trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);
				_totalVol += trade.Volume;
			}
			else
			{
				if (isNewBt)
				{
					_lastBarValue = _barDelta[bar];
					_lastMaxValue = _maxValue;
					_lastMinValue = _minValue;
				}
				else
				{
					_barDelta[bar] = _lastBarValue;
					_maxValue = _higher[bar] = _lastMaxValue;
					_minValue = _lower[bar] = _lastMinValue;
				}

				lock (_locker)
					_cumulativeDelta[bar] = _delta;

				return;
			}

			lock (_locker)
				_cumulativeDelta[bar] = _delta;

			if (isNewBt)
			{
				_lastBarValue = _barDelta[bar];
				_lastMaxValue = _maxValue;
				_lastMinValue = _minValue;
			}
			else
			{
				_barDelta[bar] = _lastBarValue;
				_maxValue = _lastMaxValue;
				_minValue = _lastMinValue;
			}

			_barDelta[bar] += trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			if (ShowCumulative)
			{
				if (_delta > _maxValue || _maxValue == 0)
					_maxValue = _delta;

				if (_delta < _minValue || _minValue == 0)
					_minValue = _delta;
			}
			else
			{
				if (_barDelta[bar] > _maxValue || _maxValue == 0)
					_maxValue = _barDelta[bar];

				if (_barDelta[bar] < _minValue || _minValue == 0)
					_minValue = _barDelta[bar];
			}

			_higher[bar] = _maxValue;

			_lower[bar] = _minValue;
		}

		#endregion

		#region Overrides of ExtendedIndicator

		protected override void OnCumulativeTrade(CumulativeTrade trade)
		{
			if (!_bigTradesIsReceived)
				return;

			lock (_locker)
				CalculateBigTrade(trade, true, ChartInfo.PriceChartContainer.TotalBars, false);
		}

		protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
		{
			if (!_bigTradesIsReceived)
				return;

			lock (_locker)
				CalculateBigTrade(trade, true, ChartInfo.PriceChartContainer.TotalBars, true);
		}

		#endregion
	}
}