namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	using Utils.Common.Logging;

	using Color = System.Windows.Media.Color;

	[DisplayName("Cluster Statistic")]
	[Category("Clusters, Profiles, Levels")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/454-cluster-statistic")]
	public class ClusterStatistic : Indicator
	{
		#region Static and constants

		private const int _headerOffset = 3;
		private const int _headerWidth = 140;

		#endregion

		#region Fields

		private readonly ValueDataSeries _cDelta = new ValueDataSeries("cDelta");
		private readonly ValueDataSeries _cVolume = new ValueDataSeries("cVolume");
		private readonly ValueDataSeries _deltaPerVol = new ValueDataSeries("DeltaPerVol");
		private readonly RenderFont _font = new RenderFont("Arial", 9);

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

		private int _height = 15;
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
			Panel = IndicatorDataProvider.NewPanel;
			EnableCustomDrawing = true;
			ShowAsk = ShowBid = ShowDelta = ShowDeltaPerVolume = true;
			ShowSessionDelta = ShowSessionDeltaPerVolume = ShowMaximumDelta = true;
			ShowMinimumDelta = ShowVolume = ShowSessionVolume = ShowTime = true;
			SubscribeToDrawingEvents(DrawingLayouts.LatestBar | DrawingLayouts.Final);
			_backGroundColor = Colors.Black;
			AskColor = Colors.Green;
			BidColor = Colors.Red;
			TextColor = Colors.White;
			VolumeColor = Colors.DarkGray;
			DataSeries[0].IsHidden = true;
			ShowDescription = false;
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
			if (ChartInfo.PriceChartContainer.BarsWidth < 20)
				return;

			try
			{
				var renderField = new Rectangle(Container.Region.X + _headerOffset, Container.Region.Y, Container.Region.Width - _headerOffset,
					Container.Region.Height);
				context.SetClip(renderField);

				context.SetTextRenderingHint(RenderTextRenderingHint.Aliased);

				var strCount = GetStrCount();

				_height = Container.Region.Height / strCount;
				var overPixels = Container.Region.Height % strCount;

				var y = Container.Region.Y;

				var firstY = y;
				var linePen = new RenderPen(System.Drawing.Color.White);
				var maxX = 0;

				var fullBarsWidth = ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0);
				var showText = fullBarsWidth >= 30 && context.MeasureString("1", _font).Height <= _height;
				var textColor = TextColor.Convert();

				for (var j = LastVisibleBarNumber; j >= FirstVisibleBarNumber; j--)
				{
					var x = ChartInfo.GetXByBar(j) + 3;

					maxX = Math.Max(x, maxX);

					var y1 = y;
					var candle = GetCandle(j);
					var rate = GetRate(Math.Abs(candle.Delta), _maxDelta);
					var bgBrush = Blend(candle.Delta > 0 ? AskColor : BidColor, BackGroundColor, rate);

					if (ShowAsk)
					{
						var rectHeight = _height + (overPixels > 0 ? 1 : 0);

						var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

						context.FillRectangle(bgBrush, rect);

						if (showText)
						{
							var s = $"{candle.Ask:0.##}";
							context.DrawString(s, _font, textColor, rect, _stringLeftFormat);
						}

						y1 += rectHeight;
						overPixels--;
					}

					if (ShowBid)
					{
						var rectHeight = _height + (overPixels > 0 ? 1 : 0);
						var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

						context.FillRectangle(bgBrush, rect);

						if (showText)
						{
							var s = $"{candle.Bid:0.##}";
							context.DrawString(s, _font, textColor, rect, _stringLeftFormat);
						}

						y1 += rectHeight;
						overPixels--;
					}

					if (ShowDelta)
					{
						var rectHeight = _height + (overPixels > 0 ? 1 : 0);
						var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

						context.FillRectangle(bgBrush, rect);

						if (showText)
						{
							var s = $"{candle.Delta:0.##}";
							context.DrawString(s, _font, textColor, rect, _stringLeftFormat);
						}

						y1 += rectHeight;
						overPixels--;
					}

					if (ShowDeltaPerVolume && candle.Volume != 0)
					{
						var rectHeight = _height + (overPixels > 0 ? 1 : 0);
						var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

						var deltaPerVol = candle.Delta * 100.0m / candle.Volume;

						context.FillRectangle(bgBrush, rect);

						if (showText)
						{
							var s = deltaPerVol.ToString("F") + "%";
							context.DrawString(s, _font, textColor, rect, _stringLeftFormat);
						}

						y1 += rectHeight;
						overPixels--;
					}

					if (ShowSessionDelta)
					{
						var rectHeight = _height + (overPixels > 0 ? 1 : 0);
						var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);
						bgBrush = Blend(_cDelta[j] > 0 ? AskColor : BidColor, BackGroundColor, rate);
						context.FillRectangle(bgBrush, rect);

						if (showText)
						{
							var s = $"{_cDelta[j]:0.##}";
							context.DrawString(s, _font, textColor, rect, _stringLeftFormat);
						}

						y1 += rectHeight;
						overPixels--;
					}

					if (ShowSessionDeltaPerVolume)
					{
						var rectHeight = _height + (overPixels > 0 ? 1 : 0);
						var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);
						bgBrush = Blend(_deltaPerVol[j] > 0 ? AskColor : BidColor, BackGroundColor, rate);
						context.FillRectangle(bgBrush, rect);

						if (showText)
						{
							var s = _deltaPerVol[j].ToString("F") + "%";
							context.DrawString(s, _font, textColor, rect, _stringLeftFormat);
						}

						y1 += rectHeight;
						overPixels--;
					}

					if (ShowMaximumDelta)
					{
						var rectHeight = _height + (overPixels > 0 ? 1 : 0);
						var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

						rate = GetRate(candle.Volume, _maxVolume);
						bgBrush = Blend(VolumeColor, BackGroundColor, rate);

						context.FillRectangle(bgBrush, rect);

						if (showText)
						{
							var s = $"{candle.MaxDelta:0.##}";
							context.DrawString(s, _font, textColor, rect, _stringLeftFormat);
						}

						y1 += rectHeight;
						overPixels--;
					}

					if (ShowMinimumDelta)
					{
						var rectHeight = _height + (overPixels > 0 ? 1 : 0);
						var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

						rate = GetRate(candle.Volume, _maxVolume);
						bgBrush = Blend(VolumeColor, BackGroundColor, rate);

						context.FillRectangle(bgBrush, rect);

						if (showText)
						{
							var s = $"{candle.MinDelta:0.##}";
							context.DrawString(s, _font, textColor, rect, _stringLeftFormat);
						}

						y1 += rectHeight;
						overPixels--;
					}

					if (ShowVolume)
					{
						var rectHeight = _height + (overPixels > 0 ? 1 : 0);
						var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

						rate = GetRate(candle.Volume, _maxVolume);
						bgBrush = Blend(VolumeColor, BackGroundColor, rate);

						context.FillRectangle(bgBrush, rect);

						if (showText)
						{
							var s = $"{candle.Volume:0.##}";
							context.DrawString(s, _font, textColor, rect, _stringLeftFormat);
						}

						y1 += rectHeight;
						overPixels--;
					}

					if (ShowVolumePerSecond)
					{
						var rectHeight = _height + (overPixels > 0 ? 1 : 0);
						var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

						rate = GetRate(candle.Volume, _maxVolume);
						bgBrush = Blend(VolumeColor, BackGroundColor, rate);

						context.FillRectangle(bgBrush, rect);

						if (showText)
						{
							var s = $"{_volPerSecond[j]:0.##}";
							context.DrawString(s, _font, textColor, rect, _stringLeftFormat);
						}

						y1 += rectHeight;
						overPixels--;
					}

					if (ShowSessionVolume)
					{
						var rectHeight = _height + (overPixels > 0 ? 1 : 0);
						var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

						rate = GetRate(_cVolume[j], _cumVolume);
						bgBrush = Blend(VolumeColor, BackGroundColor, rate);

						context.FillRectangle(bgBrush, rect);

						if (showText)
						{
							var s = $"{_cVolume[j]:0.##}";
							context.DrawString(s, _font, textColor, rect, _stringLeftFormat);
						}

						y1 += rectHeight;
						overPixels--;
					}

					if (ShowTime)
					{
						var rectHeight = _height + (overPixels > 0 ? 1 : 0);
						var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

						rate = GetRate(_cVolume[j], _cumVolume);
						bgBrush = Blend(VolumeColor, BackGroundColor, rate);

						context.FillRectangle(bgBrush, rect);

						if (showText)
						{
							var s = candle.Time.ToString("HH:mm:ss");
							context.DrawString(s, _font, textColor, rect, _stringLeftFormat);
						}

						y1 += rectHeight;
						overPixels--;
					}

					if (ShowDuration)
					{
						var rectHeight = _height + (overPixels > 0 ? 1 : 0);
						var rect = new Rectangle(x, y1, fullBarsWidth, rectHeight);

						rate = GetRate(_cVolume[j], _cumVolume);
						bgBrush = Blend(VolumeColor, BackGroundColor, rate);

						context.FillRectangle(bgBrush, rect);

						if (showText)
						{
							var s = (int)(candle.LastTime - candle.Time).TotalSeconds;
							context.DrawString($"{s}", _font, textColor, rect, _stringLeftFormat);
						}
					}

					context.DrawLine(linePen, x + fullBarsWidth, Container.Region.Bottom, x + fullBarsWidth, Container.Region.Y);
					overPixels = Container.Region.Height % strCount;
				}

				maxX += fullBarsWidth;

				if (HideRowsDescription)
					return;

				var bgbrushd = Blend(_backGroundColor, Invert(_backGroundColor), 70);

				if (ShowAsk)
				{
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);
					var descRect = new Rectangle(_headerOffset, y, _headerWidth, rectHeight);
					context.DrawFillRectangle(linePen, bgbrushd, descRect);

					if (showText)
						context.DrawString("Ask", _font, textColor, descRect, _stringLeftFormat);
					y += rectHeight;
					overPixels--;
					context.DrawLine(linePen, Container.Region.X, y, Container.Region.Width, y);
				}

				if (ShowBid)
				{
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);
					var descRect = new Rectangle(_headerOffset, y, _headerWidth, rectHeight);
					context.DrawFillRectangle(linePen, bgbrushd, descRect);

					if (showText)
						context.DrawString("Bid", _font, textColor, descRect, _stringLeftFormat);
					y += rectHeight;
					overPixels--;
					context.DrawLine(linePen, Container.Region.X, y, Container.Region.Width, y);
				}

				if (ShowDelta)
				{
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);
					var descRect = new Rectangle(_headerOffset, y, _headerWidth, rectHeight);
					context.DrawFillRectangle(linePen, bgbrushd, descRect);

					if (showText)
						context.DrawString("Delta", _font, textColor, descRect, _stringLeftFormat);
					y += rectHeight;
					overPixels--;
					context.DrawLine(linePen, Container.Region.X, y, Container.Region.Width, y);
				}

				if (ShowDeltaPerVolume)
				{
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);
					var descRect = new Rectangle(_headerOffset, y, _headerWidth, rectHeight);
					context.DrawFillRectangle(linePen, bgbrushd, descRect);

					if (showText)
						context.DrawString("Delta/Volume", _font, textColor, descRect, _stringLeftFormat);
					y += rectHeight;
					overPixels--;
					context.DrawLine(linePen, Container.Region.X, y, Container.Region.Width, y);
				}

				if (ShowSessionDelta)
				{
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);
					var descRect = new Rectangle(_headerOffset, y, _headerWidth, rectHeight);
					context.DrawFillRectangle(linePen, bgbrushd, descRect);

					if (showText)
						context.DrawString("Session Delta", _font, textColor, descRect, _stringLeftFormat);
					y += rectHeight;
					overPixels--;
					context.DrawLine(linePen, Container.Region.X, y, Container.Region.Width, y);
				}

				if (ShowSessionDeltaPerVolume)
				{
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);
					var descRect = new Rectangle(_headerOffset, y, _headerWidth, rectHeight);
					context.DrawFillRectangle(linePen, bgbrushd, descRect);

					if (showText)
						context.DrawString("Session Delta/Volume", _font, textColor, descRect, _stringLeftFormat);
					y += rectHeight;
					overPixels--;
					context.DrawLine(linePen, Container.Region.X, y, Container.Region.Width, y);
				}

				if (ShowMaximumDelta)
				{
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);
					var descRect = new Rectangle(_headerOffset, y, _headerWidth, rectHeight);
					context.DrawFillRectangle(linePen, bgbrushd, descRect);

					if (showText)
						context.DrawString("Max.Delta", _font, textColor, descRect, _stringLeftFormat);
					y += rectHeight;
					overPixels--;
					context.DrawLine(linePen, Container.Region.X, y, Container.Region.Width, y);
				}

				if (ShowMinimumDelta)
				{
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);
					var descRect = new Rectangle(_headerOffset, y, _headerWidth, rectHeight);
					context.DrawFillRectangle(linePen, bgbrushd, descRect);

					if (showText)
						context.DrawString("Min.Delta", _font, textColor, descRect, _stringLeftFormat);
					y += rectHeight;
					overPixels--;
					context.DrawLine(linePen, Container.Region.X, y, Container.Region.Width, y);
				}

				if (ShowVolume)
				{
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);
					var descRect = new Rectangle(_headerOffset, y, _headerWidth, rectHeight);
					context.DrawFillRectangle(linePen, bgbrushd, descRect);

					if (showText)
						context.DrawString("Volume", _font, textColor, descRect, _stringLeftFormat);
					y += rectHeight;
					overPixels--;
					context.DrawLine(linePen, Container.Region.X, y, Container.Region.Width, y);
				}

				if (ShowVolumePerSecond)
				{
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);
					var descRect = new Rectangle(_headerOffset, y, _headerWidth, rectHeight);
					context.DrawFillRectangle(linePen, bgbrushd, descRect);

					if (showText)
						context.DrawString("Volume/sec", _font, textColor, descRect, _stringLeftFormat);
					y += rectHeight;
					overPixels--;
					context.DrawLine(linePen, Container.Region.X, y, Container.Region.Width, y);
				}

				if (ShowSessionVolume)
				{
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);
					var descRect = new Rectangle(_headerOffset, y, _headerWidth, rectHeight);
					context.DrawFillRectangle(linePen, bgbrushd, descRect);

					if (showText)
						context.DrawString("Session Volume", _font, textColor, descRect, _stringLeftFormat);
					y += rectHeight;
					overPixels--;
					context.DrawLine(linePen, Container.Region.X, y, Container.Region.Width, y);
				}

				if (ShowTime)
				{
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);
					var descRect = new Rectangle(_headerOffset, y, _headerWidth, rectHeight);
					context.DrawFillRectangle(linePen, bgbrushd, descRect);

					if (showText)
						context.DrawString("Time", _font, textColor, descRect, _stringLeftFormat);
					y += rectHeight;
					overPixels--;
					context.DrawLine(linePen, Container.Region.X, y, Container.Region.Width, y);
				}

				if (ShowDuration)
				{
					var rectHeight = _height + (overPixels > 0 ? 1 : 0);
					var descRect = new Rectangle(_headerOffset, y, _headerWidth, rectHeight);
					context.DrawFillRectangle(linePen, bgbrushd, descRect);

					if (showText)
						context.DrawString("Duration", _font, textColor, descRect, _stringLeftFormat);
					y += rectHeight;
					context.DrawLine(linePen, Container.Region.X, y, Container.Region.Width, y);
				}

				context.DrawLine(linePen, 0, firstY - y, maxX, firstY - y);
			}
			catch (Exception e)
			{
				this.LogError("Cluster statistic rendering error ", e);
				throw;
			}
			finally
			{
				context.SetTextRenderingHint(RenderTextRenderingHint.AntiAlias);
				context.ResetClip();
			}
		}

		#endregion

		#region Private methods

		private Color Invert(Color color)
		{
			return Color.FromArgb(255, (byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B));
		}

		private int GetStrCount()
		{
			var height = 0;

			if (ShowAsk)
				height++;

			if (ShowBid)
				height++;

			if (ShowDelta)
				height++;

			if (ShowSessionDelta)
				height++;

			if (ShowSessionDeltaPerVolume)
				height++;

			if (ShowSessionVolume)
				height++;

			if (ShowVolumePerSecond)
				height++;

			if (ShowVolume)
				height++;

			if (ShowDeltaPerVolume)
				height++;

			if (ShowTime)
				height++;

			if (ShowMaximumDelta)
				height++;

			if (ShowMinimumDelta)
				height++;

			if (ShowDuration)
				height++;
			return height;
		}

		private decimal GetRate(decimal value, decimal maximumValue)
		{
			var rate = value * 100.0m / (maximumValue * 0.6m);

			if (rate < 10)
				rate = 10;

			if (rate > 100)
				return 100;

			return rate;
		}

		private System.Drawing.Color Blend(Color color, Color backColor, decimal amount)
		{
			var r = (byte)(color.R + (backColor.R - color.R) * (1 - amount * 0.01m));
			var g = (byte)(color.G + (backColor.G - color.G) * (1 - amount * 0.01m));
			var b = (byte)(color.B + (backColor.B - color.B) * (1 - amount * 0.01m));
			return System.Drawing.Color.FromArgb(255, r, g, b);
		}

		#endregion
	}
}