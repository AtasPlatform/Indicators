namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Threading;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Attributes.Editors;

	[Category("Order Flow")]
	[DisplayName("CVD pro / Market Power")]
	[Description("Cumulative delta volume pro. Allows to the CVD line for specifyed trade sizes")]
    [HelpLink("https://support.atas.net/knowledge-bases/2/articles/382-market-power")]
	public class MarketPower : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _barDelta = new("BarDelta")
		{
			Color = Color.FromRgb(100, 149, 237),
			VisualType = VisualMode.Hide,
			IsHidden = true,
            UseMinimizedModeIfEnabled = true
		};
		private readonly ValueDataSeries _cumulativeDelta = new("HiLo")
		{
			Color = Color.FromRgb(100, 149, 237),
			Width = 2,
			IsHidden = true,
			ShowZeroValue = false,
            UseMinimizedModeIfEnabled = true
		};
        private readonly ValueDataSeries _higher = new("Higher")
        {
	        Color = Color.FromRgb(135, 206, 235),
			VisualType = VisualMode.Hide,
			IsHidden = true,
            UseMinimizedModeIfEnabled = true
        };
        private readonly ValueDataSeries _lower = new("Lower")
        {
			Color = Color.FromRgb(135, 206, 235),
			VisualType = VisualMode.Line,
			IsHidden = true,
			UseMinimizedModeIfEnabled = true
        };

        private readonly SMA _sma = new() { Period = 14 };

        private readonly ValueDataSeries _smaSeries = new("SMA")
        {
	        Color = Color.FromRgb(128, 128, 128),
	        IsHidden = true,
			ShowZeroValue = false,
            UseMinimizedModeIfEnabled = true
        };

		private bool _bigTradesIsReceived;
		private bool _cumulativeTrades = true;
		private decimal _delta;
		private bool _first = true;
		private int _lastBar = -1;
        private decimal _lastDelta;
		private decimal _lastMaxValue;
		private decimal _lastMinValue;

		private CumulativeTrade _lastTrade;
		private object _locker = new();
		private decimal _maxValue;
		private decimal _maxVolume;
		private decimal _minValue;
		private decimal _minVolume;
		private int _sessionBegin;
		private bool _showCumulative = true;
		private bool _showHiLo = true;
		private bool _showSma = true;
		private decimal _sum;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ShowSMA", GroupName = "Visualization", Order = 100)]
		public bool ShowSma
		{
			get => _showSma;
			set
			{
				_showSma = value;

				if (ShowCumulative)
					_smaSeries.VisualType = value ? VisualMode.Line : VisualMode.Hide;
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

		[Display(ResourceType = typeof(Resources), Name = "CumulativeTrades", GroupName = "VolumeFilter", Order = 200)]
		[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
		public bool CumulativeTrades
		{
			get => _cumulativeTrades;
			set
			{
				_cumulativeTrades = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumVolume", GroupName = "VolumeFilter", Order = 205)]
		[Range(0, 1000000)]
		[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
		public decimal MinimumVolume
		{
			get => _minVolume;
			set
			{
				_minVolume = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "VolumeFilter", Order = 210)]
		[Range(0, 1000000)]
		[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
		public decimal MaximumVolume
		{
			get => _maxVolume;
			set
			{
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
		[Range(1, 100)]
		public int Width
		{
			get => _cumulativeDelta.Width;
			set => _cumulativeDelta.Width = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod", GroupName = "Settings", Order = 340)]
		public int SmaPeriod
		{
			get => _sma.Period;
			set
			{
				if (_sma.Period == value)
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
				_maxValue = _minValue = _lastMaxValue = _lastMinValue = 0;
				_first = true;
				_bigTradesIsReceived = false;
				DataSeries.ForEach(x => x.Clear());
				_delta = _lastDelta = 0;
				_barDelta.Clear();
			}

			if (bar == CurrentBar - 1 && _first)
			{
				_first = false;

				_sessionBegin = CurrentBar - 1;
				_lastBar = CurrentBar - 1;

				for (var i = CurrentBar - 1; i >= 0; i--)
				{
					if (!IsNewSession(i))
						continue;

					_sessionBegin = i;
					break;
				}

				for (var i = 0; i < _sessionBegin; i++)
					_sma.Calculate(i, 0);

				try
				{
					RequestForCumulativeTrades(new CumulativeTradesRequest(GetCandle(_sessionBegin).Time));
				}
				catch (ArgumentException)
				{
					var startTime = DateTime.Now;

					while ((DateTime.Now - startTime).TotalSeconds < 1 && !_bigTradesIsReceived)
						Thread.Sleep(10);

					RecalculateValues();
				}
			}

			if (bar == CurrentBar - 1)
				_smaSeries[bar] = _sma.Calculate(bar, _cumulativeDelta[bar]);
		}

		protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
		{
			lock (_locker)
			{
				var trades = cumulativeTrades.ToList();

				CalculateHistory(trades);
			}

			_bigTradesIsReceived = true;
		}

		protected override void OnCumulativeTrade(CumulativeTrade trade)
		{
			if (!_bigTradesIsReceived)
				return;

			var newBar = _lastBar < CurrentBar - 1;

			if (newBar)
				_lastBar = CurrentBar - 1;

			CalculateTrade(trade, false, newBar);
		}

		protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
		{
			if (!_bigTradesIsReceived)
				return;

			CalculateTrade(trade, true, false);
		}

		#endregion

		#region Private methods

		private void CalculateHistory(List<CumulativeTrade> trades)
		{
			for (var i = 0; i < _sessionBegin; i++)
				_sma.Calculate(i, 0); //SMA must be calculated from first bar

			for (var i = _sessionBegin; i <= CurrentBar - 1; i++)
			{
				CalculateBarTrades(trades, i);

				if (_cumulativeDelta[i] == 0)
					_cumulativeDelta[i] = _cumulativeDelta[i - 1];

				_smaSeries[i] = _sma.Calculate(i, _cumulativeDelta[i]);

				RaiseBarValueChanged(i);
			}

			RedrawChart();
		}

		private void CalculateBarTrades(List<CumulativeTrade> trades, int bar, bool realTime = false, bool newBar = false)
		{
			var candle = GetCandle(bar);

			var candleTrades = trades
				.Where(x => x is not null)
				.Where(x => x.Time >= candle.Time && x.Time <= candle.LastTime && x.Direction != TradeDirection.Between)
				.ToList();

			if (realTime && !newBar)
				_delta -= _lastDelta;

			var sum = ShowCumulative ? _delta : 0;

			_lastMinValue = _lastMaxValue = 0;

			if (CumulativeTrades)
			{
				var filterTrades = candleTrades
					.Where(x => x.Volume >= _minVolume && (x.Volume <= _maxVolume || _maxVolume == 0))
					.ToList();

				foreach (var trade in filterTrades)
				{
					sum += trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

					if (sum > _lastMaxValue || _lastMaxValue == 0)
						_lastMaxValue = sum;

					if (sum < _lastMinValue || _lastMinValue == 0)
						_lastMinValue = sum;
				}

				_lastDelta =
					filterTrades
						.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1)
						);
			}
			else
			{
				var filterTrades = candleTrades
					.SelectMany(x => x.Ticks)
					.Where(x => x.Volume >= _minVolume && (x.Volume <= _maxVolume || _maxVolume == 0))
					.ToList();

				foreach (var trade in filterTrades)
				{
					sum += trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

					if (sum > _lastMaxValue || _lastMaxValue == 0)
						_lastMaxValue = sum;

					if (sum < _lastMinValue || _lastMinValue == 0)
						_lastMinValue = sum;
				}

				_lastDelta =
					filterTrades
						.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1)
						);
			}

			_maxValue = _lastMaxValue;
			_minValue = _lastMinValue;

			_delta += _lastDelta;

			_cumulativeDelta[bar] = _delta == 0 ? _cumulativeDelta[bar - 1] : _delta;

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

				_higher[bar] = _lastMaxValue == 0 ? _higher[bar - 1] : _lastMaxValue;
				_lower[bar] = _lastMinValue == 0 ? _lower[bar - 1] : _lastMinValue;
			}

			if (bar == CurrentBar - 1)
				_smaSeries[bar] = _sma.Calculate(bar, _cumulativeDelta[bar]);

			RaiseBarValueChanged(bar);
		}

		private void CalculateTrade(CumulativeTrade trade, bool isUpdate, bool newBar)
		{
			if (!newBar || isUpdate)
				_delta -= _lastDelta;

			if (newBar)
			{
				_lastMinValue = _lastMaxValue = 0;
				_sum = ShowCumulative ? _delta : 0;
			}

			if (isUpdate && _lastTrade != null)
				_sum -= _lastTrade.Volume * (_lastTrade.Direction == TradeDirection.Buy ? 1 : -1);

			_sum += trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			if (_sum > _lastMaxValue || _lastMaxValue == 0)
				_lastMaxValue = _sum;

			if (_sum < _lastMinValue || _lastMinValue == 0)
				_lastMinValue = _sum;

			_maxValue = _lastMaxValue;
			_minValue = _lastMinValue;

			_lastDelta = trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);
			_delta += _lastDelta;

			_cumulativeDelta[CurrentBar - 1] = _delta == 0 ? _cumulativeDelta[CurrentBar - 1] : _delta;

			_barDelta[CurrentBar - 1] = _sum;

			if (ShowCumulative)
			{
				_higher[CurrentBar - 1] = _maxValue;

				_lower[CurrentBar - 1] = _minValue;
			}
			else
			{
				if (_barDelta[CurrentBar - 1] > _lastMaxValue || _lastMaxValue == 0)
					_lastMaxValue = _barDelta[CurrentBar - 1];

				if (_barDelta[CurrentBar - 1] < _lastMinValue || _lastMinValue == 0)
					_lastMinValue = _barDelta[CurrentBar - 1];

				_higher[CurrentBar - 1] = _lastMaxValue == 0 ? _higher[CurrentBar - 2] : _lastMaxValue;
				_lower[CurrentBar - 1] = _lastMinValue == 0 ? _lower[CurrentBar - 2] : _lastMinValue;
			}

			_smaSeries[CurrentBar - 1] = _sma.Calculate(CurrentBar - 1, _cumulativeDelta[CurrentBar - 1]);

			_lastTrade = trade;

			RaiseBarValueChanged(CurrentBar - 1);
		}

		#endregion
	}
}