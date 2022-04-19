namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Attributes.Editors;

	[Category("Order Flow")]
	[DisplayName("Multi Market Powers")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/371-multi-market-powers")]
	public class MultiMarketPower : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _filter1Series = new("Filter1");
		private readonly ValueDataSeries _filter2Series = new("Filter2");
		private readonly ValueDataSeries _filter3Series = new("Filter3");
		private readonly ValueDataSeries _filter4Series = new("Filter4");
		private readonly ValueDataSeries _filter5Series = new("Filter5");
		private bool _bigTradesIsReceived;
		private bool _cumulativeTrades = true;

		private decimal _delta1;
		private decimal _delta2;
		private decimal _delta3;
		private decimal _delta4;
		private decimal _delta5;
		private int _lastBar;
		private decimal _lastDelta1;
		private decimal _lastDelta2;
		private decimal _lastDelta3;
		private decimal _lastDelta4;
		private decimal _lastDelta5;
		private CumulativeTrade _lastTrade;
		private decimal _maxVolume1;
		private decimal _maxVolume2;
		private decimal _maxVolume3;
		private decimal _maxVolume4;
		private decimal _maxVolume5;
		private decimal _minVolume1;
		private decimal _minVolume2;
		private decimal _minVolume3;
		private decimal _minVolume4;
		private decimal _minVolume5;
		private int _sessionBegin;

		private bool _useFilter1;
		private bool _useFilter2;
		private bool _useFilter3;
		private bool _useFilter4;
		private bool _useFilter5;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "CumulativeTrades", GroupName = "Filters", Order = 90)]
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

		[Display(ResourceType = typeof(Resources), Name = "Enabled", GroupName = "Filter1", Order = 100)]
		public bool UseFilter1
		{
			get => _useFilter1;
			set
			{
				_useFilter1 = value;
				_filter1Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LineWidth", GroupName = "Filter1", Order = 120)]
		public int LineWidth1
		{
			get => _filter1Series.Width;
			set
			{
				if (value <= 0)
					return;

				_filter1Series.Width = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumVolume", GroupName = "Filter1", Order = 130)]
		[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
		public decimal MinVolume1
		{
			get => _minVolume1;
			set
			{
				if (value < 0)
					return;

				_minVolume1 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "Filter1", Order = 140)]
		[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
		public decimal MaxVolume1
		{
			get => _maxVolume1;
			set
			{
				if (value < 0)
					return;

				_maxVolume1 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Filter1", Order = 150)]
		public Color Color1
		{
			get => _filter1Series.Color;
			set => _filter1Series.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "Enabled", GroupName = "Filter2", Order = 200)]
		public bool UseFilter2
		{
			get => _useFilter2;
			set
			{
				_useFilter2 = value;
				_filter2Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LineWidth", GroupName = "Filter2", Order = 220)]
		public int LineWidth2
		{
			get => _filter2Series.Width;
			set
			{
				if (value <= 0)
					return;

				_filter2Series.Width = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumVolume", GroupName = "Filter2", Order = 230)]
		[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
		public decimal MinVolume2
		{
			get => _minVolume2;
			set
			{
				if (value < 0)
					return;

				_minVolume2 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "Filter2", Order = 240)]
		[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
		public decimal MaxVolume2
		{
			get => _maxVolume2;
			set
			{
				if (value < 0)
					return;

				_maxVolume2 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Filter2", Order = 250)]
		public Color Color2
		{
			get => _filter2Series.Color;
			set => _filter2Series.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "Enabled", GroupName = "Filter3", Order = 300)]
		public bool UseFilter3
		{
			get => _useFilter3;
			set
			{
				_useFilter3 = value;
				_filter3Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LineWidth", GroupName = "Filter3", Order = 320)]
		public int LineWidth3
		{
			get => _filter3Series.Width;
			set
			{
				if (value <= 0)
					return;

				_filter3Series.Width = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumVolume", GroupName = "Filter3", Order = 330)]
		[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
		public decimal MinVolume3
		{
			get => _minVolume3;
			set
			{
				if (value < 0)
					return;

				_minVolume3 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "Filter3", Order = 340)]
		[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
		public decimal MaxVolume3
		{
			get => _maxVolume3;
			set
			{
				if (value < 0)
					return;

				_maxVolume3 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Filter3", Order = 350)]
		public Color Color3
		{
			get => _filter3Series.Color;
			set => _filter3Series.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "Enabled", GroupName = "Filter4", Order = 400)]
		public bool UseFilter4
		{
			get => _useFilter4;
			set
			{
				_useFilter4 = value;
				_filter4Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LineWidth", GroupName = "Filter4", Order = 420)]
		public int LineWidth4
		{
			get => _filter4Series.Width;
			set
			{
				if (value <= 0)
					return;

				_filter4Series.Width = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumVolume", GroupName = "Filter4", Order = 430)]
		[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
		public decimal MinVolume4
		{
			get => _minVolume4;
			set
			{
				if (value < 0)
					return;

				_minVolume4 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "Filter4", Order = 440)]
		[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
		public decimal MaxVolume4
		{
			get => _maxVolume4;
			set
			{
				if (value < 0)
					return;

				_maxVolume4 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Filter4", Order = 450)]
		public Color Color4
		{
			get => _filter4Series.Color;
			set => _filter4Series.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "Enabled", GroupName = "Filter5", Order = 500)]
		public bool UseFilter5
		{
			get => _useFilter5;
			set
			{
				_useFilter5 = value;
				_filter5Series.VisualType = value ? VisualMode.Line : VisualMode.Hide;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LineWidth", GroupName = "Filter5", Order = 520)]
		public int LineWidth5
		{
			get => _filter5Series.Width;
			set
			{
				if (value <= 0)
					return;

				_filter5Series.Width = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumVolume", GroupName = "Filter5", Order = 530)]
		[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
		public decimal MinVolume5
		{
			get => _minVolume5;
			set
			{
				if (value < 0)
					return;

				_minVolume5 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "Filter5", Order = 540)]
		[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
		public decimal MaxVolume5
		{
			get => _maxVolume5;
			set
			{
				if (value < 0)
					return;

				_maxVolume5 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Filter5", Order = 550)]
		public Color Color5
		{
			get => _filter5Series.Color;
			set => _filter5Series.Color = value;
		}

		#endregion

		#region ctor

		public MultiMarketPower()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DenyToChangePanel = true;

			_lastBar = -1;
			UseFilter1 = UseFilter2 = UseFilter3 = UseFilter4 = UseFilter5 = true;
			_filter4Series.Width = _filter5Series.Width = 2;
			_minVolume2 = 6;
			_minVolume3 = 11;
			_minVolume4 = 21;
			_minVolume5 = 41;

			_maxVolume1 = 5;
			_maxVolume2 = 10;
			_maxVolume3 = 20;
			_maxVolume4 = 40;

			_filter1Series.Color = Color.FromRgb(135, 206, 235);
			_filter2Series.Color = Colors.Red;
			_filter3Series.Color = Colors.Green;
			_filter4Series.Color = Color.FromRgb(128, 128, 128);
			_filter5Series.Color = Color.FromRgb(205, 92, 92);

			_filter1Series.IsHidden = _filter2Series.IsHidden = _filter3Series.IsHidden
				= _filter4Series.IsHidden = _filter5Series.IsHidden = true;

			_filter1Series.ShowZeroValue = _filter2Series.ShowZeroValue = _filter3Series.ShowZeroValue
				= _filter4Series.ShowZeroValue = _filter5Series.ShowZeroValue = false;

			DataSeries[0] = _filter1Series;
			DataSeries.Add(_filter2Series);
			DataSeries.Add(_filter3Series);
			DataSeries.Add(_filter4Series);
			DataSeries.Add(_filter5Series);
		}

		#endregion

		#region Protected methods

		protected override void OnRecalculate()
		{
			_bigTradesIsReceived = false;
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_bigTradesIsReceived = false;
				DataSeries.ForEach(x => x.Clear());
				_delta1 = _delta2 = _delta3 = _delta4 = _delta5 = 0;

				var totalBars = CurrentBar - 1;
				_sessionBegin = totalBars;
				_lastBar = totalBars;

				for (var i = totalBars; i >= 0; i--)
				{
					if (!IsNewSession(i))
						continue;

					_sessionBegin = i;
					break;
				}

				RequestForCumulativeTrades(new CumulativeTradesRequest(GetCandle(_sessionBegin).Time));
			}

			if (_filter1Series[bar] != 0)
				return;

			_filter1Series[bar] = _filter1Series[bar - 1];
			_filter2Series[bar] = _filter2Series[bar - 1];
			_filter3Series[bar] = _filter3Series[bar - 1];
			_filter4Series[bar] = _filter4Series[bar - 1];
			_filter5Series[bar] = _filter5Series[bar - 1];
		}

		protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
		{
			var trades = cumulativeTrades.ToList();
			CalculateHistory(trades);

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

		private void CalculateTrade(CumulativeTrade trade, bool isUpdate, bool newBar)
		{
			if (CumulativeTrades && isUpdate && _lastTrade != null)
			{
				if (_lastTrade.IsEqual(trade))
				{
					if (_lastTrade.Volume >= _minVolume1 && (_lastTrade.Volume <= _maxVolume1 || _maxVolume1 == 0))
						_delta1 -= _lastTrade.Volume * (_lastTrade.Direction == TradeDirection.Buy ? 1 : -1);

					if (_lastTrade.Volume >= _minVolume2 && (_lastTrade.Volume <= _maxVolume2 || _maxVolume2 == 0))
						_delta2 -= _lastTrade.Volume * (_lastTrade.Direction == TradeDirection.Buy ? 1 : -1);

					if (_lastTrade.Volume >= _minVolume3 && (_lastTrade.Volume <= _maxVolume3 || _maxVolume3 == 0))
						_delta3 -= _lastTrade.Volume * (_lastTrade.Direction == TradeDirection.Buy ? 1 : -1);

					if (_lastTrade.Volume >= _minVolume4 && (_lastTrade.Volume <= _maxVolume4 || _maxVolume4 == 0))
						_delta4 -= _lastTrade.Volume * (_lastTrade.Direction == TradeDirection.Buy ? 1 : -1);

					if (_lastTrade.Volume >= _minVolume5 && (_lastTrade.Volume <= _maxVolume5 || _maxVolume5 == 0))
						_delta5 -= _lastTrade.Volume * (_lastTrade.Direction == TradeDirection.Buy ? 1 : -1);
				}
			}

			var volume = CumulativeTrades ? trade.Volume : trade.Ticks.Last().Volume;

			if (volume >= _minVolume1 && (volume <= _maxVolume1 || _maxVolume1 == 0))
				_delta1 += volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			_filter1Series[CurrentBar - 1] = _delta1;

			if (volume >= _minVolume2 && (volume <= _maxVolume2 || _maxVolume2 == 0))
				_delta2 += volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			_filter2Series[CurrentBar - 1] = _delta2;

			if (volume >= _minVolume3 && (volume <= _maxVolume3 || _maxVolume3 == 0))
				_delta3 += volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			_filter3Series[CurrentBar - 1] = _delta3;

			if (volume >= _minVolume4 && (volume <= _maxVolume4 || _maxVolume4 == 0))
				_delta4 += volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			_filter4Series[CurrentBar - 1] = _delta4;

			if (volume >= _minVolume5 && (volume <= _maxVolume5 || _maxVolume5 == 0))
				_delta5 += volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			_filter5Series[CurrentBar - 1] = _delta5;

			RaiseBarValueChanged(CurrentBar - 1);
			_lastTrade = trade;
		}

		private void CalculateHistory(List<CumulativeTrade> trades)
		{
			try
			{
				for (var i = _sessionBegin; i <= CurrentBar - 1; i++)
					CalculateBarTrades(trades, i);

				RedrawChart();
			}
			catch (NullReferenceException)
			{
				//on reset exception ignored
			}
		}

		private void CalculateBarTrades(List<CumulativeTrade> trades, int bar, bool realTime = false, bool newBar = false)
		{
			if (newBar)
				CalculateBarTrades(trades, bar - 1, true);

			if (CumulativeTrades && realTime && !newBar)
			{
				_delta1 -= _lastDelta1;
				_delta2 -= _lastDelta2;
				_delta3 -= _lastDelta3;
				_delta4 -= _lastDelta4;
				_delta5 -= _lastDelta5;
			}

			var candle = GetCandle(bar);

			var candleTicks = new List<MarketDataArg>();

			var candleTrades = trades
				.Where(x => x.Time >= candle.Time && x.Time <= candle.LastTime && x.Direction != TradeDirection.Between)
				.ToList();

			if (!CumulativeTrades)
				candleTicks = candleTrades.SelectMany(x => x.Ticks).ToList();

			_lastDelta1 = CumulativeTrades
				? candleTrades
					.Where(x => x.Volume >= _minVolume1 && (x.Volume <= _maxVolume1 || _maxVolume1 == 0))
					.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1))
				: candleTicks
					.Where(x => x.Volume >= _minVolume1 && (x.Volume <= _maxVolume1 || _maxVolume1 == 0))
					.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1));

			_delta1 += _lastDelta1;

			_filter1Series[bar] = _delta1;

			_lastDelta2 = CumulativeTrades
				? candleTrades
					.Where(x => x.Volume >= _minVolume2 && (x.Volume <= _maxVolume2 || _maxVolume2 == 0))
					.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1))
				: candleTicks
					.Where(x => x.Volume >= _minVolume2 && (x.Volume <= _maxVolume2 || _maxVolume2 == 0))
					.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1));

			_delta2 += _lastDelta2;

			_filter2Series[bar] = _delta2;

			_lastDelta3 = CumulativeTrades
				? candleTrades
					.Where(x => x.Volume >= _minVolume3 && (x.Volume <= _maxVolume3 || _maxVolume3 == 0))
					.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1))
				: candleTicks
					.Where(x => x.Volume >= _minVolume3 && (x.Volume <= _maxVolume3 || _maxVolume3 == 0))
					.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1));

			_delta3 += _lastDelta3;

			_filter3Series[bar] = _delta3;

			_lastDelta4 = CumulativeTrades
				? candleTrades
					.Where(x => x.Volume >= _minVolume4 && (x.Volume <= _maxVolume4 || _maxVolume4 == 0))
					.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1))
				: candleTicks
					.Where(x => x.Volume >= _minVolume4 && (x.Volume <= _maxVolume4 || _maxVolume4 == 0))
					.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1));

			_delta4 += _lastDelta4;

			_filter4Series[bar] = _delta4;

			_lastDelta5 = CumulativeTrades
				? candleTrades
					.Where(x => x.Volume >= _minVolume5 && (x.Volume <= _maxVolume5 || _maxVolume5 == 0))
					.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1))
				: candleTicks
					.Where(x => x.Volume >= _minVolume5 && (x.Volume <= _maxVolume5 || _maxVolume5 == 0))
					.Sum(x => x.Volume * (x.Direction == TradeDirection.Buy ? 1 : -1));

			_delta5 += _lastDelta5;

			_filter5Series[bar] = _delta5;

			RaiseBarValueChanged(bar);
		}

		#endregion
	}
}