namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Properties;

	using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	using Color = System.Drawing.Color;
	using LibResources = ATAS.Indicators.Technical.Properties.Resources;

	[DisplayName("External Chart")]
	[Category("Other")]
	public class ExternalCharts : Indicator
	{
		#region Nested types

		public class RectangleInfo
		{
			#region Fields

			public decimal ClosePrice;
			public int FirstPos;
			public decimal FirstPrice;

			public decimal OpenPrice;
			public double PercentComplete;
			public int SecondPos;
			public decimal SecondPrice;

			#endregion
		}

		public enum CandleMode
		{
			[Display(ResourceType = typeof(LibResources), Name = "MaximumCandleHeight")]
			CandleHeight,

			[Display(ResourceType = typeof(LibResources), Name = "CandleBodyHeight")]
			CandleBody
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

		private readonly Color _emptyColor = Color.FromArgb(0, 0, 0, 0);

		private readonly List<RectangleInfo> _rectangles = new List<RectangleInfo>();
		private Color _areaColor;
		private Color _downColor;
		private bool _isFixedTimeFrame;
		private int _opacity;
		private int _secondsPerCandle;
		private TimeFrameScale _tFrame;
		private Color _upColor;
		private int _width;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(LibResources), Name = "CandleHeightFilter", GroupName = "Visualization", Order = 9)]
		public CandleMode ExtCandleMode { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "AreaColor", GroupName = "Visualization", Order = 10)]
		public System.Windows.Media.Color AreaColor
		{
			get => _areaColor.Convert();
			set
			{
				var alpha = (int)Math.Floor(255 * _opacity * 0.1);
				_areaColor = Color.FromArgb(alpha, value.R, value.G, value.B);
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ClusterSelectionTransparency", GroupName = "Visualization", Order = 20)]
		public int Opacity
		{
			get => _opacity;
			set
			{
				if (value > 10 || value < 0)
					return;

				_opacity = value;
				var alpha = (int)Math.Floor(255 * _opacity * 0.1);
				_areaColor = Color.FromArgb(alpha, _areaColor);
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "UpCandleColor", GroupName = "Visualization", Order = 30)]
		public System.Windows.Media.Color UpCandleColor
		{
			get => _upColor.Convert();
			set => _upColor = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "DownCandleColor", GroupName = "Visualization", Order = 40)]
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

		[Display(ResourceType = typeof(Resources), Name = "LineDashStyle", GroupName = "Visualization", Order = 60)]
		public LineDashStyle Style { get; set; }

		[Display(ResourceType = typeof(LibResources), Name = "TimeFrame", GroupName = "TimeFrame", Order = 5)]
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
			SubscribeToDrawingEvents(DrawingLayouts.Final | DrawingLayouts.LatestBar | DrawingLayouts.Historical);
			Width = 1;
			DataSeries[0].IsHidden = true;
			UpCandleColor = Colors.DodgerBlue;
			DownCandleColor = Colors.Firebrick;
			_areaColor = Colors.DeepSkyBlue.Convert();
			_tFrame = TimeFrameScale.H2;
			_opacity = 1;
			_width = 1;
			ExtCandleMode = CandleMode.CandleHeight;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_isFixedTimeFrame = false;
				_rectangles.Clear();

				if (ChartInfo.ChartType == "TimeFrame" || ChartInfo.ChartType == "Seconds")
				{
					_isFixedTimeFrame = true;

					switch (ChartInfo.TimeFrame)
					{
						case "M1":
							_secondsPerCandle = 60 * (int)TimeFrameScale.M1;
							break;

						case "M5":
							_secondsPerCandle = 60 * (int)TimeFrameScale.M5;
							break;

						case "M10":
							_secondsPerCandle = 60 * (int)TimeFrameScale.M10;
							break;
						case "M15":
							_secondsPerCandle = 60 * (int)TimeFrameScale.M15;
							break;

						case "M30":
							_secondsPerCandle = 60 * (int)TimeFrameScale.M30;
							break;
						case "Hourly":
							_secondsPerCandle = 60 * (int)TimeFrameScale.Hourly;
							break;

						case "H2":
							_secondsPerCandle = 60 * (int)TimeFrameScale.H2;
							break;
						case "H4":
							_secondsPerCandle = 60 * (int)TimeFrameScale.H4;
							break;
						case "H6":
							_secondsPerCandle = 60 * (int)TimeFrameScale.H6;
							break;
						case "Daily":
							_secondsPerCandle = 60 * (int)TimeFrameScale.Daily;
							break;
						case "Weekly":
							_secondsPerCandle = 60 * (int)TimeFrameScale.Weekly;
							break;
					}
				}

				return;
			}

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);
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
			var firstBar = _rectangles.Last().FirstPos;
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

			if (isNewBar || !isCustomPeriod && tim >= GetCandle(lastBar).LastTime)
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

			firstBar = _rectangles.Last().FirstPos;
			lastBar = _rectangles.Last().SecondPos;

			var firstCandle = GetCandle(firstBar);
			var lastCandle = GetCandle(lastBar);

			var rectTim = (lastCandle.Time - firstCandle.Time).TotalSeconds;

			if (_rectangles.Last().FirstPos == _rectangles.Last().SecondPos)
				rectTim = (lastCandle.LastTime - firstCandle.Time).TotalSeconds;

			var secondsPerTframe = 60 * (int)TFrame;

			if (_tFrame == TimeFrameScale.Monthly)
				secondsPerTframe = DateTime.DaysInMonth(firstCandle.Time.Year, firstCandle.Time.Month) * 24 * 60 * 60;

			var perc = rectTim * 100.0 / secondsPerTframe;

			if (_isFixedTimeFrame)
			{
				var totalBars = _rectangles.Last().SecondPos - _rectangles.Last().FirstPos + 1;

				perc = totalBars * _secondsPerCandle * 100.0 / secondsPerTframe;
			}

			if (perc > 100)
				perc = 100;

			if (perc < 1)
				perc = 1;

			_rectangles[_rectangles.Count - 1].PercentComplete = perc;
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			foreach (var rect in _rectangles)
			{
				if (rect.FirstPos >= LastVisibleBarNumber || rect.SecondPos < FirstVisibleBarNumber)
					continue;

				var x1 = ChartInfo.GetXByBar(rect.FirstPos);
				var x2 = ChartInfo.GetXByBar(rect.SecondPos + 1);
				var y1 = ChartInfo.GetYByPrice(rect.FirstPrice) + 2;
				var y2 = ChartInfo.GetYByPrice(rect.SecondPrice) + 2;

				var penColor = DownCandleColor;

				if (rect.OpenPrice < rect.ClosePrice)
					penColor = UpCandleColor;

				var renderRectangle = new Rectangle(x1 + 1, y1, x2 - x1 - 2, y2 - y1);
				context.DrawFillRectangle(new RenderPen(_emptyColor), _areaColor, renderRectangle);

				var renderPen = new RenderPen(penColor.Convert(), Width, Style.To());

				if (ExtCandleMode == CandleMode.CandleBody)
				{
					var max = Math.Max(y1, y2);
					var min = Math.Min(y1, y2);
					y1 = ChartInfo.GetYByPrice(Math.Min(rect.OpenPrice, rect.ClosePrice)) + 2;
					y2 = ChartInfo.GetYByPrice(Math.Max(rect.OpenPrice, rect.ClosePrice)) + 2;
					renderRectangle = new Rectangle(x1 + 1, y1, x2 - x1 - 2, y2 - y1);
					context.DrawLine(renderPen, (x2 + x1) / 2, y2, (x2 + x1) / 2, min);
					context.DrawLine(renderPen, (x2 + x1) / 2, y1, (x2 + x1) / 2, max);
				}

				context.DrawFillRectangle(renderPen, _emptyColor, renderRectangle);
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

		#endregion
	}
}