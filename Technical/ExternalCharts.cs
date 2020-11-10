namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes.Editors;
	using OFT.Rendering.Context;
	using OFT.Rendering.Settings;
	using OFT.Rendering.Tools;

	using Color = System.Drawing.Color;

	[DisplayName("External Chart")]
	[Category("Other")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/347-external-chart")]
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
			M1 = 1,
			M5 = 5,
			M10 = 10,
			M15 = 15,
			M30 = 30,
			Hourly = 60,
			H2 = 120,
			H4 = 240,
			H6 = 360,
			Daily = 1440,
			Weekly = 10080,
			Monthly = 0
		}

		#endregion

		#region Fields

		private readonly object _locker = new object();
		private readonly List<RectangleInfo> _rectangles = new List<RectangleInfo>();
		private Color _areaColor;
		private Color _downColor;
		private bool _isFixedTimeFrame;
		private int _lastBar = -1;
		private int _secondsPerCandle;
		private int _secondsPerTframe;
		private TimeFrameScale _tFrame;
		private Color _upColor;
		private int _width;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ShowGrid", GroupName = "Grid", Order = 7)]
		public bool ShowGrid { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Grid", Order = 8)]
		public System.Windows.Media.Color GridColor { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowAsCandle", GroupName = "Visualization", Order = 9)]
		public bool ExtCandleMode { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "AreaColor", GroupName = "Visualization", Order = 10)]
		public System.Windows.Media.Color AreaColor
		{
			get => _areaColor.Convert();
			set => _areaColor = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "BullishColor", GroupName = "Visualization", Order = 30)]
		public System.Windows.Media.Color UpCandleColor
		{
			get => _upColor.Convert();
			set => _upColor = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "BearlishColor", GroupName = "Visualization", Order = 40)]
		public System.Windows.Media.Color DownCandleColor
		{
			get => _downColor.Convert();
			set => _downColor = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "Width", GroupName = "Visualization", Order = 50)]
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

		[Display(ResourceType = typeof(Resources), Name = "DashStyle", GroupName = "Visualization", Order = 60)]
		public LineDashStyle Style { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ExternalPeriod", GroupName = "TimeFrame", Order = 5)]
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
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.LatestBar);

			Width = 1;
			DataSeries[0].IsHidden = true;
			UpCandleColor = Colors.RoyalBlue;
			DownCandleColor = Colors.Red;
			_areaColor = Color.FromArgb(26, 65, 105, 255);
			GridColor =System.Windows.Media.Color.FromArgb(50, 128, 128, 128);
			_tFrame = TimeFrameScale.Hourly;
			_width = 1;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			lock (_locker)
			{
				if (bar == 0)
				{
					_isFixedTimeFrame = false;
					_rectangles.Clear();
					GetCandleSeconds();
					_secondsPerTframe = 60 * (int)TFrame;
					return;
				}

				var candle = GetCandle(bar);
				var tim = GetBeginTime(candle.Time, TFrame);

				if (_rectangles.Count == 0)
				{
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

				var isNewBar = false;
				var isCustomPeriod = false;
				var lastBar = _rectangles.Last().SecondPos;

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

				if (isNewBar || !isCustomPeriod && tim >= GetCandle(lastBar).LastTime || !_isFixedTimeFrame && tim >= GetCandle(lastBar - 1).LastTime)
				{
					if (_rectangles.Count > 0 && bar > 0)
						_rectangles[_rectangles.Count - 1].SecondPos = bar - 1;

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

				if (candle.Low < _rectangles.Last().FirstPrice)
					_rectangles[_rectangles.Count - 1].FirstPrice = candle.Low;

				if (candle.High > _rectangles.Last().SecondPrice)
					_rectangles[_rectangles.Count - 1].SecondPrice = candle.High;

				_rectangles[_rectangles.Count - 1].SecondPos = bar;
				_rectangles[_rectangles.Count - 1].ClosePrice = candle.Close;

				_lastBar = bar;
			}
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			lock (_locker)
			{
				var gridPen = new RenderPen(GridColor.Convert());

				foreach (var rect in _rectangles)
				{
					if (rect.FirstPos > LastVisibleBarNumber || rect.SecondPos < FirstVisibleBarNumber)
						continue;

					var chartType = ChartInfo.ChartVisualMode;
					var useShift = chartType == ChartVisualModes.Clusters || chartType == ChartVisualModes.Line;

					var x1 = ChartInfo.GetXByBar(rect.FirstPos);
					var x2 = ChartInfo.GetXByBar(rect.SecondPos + 1);
					var yBot = ChartInfo.GetYByPrice(rect.FirstPrice - (useShift ? TickSize : 0), useShift);
					var yTop = ChartInfo.GetYByPrice(rect.SecondPrice, useShift);

					if (_isFixedTimeFrame && CurrentBar - 1 == _lastBar && rect.SecondPos == _lastBar)
					{
						var barWidth = ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0);
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

					var penColor = DownCandleColor;

					if (rect.OpenPrice < rect.ClosePrice)
						penColor = UpCandleColor;

					var renderRectangle = new Rectangle(x1, yBot, x2 - x1, yTop - yBot);
					context.FillRectangle(_areaColor, renderRectangle);
					var renderPen = new RenderPen(penColor.Convert(), Width, Style.To());

					if (ExtCandleMode)
					{
						var max = Math.Max(yTop, yBot);
						var min = Math.Min(yTop, yBot);
						var y1 = ChartInfo.GetYByPrice(Math.Min(rect.OpenPrice, rect.ClosePrice), false);
						var y2 = ChartInfo.GetYByPrice(Math.Max(rect.OpenPrice, rect.ClosePrice), false);
						renderRectangle = new Rectangle(x1, y1, x2 - x1, y2 - y1);
						context.DrawLine(renderPen, (x2 + x1) / 2, y2, (x2 + x1) / 2, min);
						context.DrawLine(renderPen, (x2 + x1) / 2, y1, (x2 + x1) / 2, max);
					}

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

			var begin = (tim - new DateTime()).TotalMinutes % (int)period;
			var res = tim.AddMinutes(-begin);
			return res;
		}

		private void GetCandleSeconds()
		{
			if (ChartInfo.ChartType == "Seconds")
			{
				_isFixedTimeFrame = true;

				_secondsPerCandle = ChartInfo.TimeFrame switch
				{
					"5" => 5,
					"10" => 10,
					"15" => 15,
					"30" => 30,
					_ => _secondsPerCandle
				};
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
				_ => _secondsPerCandle
			};
		}

		#endregion
	}
}