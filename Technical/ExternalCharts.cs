namespace ATAS.Indicators.Technical
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Drawing;
    using System.Text.RegularExpressions;

    using ATAS.Indicators.Drawing;

    using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Context;
    using OFT.Rendering.Settings;
    using OFT.Rendering.Tools;

    using Color = System.Drawing.Color;

    [DisplayName("External Chart")]
	[Category(IndicatorCategories.Other)]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ExternalChartsDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602383")]
	public class ExternalCharts : Indicator
	{
		#region Nested types

		public class RectangleInfo
		{
			#region Properties

			public decimal ClosePrice { get; set; }

			public int FirstPos { get; set; }

			public decimal FirstPrice { get; set; }

			public decimal OpenPrice { get; set; }

			public int SecondPos { get; set; }

			public decimal SecondPrice { get; set; }

			#endregion
		}

		public enum TimeFrameScale
		{
            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.M1))]
            M1 = 1,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.M5))]
            M5 = 5,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.M10))]
            M10 = 10,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.M15))]
            M15 = 15,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.M30))]
            M30 = 30,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Hourly))]
			Hourly = 60,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.H2))]
			H2 = 120,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.H4))]
			H4 = 240,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.H6))]
			H6 = 360,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Daily))]
			Daily = 1440,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Weekly))]
			Weekly = 10080,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Monthly))]
			Monthly = 0
		}

		#endregion

		#region Fields

		private readonly object _locker = new();
		private readonly List<RectangleInfo> _rectangles = new();
		private int _days = 20;
		
		private bool _isFixedTimeFrame;
		private bool _isLastRect;
		private int _lastBar = -1;
		private int _secondsPerCandle;
		private int _secondsPerTframe;
		private int _targetBar;
		private TimeFrameScale _tFrame = TimeFrameScale.Hourly;
		private int _width = 1;

        private Color _upColor = Color.RoyalBlue;
		private Color _downColor = DefaultColors.Red;
        private DateTime _firstCandleTime;

        #endregion

        #region Properties

        //Old property
        [Browsable(false)]
        public CrossColor AreaColor { get; set; }

		[Range(0, int.MaxValue)]
        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue, Description = nameof(Strings.DaysLookBackDescription))]
        public int Days
		{
			get => _days;
			set
			{
				_days = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowGrid), GroupName = nameof(Strings.Grid), Description = nameof(Strings.DisplayClusterBorderDescription), Order = 7)]
		public bool ShowGrid { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Grid), Description = nameof(Strings.GridColorDescription), Order = 8)]
		public CrossColor GridColor { get; set; } = CrossColor.FromArgb(50, 128, 128, 128);

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowAsCandle), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.ShowAsCandleDescription), Order = 9)]
		public bool ExtCandleMode { get; set; }
		
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BullishColorDescription), Order = 30)]
		public CrossColor UpCandleColor
		{
			get => _upColor.Convert();
			set => _upColor = value.Convert();
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearlishColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BearishColorDescription), Order = 40)]
		public CrossColor DownCandleColor
		{
			get => _downColor.Convert();
			set => _downColor = value.Convert();
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackgroundBullish), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BullishFillColorDescription), Order = 45)]
		public Color UpBackground { get; set; } = Color.FromArgb(100, Color.LightSkyBlue);

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackgroundBearlish), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BearishFillColorDescription), Order = 47)]
		public Color DownBackground { get; set; } = Color.FromArgb(100, Color.DarkRed);

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BorderWidthPixelDescription), Order = 50)]
		[Range(1, 100)]
		public int Width
		{
			get => _width;
			set => _width = value;
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.DashStyle), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BorderStyleDescription), Order = 60)]
		public LineDashStyle Style { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FillCandles), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.IsNeedFillDescription), Order = 65)]
		public bool FillCandles { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowAboveChart), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.DrawAbovePriceDescription), Order = 70)]
		public bool Above
		{
			get => DrawAbovePrice;
			set
			{
				DrawAbovePrice = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ExternalPeriod), GroupName = nameof(Strings.TimeFrame), Description = nameof(Strings.SelectTimeframeDescription), Order = 5)]
		public TimeFrameScale TFrame
		{
			get => _tFrame;
			set
			{
				_tFrame = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ExternalCharts()
			: base(true)
		{
			DrawAbovePrice = true;
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.LatestBar | DrawingLayouts.Historical);
			
			DataSeries[0].IsHidden = true;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			lock (_locker)
			{
				var candle = GetCandle(bar);

				if (bar == 0)
				{
					_isLastRect = false;
					_isFixedTimeFrame = false;
					_rectangles.Clear();
					GetCandleSeconds();
					_secondsPerTframe = 60 * (int)TFrame;
					_targetBar = 0;

					if (_days > 0)
					{
						var days = 0;

						for (var i = CurrentBar - 1; i >= 0; i--)
						{
							_targetBar = i;

							if (!IsNewSession(i))
								continue;

							days++;

							if (days == _days)
								break;
						}
					}
				}

				if (bar < _targetBar)
					return;

				if (bar == _targetBar)
				{
					_firstCandleTime = candle.Time;

                    _rectangles.Add(new RectangleInfo
					{
						FirstPos = bar,
						SecondPos = bar,
						FirstPrice = candle.Low,
						SecondPrice = candle.High,
						OpenPrice = candle.Open,
						ClosePrice = candle.Close
					});

					return;
				}

				var tim = GetBeginTime(candle.Time, TFrame);

				if (_rectangles.Count == 0)
				{
				}

				var isNewBar = false;
				var isCustomPeriod = false;
				var lastBar = _rectangles[^1].SecondPos;

				if (TFrame == TimeFrameScale.Weekly)
				{
					isCustomPeriod = true;
					isNewBar = IsNewWeek(bar);
				}
				else if (TFrame == TimeFrameScale.Monthly)
				{
					isCustomPeriod = true;
					isNewBar = IsNewMonth(bar);
				}
				else if (TFrame == TimeFrameScale.Daily)
				{
					isCustomPeriod = true;
					isNewBar = IsNewSession(bar);
				}

				if (isNewBar || !isCustomPeriod &&
				    (tim >= GetCandle(lastBar).LastTime || !_isFixedTimeFrame && tim >= GetCandle(Math.Max(lastBar - 1, 0)).LastTime))
				{
					_rectangles[^1].SecondPos = bar - 1;

					_rectangles.Add(new RectangleInfo
					{
						FirstPos = bar,
						SecondPos = bar, 
						FirstPrice = candle.Low,
						SecondPrice = candle.High,
						OpenPrice = candle.Open,
						ClosePrice = candle.Close
					});
				}

				if (candle.Low < _rectangles[^1].FirstPrice)
					_rectangles[^1].FirstPrice = candle.Low;

				if (candle.High > _rectangles[^1].SecondPrice)
					_rectangles[^1].SecondPrice = candle.High;

				_rectangles[^1].SecondPos = bar;
				_rectangles[^1].ClosePrice = candle.Close;

				if (_lastBar == bar)
					_isLastRect = true;

				_lastBar = bar;
			}
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if (ChartInfo.PriceChartContainer.TotalBars == ChartInfo.PriceChartContainer.LastVisibleBarNumber)
			{
				if (layout != DrawingLayouts.LatestBar)
					return;
			}
			else
			{
				if (layout != DrawingLayouts.Historical)
					return;
			}

			lock (_locker)
			{
				var gridPen = new RenderPen(GridColor.Convert());

				foreach (var rect in _rectangles)
				{
					var chartType = ChartInfo.ChartVisualMode;
					var useShift = chartType is ChartVisualModes.Clusters or ChartVisualModes.Line;

					var x1 = ChartInfo.GetXByBar(rect.FirstPos);
					var x2 = ChartInfo.GetXByBar(rect.SecondPos + 1);

					if (Math.Max(x1, x2) < 0)
						continue;
					
					var yBot = ChartInfo.GetYByPrice(rect.FirstPrice - (useShift ? InstrumentInfo.TickSize : 0), useShift);
					var yTop = ChartInfo.GetYByPrice(rect.SecondPrice, useShift);

					if (_isFixedTimeFrame && CurrentBar - 1 == _lastBar && rect.SecondPos == _lastBar && _isLastRect)
					{
						var barWidth = ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0);

						if (_rectangles.Count > 1)
						{
							var lastRect = _rectangles[_rectangles.Count - 2];
							var rectWidth = ChartInfo.GetXByBar(lastRect.SecondPos + 1) - ChartInfo.GetXByBar(lastRect.FirstPos);
							x2 = x1 + rectWidth;
						}
						else
							x2 = x1 + barWidth * (_secondsPerTframe / _secondsPerCandle);
					}

					if (ShowGrid && chartType == ChartVisualModes.Clusters)
					{
						for (var i = rect.FirstPrice; i < rect.SecondPrice; i += InstrumentInfo.TickSize)
						{
							var y = ChartInfo.GetYByPrice(i);
							context.DrawLine(gridPen, x1, y, x2, y);
						}

						for (var i = rect.FirstPos; i <= rect.SecondPos; i++)
						{
							var x = ChartInfo.GetXByBar(i);
							context.DrawLine(gridPen, x, yBot, x, yTop);
						}
					}

					var bearish = rect.OpenPrice > rect.ClosePrice;

					var penColor = bearish
                        ? _downColor
						: _upColor;

					var renderPen = new RenderPen(penColor, Width, Style.To());

					var renderRectangle = new Rectangle(x1, yTop, x2 - x1, yBot - yTop);
					
					if (ExtCandleMode)
					{
						var max = Math.Max(yTop, yBot);
						var min = Math.Min(yTop, yBot);
						var y1 = ChartInfo.GetYByPrice(Math.Min(rect.OpenPrice, rect.ClosePrice), false);
						var y2 = ChartInfo.GetYByPrice(Math.Max(rect.OpenPrice, rect.ClosePrice), false);
						renderRectangle = new Rectangle(x1, y2, x2 - x1, y1 - y2);
						context.DrawLine(renderPen, (x2 + x1) / 2, y2, (x2 + x1) / 2, min);
						context.DrawLine(renderPen, (x2 + x1) / 2, y1, (x2 + x1) / 2, max);
					}

					if (FillCandles)
						context.FillRectangle(bearish ? DownBackground : UpBackground, renderRectangle);
					
					context.DrawRectangle(renderPen, renderRectangle);
				}
			}
		}

		#endregion

		#region Private methods

		private DateTime GetBeginTime(DateTime time, TimeFrameScale period)
		{
			if (period == TimeFrameScale.Monthly)
				return new DateTime(time.Year, time.Month, 1);

			var tim = time;
			tim = tim.AddMilliseconds(-tim.Millisecond);
			tim = tim.AddSeconds(-tim.Second);

			var begin = (tim - _firstCandleTime).TotalMinutes % (int)period;
			var res = tim.AddMinutes(-begin);
			return res;
		}

		private void GetCandleSeconds()
		{
			var timeFrame = ChartInfo.TimeFrame;

			if (ChartInfo.ChartType == "Seconds")
			{
				_isFixedTimeFrame = true;

				_secondsPerCandle = ChartInfo.TimeFrame switch
				{
					"5" => 5,
					"10" => 10,
					"15" => 15,
					"30" => 30,
					_ => 0
				};

				if (_secondsPerCandle == 0)
				{
					if (int.TryParse(Regex.Match(timeFrame, @"\d{1,}$").Value, out var periodSec))
					{
						_secondsPerCandle = periodSec;
						return;
					}
				}
			}

			if (ChartInfo.ChartType != "TimeFrame")
				return;

			_isFixedTimeFrame = true;

			_secondsPerCandle = ChartInfo.TimeFrame switch
			{
				"M1" => 60 * (int)TimeFrameScale.M1,
				"M5" => 60 * (int)TimeFrameScale.M5,
				"M10" => 60 * (int)TimeFrameScale.M10,
				"M15" => 60 * (int)TimeFrameScale.M15,
				"M30" => 60 * (int)TimeFrameScale.M30,
				"Hourly" => 60 * (int)TimeFrameScale.Hourly,
				"H2" => 60 * (int)TimeFrameScale.H2,
				"H4" => 60 * (int)TimeFrameScale.H4,
				"H6" => 60 * (int)TimeFrameScale.H6,
				"Daily" => 60 * (int)TimeFrameScale.Daily,
				"Weekly" => 60 * (int)TimeFrameScale.Weekly,
				_ => 0
			};

			if (_secondsPerCandle != 0)
				return;

			if (!int.TryParse(Regex.Match(timeFrame, @"\d{1,}$").Value, out var period))
				return;

			if (timeFrame.Contains("M"))
			{
				_secondsPerCandle = 60 * (int)TimeFrameScale.M1 * period;
				return;
			}

			if (timeFrame.Contains("H"))
			{
				_secondsPerCandle = 60 * (int)TimeFrameScale.Daily * period;
				return;
			}

			if (timeFrame.Contains("D"))
				_secondsPerCandle = 60 * (int)TimeFrameScale.Daily * period;
		}

		#endregion
	}
}