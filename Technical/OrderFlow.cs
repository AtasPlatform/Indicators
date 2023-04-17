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

	using Utils.Common.Logging;

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

		public enum TradesType
		{
			[Display(ResourceType = typeof(Resources), Name = "CumulativeTrades")]
			Cumulative,

			[Display(ResourceType = typeof(Resources), Name = "SeparatedTrades")]
			Separated
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

		private readonly List<MarketDataArg> _singleTrades = new();

		private readonly List<CumulativeTrade> _trades = new();
		private bool _alertRaised;
		private bool _combineSmallTrades;
		private decimal _filter = 10;
        private DateTime _lastRender = DateTime.Now;
		private object _locker = new();
		private int _offset = 100;
        private string _priceFormat = "{0:0.##}";
        private bool _showSmallTrades = true;
        private int _size = 10;
        private int _spacing = 8;
        private int _speedInterval = 300;
        private Timer _timer;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "VisualMode", GroupName = "Mode", Order = 100)]
		public VisualType VisMode { get; set; } = VisualType.Circles;

		[Display(ResourceType = typeof(Resources), Name = "Trades", GroupName = "Mode", Order = 100)]
		public TradesType TradesMode { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Buys", GroupName = "Visualization", Order = 110)]
		public System.Windows.Media.Color Buys { get; set; } = System.Windows.Media.Color.FromArgb(255, 106, 214, 106);

        [Display(ResourceType = typeof(Resources), Name = "Sells", GroupName = "Visualization", Order = 120)]
		public System.Windows.Media.Color Sells { get; set; } = System.Windows.Media.Color.FromArgb(255, 240, 122, 125);

        [Display(ResourceType = typeof(Resources), Name = "Font", GroupName = "Visualization", Order = 130)]
		public FontSetting Font { get; set; } = new("Arial", 10);

		[Display(ResourceType = typeof(Resources), Name = "TextColor", GroupName = "Visualization", Order = 135)]
		public System.Windows.Media.Color TextColor { get; set; } = Colors.Black;

		[Display(ResourceType = typeof(Resources), Name = "Line", GroupName = "Visualization", Order = 140)]
		public PenSettings LineColor { get; set; } = new()
		{
			Color = Colors.Black,
			LineDashStyle = LineDashStyle.Solid,
			Width = 1
		};

		[Display(ResourceType = typeof(Resources), Name = "Border", GroupName = "Visualization", Order = 141)]
		public PenSettings BorderColor { get; set; } = new();

		[Display(ResourceType = typeof(Resources), Name = "Spacing", GroupName = "Visualization", Order = 150)]
		[Range(1, 300)]
		public int Spacing
		{
			get => _spacing;
			set
			{
				_spacing = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Size", GroupName = "Visualization", Order = 160)]
		[Range(1, 200)]
		public int Size
		{
			get => _size;
			set
			{
				_size = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "DoNotShowAboveChart", GroupName = "Visualization", Order = 161)]
		public bool DoNotShowAboveChart { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "SpeedInterval", GroupName = "Visualization", Order = 170)]
		[Range(100, 10000)]
		public int SpeedInterval
		{
			get => _speedInterval;
			set
			{
				_timer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(value));

				_speedInterval = value;
			}
		}
		
		[Display(ResourceType = typeof(Resources), Name = "LinkingToBar", GroupName = "Location", Order = 300)]
		public bool LinkingToBar { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Offset", GroupName = "Location", Order = 310)]
		[Range(0, 1000)]
		public int Offset
		{
			get => _offset;
			set
			{
				_offset = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Filters", Order = 400)]
		[Range(0, 100000)]
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
		public string AlertFile { get; set; } = "alert2";

        [Display(ResourceType = typeof(Resources), Name = "BackGround", GroupName = "Alerts", Order = 530)]
		public System.Windows.Media.Color AlertColor { get; set; } = Colors.Black;

		#endregion

		#region ctor

		public OrderFlow()
			: base(true)
		{
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			DrawAbovePrice = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
			
			DataSeries[0].IsHidden = true;
		}

		#endregion

		#region Protected methods
		
		protected override void OnApplyDefaultColors()
		{
			if (ChartInfo is null)
				return;

			Buys = ChartInfo.ColorsStore.FootprintAskColor.Convert();
			Sells = ChartInfo.ColorsStore.FootprintBidColor.Convert();
			LineColor.Color = BorderColor.Color = ChartInfo.ColorsStore.FootprintTextColor.Convert();
		}
		
		protected override void OnCalculate(int bar, decimal value)
		{
		}

		protected override void OnNewTrade(MarketDataArg trade)
		{
			if (TradesMode is TradesType.Cumulative)
				return;

			_alertRaised = false;

			lock (_locker)
			{
				_singleTrades.Add(trade);

				if (UseAlerts && trade.Volume > AlertFilter)
				{
					_alertRaised = true;
					AddTradeAlert(trade.Direction, trade.Price);
				}

				CleanUpTrades();
			}
		}

		protected override void OnCumulativeTrade(CumulativeTrade trade)
		{
			if (TradesMode is TradesType.Separated)
				return;

			_alertRaised = false;

			lock (_locker)
			{
				_trades.Add(trade);

				if (UseAlerts && trade.Volume > AlertFilter)
				{
					_alertRaised = true;
					AddTradeAlert(trade.Direction, trade.FirstPrice);
				}

				CleanUpTrades();
			}
		}

		protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
		{
			if (TradesMode is TradesType.Separated)
				return;

			lock (_locker)
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
				AddTradeAlert(trade.Direction, trade.FirstPrice);
			}
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			lock (_locker)
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
			CumulativeTrade lastTrade = null;
			MarketDataArg lastSingleTrade = null;
			var currentX = x1 - _offset;
			var j = -1;
			var firstY = 0;
			var sells = Sells.Convert();
			var buys = Buys.Convert();
			var border = BorderColor.RenderObject;

			lock (_locker)
			{
				var start = TradesMode is TradesType.Cumulative ? _trades.Count - 1 : _singleTrades.Count - 1;

				for (var i = start; i >= 0; i--)
				{
					if (TradesMode is TradesType.Cumulative && _trades[i] == null || TradesMode is TradesType.Separated && _singleTrades[i] == null)
					{
						currentX -= 2;
						continue;
					}

					var volume = TradesMode is TradesType.Cumulative
						? _trades[i].Volume
						: _singleTrades[i].Volume;

					var price = TradesMode is TradesType.Cumulative
						? _trades[i].FirstPrice
						: _singleTrades[i].Price;

					if (!ShowSmallTrades && volume < Filter)
						continue;

					if (CombineSmallTrades && volume < Filter &&
					    (lastTrade != null && TradesMode is TradesType.Cumulative || lastSingleTrade != null && TradesMode is TradesType.Separated)
					   )
					{
						var lastPrice = TradesMode is TradesType.Cumulative
							? lastTrade.FirstPrice
							: lastSingleTrade.Price;

						if (lastPrice == price)

						{
							switch (VisMode)
							{
								case VisualType.Circles when lastPrice == price:
									if (TradesMode is TradesType.Cumulative)
										lastTrade = _trades[i];
									else
										lastSingleTrade = _singleTrades[i];
									continue;
								case VisualType.Rectangles when lastPrice == price:
									if (TradesMode is TradesType.Cumulative)
									{
										if (lastTrade.Lastprice == _trades[i].Lastprice)
											lastTrade = _trades[i];
									}
									else
										lastSingleTrade = _singleTrades[i];

									continue;
							}
						}
					}

					if (TradesMode is TradesType.Cumulative)
						lastTrade = _trades[i];
					else
						lastSingleTrade = _singleTrades[i];

					j++;
					var lastX = 0;

					var direction = TradesMode is TradesType.Cumulative
						? _trades[i].Direction
						: _singleTrades[i].Direction;

					var fillColor = direction is TradeDirection.Sell ? sells : buys;

					lastX = currentX - j * Spacing;

					if (lastX < minX)
						break;

					var lastY = ChartInfo.GetYByPrice(price, false);

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
						Volume = volume >= Filter ? volume : 0
					});

					if (volume >= Filter)
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
					try
					{
						if (_lastRender.AddMilliseconds(_speedInterval) >= DateTime.Now)
							return;

						lock (_locker)
						{
							if (TradesMode is TradesType.Cumulative)
							{
								_trades.Add(null);
								CleanUpTrades();
							}
							else
							{
								_singleTrades.Add(null);
								CleanUpTrades();
							}
						}

						if (Container != null)
							RedrawChart(new RedrawArg(Container.Region));

						_lastRender = DateTime.Now;
					}
					catch (Exception ex)
					{
						this.LogError("Refresh error: ", ex);
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

		private void AddTradeAlert(TradeDirection dir, decimal price)
		{
			var message = $"Trade volume is greater than {AlertFilter}. {dir} at {price}";
			AddAlert(AlertFile, InstrumentInfo.Instrument, message, AlertColor, Colors.White);
		}

		private void CleanUpTrades()
		{
			if (TradesMode is TradesType.Cumulative && _trades.Count > 2000)
			{
				_trades.RemoveRange(0, 1000);
				return;
			}

			if (TradesMode is TradesType.Separated && _singleTrades.Count > 2000)
				_singleTrades.RemoveRange(0, 1000);
		}

		#endregion
	}
}