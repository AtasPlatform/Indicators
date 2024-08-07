namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Threading;

	using OFT.Attributes;
	using OFT.Attributes.Editors;
    using OFT.Localization;
	
    [Category("Order Flow")]
	[DisplayName("CVD pro / Market Power")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.MarketPowerDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602424")]
	public class MarketPower : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _barDelta = new("BarDeltaId", "BarDelta")
		{
			Color = CrossColor.FromArgb(255, 100, 149, 237),
			VisualType = VisualMode.Hide,
			IsHidden = true,
            UseMinimizedModeIfEnabled = true
		};
		private readonly ValueDataSeries _cumulativeDelta = new("CumulativeDelta", "HiLo")
		{
			Color = CrossColor.FromArgb(255, 100, 149, 237),
			Width = 2,
			IsHidden = true,
			ShowZeroValue = false,
            UseMinimizedModeIfEnabled = true
		};
        private readonly ValueDataSeries _higher = new("HigherId", "Higher")
        {
	        Color = CrossColor.FromArgb(255, 135, 206, 235),
			VisualType = VisualMode.Hide,
			IsHidden = true,
            UseMinimizedModeIfEnabled = true
        };
        private readonly ValueDataSeries _lower = new("LowerId", "Lower")
        {
			Color = CrossColor.FromArgb(255, 135, 206, 235),
			VisualType = VisualMode.Line,
			IsHidden = true,
			UseMinimizedModeIfEnabled = true
        };

        private readonly SMA _sma = new() { Period = 14 };

        private readonly ValueDataSeries _smaSeries = new("SmaSeries", "SMA")
        {
	        Color = CrossColor.FromArgb(255, 128, 128, 128),
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

		private ConcurrentQueue<CumulativeTrade> _gapTrades = new();
		private ConcurrentQueue<MarketDataArg> _gapTicks = new();

		private bool _calculating;

        #endregion

        #region Properties

        #region Settings

        [Parameter]
        [Range(0, 1000000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 10)]
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

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CumulativeTrades), GroupName = nameof(Strings.Settings), Description = nameof(Strings.CumulativeTradesModeDescription), Order = 20)]
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

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumVolume), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MinVolumeFilterCommonDescription), Order = 30)]
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

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumVolume), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MaxVolumeFilterCommonDescription), Order = 40)]
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

        #endregion

        #region Visualization

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowSMA), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.DisplaySMADescription), Order = 100)]
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

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowHighLow), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.DisplayHighLowLineDescription), Order = 110)]
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

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowCumulative), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.VisualModeHistogramDescription), Order = 120)]
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

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HighLowColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.HighLowLineColorDescription), Order = 300)]
		public CrossColor HighLowColor
		{
			get => _lower.Color;
			set => _lower.Color = _higher.Color = value;
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.CumDeltaLineColorDescription), Order = 310)]
		public CrossColor LineColor
		{
			get => _cumulativeDelta.Color;
			set => _cumulativeDelta.Color = _barDelta.Color = value;
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.SMALineColorDescription), Order = 320)]
		public CrossColor SmaColor
		{
			get => _smaSeries.Color;
			set => _smaSeries.Color = value;
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.CumDeltaLineWidthDescription), Order = 330)]
		[Range(1, 100)]
		public int Width
		{
			get => _cumulativeDelta.Width;
			set => _cumulativeDelta.Width = value;
        }

        #endregion

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

		protected override void OnRecalculate()
		{
			_gapTrades.Clear();
			_gapTicks.Clear();
			_calculating = true;
			_sma.SourceDataSeries = new ValueDataSeries("SMA");
		}

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

			if (bar == CurrentBar - 1 && _bigTradesIsReceived)
			{
				if (ShowCumulative)
				{
					if (_cumulativeDelta[bar] is 0)
						_cumulativeDelta[bar] = _cumulativeDelta[bar - 1];

					if (_lower[bar] is 0)
						_lower[bar] = _lower[bar - 1];
				}

				_smaSeries[bar] = _sma.Calculate(bar, _cumulativeDelta[bar]);
			}
		}

		protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
		{
			lock (_locker)
			{
				var trades = cumulativeTrades.Where(t=>t.Direction is not TradeDirection.Between).ToList();

				CalculateHistory(trades);

				if (CumulativeTrades)
					CalcCumulativeGap();
				else
					CalcSeparateGap();
			}

			_bigTradesIsReceived = true;
			_calculating = false;
        }
		
		protected override void OnNewTrade(MarketDataArg trade)
		{
			if (CumulativeTrades)
				return;

			if (!_bigTradesIsReceived)
			{
				if (_calculating)
					_gapTicks.Enqueue(trade);

				return;
			}

			var newBar = _lastBar < CurrentBar - 1;

			if (newBar)
				_lastBar = CurrentBar - 1;

			CalculateTick(trade, newBar, CurrentBar - 1);
        }
		
		protected override void OnCumulativeTrade(CumulativeTrade trade)
		{
			if(!CumulativeTrades)
				return;

			if (!_bigTradesIsReceived)
			{
				if(_calculating)
					_gapTrades.Enqueue(trade);

				return;
			}

			var newBar = _lastBar < CurrentBar - 1;

			if (newBar)
				_lastBar = CurrentBar - 1;
			
            CalculateTrade(trade, false, newBar, CurrentBar - 1);
        }

		protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
		{
			if (!CumulativeTrades)
				return;

            if (!_bigTradesIsReceived)
			{
				if (_calculating)
					_gapTrades.Enqueue(trade);

                return;
			}

            CalculateTrade(trade, true, false, CurrentBar - 1);
		}

		protected override void OnDispose()
		{
			_sma.Dispose();
			_gapTrades.Clear();
			_gapTicks.Clear();
		}

		#endregion

        #region Private methods

        private void CalcSeparateGap()
        {
            if (_gapTicks.TryPeek(out var firstTrade))
            {
                var histBar = CurrentBar - 1;
                var lastHistBar = 0;
                var candle = GetCandle(CurrentBar - 1);

                for (var i = CurrentBar - 1; i >= _sessionBegin; i--)
                {
                    candle = GetCandle(i);

                    if (firstTrade.Time >= candle.Time && firstTrade.Time <= candle.LastTime)
                    {
                        histBar = i;
                        break;
                    }
                }

                while (_gapTicks.TryDequeue(out var trade))
                {
                    if (trade.Time < _lastTrade.Time)
                        continue;

                    if (!(trade.Time >= candle.Time && trade.Time <= candle.LastTime))
                    {
                        for (var i = histBar + 1; i < CurrentBar; i++)
                        {
                            candle = GetCandle(i);

                            if (trade.Time < candle.Time || trade.Time > candle.LastTime)
                                continue;

                            histBar = i;
                            break;
                        }
                    }

                    CalculateTick(trade, lastHistBar != histBar, histBar);
                    lastHistBar = histBar;
                }
            }
        }

        private void CalcCumulativeGap()
        {
            if (_gapTrades.TryPeek(out var firstTrade))
            {
                var histBar = CurrentBar - 1;
                var lastHistBar = 0;
                var candle = GetCandle(CurrentBar - 1);

                for (var i = CurrentBar - 1; i >= _sessionBegin; i--)
                {
                    candle = GetCandle(i);

                    if (firstTrade.Time >= candle.Time && firstTrade.Time <= candle.LastTime)
                    {
                        histBar = i;
                        break;
                    }
                }

                while (_gapTrades.TryDequeue(out var trade))
                {
                    if (trade.Time < _lastTrade.Time)
                        continue;

                    if (!(trade.Time >= candle.Time && trade.Time <= candle.LastTime))
                    {
                        for (var i = histBar + 1; i < CurrentBar; i++)
                        {
                            candle = GetCandle(i);

                            if (trade.Time < candle.Time || trade.Time > candle.LastTime)
                                continue;

                            histBar = i;
                            break;
                        }
                    }

                    var isUpdate = _lastTrade.IsEqual(trade);
                    CalculateTrade(trade, isUpdate, lastHistBar != histBar, histBar);
                    lastHistBar = histBar;
                }
            }
        }

        private void CalculateHistory(List<CumulativeTrade> trades)
		{
			for (var i = 0; i < _sessionBegin; i++)
				_sma.Calculate(i, 0); //SMA must be calculated from first bar

			var lastTradeIdx = 0;

			if (trades.Count is 0)
				return;

            trades = trades.OrderBy(x => x.Time).ToList();
			
			for (var i = _sessionBegin; i <= CurrentBar - 1; i++)
			{
				CalculateBarTrades(trades, i, ref lastTradeIdx);

				if (_cumulativeDelta[i] == 0)
					_cumulativeDelta[i] = _cumulativeDelta[i - 1];

				_smaSeries[i] = _sma.Calculate(i, _cumulativeDelta[i]);

				RaiseBarValueChanged(i);
			}
			
			_lastTrade = trades[^1];

			_lastDelta = (_lastTrade.Direction is TradeDirection.Buy ? 1 : -1) *
				(CumulativeTrades 
					? _lastTrade.Volume 
					: _lastTrade.Ticks.Last().Volume);

			if (!ShowCumulative)
			{
				_lastMinValue = _lastMaxValue = 0;
				_sum = ShowCumulative ? _delta : 0;
				_lastDelta = 0;
            }

			RedrawChart();
		}

		//History
		private void CalculateBarTrades(List<CumulativeTrade> trades, int bar, ref int startIdx)
		{
			var candle = GetCandle(bar);
			
			var candleTrades = new List<CumulativeTrade>();
			
            for (var i = startIdx; i < trades.Count; i++)
            {
	            var trade = trades[i];

				if(trade.Direction is TradeDirection.Between)
					continue;

				if (trade.Time > candle.LastTime)
				{
					startIdx = i;
					break;
				}

				if (trade.Time < candle.Time)
					continue;

				candleTrades.Add(trade);
            }
			
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

			_sum += sum;
			_maxValue = _lastMaxValue;
			_minValue = _lastMinValue;

			_delta += _lastDelta;

			_cumulativeDelta[bar] = _delta == 0 ? _cumulativeDelta[bar - 1] : _delta;

			_barDelta[bar] = _lastDelta;

			if (ShowCumulative)
			{
				_higher[bar] = _maxValue == 0 ? _higher[bar - 1] : _maxValue;
				_lower[bar] = _minValue == 0 ? _lower[bar - 1] : _minValue;
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

		private void CalculateTick(MarketDataArg trade, bool newBar, int bar)
		{
			if (newBar)
			{
				_lastMinValue = _lastMaxValue = 0;
				_sum = ShowCumulative ? _delta : 0;
				_lastDelta = 0;
			}

			if(trade.Volume < _minVolume || trade.Volume > _maxVolume && _maxVolume is not 0)
				return;

			_sum += trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			if (_sum > _lastMaxValue || _lastMaxValue == 0)
				_lastMaxValue = _sum;

			if (_sum < _lastMinValue || _lastMinValue == 0)
				_lastMinValue = _sum;

			_maxValue = _lastMaxValue;
			_minValue = _lastMinValue;

			_lastDelta = trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			_delta += _lastDelta;
			_barDelta[bar] += _lastDelta; _cumulativeDelta[bar] = _delta == 0 ? _cumulativeDelta[bar] : _delta;

			if (ShowCumulative)
			{
				_higher[bar] = _maxValue == 0 ? _higher[bar - 1] : _maxValue;
				_lower[bar] = _minValue == 0 ? _lower[bar - 1] : _minValue;
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

			_smaSeries[bar] = _sma.Calculate(bar, _cumulativeDelta[bar]);
			
			RaiseBarValueChanged(bar);
        }

        private void CalculateTrade(CumulativeTrade trade, bool isUpdate, bool newBar, int bar)
		{
			if (newBar)
			{
				_lastMinValue = _lastMaxValue = 0;
				_sum = ShowCumulative ? _delta : 0;
				_lastDelta = 0;
			}

			if (isUpdate && _lastTrade != null && IsTradeValid(_lastTrade))
			{
				var oldSum = _sum;
				_sum -= _lastTrade.Volume * (_lastTrade.Direction == TradeDirection.Buy ? 1 : -1);

				if (oldSum == _lastMaxValue)
					_lastMaxValue = _sum;

				if (oldSum == _lastMinValue)
					_lastMinValue = _sum; 
				
				_delta -= _lastDelta;
				_barDelta[bar] -= _lastDelta;
            }
			
			if (IsTradeValid(trade))
			{
				_sum += trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

				if (_sum > _lastMaxValue || _lastMaxValue == 0)
					_lastMaxValue = _sum;

				if (_sum < _lastMinValue || _lastMinValue == 0)
					_lastMinValue = _sum;

				_maxValue = _lastMaxValue;
				_minValue = _lastMinValue;

				_lastDelta = trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);
				_delta += _lastDelta;
				_barDelta[bar] += _lastDelta;
            }

			_cumulativeDelta[bar] = _delta == 0 ? _cumulativeDelta[bar] : _delta;
            
            if (ShowCumulative)
			{
				_higher[bar] = _maxValue == 0 ? _higher[bar - 1] : _maxValue;
				_lower[bar] = _minValue == 0 ? _lower[bar - 1] : _minValue;
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

			_smaSeries[bar] = _sma.Calculate(bar, _cumulativeDelta[bar]);

			_lastTrade = trade;

			RaiseBarValueChanged(bar);
		}

		private bool IsTradeValid(CumulativeTrade trade)
		{
			return trade.Volume >= _minVolume && (trade.Volume <= _maxVolume || _maxVolume is 0);
		}

        #endregion
	}
}