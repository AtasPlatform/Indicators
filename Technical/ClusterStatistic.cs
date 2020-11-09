namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	using Color = System.Windows.Media.Color;

	[DisplayName("Cluster Statistic")]
	public class ClusterStatistic : Indicator
	{
		#region Static and constants

		private const int _height = 15;

		#endregion

		#region Fields

		private readonly ValueDataSeries _cDelta = new ValueDataSeries("cDelta");
		private readonly ValueDataSeries _cVolume = new ValueDataSeries("cVolume");
		private readonly ValueDataSeries _deltaPerVol = new ValueDataSeries("DeltaPerVol");
		private readonly RenderFont _font = new RenderFont("Arial", 14);

		private readonly RenderStringFormat _stringLeftFormat = new RenderStringFormat
		{
			Alignment = StringAlignment.Near,
			LineAlignment = StringAlignment.Center,
			Trimming = StringTrimming.EllipsisCharacter,
			FormatFlags = StringFormatFlags.NoWrap
		};

		private readonly ValueDataSeries _volPerSecond = new ValueDataSeries("VolPerSecond");
		private decimal _cumDelta;
		private decimal _cumVolume;
		private int _lastDpi = 1;
		private decimal _maxDelta;
		private decimal _maxVolume;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "BackGround", GroupName = "Colors", Order = 2)]
		public Color BackGroundColor
		{
			get => BackGroundColor;
			set => Color.FromArgb(120, value.R, value.G, value.B);
		}

		[Display(ResourceType = typeof(Resources), Name = "AskColor", GroupName = "Colors", Order = 3)]
		public Color AskColor { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "BidColor", GroupName = "Colors", Order = 4)]
		public Color BidColor { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "VolumeColor", GroupName = "Colors", Order = 4)]
		public Color VolumeColor { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "TextColor", GroupName = "Colors", Order = 5)]
		public Color TextColor { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowAsk", GroupName = "Strings", Order = 10)]
		public bool ShowAsk { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowBid", GroupName = "Strings", Order = 11)]
		public bool ShowBid { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowDelta", GroupName = "Strings", Order = 12)]
		public bool ShowDelta { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowDeltaPerVolume", GroupName = "Strings", Order = 103)]
		public bool ShowDeltaPerVolume { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowSessionDelta", GroupName = "Strings", Order = 104)]
		public bool ShowSessionDelta { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowSessionDeltaPerVolume", GroupName = "Strings", Order = 105)]
		public bool ShowSessionDeltaPerVolume { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowMaximumDelta", GroupName = "Strings", Order = 106)]
		public bool ShowMaximumDelta { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowMinimumDelta", GroupName = "Strings", Order = 107)]
		public bool ShowMinimumDelta { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowVolume", GroupName = "Strings", Order = 108)]
		public bool ShowVolume { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowVolumePerSecond", GroupName = "Strings", Order = 109)]
		public bool ShowVolumePerSecond { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowSessionVolume", GroupName = "Strings", Order = 110)]
		public bool ShowSessionVolume { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowTime", GroupName = "Strings", Order = 111)]
		public bool ShowTime { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowDuration", GroupName = "Strings", Order = 112)]
		public bool ShowDuration { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "HideRowsDescription", Order = 200)]
		public bool HideRowsDescription { get; set; }

		#endregion

		#region ctor

		public ClusterStatistic()
			: base(true)
		{
			DenyToChangePanel = true;
			EnableCustomDrawing = true;

			ShowAsk = ShowBid = ShowDelta = ShowDeltaPerVolume
				= ShowSessionDelta = ShowSessionDeltaPerVolume = ShowMaximumDelta
					= ShowMinimumDelta = ShowVolume = ShowSessionVolume = ShowTime = true;
			SubscribeToDrawingEvents(DrawingLayouts.LatestBar | DrawingLayouts.Historical);
			BackGroundColor = Colors.Black;
			AskColor = Colors.Green;
			BidColor = Colors.Red;
			TextColor = Colors.White;
			VolumeColor = Colors.DarkGray;
		}

		#endregion

		#region Public methods

		public Color Invert(Color color)
		{
			return Color.FromArgb(255, (byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B));
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			if (bar == 0)
			{
				_cumVolume = 0;
				_cumDelta = 0;
				_maxVolume = 0;
				_maxDelta = 0;
				return;
			}

			if (IsNewSession(bar))
			{
				_cVolume[bar] = _cumVolume = candle.Volume;
				_cDelta[bar] = _cumDelta = candle.Delta;
			}
			else
			{
				_cumVolume = _cVolume[bar] = _cVolume[bar - 1] + candle.Volume;
				_cumDelta = _cDelta[bar] = _cDelta[bar - 1] + candle.Delta;
			}

			_maxDelta = Math.Max(Math.Abs(candle.Delta), _maxDelta);

			_maxVolume = Math.Max(candle.Volume, _maxVolume);

			if (Math.Abs(_cVolume[bar] - 0) > 0.000001m)
				_deltaPerVol[bar] = 100.0m * _cDelta[bar] / _cVolume[bar];

			_volPerSecond[bar] = candle.Volume / Math.Max(1, Convert.ToDecimal((candle.LastTime - candle.Time).TotalSeconds));
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			var y = Container.Region.Y + 2;
			var height = Container.Region.Y + Container.Region.Height - 2;

			var count = GetHeight() / _height;
			var additional = count - 2;
			y += additional - 1;
			var firstY = y;
			var color = BackGroundColor;

			var linePen = new RenderPen(color.Convert());

			var widthLabels = 140;
			var maxX = 0;
			var i = 0;
			var fullBarsWidth = (int)ChartInfo.PriceChartContainer.BarsWidth;
			var showText = fullBarsWidth > 30;

			for (var j = LastVisibleBarNumber; j >= FirstVisibleBarNumber; j--, i++)
			{
				var x = ChartInfo.GetXByBar(i);

				if (fullBarsWidth > 2)
					x++;

				maxX = Math.Max(x, maxX);

				var y1 = y;
				var candle = GetCandle(j);
				var rate = GetRate(Convert.ToDouble(Math.Abs(candle.Delta)), Convert.ToDouble(_maxDelta));
				var bgBrush = Blend(candle.Delta > 0 ? AskColor : BidColor, BackGroundColor, rate);
				var rect = new Rectangle(x, y1, fullBarsWidth, _height);

				if (ShowAsk)
				{
					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = string.Format(ChartInfo.StringFormat, candle.Ask);
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowBid)
				{
					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = string.Format(ChartInfo.StringFormat, candle.Bid);
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowDelta)
				{
					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = string.Format(ChartInfo.StringFormat, candle.Delta);
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowDeltaPerVolume && candle.Volume != 0)
				{
					var deltaPerVol = candle.Delta * 100.0m / candle.Volume;

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = deltaPerVol.ToString("F") + "%";
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowSessionDelta)
				{
					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = string.Format(ChartInfo.StringFormat, _cDelta[j]);
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowSessionDeltaPerVolume)
				{
					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = _deltaPerVol[j].ToString("F") + "%";
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowMaximumDelta)
				{
					rate = GetRate(Convert.ToDouble(candle.Volume), Convert.ToDouble(_maxVolume));
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = string.Format(ChartInfo.StringFormat, candle.MaxDelta);
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowMaximumDelta)
				{
					rate = GetRate(Convert.ToDouble(candle.Volume), Convert.ToDouble(_maxVolume));
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = string.Format(ChartInfo.StringFormat, candle.MinDelta);
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowVolume)
				{
					rate = GetRate(Convert.ToDouble(candle.Volume), Convert.ToDouble(_maxVolume));
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = string.Format(ChartInfo.StringFormat, candle.Volume);
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowVolumePerSecond)
				{
					rate = GetRate(Convert.ToDouble(candle.Volume), Convert.ToDouble(_maxVolume));
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = string.Format(ChartInfo.StringFormat, _volPerSecond[j]);
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowSessionVolume)
				{
					rate = GetRate(Convert.ToDouble(_cVolume[j]), Convert.ToDouble(_cumVolume));
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = string.Format(ChartInfo.StringFormat, _cVolume[j]);
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowTime)
				{
					rate = GetRate(Convert.ToDouble(_cVolume[j]), Convert.ToDouble(_cumVolume));
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = candle.Time.ToString("HH:mm:ss");
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowDuration)
				{
					rate = GetRate(Convert.ToDouble(_cVolume[j]), Convert.ToDouble(_cumVolume));
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = ((int)(candle.LastTime - candle.Time).TotalSeconds).ToString();
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				context.DrawLine(linePen, x + fullBarsWidth, y, x + fullBarsWidth, height);
			}

			maxX += fullBarsWidth;

			if (layout != DrawingLayouts.Historical)
				return;

			if (HideRowsDescription)
				return;

			var bgbrushd = Blend(BackGroundColor, Invert(BackGroundColor), 70);

			var descRect = new Rectangle(0, y, widthLabels, _height);

			if (ShowAsk)
			{
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Ask", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowBid)
			{
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Bid", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowDelta)
			{
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Delta", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowDeltaPerVolume)
			{
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Delta/Volume", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowSessionDelta)
			{
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Session Delta", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowSessionDeltaPerVolume)
			{
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Session Delta/Volume", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowMaximumDelta)
			{
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Max.Delta", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowMinimumDelta)
			{
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Min.Delta", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowVolume)
			{
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Volume", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowVolumePerSecond)
			{
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Volume/sec", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowTime)
			{
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Time", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowDuration)
			{
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Duration", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			context.DrawLine(linePen, 0, firstY, maxX, firstY);
		}

		#endregion

		#region Private methods

		private int GetHeight(int dpi = 0)
		{
			if (dpi == 0)
				dpi = _lastDpi;
			else
				_lastDpi = dpi;

			var dpiHeight = _height * dpi;
			var res = 0;

			if (ShowAsk)
				res += dpiHeight;

			if (ShowBid)
				res += dpiHeight;

			if (ShowDelta)
				res += dpiHeight;

			if (ShowSessionDelta)
				res += dpiHeight;

			if (ShowSessionDeltaPerVolume)
				res += dpiHeight;

			if (ShowSessionVolume)
				res += dpiHeight;

			if (ShowVolumePerSecond)
				res += dpiHeight;

			if (ShowVolume)
				res += dpiHeight;

			if (ShowDeltaPerVolume)
				res += dpiHeight;

			if (ShowTime)
				res += dpiHeight;

			if (ShowMaximumDelta)
				res += dpiHeight;

			if (ShowMinimumDelta)
				res += dpiHeight;

			if (ShowDuration)
				res += dpiHeight;
			return res;
		}

		private double GetRate(double value, double maximumValue)
		{
			var rate = value * 100.0 / (maximumValue * 0.6);

			if (rate < 10)
				rate = 10;
			return rate;
		}

		private Color Blend(Color color, Color backColor, double amount)
		{
			var r = (byte)(color.R * amount + backColor.R * (1 - amount * 0.01));
			var g = (byte)(color.G * amount + backColor.G * (1 - amount * 0.01));
			var b = (byte)(color.B * amount + backColor.B * (1 - amount * 0.01));
			return Color.FromArgb(255, r, g, b);
		}

		#endregion
	}
}