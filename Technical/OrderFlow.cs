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

	using Color = System.Drawing.Color;

	[DisplayName("Order Flow Indicator")]
	[Category("Order Flow")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/461-order-flow-indicator")]
	public class OrderFlow : Indicator
	{
		#region Nested types

		private class Ellipse
		{
			#region Properties

			public int Y { get; set; }

			public int X { get; set; }

			public Color FillBrush { get; set; }

			public decimal Volume { get; set; }

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

		private readonly RenderStringFormat _format = new()
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		};

		private readonly List<CumulativeTrade> _trades = new();
		private bool _alertRaised;
		private bool _combineSmallTrades;
		private int _digitsAfterComma;
		private decimal _filter;
		private DateTime _lastRender = DateTime.Now;
		private int _offset;
		private string _priceFormat;
		private bool _showSmallTrades;
		private int _size;
		private int _spacing;
		private int _speedInterval;
		private Timer _timer;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "VisualMode", GroupName = "Mode", Order = 100)]
		public VisualType VisMode { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Buys", GroupName = "Visualization", Order = 110)]
		public System.Windows.Media.Color Buys { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Sells", GroupName = "Visualization", Order = 120)]
		public System.Windows.Media.Color Sells { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Font", GroupName = "Visualization", Order = 130)]
		public FontSetting Font { get; set; } = new("Arial", 10);

		[Display(ResourceType = typeof(Resources), Name = "TextColor", GroupName = "Visualization", Order = 135)]
		public System.Windows.Media.Color TextColor { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Line", GroupName = "Visualization", Order = 140)]
		public PenSettings LineColor { get; set; } = new();

		[Display(ResourceType = typeof(Resources), Name = "Border", GroupName = "Visualization", Order = 141)]
		public PenSettings BorderColor { get; set; } = new();

		[Display(ResourceType = typeof(Resources), Name = "Spacing", GroupName = "Visualization", Order = 150)]
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

		[Display(ResourceType = typeof(Resources), Name = "Size", GroupName = "Visualization", Order = 160)]
		public int Size
		{
			get => _size;
			set
			{
				if (value <= 0)
					return;

				_size = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "DoNotShowAboveChart", GroupName = "Visualization", Order = 161)]
		public bool DoNotShowAboveChart { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "SpeedInterval", GroupName = "Visualization", Order = 170)]
		[Range(100,10000)]
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

		[Display(ResourceType = typeof(Resources), Name = "DigitsAfterComma", GroupName = "Settings", Order = 200)]
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

		[Display(ResourceType = typeof(Resources), Name = "LinkingToBar", GroupName = "Location", Order = 300)]
		public bool LinkingToBar { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Offset", GroupName = "Location", Order = 310)]
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

		[Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Filters", Order = 400)]
		public decimal Filter
		{
			get => _filter;
			set
			{
				_filter = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShowSmallTrades", GroupName = "Filters", Order = 410)]
		public bool ShowSmallTrades
		{
			get => _showSmallTrades;
			set
			{
				_showSmallTrades = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "CombineSmallTrades", GroupName = "Filters", Order = 420)]
		public bool CombineSmallTrades
		{
			get => _combineSmallTrades;
			set
			{
				_combineSmallTrades = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "Alerts", Order = 500)]
		public bool UseAlerts { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "AlertFilter", GroupName = "Alerts", Order = 510)]
		public decimal AlertFilter { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts", Order = 520)]
		public string AlertFile { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "BackGround", GroupName = "Alerts", Order = 530)]
		public System.Windows.Media.Color AlertColor { get; set; }

		#endregion

		#region ctor

		public OrderFlow()
			: base(true)
		{
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);
			VisMode = VisualType.Circles;
			Buys = System.Windows.Media.Color.FromArgb(255, 106, 214, 106);
			Sells = System.Windows.Media.Color.FromArgb(255, 240, 122, 125);
			LineColor.Color = Colors.Black;
			LineColor.LineDashStyle = LineDashStyle.Solid;
			LineColor.Width = 1;
			TextColor = Colors.Black;
			_spacing = 8;
			_size = 10;
			_speedInterval = 300;
			_priceFormat = "{0:0.##}";
			_digitsAfterComma = 0;
			_offset = 100;
			_filter = 10;
			_showSmallTrades = true;
			AlertFile = "alert2";
			AlertColor = Colors.Black;
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
			_alertRaised = false;

			lock (_trades)
			{
				_trades.Add(trade);

				if (UseAlerts && trade.Volume > AlertFilter)
				{
					_alertRaised = true;
					AddTradeAlert(trade);
				}

				CleanUpTrades();
			}
		}

		protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
		{
			lock (_trades)
			{
				if (_trades.Any(x => x != null))
				{
					for (var i = _trades.Count - 1; i >= 0; i--)
					{
						if (_trades[i] == null)
							continue;

						if (!trade.IsEqual(_trades[i]))
							continue;

						_trades.RemoveAt(i);
						_trades.Insert(i, null);
						break;
					}

					_trades.Add(trade);
				}
				else
					_trades.Add(trade);
			}

			if (!_alertRaised && UseAlerts && trade.Volume > AlertFilter)
			{
				_alertRaised = true;
				AddTradeAlert(trade);
			}
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
			CumulativeTrade lastTrade = default;
			var currentX = x1 - _offset;
			var j = -1;
			var firstY = 0;
			var sells = Sells.Convert();
			var buys = Buys.Convert();
			var border = BorderColor.RenderObject;

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

					var fillColor = _trades[i].Direction == TradeDirection.Sell ? sells : buys;

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
						Volume = _trades[i].Volume >= Filter ? _trades[i].Volume : 0
					});

					if (_trades[i].Volume >= Filter)
						j++;

					if (lastX < 0)
						break;
				}
			}

			if (points.Count > 2)
			{
				points.Insert(0, new Point(x1 - Offset, firstY));
				context.DrawLines(LineColor.RenderObject, points.ToArray());
			}

			foreach (var ellipse in ellipses)
			{
				if (ellipse == null)
					continue;

				if (ellipse.Y + 1 > Container.Region.Height)
					continue;

				var ellipseRect = new Rectangle(ellipse.X - _radius, ellipse.Y - _radius, 2 * _radius, 2 * _radius);

				if (VisMode == VisualType.Circles)
				{
					context.FillEllipse(ellipse.FillBrush, ellipseRect);
					context.DrawEllipse(border, ellipseRect);
				}
				else
				{
					context.FillRectangle(ellipse.FillBrush, ellipseRect);
					context.DrawRectangle(border, ellipseRect);
				}
			}

			ellipses.RemoveAll(x => x == null || x.Volume == 0);

			for (var i = ellipses.Count - 1; i >= 0; i--)
			{
				if (ellipses[i].Y + 1 > Container.Region.Height)
					continue;

				var str = string.Format(_priceFormat, ellipses[i].Volume);

				var width = context.MeasureString(str, Font.RenderObject).Width;
				var height = context.MeasureString(str, Font.RenderObject).Height;
				var objSize = Math.Max(width, height) + _size;
				var radius = objSize / 2;
				var rect = new Rectangle(ellipses[i].X - radius, ellipses[i].Y - radius, objSize, objSize);

				if (VisMode == VisualType.Circles)
				{
					context.FillEllipse(ellipses[i].FillBrush, rect);
					context.DrawEllipse(border, rect);
				}
				else
				{
					context.FillRectangle(ellipses[i].FillBrush, rect);
					context.DrawRectangle(border, rect);
				}

				context.DrawString(str, Font.RenderObject, textColor, rect, _format);
			}
		}

		protected override void OnInitialize()
		{
			_timer = new Timer(
				e =>
				{
					if (_lastRender.AddMilliseconds(_speedInterval) >= DateTime.Now)
						return;

					lock (_trades)
					{
						_trades.Add(null);
						CleanUpTrades();
					}

					RedrawChart(new RedrawArg(Container.Region));
					_lastRender = DateTime.Now;
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
			var message = $"BigTrade volume is greater than {AlertFilter}. {trade.Direction} at {trade.FirstPrice}";
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