namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Globalization;
	using System.Reflection;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;
	using OFT.Rendering.Settings;
	using OFT.Rendering.Tools;

	using Utils.Common.Logging;

	using Color = System.Drawing.Color;

	[DisplayName("Daily Lines")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/17029-daily-lines")]
	public class DailyLines : Indicator
	{
		#region Nested types

		[Serializable]
		[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
		public enum PeriodType
		{
			[Display(ResourceType = typeof(Resources), Name = "CurrentDay")]
			CurrentDay,

			[Display(ResourceType = typeof(Resources), Name = "PreviousDay")]
			PreviousDay,

			[Display(ResourceType = typeof(Resources), Name = "CurrentWeek")]
			CurrenWeek,

			[Display(ResourceType = typeof(Resources), Name = "PreviousWeek")]
			PreviousWeek,

			[Display(ResourceType = typeof(Resources), Name = "CurrentMonth")]
			CurrentMonth,

			[Display(ResourceType = typeof(Resources), Name = "PreviousMonth")]
			PreviousMonth
		}

		#endregion

		#region Fields

		private readonly RenderFont _font = new ("Arial", 8);

		private decimal _close;

		private int _closeBar;
		private DynamicLevels.DynamicCandle _currentCandle = new ();
		private decimal _currentClose;
		private decimal _currentHigh;
		private decimal _currentLow;

		private decimal _currentOpen;
		private int _days;
		private bool _drawFromBar;
		private decimal _high;
		private int _highBar;
		private int _lastNewSessionBar;
		private decimal _low;
		private int _lowBar;
		private decimal _open;
		private int _openBar;
		private PeriodType _per = PeriodType.PreviousDay;
		private int _prevCloseBar;
		private int _prevHighBar;
		private DynamicLevels.DynamicCandle _previousCandle = new ();
		private int _prevLowBar;
		private int _prevOpenBar;
		private bool _showTest = true;
		private int _targetBar;
		private bool _tickBasedCalculation;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Days", GroupName = "Filters", Order = 100)]
		public int Days
		{
			get => _days;
			set
			{
				if (value < 0)
					return;

				_days = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Filters", Order = 110)]
		public PeriodType Period
		{
			get => _per;
			set
			{
				_per = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Text", GroupName = "Show", Order = 200)]
		public bool ShowText
		{
			get => _showTest;
			set
			{
				_showTest = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "PriceLocation", GroupName = "Show", Order = 210)]
		public bool ShowPrice { get; set; } = true;

		[Display(ResourceType = typeof(Resources), Name = "FirstBar", GroupName = "Drawing", Order = 300)]
		public bool DrawFromBar
		{
			get => _drawFromBar;
			set
			{
				_drawFromBar = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Open", GroupName = "Line", Order = 310)]
		public PenSettings OpenPen { get; set; } = new () { Color = Colors.Red, Width = 2 };

		[Display(ResourceType = typeof(Resources), Name = "Close", GroupName = "Line", Order = 320)]
		public PenSettings ClosePen { get; set; } = new () { Color = Colors.Red, Width = 2 };

		[Display(ResourceType = typeof(Resources), Name = "High", GroupName = "Line", Order = 330)]
		public PenSettings HighPen { get; set; } = new () { Color = Colors.Red, Width = 2 };

		[Display(ResourceType = typeof(Resources), Name = "Low", GroupName = "Line", Order = 340)]
		public PenSettings LowPen { get; set; } = new () { Color = Colors.Red, Width = 2 };

		#endregion

		#region ctor

		public DailyLines()
			: base(true)
		{
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);

			DataSeries[0].IsHidden = true;
			_days = 20;
			((ValueDataSeries)DataSeries[0]).ScaleIt = false;
			((ValueDataSeries)DataSeries[0]).ShowZeroValue = false;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		}

		#endregion

		#region Public methods

		public override string ToString()
		{
			return "Daily Lines";
		}

		#endregion

		#region Protected methods

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if (ChartInfo is null)
				return;

			string periodStr;

			switch (Period)
			{
				case PeriodType.CurrentDay:
				{
					periodStr = "Curr. Day ";
					break;
				}
				case PeriodType.PreviousDay:
				{
					periodStr = "Prev. Day ";
					break;
				}
				case PeriodType.CurrenWeek:
				{
					periodStr = "Curr. Week ";
					break;
				}
				case PeriodType.PreviousWeek:
				{
					periodStr = "Prev. Week ";
					break;
				}
				case PeriodType.CurrentMonth:
				{
					periodStr = "Curr. Month ";
					break;
				}
				case PeriodType.PreviousMonth:
				{
					periodStr = "Prev. Month ";
					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}

			if (DrawFromBar)
			{
				var isLastPeriod = Period is PeriodType.CurrentDay or PeriodType.CurrentMonth or PeriodType.CurrenWeek;

				var openBar = isLastPeriod
					? _openBar
					: _prevOpenBar;

				var closeBar = isLastPeriod
					? _closeBar
					: _prevCloseBar;

				var highBar = isLastPeriod
					? _highBar
					: _prevHighBar;

				var lowBar = isLastPeriod
					? _lowBar
					: _prevLowBar;

				if (openBar >= 0 && openBar <= LastVisibleBarNumber)
				{
					var x = ChartInfo.PriceChartContainer.GetXByBar(openBar, false);
					var y = ChartInfo.PriceChartContainer.GetYByPrice(_open, false);
					context.DrawLine(OpenPen.RenderObject, x, y, Container.Region.Right, y);
					DrawString(context, periodStr + "Open", y, OpenPen.RenderObject.Color);
				}

				if (closeBar >= 0 && closeBar <= LastVisibleBarNumber)
				{
					var x = ChartInfo.PriceChartContainer.GetXByBar(closeBar, false);
					var y = ChartInfo.PriceChartContainer.GetYByPrice(_close, false);
					context.DrawLine(ClosePen.RenderObject, x, y, Container.Region.Right, y);
					DrawString(context, periodStr + "Close", y, ClosePen.RenderObject.Color);
				}

				if (highBar >= 0 && highBar <= LastVisibleBarNumber)
				{
					var x = ChartInfo.PriceChartContainer.GetXByBar(highBar, false);
					var y = ChartInfo.PriceChartContainer.GetYByPrice(_high, false);
					context.DrawLine(HighPen.RenderObject, x, y, Container.Region.Right, y);
					DrawString(context, periodStr + "High", y, HighPen.RenderObject.Color);
				}

				if (lowBar >= 0 && lowBar <= LastVisibleBarNumber)
				{
					var x = ChartInfo.PriceChartContainer.GetXByBar(lowBar, false);
					var y = ChartInfo.PriceChartContainer.GetYByPrice(_low, false);
					context.DrawLine(LowPen.RenderObject, x, y, Container.Region.Right, y);
					DrawString(context, periodStr + "Low", y, LowPen.RenderObject.Color);
				}
			}
			else
			{
				var yOpen = ChartInfo.PriceChartContainer.GetYByPrice(_open, false);
				context.DrawLine(OpenPen.RenderObject, Container.Region.Left, yOpen, Container.Region.Right, yOpen);
				DrawString(context, periodStr + "Open", yOpen, OpenPen.RenderObject.Color);

				var yClose = ChartInfo.PriceChartContainer.GetYByPrice(_close, false);
				context.DrawLine(ClosePen.RenderObject, Container.Region.Left, yClose, Container.Region.Right, yClose);
				DrawString(context, periodStr + "Close", yClose, ClosePen.RenderObject.Color);

				var yHigh = ChartInfo.PriceChartContainer.GetYByPrice(_high, false);
				context.DrawLine(HighPen.RenderObject, Container.Region.Left, yHigh, Container.Region.Right, yHigh);
				DrawString(context, periodStr + "High", yHigh, HighPen.RenderObject.Color);

				var yLow = ChartInfo.PriceChartContainer.GetYByPrice(_low, false);
				context.DrawLine(LowPen.RenderObject, Container.Region.Left, yLow, Container.Region.Right, yLow);
				DrawString(context, periodStr + "Low", yLow, HighPen.RenderObject.Color);
			}

			if (!ShowPrice)
				return;

			var bounds = context.ClipBounds;
			context.ResetClip();
			context.SetTextRenderingHint(RenderTextRenderingHint.Aliased);

			if (_openBar >= 0 && _openBar <= LastVisibleBarNumber || !DrawFromBar)
				DrawPrice(context, _open, OpenPen.RenderObject);

			if (_closeBar >= 0 && _closeBar <= LastVisibleBarNumber || !DrawFromBar)
				DrawPrice(context, _close, ClosePen.RenderObject);

			if (_highBar >= 0 && _highBar <= LastVisibleBarNumber || !DrawFromBar)
				DrawPrice(context, _high, HighPen.RenderObject);

			if (_lowBar >= 0 && _lowBar <= LastVisibleBarNumber || !DrawFromBar)
				DrawPrice(context, _low, LowPen.RenderObject);

			context.SetTextRenderingHint(RenderTextRenderingHint.AntiAlias);
			context.SetClip(bounds);
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			try
			{
				if (bar == 0)
				{
					_tickBasedCalculation = false;
					_currentCandle = new DynamicLevels.DynamicCandle();
					_previousCandle = new DynamicLevels.DynamicCandle();
					_lastNewSessionBar = -1;
					_openBar = _closeBar = _highBar = _lowBar = -1;

					if (_days == 0 || Period is PeriodType.CurrentMonth or PeriodType.PreviousMonth)
					_targetBar = 0;
					else

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

					return;
				}

				if (bar < _targetBar)
					return;

				if (bar != _lastNewSessionBar)
				{
					if (Period is PeriodType.CurrentDay or PeriodType.PreviousDay && IsNewSession(bar))

					{
						_previousCandle = _currentCandle;
						_prevOpenBar = _openBar;
						_prevCloseBar = _closeBar;
						_prevHighBar = _highBar;
						_prevLowBar = _lowBar;
						_currentCandle = new DynamicLevels.DynamicCandle();
						_lastNewSessionBar = bar;
					}
					else if (Period is PeriodType.CurrenWeek or PeriodType.PreviousWeek && IsNewWeek(bar))

					{
						_previousCandle = _currentCandle;
						_prevOpenBar = _openBar;
						_prevCloseBar = _closeBar;
						_prevHighBar = _highBar;
						_prevLowBar = _lowBar;
						_currentCandle = new DynamicLevels.DynamicCandle();
						_lastNewSessionBar = bar;
					}
					else if (Period is PeriodType.CurrentMonth or PeriodType.PreviousMonth && IsNewMonth(bar))

					{
						_previousCandle = _currentCandle;
						_prevOpenBar = _openBar;
						_prevCloseBar = _closeBar;
						_prevHighBar = _highBar;
						_prevLowBar = _lowBar;
						_currentCandle = new DynamicLevels.DynamicCandle();
						_lastNewSessionBar = bar;
					}
				}

				if (!_tickBasedCalculation)
					_currentCandle.AddCandle(GetCandle(bar), InstrumentInfo.TickSize);

				var showedCandle = Period is PeriodType.CurrentDay or PeriodType.CurrenWeek or PeriodType.CurrentMonth
					? _currentCandle
					: _previousCandle;

				if (_currentCandle.Open != _currentOpen)
				{
					_currentOpen = _currentCandle.Open;
					_openBar = bar;
				}

				if (_currentCandle.Close != _currentClose)
				{
					_currentClose = _currentCandle.Close;
					_closeBar = bar;
				}

				if (_currentCandle.High != _currentHigh)
				{
					_currentHigh = _currentCandle.High;
					_highBar = bar;
				}

				if (_currentCandle.Low != _currentLow)
				{
					_currentLow = _currentCandle.Low;
					_lowBar = bar;
				}

				if (bar == CurrentBar - 1)
				{
					_open = showedCandle.Open;
					_close = showedCandle.Close;
					_high = showedCandle.High;
					_low = showedCandle.Low;
					_tickBasedCalculation = true;
				}
			}
			catch (Exception e)
			{
				this.LogError("Daily lines error ", e);
			}
		}

		protected override void OnNewTrade(MarketDataArg arg)
		{
			if (_tickBasedCalculation)
				_currentCandle.AddTick(arg);
		}

		#endregion

		#region Private methods

		private void DrawString(RenderContext context, string renderText, int yPrice, Color color)
		{
			var textSize = context.MeasureString(renderText, _font);
			context.DrawString(renderText, _font, color, Container.Region.Right - textSize.Width - 5, yPrice - textSize.Height);
		}

		private void DrawPrice(RenderContext context, decimal price, RenderPen pen)
		{
			var y = ChartInfo.GetYByPrice(price, false);

			var renderText = price.ToString(CultureInfo.InvariantCulture);
			var textWidth = context.MeasureString(renderText, _font).Width;

			if (y + 8 > Container.Region.Height)
				return;
		
			var polygon = new Point[]
			{
				new (Container.Region.Right, y),
				new (Container.Region.Right + 6, y - 7),
				new (Container.Region.Right + textWidth + 8, y - 7),
				new (Container.Region.Right + textWidth + 8, y + 8),
				new (Container.Region.Right + 6, y + 8)
			};

			context.FillPolygon(pen.Color, polygon);
			context.DrawString(renderText, _font, Color.White, Container.Region.Right + 6, y - 6);
		}

		#endregion
	}
}