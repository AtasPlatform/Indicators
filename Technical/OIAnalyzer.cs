namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Linq;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Editors;

	using OFT.Attributes;
	using OFT.Attributes.Editors;
    using OFT.Localization;
    using OFT.Rendering.Context;
	using OFT.Rendering.Settings;
	using OFT.Rendering.Tools;

	using Utils.Common;

	using Color = System.Drawing.Color;

    [Category(IndicatorCategories.VolumeOrderFlow)]
	[DisplayName("OI Analyzer")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.OIAnalyzerDescription))]
    [HelpLink("https://help.atas.net/support/solutions/articles/72000602437")]
	public class OIAnalyzer : Indicator
	{
        #region Nested types

        [Editor(typeof(RangeEditor), typeof(RangeEditor))]
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
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CumulativeTrades), Description = nameof(Strings.OIAnalyzerCumulativeTradesModeDescription))]
			CumulativeTrades,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SeparatedTrades), Description = nameof(Strings.OIAnalyzerSeparatedTradesModeDescription))]
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

		private const int _minGridLineSpacing = 5;

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
			ShowCurrentValue = false,
			ScaleIt = true,
			DownCandleColor = DefaultColors.Green.Convert(),
			BorderColor = DefaultColors.Gray.Convert(),
			UpCandleColor = DefaultColors.White.Convert(),
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
				_renderValues.DownCandleColor = value;
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), Order = 180, GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BullishColorDescription))]
		public CrossColor UpColor
		{
			get => _renderValues.UpCandleColor;
			set => _renderValues.UpCandleColor = value;
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BorderColor), Order = 180, GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BullishColorDescription))]
		public CrossColor BorderColor
		{
			get => _renderValues.BorderColor;
			set => _renderValues.BorderColor = value;
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), Order = 190, GroupName = nameof(Strings.AxisTextColor),
			Description = nameof(Strings.AxisTextColorDescription))]
		public Color FontColor { get; set; } = Color.Black;

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearishColor), Order = 200, GroupName = nameof(Strings.AxisTextColor),
			Description = nameof(Strings.AxisTextColorDescription))]
		public Color BearishFontColor { get; set; } = Color.White;

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Author), GroupName = nameof(Strings.Copyright), Order = 200, Description = nameof(Strings.IndicatorAuthorDescription))]
		public string Author => "Sotnikov Denis (sotnik)";

		#endregion

		#region ctor

		public OIAnalyzer()
			: base(true)
		{
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.LatestBar | DrawingLayouts.Historical);
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderValues;
			LineSeries.Add(_up);
			LineSeries.Add(_dn);

			FilterRange.PropertyChanged += FilterRange_PropertyChanged;
		}

		#endregion

		#region Protected methods

		protected override void OnInitialize()
		{
			_renderValues.ShowCurrentValue = false;
		}

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

		protected override void OnRender(RenderContext g, DrawingLayouts layout)
		{
			if (ClustersMode)
			{
				var lastBar = ChartInfo.PriceChartContainer.LastVisibleBarNumber;
                var firstBar = Math.Max(ChartInfo.PriceChartContainer.FirstVisibleBarNumber, _sessionBegin);
				
				for (var i = firstBar; i <= lastBar; i++)
				{
					var x = ChartInfo.GetXByBar(i);
					var rect = new Rectangle(x, Container.Region.Y, (int)ChartInfo.PriceChartContainer.BarsWidth, Container.Region.Height);
					var diff = _renderValues[i].Close - _renderValues[i].Open;
					g.DrawString(diff.ToString("+#;-#;0"), _font, _candlesColor, rect, _stringAxisFormat);
				}
			}
			else
			{
				if (layout is DrawingLayouts.Historical)
                    DrawGrid(g);

				DrawAxisValue(g);
            }
		}

		private void DrawAxisValue(RenderContext g)
		{
			var bounds = g.ClipBounds;

			try
			{
				var lastBar = ChartInfo.PriceChartContainer.LastVisibleBarNumber;

                var candle = _renderValues[lastBar];
				var closeValue = candle.Close;

				var x = ChartInfo.PriceChartContainer.Region.Right;
				var y = Container.GetYByValue(closeValue);

				var font = ChartInfo.PriceAxisFont;
				var priceString = ChartInfo.TryGetMinimizedVolumeString(closeValue);
				var size = g.MeasureString(priceString, font);

				var priceHeight = size.Height / 2;

				var leftX = x + priceHeight;
				var rightX = ChartInfo.ChartContainer.Region.Right;
				var upperY = y - priceHeight;
				var lowerY = y + priceHeight;

				var points = new Point[]
				{
					new(x, y),
					new(leftX, upperY),
					new(rightX, upperY),
					new(rightX, lowerY),
					new(leftX, lowerY)
				};

				var isBullish = candle.Close > candle.Open;

				var bgColor = isBullish
					? _renderValues.UpCandleColor
					: _renderValues.DownCandleColor;

				var axis = Container.Region with
				{
					X = x,
					Width = rightX - x
				};

				g.SetClip(axis);

                g.FillPolygon(bgColor.Convert(), points);

				var textRect = new Rectangle(leftX, upperY, rightX - leftX, lowerY - upperY);

				var textColor = isBullish
					? FontColor
					: BearishFontColor;

				g.DrawString(priceString, font, textColor, textRect);
			}
			finally
			{
				g.SetClip(bounds);
			}
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
			if (GridStep is 0)
				return;

			var minimum = Container.Minimum;
			var maximum = Container.Maximum;
			var levelsCnt = (int)((maximum - minimum) / GridStep);

			if (Container.Region.Height < levelsCnt * _minGridLineSpacing)
				return;

			var linePen = Pen.RenderObject;
			var x1 = Container.Region.X;
			var x2 = Container.Region.Right;

			for (var level = maximum - maximum % GridStep; level > minimum; level -= GridStep)
			{
				var y = Container.GetYByValue(level);
				context.DrawLine(linePen, x1, y, x2, y);
			}
		}

		#endregion
	}
}