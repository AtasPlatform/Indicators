namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Linq;

	using ATAS.Indicators.Technical.Editors;

	using OFT.Attributes;
	using OFT.Attributes.Editors;
    using OFT.Localization;
    using OFT.Rendering.Context;
	using OFT.Rendering.Settings;
	using OFT.Rendering.Tools;

	using Utils.Common;

	using Color = System.Drawing.Color;

#if CROSS_PLATFORM
    using CrossColor = System.Drawing.Color;
#else
    using CrossColor = System.Windows.Media.Color;
#endif

    [Category("Order Flow")]
	[DisplayName("OI Analyzer")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.OIAnalyzerDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602437")]
	public class OIAnalyzer : Indicator
	{
        #region Nested types

#if !CROSS_PLATFROM
        [Editor(typeof(RangeEditor), typeof(RangeEditor))]
#endif
        public class Range : NotifyPropertyChangedBase
		{
			#region Properties

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Minimum), Order = 20)]
			public int From
			{
				get => _from;
				set => SetProperty(ref _from, value);
			}

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Maximum), Order = 10)]
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
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CumulativeTrades))]
			CumulativeTrades,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SeparatedTrades))]
			SeparatedTrades
		}

		public enum Mode
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Buys))]
			Buys,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Sells))]
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

		private LineSeries _dn = new("Dn", "Down")
		{
			Color = Color.Transparent.Convert(),
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

		private CandleDataSeries _renderValues = new("RenderValues", "Values")
		{
			IsHidden = true,
			ScaleIt = true,
			DownCandleColor = Color.Green.Convert(),
			BorderColor = Color.Green.Convert(),
			UpCandleColor = Color.White.Convert(),
			ValuesColor = Color.LightBlue,
			UseMinimizedModeIfEnabled = true
		};

		private bool _requestFailed;
		private bool _requestWaiting;

		private bool _requireNewRequest;
		private int _sessionBegin;
		private List<CumulativeTrade> _tradeBuffer = new();

		private LineSeries _up = new("UpId", "Up")
		{
			Color = Color.Transparent.Convert(),
			LineDashStyle = LineDashStyle.Dash,
			Value = 300,
			Width = 1,
			UseScale = false,
			IsHidden = true
		};
		
		#endregion

		#region Properties

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.CustomDiapason), Description = nameof(Strings.UseCustomDiapasonDescription), Order = 100)]
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
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Range), GroupName = nameof(Strings.CustomDiapason), Description = nameof(Strings.ValuesRangeDescription), Order = 105)]
		public Range FilterRange { get; set; } = new(){ From = 0, To = 0 };

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Mode), Order = 130, GroupName = nameof(Strings.Calculation), Description = nameof(Strings.BuySellModeDescription))]
		public Mode OiMode
		{
			get => _mode;
			set
			{
				_mode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CalculationMode), Order = 140, GroupName = nameof(Strings.Calculation), Description = nameof(Strings.CalculationModeDescription))]
		public CalcMode CalculationMode
		{
			get => _calcMode;
			set
			{
				_calcMode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CumulativeMode), Order = 150, GroupName = nameof(Strings.Calculation), Description = nameof(Strings.CumulativeTradesModeDescription))]
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

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ClustersMode), Order = 150, GroupName = nameof(Strings.Calculation), Description = nameof(Strings.ClustersModeDescription))]
		public bool ClustersMode
		{
			get => !_renderValues.Visible;
			set
			{
				_renderValues.Visible = !value;
				FilterRange_PropertyChanged(null, null);
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.GridStep), Order = 160, GroupName = nameof(Strings.Grid), Description = nameof(Strings.GridRowHeihgtDescription))]
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

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Line), GroupName = nameof(Strings.Grid), Order = 170, Description = nameof(Strings.GridLineSettingsDescription))]
		public PenSettings Pen { get; set; } = new()
			{ Color = CrossColor.FromArgb(100, 128, 128, 128), Width = 1 };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowCurrentValue), Order = 170, GroupName = nameof(Strings.Visualization), Description = nameof(Strings.ShowCurrentValueDescription))]
		public bool ShowCurrentValue
		{
			get => _renderValues.ShowCurrentValue;
			set => _renderValues.ShowCurrentValue = value;
		}
		
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearlishColor), Order = 170, GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BearishColorDescription))]
		public CrossColor DownColor
		{
			get => _renderValues.DownCandleColor;
			set
			{
				_candlesColor = value.Convert();
				_renderValues.DownCandleColor = _renderValues.BorderColor = value;
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), Order = 180, GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BullishColorDescription))]
		public CrossColor UpColor
		{
			get => _renderValues.UpCandleColor;
			set => _renderValues.UpCandleColor = value;
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontColor), Order = 190, GroupName = nameof(Strings.Visualization), Description = nameof(Strings.AxisTextColorDescription))]
		public Color FontColor
        {
			get => _renderValues.ValuesColor;
			set => _renderValues.ValuesColor = value;
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Author), GroupName = nameof(Strings.Copyright), Order = 200, Description = nameof(Strings.IndicatorAuthorDescription))]
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