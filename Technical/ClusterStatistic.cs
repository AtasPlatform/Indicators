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

	using Utils.Common.Attributes;

	using Color = System.Windows.Media.Color;

	[DisplayName("Cluster Statistic")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/454-cluster-statistic")]
	public class ClusterStatistic : Indicator
	{
		#region Static and constants

		private const int _height = 15;

		#endregion

		#region Fields

		private readonly ValueDataSeries _cDelta = new ValueDataSeries("cDelta");
		private readonly ValueDataSeries _cVolume = new ValueDataSeries("cVolume");
		private readonly ValueDataSeries _deltaPerVol = new ValueDataSeries("DeltaPerVol");
		private readonly RenderFont _font = new RenderFont("Arial", 10);

		private readonly RenderStringFormat _stringLeftFormat = new RenderStringFormat
		{
			Alignment = StringAlignment.Near,
			LineAlignment = StringAlignment.Center,
			Trimming = StringTrimming.EllipsisCharacter,
			FormatFlags = StringFormatFlags.NoWrap
		};

		private readonly ValueDataSeries _volPerSecond = new ValueDataSeries("VolPerSecond");
		private Color _backGroundColor;
		private decimal _cumVolume;
		private int _lastDpi = 1;
		private decimal _maxDelta;
		private decimal _maxVolume;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "BackGround", GroupName = "Colors", Order = 2)]
		public Color BackGroundColor
		{
			get => _backGroundColor;
			set => _backGroundColor = Color.FromArgb(120, value.R, value.G, value.B);
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
			//Panel = IndicatorDataProvider.NewPanel;

			ShowAsk = ShowBid = ShowDelta = ShowDeltaPerVolume = true;
			ShowSessionDelta = ShowSessionDeltaPerVolume = ShowMaximumDelta = true;
			ShowMinimumDelta = ShowVolume = ShowSessionVolume = ShowTime = true;
			SubscribeToDrawingEvents(DrawingLayouts.LatestBar | DrawingLayouts.Final);
			_backGroundColor = Colors.Black;
			AskColor = Colors.Green;
			BidColor = Colors.Red;
			TextColor = Colors.White;
			VolumeColor = Colors.DarkGray;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			if (bar == 0)
			{
				_cumVolume = 0;
				_maxVolume = 0;
				_maxDelta = 0;
				return;
			}

			if (IsNewSession(bar))
			{
				_cVolume[bar] = _cumVolume = candle.Volume;
				_cDelta[bar] = candle.Delta;
			}
			else
			{
				_cumVolume = _cVolume[bar] = _cVolume[bar - 1] + candle.Volume;
				_cDelta[bar] = _cDelta[bar - 1] + candle.Delta;
			}

			_maxDelta = Math.Max(Math.Abs(candle.Delta), _maxDelta);

			_maxVolume = Math.Max(candle.Volume, _maxVolume);

			if (Math.Abs(_cVolume[bar] - 0) > 0.000001m)
				_deltaPerVol[bar] = 100.0m * _cDelta[bar] / _cVolume[bar];

			_volPerSecond[bar] = candle.Volume / Math.Max(1, Convert.ToDecimal((candle.LastTime - candle.Time).TotalSeconds));
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if (ChartInfo.PriceChartContainer.BarsWidth < 30)
				return;

			var y = Container.Region.Height - GetHeight() - 6;
			var height = Container.Region.Height + Container.RelativeRegion.Height;

			var count = GetHeight() / _height;
			var additional = count - 2;
			y += additional - 1;
			var firstY = y;

			var linePen = new RenderPen(_backGroundColor.Convert());

			var widthLabels = 140;
			var maxX = 0;
			var fullBarsWidth = ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0);
			var showText = fullBarsWidth > 30;

			for (var j = LastVisibleBarNumber; j >= FirstVisibleBarNumber; j--)
			{
				var x = ChartInfo.GetXByBar(j);

				if (fullBarsWidth > 2)
					x++;

				maxX = Math.Max(x, maxX);

				var y1 = y;
				var candle = GetCandle(j);
				var rate = GetRate(Convert.ToDouble(Math.Abs(candle.Delta)), Convert.ToDouble(_maxDelta));
				var bgBrush = Blend(candle.Delta > 0 ? AskColor : BidColor, BackGroundColor, rate);

				if (ShowAsk)
				{
					var rect = new Rectangle(x, y1, fullBarsWidth, _height);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = $" {candle.Ask:0.##}";
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowBid)
				{
					var rect = new Rectangle(x, y1, fullBarsWidth, _height);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = $" {candle.Bid:0.##}";
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowDelta)
				{
					var rect = new Rectangle(x, y1, fullBarsWidth, _height);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = $" {candle.Delta:0.##}";
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowDeltaPerVolume && candle.Volume != 0)
				{
					var rect = new Rectangle(x, y1, fullBarsWidth, _height);

					var deltaPerVol = candle.Delta * 100.0m / candle.Volume;

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = " " + deltaPerVol.ToString("F") + "%";
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowSessionDelta)
				{
					var rect = new Rectangle(x, y1, fullBarsWidth, _height);
					bgBrush = Blend(_cDelta[j] > 0 ? AskColor : BidColor, BackGroundColor, rate);
					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = $" {_cDelta[j]:0.##}";
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowSessionDeltaPerVolume)
				{
					var rect = new Rectangle(x, y1, fullBarsWidth, _height);
					bgBrush = Blend(_deltaPerVol[j] > 0 ? AskColor : BidColor, BackGroundColor, rate);
					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = " " + _deltaPerVol[j].ToString("F") + "%";
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowMaximumDelta)
				{
					var rect = new Rectangle(x, y1, fullBarsWidth, _height);

					rate = GetRate(Convert.ToDouble(candle.Volume), Convert.ToDouble(_maxVolume));
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = $" {candle.MaxDelta:0.##}";
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowMaximumDelta)
				{
					var rect = new Rectangle(x, y1, fullBarsWidth, _height);

					rate = GetRate(Convert.ToDouble(candle.Volume), Convert.ToDouble(_maxVolume));
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = $" {candle.MinDelta:0.##}";
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowVolume)
				{
					var rect = new Rectangle(x, y1, fullBarsWidth, _height);

					rate = GetRate(Convert.ToDouble(candle.Volume), Convert.ToDouble(_maxVolume));
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = $" {candle.Volume:0.##}";
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowVolumePerSecond)
				{
					var rect = new Rectangle(x, y1, fullBarsWidth, _height);

					rate = GetRate(Convert.ToDouble(candle.Volume), Convert.ToDouble(_maxVolume));
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = $" {_volPerSecond[j]:0.##}";
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowSessionVolume)
				{
					var rect = new Rectangle(x, y1, fullBarsWidth, _height);

					rate = GetRate(Convert.ToDouble(_cVolume[j]), Convert.ToDouble(_cumVolume));
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = $" {_cVolume[j]:0.##}";
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowTime)
				{
					var rect = new Rectangle(x, y1, fullBarsWidth, _height);

					rate = GetRate(Convert.ToDouble(_cVolume[j]), Convert.ToDouble(_cumVolume));
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = candle.Time.ToString(" HH:mm:ss");
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				if (ShowDuration)
				{
					var rect = new Rectangle(x, y1, fullBarsWidth, _height);

					rate = GetRate(Convert.ToDouble(_cVolume[j]), Convert.ToDouble(_cumVolume));
					bgBrush = Blend(VolumeColor, BackGroundColor, rate);

					context.FillRectangle(bgBrush.Convert(), rect);

					if (showText)
					{
						var s = " " + (int)(candle.LastTime - candle.Time).TotalSeconds;
						context.DrawString(s, _font, TextColor.Convert(), rect, _stringLeftFormat);
					}

					y1 += _height;
				}

				context.DrawLine(linePen, x + fullBarsWidth, y, x + fullBarsWidth, height);
			}

			maxX += fullBarsWidth;

			if (HideRowsDescription)
				return;

			var bgbrushd = Blend(_backGroundColor, Invert(_backGroundColor), 70);

			if (ShowAsk)
			{
				var descRect = new Rectangle(0, y, widthLabels, _height);
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Ask", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowBid)
			{
				var descRect = new Rectangle(0, y, widthLabels, _height);
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Bid", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowDelta)
			{
				var descRect = new Rectangle(0, y, widthLabels, _height);
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Delta", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowDeltaPerVolume)
			{
				var descRect = new Rectangle(0, y, widthLabels, _height);
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Delta/Volume", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowSessionDelta)
			{
				var descRect = new Rectangle(0, y, widthLabels, _height);
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Session Delta", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowSessionDeltaPerVolume)
			{
				var descRect = new Rectangle(0, y, widthLabels, _height);
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Session Delta/Volume", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowMaximumDelta)
			{
				var descRect = new Rectangle(0, y, widthLabels, _height);
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Max.Delta", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowMinimumDelta)
			{
				var descRect = new Rectangle(0, y, widthLabels, _height);
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Min.Delta", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowVolume)
			{
				var descRect = new Rectangle(0, y, widthLabels, _height);
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Volume", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowVolumePerSecond)
			{
				var descRect = new Rectangle(0, y, widthLabels, _height);
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Volume/sec", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowSessionVolume)
			{
				var descRect = new Rectangle(0, y, widthLabels, _height);
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Session Volume", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowTime)
			{
				var descRect = new Rectangle(0, y, widthLabels, _height);
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Time", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			if (ShowDuration)
			{
				var descRect = new Rectangle(0, y, widthLabels, _height);
				context.DrawFillRectangle(linePen, bgbrushd.Convert(), descRect);
				context.DrawString("Duration", _font, TextColor.Convert(), descRect, _stringLeftFormat);
				y += _height;
				context.DrawLine(linePen, 0, y, widthLabels, y);
			}

			context.DrawLine(linePen, 0, firstY - y, maxX, firstY - y);
		}

		#endregion

		#region Private methods

		private Color Invert(Color color)
		{
			return Color.FromArgb(255, (byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B));
		}

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
			var r = (byte)(color.R + (backColor.R - color.R) * (1 - amount * 0.01));
			var g = (byte)(color.G + (backColor.G - color.G) * (1 - amount * 0.01));
			var b = (byte)(color.B + (backColor.B - color.B) * (1 - amount * 0.01));
			return Color.FromArgb(255, r, g, b);
		}

		#endregion
	}
}