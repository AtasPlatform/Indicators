namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Linq;
	using System.Threading;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;
	using OFT.Rendering.Settings;
	using OFT.Rendering.Tools;

	using Color = System.Windows.Media.Color;

	[DisplayName("Order Flow Indicator")]
	[Category("Order Flow")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/461-order-flow-indicator")]
	public class OrderFlow : Indicator
	{
		#region Nested types

		public class Ellipse
		{
			#region Properties

			public int Y { get; set; }

			public int X { get; set; }

			public Color FillBrush { get; set; }

			public decimal Vol { get; set; }

			#endregion
		}

		public class Rect
		{
			#region Properties

			public Rectangle Rectan { get; set; }

			public Color FillBrush { get; set; }

			public string Vol { get; set; }

			#endregion
		}

		public enum VisualType
		{
			[Display(ResourceType = typeof(Resources), Name = "Circles")]
			Circles,

			[Display(ResourceType = typeof(Resources), Name = "Rectangles")]
			Rectangles
		}

		#endregion

		#region Static and constants

		private const int _radius = 2;

		#endregion

		#region Fields

		private readonly RenderFont _font = new RenderFont("Arial", 10);

		private readonly RenderStringFormat _format = new RenderStringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		};

		private readonly List<CumulativeTrade> _trades = new List<CumulativeTrade>();
		private RenderPen _borderPen;
		private bool _combineSmallTrades;
		private int _digitsAfterComma;
		private decimal _filter;
		private DateTime _lastRender = DateTime.Now;

		private RenderPen _linePen;
		private int _offset;
		private string _priceFormat;
		private bool _showSmallTrades;
		private int _spacing;
		private int _speedInterval;
		private Timer _timer;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "VisualMode", GroupName = "Visualization", Order = 10)]
		public VisualType VisMode { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Buys", GroupName = "Visualization", Order = 11)]
		public Color Buys { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Sells", GroupName = "Visualization", Order = 12)]
		public Color Sells { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "TextColor", GroupName = "Visualization", Order = 13)]
		public Color TextColor { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Line", Order = 14)]
		public Color LineColor
		{
			get => _linePen.Color.Convert();
			set => _linePen = new RenderPen(value.Convert(), Width, DashStyle.To());
		}

		[Display(ResourceType = typeof(Resources), Name = "Width", GroupName = "Line", Order = 14)]
		public int Width
		{
			get => (int)_linePen.Width;
			set => _linePen = new RenderPen(LineColor.Convert(), value, DashStyle.To());
		}

		[Display(ResourceType = typeof(Resources), Name = "DashStyle", GroupName = "Line", Order = 14)]
		public LineDashStyle DashStyle
		{
			get => _linePen.DashStyle.To();
			set => _linePen = new RenderPen(LineColor.Convert(), Width, value.To());
		}

		[Display(ResourceType = typeof(Resources), Name = "Spacing", GroupName = "Visualization", Order = 15)]
		public int Spacing
		{
			get => _spacing;
			set
			{
				if (value <= 0)
					return;

				_spacing = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "DoNotShowAboveChart", GroupName = "Visualization", Order = 16)]
		public bool DoNotShowAboveChart { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "SpeedInterval", GroupName = "Visualization", Order = 17)]
		public int SpeedInterval
		{
			get => _speedInterval;
			set
			{
				if (value < 100)
					return;

				_timer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(value));

				_speedInterval = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "DigitsAfterComma", GroupName = "Settings", Order = 20)]
		public int DigitsAfterComma
		{
			get => _digitsAfterComma;
			set
			{
				if (value < 0)
					return;

				_digitsAfterComma = value;

				var priceFormat = " {0:0.";

				for (var i = 0; i < value; i++)
					priceFormat += "0";

				priceFormat += "}";
				_priceFormat = priceFormat;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LinkingToBar", GroupName = "Location", Order = 30)]
		public bool LinkingToBar { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Offset", GroupName = "Location", Order = 31)]
		public int Offset
		{
			get => _offset;
			set
			{
				if (value <= 1)
					return;

				_offset = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Filters", Order = 40)]
		public decimal Filter
		{
			get => _filter;
			set
			{
				_filter = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShowSmallTrades", GroupName = "Filters", Order = 41)]
		public bool ShowSmallTrades
		{
			get => _showSmallTrades;
			set
			{
				_showSmallTrades = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "CombineSmallTrades", GroupName = "Filters", Order = 42)]
		public bool CombineSmallTrades
		{
			get => _combineSmallTrades;
			set
			{
				_combineSmallTrades = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "Alerts", Order = 50)]
		public bool UseAlerts { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "AlertFilter", GroupName = "Alerts", Order = 51)]
		public decimal AlertFilter { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts", Order = 52)]
		public string AlertFile { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "BackGround", GroupName = "Alerts", Order = 53)]
		public Color AlertColor { get; set; }

		#endregion

		#region ctor

		public OrderFlow()
			: base(true)
		{
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);
			VisMode = VisualType.Circles;
			Buys = Color.FromArgb(255, 106, 214, 106);
			Sells = Color.FromArgb(255, 240, 122, 125);
			LineColor = Colors.Black;
			DashStyle = LineDashStyle.Solid;
			Width = 1;
			TextColor = Colors.Black;
			_spacing = 8;
			_speedInterval = 300;
			_priceFormat = "{0:0.##}";
			_digitsAfterComma = 0;
			_offset = 100;
			_filter = 10;
			_showSmallTrades = true;
			AlertFile = "alert2";
			AlertColor = Colors.Black;

			_linePen = new RenderPen(LineColor.Convert(), Width, DashStyle.To());
			DataSeries[0].IsHidden = true;
			DrawAbovePrice = true;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
		}

		protected override void OnCumulativeTrade(CumulativeTrade trade)
		{
			lock (_trades)
			{
				_trades.Add(trade);

				if (UseAlerts && trade.Volume > AlertFilter)
					AddTradeAlert(trade);

				CleanUpTrades();
			}
		}

		protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
		{
			lock (_trades)
			{
				if (_trades.Any(x => x != null))
				{
					var lastTrade = _trades.Last(x => x != null);

					if (trade.IsEqual(lastTrade))
						_trades[_trades.Count - 1] = trade;
					else
						_trades.Add(trade);
				}
				else
					_trades.Add(trade);
			}

			if (UseAlerts && trade.Volume > AlertFilter)
				AddTradeAlert(trade);
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			lock (_trades)
			{
				if (_trades.Count(x => x != null) == 0)
					return;
			}

			var textColor = TextColor.Convert();
			var barsWidth = ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0);
			var minX = DoNotShowAboveChart ? ChartInfo.GetXByBar(LastVisibleBarNumber) + barsWidth : 0;

			var x1 = Container.Region.Width;

			if (LinkingToBar)
				x1 = ChartInfo.GetXByBar(LastVisibleBarNumber);
			var points = new List<Point>();
			var ellipses = new List<Ellipse>();
			var rects = new List<Rect>();
			CumulativeTrade lastTrade = default;
			var currentX = x1 - _offset;
			var j = -1;
			var firstY = 0;

			lock (_trades)
			{
				for (var i = _trades.Count - 1; i >= 0; i--)
				{
					if (_trades[i] == default)
					{
						currentX -= 2;
						continue;
					}

					if (!ShowSmallTrades && _trades[i].Volume < Filter)
						continue;

					if (CombineSmallTrades && _trades[i].Volume < Filter && lastTrade != default)
					{
						switch (VisMode)
						{
							case VisualType.Circles when lastTrade.FirstPrice == _trades[i].FirstPrice:
								lastTrade = _trades[i];
								continue;
							case VisualType.Rectangles when lastTrade.FirstPrice == _trades[i].FirstPrice && lastTrade.Lastprice == _trades[i].Lastprice:
								lastTrade = _trades[i];
								continue;
						}
					}

					lastTrade = _trades[i];
					j++;
					var lastX = 0;
					var fillColor = _trades[i].Direction == TradeDirection.Sell ? Sells : Buys;

					if (VisMode == VisualType.Circles)
					{
						lastX = currentX - j * Spacing;

						if (lastX < minX)
							break;

						var lastY = ChartInfo.GetYByPrice(_trades[i].FirstPrice, false);

						if (firstY == 0)
							firstY = lastY;

						if (firstY + 1 > Container.Region.Height)
							firstY = Container.Region.Height;

						var y = lastY;

						if (y + 1 > Container.Region.Height)
							y = Container.Region.Height;

						points.Add(new Point(lastX, y));

						ellipses.Add(new Ellipse
						{
							FillBrush = fillColor,
							X = lastX,
							Y = lastY,
							Vol = _trades[i].Volume >= Filter ? _trades[i].Volume : 0
						});

						if (_trades[i].Volume >= Filter)
							j++;
					}
					else
					{
						var width = 3;
						var vol = "";

						if (_trades[i].Volume >= Filter)
						{
							vol = string.Format(_priceFormat, _trades[i].Volume);
							width = context.MeasureString(vol, _font).Width;
						}

						currentX -= width + Spacing;

						if (currentX < minX)
							break;

						var y1 = ChartInfo.GetYByPrice(Math.Min(_trades[i].FirstPrice, _trades[i].Lastprice));
						var y2 = ChartInfo.GetYByPrice(Math.Max(_trades[i].FirstPrice, _trades[i].Lastprice));
						var height = Math.Max(11, y2 - y1);

						rects.Add(new Rect
						{
							FillBrush = fillColor,
							Rectan = new Rectangle(currentX, y1, width, height),
							Vol = vol
						});
					}

					if (lastX < 0)
						break;
				}
			}

			if (points.Count > 2)
				points.Insert(0, new Point(x1 - Offset, firstY));

			if (VisMode == VisualType.Circles)
			{
				if (points.Count > 3)
					context.DrawLines(_linePen, points.ToArray());

				foreach (var ellipse in ellipses)
				{
					if (ellipse == null)
						continue;

					if (ellipse.Y + 1 > Container.Region.Height)
						continue;

					var ellipseColor = ellipse.FillBrush.Convert();
					var ellipseRect = new Rectangle(ellipse.X - _radius, ellipse.Y - _radius, 2 * _radius, 2 * _radius);
					context.FillEllipse(ellipseColor, ellipseRect);
				}

				ellipses.RemoveAll(x => x == null);
				ellipses.RemoveAll(x => x.Vol == 0);

				for (var i = ellipses.Count - 1; i >= 0; i--)
				{
					if (ellipses[i].Y + 1 > Container.Region.Height)
						continue;

					var str = string.Format(_priceFormat, ellipses[i].Vol);
					var radius = (int)(context.MeasureString(str, _font).Width * 0.6);
					var rect = new Rectangle(ellipses[i].X - _radius, ellipses[i].Y - radius, 2 * radius, 2 * radius);
					context.FillEllipse(ellipses[i].FillBrush.Convert(), rect);
					context.DrawString(str, _font, textColor, rect, _format);
				}
			}
			else
			{
				for (var i = rects.Count - 1; i >= 0; i--)
				{
					if (rects[i].Rectan.Y + 1 > Container.Region.Height)
						continue;

					context.FillRectangle(rects[i].FillBrush.Convert(), rects[i].Rectan);

					if (rects[i].Vol != "" && rects[i].Rectan.Height > 10)
					{
						var x = (rects[i].Rectan.Right + rects[i].Rectan.Left) / 2 - 2;
						var y = (rects[i].Rectan.Top + rects[i].Rectan.Bottom) / 2;
						context.DrawString(rects[i].Vol, _font, textColor, x, y, _format);
					}
				}
			}
		}

		protected override void OnInitialize()
		{
			_timer = new Timer(
				e =>
				{
					if (_lastRender.AddMilliseconds(_speedInterval) < DateTime.Now)
					{
						lock (_trades)
						{
							_trades.Add(null);
							CleanUpTrades();
						}

						RedrawChart(new RedrawArg(Container.Region));
						_lastRender = DateTime.Now;
					}
				},
				null,
				TimeSpan.Zero,
				TimeSpan.FromMilliseconds(_speedInterval));
		}

		protected override void OnDispose()
		{
			_timer?.Dispose();
		}

		#endregion

		#region Private methods

		private void AddTradeAlert(CumulativeTrade trade)
		{
			var message = $"New Big Trade Vol={trade.Volume} at {trade.FirstPrice} ({nameof(trade.Direction)})";
			AddAlert(AlertFile, InstrumentInfo.Instrument, message, AlertColor, Colors.White);
		}

		private void CleanUpTrades()
		{
			if (_trades.Count > 2000)
				_trades.RemoveRange(0, 1000);
		}

		#endregion
	}
}