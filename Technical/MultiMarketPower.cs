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
	[DisplayName("Multi Market Powers")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/371-multi-market-powers")]
	public class MultiMarketPower : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _filter1Series = new ValueDataSeries("Filter1");
		private readonly ValueDataSeries _filter2Series = new ValueDataSeries("Filter2");
		private readonly ValueDataSeries _filter3Series = new ValueDataSeries("Filter3");
		private readonly ValueDataSeries _filter4Series = new ValueDataSeries("Filter4");
		private readonly ValueDataSeries _filter5Series = new ValueDataSeries("Filter5");

		private readonly object _locker = new object();
		private readonly List<CumulativeTrade> _trades = new List<CumulativeTrade>();
		private bool _bigTradesIsReceived;

		private decimal _delta1;
		private decimal _delta2;
		private decimal _delta3;
		private decimal _delta4;
		private decimal _delta5;
		private decimal _lastDelta1;
		private decimal _lastDelta2;
		private decimal _lastDelta3;
		private decimal _lastDelta4;
		private decimal _lastDelta5;
		private int _maxVolume1;
		private int _maxVolume2;
		private int _maxVolume3;
		private int _maxVolume4;
		private int _maxVolume5;
		private int _minVolume1;
		private int _minVolume2;
		private int _minVolume3;
		private int _minVolume4;
		private int _minVolume5;
		private bool _useFilter1;
		private bool _useFilter2;
		private bool _useFilter3;
		private bool _useFilter4;
		private bool _useFilter5;

		#endregion

		#region Properties

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
		public int MinVolume1
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
		public int MaxVolume1
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
		public int MinVolume2
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
		public int MaxVolume2
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
		public int MinVolume3
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
		public int MaxVolume3
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
		public int MinVolume4
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
		public int MaxVolume4
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
		public int MinVolume5
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
		public int MaxVolume5
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

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				_delta1 = _delta2 = _delta3 = _delta4 = _delta5 = 0;

				var totalBars = ChartInfo.PriceChartContainer.TotalBars;
				var sessionBegin = totalBars;

				for (var i = totalBars; i >= 0; i--)
				{
					if (!IsNewSession(i))
						continue;

					sessionBegin = i;
					break;
				}

				RequestForCumulativeTrades(new CumulativeTradesRequest(GetCandle(sessionBegin).Time));
			}
		}

		protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
		{
			CalculateHistory(cumulativeTrades.OrderBy(x => x.Time).ToList());
		}

		protected override void OnCumulativeTrade(CumulativeTrade trade)
		{
			lock (_locker)
			{
				if (!_bigTradesIsReceived)
					return;

				CalculateBigTrade(trade, true, ChartInfo.PriceChartContainer.TotalBars);
			}
		}

		protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
		{
			CalculateBigTrade(trade, true, ChartInfo.PriceChartContainer.TotalBars, true);
		}

		#endregion

		#region Private methods

		private void CalculateHistory(List<CumulativeTrade> trades)
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
						CalculateBigTrade(trade, true, curCandle);
						break;
					}
				}

				_bigTradesIsReceived = true;
			}
		}

		private void CalculateBigTrade(CumulativeTrade trade, bool needToAdd, int bar, bool updatingTrade = false)
		{
			var isNewBt = false;

			lock (_trades)
			{
				if (updatingTrade && _trades.Count != 0 && _trades.Last().IsEqual(trade))
				{
					_trades[_trades.Count - 1] = trade;

					_delta1 = _lastDelta1;
					_delta2 = _lastDelta2;
					_delta3 = _lastDelta3;
					_delta4 = _lastDelta4;
					_delta5 = _lastDelta5;
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
				_lastDelta1 = _delta1;
				_lastDelta2 = _delta2;
				_lastDelta3 = _delta3;
				_lastDelta4 = _delta4;
				_lastDelta5 = _delta5;
			}

			if (trade.Direction != TradeDirection.Between && trade.Volume >= _minVolume1 && (trade.Volume <= _maxVolume1 || _maxVolume1 == 0))
				_delta1 += trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			if (trade.Direction != TradeDirection.Between && trade.Volume >= _minVolume2 && (trade.Volume <= _maxVolume2 || _maxVolume2 == 0))
				_delta2 += trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			if (trade.Direction != TradeDirection.Between && trade.Volume >= _minVolume3 && (trade.Volume <= _maxVolume3 || _maxVolume3 == 0))
				_delta3 += trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			if (trade.Direction != TradeDirection.Between && trade.Volume >= _minVolume4 && (trade.Volume <= _maxVolume4 || _maxVolume4 == 0))
				_delta4 += trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			if (trade.Direction != TradeDirection.Between && trade.Volume >= _minVolume5 && (trade.Volume <= _maxVolume5 || _maxVolume5 == 0))
				_delta5 += trade.Volume * (trade.Direction == TradeDirection.Buy ? 1 : -1);

			_filter1Series[bar] = _delta1;
			_filter2Series[bar] = _delta2;
			_filter3Series[bar] = _delta3;
			_filter4Series[bar] = _delta4;
			_filter5Series[bar] = _delta5;
		}

		#endregion
	}
}