namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Editors;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Attributes.Editors;
	using OFT.Rendering.Context;
	using OFT.Rendering.Settings;
	using OFT.Rendering.Tools;

	using Utils.Common;

	using Color = System.Drawing.Color;

	[Category("Order Flow")]
	[DisplayName("OI analyzer")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/14115-oi-analyzer")]
	public class OIAnalyzer : Indicator
	{
		#region Nested types

		[Editor(typeof(RangeEditor), typeof(RangeEditor))]
		public class Range : NotifyPropertyChangedBase
		{
			#region Properties

			[Display(ResourceType = typeof(Resources), Name = "Minimum", Order = 20)]
			public int From
			{
				get => _from;
				set => SetProperty(ref _from, value);
			}

			[Display(ResourceType = typeof(Resources), Name = "Maximum", Order = 10)]
			public int To
			{
				get => _to;
				set => SetProperty(ref _to, value);
			}

			#endregion

			#region Private fields

			private int _from;
			private int _to;

			#endregion
		}

		public enum CalcMode
		{
			[Display(ResourceType = typeof(Resources), Name = "CumulativeTrades")]
			CumulativeTrades,

			[Display(ResourceType = typeof(Resources), Name = "SeparatedTrades")]
			SeparatedTrades
		}

		public enum Mode
		{
			[Display(ResourceType = typeof(Resources), Name = "Buys")]
			Buys,

			[Display(ResourceType = typeof(Resources), Name = "Sells")]
			Sells
		}

		#endregion

		#region Static and constants

		private const int _height = 15;

		#endregion

		#region Fields

		private readonly RenderFont _font = new("Arial", 9);

		private readonly RenderStringFormat _stringAxisFormat = new()
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center,
			Trimming = StringTrimming.EllipsisCharacter
		};

		private CalcMode _calcMode = CalcMode.CumulativeTrades;
        private Color _candlesColor;
		private bool _cumulativeMode = true;
		private bool _customDiapason;

		private LineSeries _dn = new("Down")
		{
			Color = Colors.Transparent,
			LineDashStyle = LineDashStyle.Dot,
			Value = -300,
			Width = 1,
			UseScale = false,
			IsHidden = true
		};

		private int _gridStep = 1000;

        private int _lastBar;
		private int _lastCalculatedBar;
		private decimal _lastOi;
		private Mode _mode = Mode.Buys;
        private Candle _prevCandle;
		private decimal _prevLastOi;
		private CumulativeTrade _prevTrade;

		private CandleDataSeries _renderValues = new("Values")
		{
			IsHidden = true,
			ScaleIt = true, 
			DownCandleColor = Colors.Green, 
			BorderColor = Colors.Green, 
			UpCandleColor = Colors.White,
			UseMinimizedModeIfEnabled = true
		};

		private bool _requestFailed;
		private bool _requestWaiting;

		private bool _requireNewRequest;
		private int _sessionBegin;
		private List<CumulativeTrade> _tradeBuffer = new();

		private LineSeries _up = new("Up")
		{
			Color = Colors.Transparent,
			LineDashStyle = LineDashStyle.Dash,
			Value = 300,
			Width = 1,
			UseScale = false,
			IsHidden = true
		};
		
		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Enabled", GroupName = "CustomDiapason", Order = 100)]
		public bool CustomDiapason
		{
			get => _customDiapason;
			set
			{
				_customDiapason = value;
				FilterRange_PropertyChanged(null, null);
			}
		}

		[IsExpanded]
		[Display(ResourceType = typeof(Resources), Name = "Range", GroupName = "CustomDiapason", Order = 105)]
		public Range FilterRange { get; set; } = new()
			{ From = 0, To = 0 };

		[Display(ResourceType = typeof(Resources), Name = "Mode", Order = 130, GroupName = "Calculation")]
		public Mode OiMode
		{
			get => _mode;
			set
			{
				_mode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "CalculationMode", Order = 140, GroupName = "Calculation")]
		public CalcMode CalculationMode
		{
			get => _calcMode;
			set
			{
				_calcMode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "CumulativeMode", Order = 150, GroupName = "Calculation")]
		public bool CumulativeMode
		{
			get => _cumulativeMode;
			set
			{
				_cumulativeMode = value;
				_renderValues.ResetAlertsOnNewBar = !value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ClustersMode", Order = 150, GroupName = "Calculation")]
		public bool ClustersMode
		{
			get => !_renderValues.Visible;
			set
			{
				_renderValues.Visible = !value;
				FilterRange_PropertyChanged(null, null);
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "GridStep", Order = 160, GroupName = "Grid")]
		[Range(1, 1000000)]
		public int GridStep
		{
			get => _gridStep;
			set
			{
				_gridStep = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Line", GroupName = "Grid", Order = 170)]
		public PenSettings Pen { get; set; } = new()
			{ Color = System.Windows.Media.Color.FromArgb(100, 128, 128, 128), Width = 1 };

		[Display(ResourceType = typeof(Resources), Name = "ShowCurrentValue", Order = 170, GroupName = "Visualization")]
		public bool ShowCurrentValue
		{
			get => _renderValues.ShowCurrentValue;
			set => _renderValues.ShowCurrentValue = value;
		}
		
		[Display(ResourceType = typeof(Resources), Name = "BearlishColor", Order = 170, GroupName = "Visualization")]
		public System.Windows.Media.Color DownColor
		{
			get => _renderValues.DownCandleColor;
			set
			{
				_candlesColor = value.Convert();
				_renderValues.DownCandleColor = _renderValues.BorderColor = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "BullishColor", Order = 180, GroupName = "Visualization")]
		public System.Windows.Media.Color UpColor
		{
			get => _renderValues.UpCandleColor;
			set => _renderValues.UpCandleColor = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "Author", GroupName = "Copyright", Order = 200)]
		public string Author => "Sotnikov Denis (sotnik)";

		#endregion

		#region ctor

		public OIAnalyzer()
			: base(true)
		{
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.LatestBar);
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _renderValues;
			LineSeries.Add(_up);
			LineSeries.Add(_dn);

			FilterRange.PropertyChanged += FilterRange_PropertyChanged;
		}

		#endregion

		#region Protected methods
		
		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_requireNewRequest = true;

				UpdateCustomDiapasonRange();
			}

			if (_requireNewRequest && bar == CurrentBar - 1)
			{
				_requireNewRequest = false;
				_renderValues.Clear();
				var totalBars = CurrentBar - 1;
				_sessionBegin = totalBars;

				for (var i = totalBars; i >= 0; i--)
				{
					if (!IsNewSession(i))
						continue;

					_sessionBegin = i;
					break;
				}

				if (!_requestWaiting)
				{
					_requestWaiting = true;

					RequestForCumulativeTrades(new CumulativeTradesRequest(GetCandle(_sessionBegin).Time, GetCandle(CurrentBar - 1).LastTime.AddMinutes(1), 0,
						0));
				}
				else
					_requestFailed = true;
			}

			if (!_requestWaiting && CurrentBar - 1 - _lastBar > 1)
			{
				CalculateHistory(_tradeBuffer
					.Where(x => x.Time >= GetCandle(_lastBar + 1).Time && x.Time <= GetCandle(CurrentBar - 1).LastTime)
					.ToList());
			}
		}

		protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
		{
			if (!_requestFailed)
			{
				var trade = cumulativeTrades
					.OrderBy(x => x.Time)
					.ToList();

				var filterTime = request.EndTime;

				if (cumulativeTrades.Any())
					filterTime = cumulativeTrades.Last().Time;

				trade.AddRange(_tradeBuffer
					.Where(x => x.Time > filterTime)
					.ToList());

				CalculateHistory(trade);
				_requestWaiting = false;
				_tradeBuffer.Clear();
			}
			else
			{
				_requestWaiting = false;
				_requestFailed = false;
				Calculate(0, 0);
				RedrawChart();
			}
		}

		protected override void OnCumulativeTrade(CumulativeTrade trade)
		{
			if (_requestWaiting)
			{
				_tradeBuffer.Add(trade);
				return;
			}

			CalculateTrade(trade, CurrentBar - 1);
		}

		protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
		{
			if (_requestWaiting)
			{
				_tradeBuffer.RemoveAll(trade.IsEqual);
				_tradeBuffer.Add(trade);
				return;
			}

			CalculateTrade(trade, CurrentBar - 1, true);
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if (ClustersMode)
			{
				var firstBar = Math.Max(ChartInfo.PriceChartContainer.FirstVisibleBarNumber, _sessionBegin);
				var lastBar = ChartInfo.PriceChartContainer.LastVisibleBarNumber;

				for (var i = firstBar; i <= lastBar; i++)
				{
					var x = ChartInfo.GetXByBar(i);
					var rect = new Rectangle(x, Container.Region.Y, (int)ChartInfo.PriceChartContainer.BarsWidth, Container.Region.Height);
					var diff = _renderValues[i].Close - _renderValues[i].Open;
					context.DrawString(diff.ToString("+#;-#;0"), _font, _candlesColor, rect, _stringAxisFormat);
				}
			}
			else
				DrawGrid(context);
		}

		#endregion

		#region Private methods

		private void FilterRange_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			UpdateCustomDiapasonRange();

			try
			{
				if (ChartInfo != null)
				{
					for (var i = 0; i <= CurrentBar - 1; i++)
						RaiseBarValueChanged(i);
				}
			}
			catch (Exception)
			{
			}
		}

		private void UpdateCustomDiapasonRange()
		{
			if (CustomDiapason)
			{
				//enabled
				_up.UseScale = _dn.UseScale = true;
				_renderValues.ScaleIt = false;

				_up.Value = FilterRange.To;
				_dn.Value = FilterRange.From;
			}
			else
			{
				//disabled
				_up.UseScale = _dn.UseScale = false;
				_renderValues.ScaleIt = true;
			}
		}

		private void CalculateHistory(List<CumulativeTrade> trades)
		{
			IndicatorCandle lastCandle = null;
			var lastCandleNumber = _sessionBegin - 1;

			foreach (var trade in trades.OrderBy(x => x.Time))
			{
				if (lastCandle == null || lastCandle.LastTime < trade.Time)
				{
					for (var i = lastCandleNumber + 1; i <= CurrentBar - 1; i++)
					{
						lastCandle = GetCandle(i);
						lastCandleNumber = i;

						if (lastCandle.LastTime >= trade.Time)
							break;
					}
				}

				CalculateTrade(trade, lastCandleNumber);
			}

			for (var i = 0; i <= CurrentBar - 1; i++)
				RaiseBarValueChanged(i);

			RedrawChart();
		}

		private void CalculateTrade(CumulativeTrade trade, int bar, bool isUpdated = false)
		{
			var newBar = false;

			if (_lastCalculatedBar != bar)
			{
				_lastBar = _lastCalculatedBar;
				_lastCalculatedBar = bar;
				newBar = true;
			}

			if (isUpdated && _prevTrade != null)
			{
				if (trade.IsEqual(_prevTrade))
					_lastOi = _prevLastOi;
			}
			else
			{
				_prevLastOi = _lastOi;
				_prevTrade = trade;
			}

			var open = 0m;

			if (_cumulativeMode && _lastBar > 0)
			{
				var prevValue = _renderValues[_lastBar];

				if (prevValue.Close != 0)
					open = prevValue.Close;
			}

			var currentValue = _renderValues[bar];

			if (IsEmpty(currentValue))
			{
				_renderValues[bar] = new Candle
				{
					High = open,
					Low = open,
					Open = open,
					Close = open
				};
			}
			else
			{
				if (currentValue.Open == currentValue.Close && currentValue.Open == 0)
				{
					_renderValues[bar] = new Candle
					{
						High = open,
						Low = open,
						Open = open,
						Close = open
					};
				}
			}

			if (isUpdated && trade.IsEqual(_prevTrade) && !newBar)
				_renderValues[bar] = _prevCandle.MemberwiseClone();
			else
				_prevCandle = _renderValues[bar].MemberwiseClone();

			if (_calcMode == CalcMode.CumulativeTrades)
			{
				if (_lastOi != 0)
				{
					var dOi = trade.Ticks.Last().OpenInterest - _lastOi;

					if (dOi != 0)
					{
						if (_mode == Mode.Buys && trade.Direction == TradeDirection.Buy
							||
							_mode == Mode.Sells && trade.Direction == TradeDirection.Sell)
						{
							var value = dOi > 0 ? trade.Volume : -trade.Volume;
							_renderValues[bar].Close += value;

							if (_renderValues[bar].Close > _renderValues[bar].High)
								_renderValues[bar].High = _renderValues[bar].Close;

							if (_renderValues[bar].Close < _renderValues[bar].Low)
								_renderValues[bar].Low = _renderValues[bar].Close;
						}
					}
				}

				if(trade.Ticks.Count != 0)
					_lastOi = trade.Ticks.Last().OpenInterest;
			}
			else
			{
				foreach (var tick in trade.Ticks)
				{
					if (_lastOi != 0)
					{
						var dOi = tick.OpenInterest - _lastOi;

						if (dOi != 0)
						{
							if (_mode == Mode.Buys && tick.Direction == TradeDirection.Buy
								||
								_mode == Mode.Sells && tick.Direction == TradeDirection.Sell)
							{
								var value = dOi > 0 ? tick.Volume : -tick.Volume;
								_renderValues[bar].Close += value;

								if (_renderValues[bar].Close > _renderValues[bar].High)
									_renderValues[bar].High = _renderValues[bar].Close;

								if (_renderValues[bar].Close < _renderValues[bar].Low)
									_renderValues[bar].Low = _renderValues[bar].Close;
							}
						}
					}

					if (trade.Ticks.Count != 0)
                        _lastOi = tick.OpenInterest;
				}
			}

			RaiseBarValueChanged(bar);
		}

		private bool IsEmpty(Candle candle)
		{
			return candle.High == 0 && candle.Low == 0 && candle.Open == 0 && candle.Close == 0;
		}

		private void DrawGrid(RenderContext context)
		{
			var linePen = Pen.RenderObject;

			var max = Container.Maximum - Container.Maximum % GridStep;

			while (max > Container.Minimum)
			{
				var y = Container.GetYByValue(max);

				if (y > Container.RelativeRegion.Y)
					context.DrawLine(linePen, 0, y, Container.Region.Width, y);

				max -= GridStep;
			}
		}

		#endregion
	}
}