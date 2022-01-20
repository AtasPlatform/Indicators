namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Globalization;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Windows.Media;
	using System.Windows.Shapes;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	using Utils.Common;
	using Utils.Common.Collections;

	using Color = System.Drawing.Color;
	using Rectangle = System.Drawing.Rectangle;

	[Category("Other")]
	[DisplayName("Cumulative Depth Of Market")]
	public class CumulativeDom : Indicator
	{
		#region Nested types

		public class DepthLevel
		{
			public TradeDirection Type { get; set; }

			public decimal Volume { get; set; }
		}
		
		#endregion

		#region Static and constants

		private const int _fontSize = 10;

		#endregion

		#region Fields

		private readonly RedrawArg _emptyRedrawArg = new(new Rectangle(0, 0, 0, 0));

		private readonly RenderStringFormat _stringRightFormat = new()
		{
			Alignment = StringAlignment.Far,
			LineAlignment = StringAlignment.Center,
			Trimming = StringTrimming.EllipsisCharacter,
			FormatFlags = StringFormatFlags.NoWrap
		};
		
		private Color _askColor;
		private Color _bidColor;

		private RenderFont _font = new("Arial", _fontSize);
		private object _locker = new();

		private decimal _maxBid;
		private decimal _maxPrice;

		private decimal _maxVolume;

		private List<MarketDataArg> _mDepth = new();
		private ConcurrentDictionary<decimal, DepthLevel> _renderDepth = new();
		private decimal _minAsk;
		private decimal _minPrice;

		private int _proportionVolume;
		private Color _textColor;
		private int _width;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "UseAutoSize", GroupName = "HistogramSize", Order = 100)]
		public bool UseAutoSize { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ProportionVolume", GroupName = "HistogramSize", Order = 110)]
		public int ProportionVolume
		{
			get => _proportionVolume;
			set
			{
				if (value < 0)
					return;

				_proportionVolume = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Width", GroupName = "HistogramSize", Order = 120)]
		public int Width
		{
			get => _width;
			set
			{
				if (value < 0)
					return;

				_width = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "RightToLeft", GroupName = "HistogramSize", Order = 130)]
		public bool RightToLeft { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "BidRows", GroupName = "Colors", Order = 200)]
		public System.Windows.Media.Color BidRows
		{
			get => _bidColor.Convert();
			set => _bidColor = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "TextColor", GroupName = "Colors", Order = 210)]
		public System.Windows.Media.Color TextColor
		{
			get => _textColor.Convert();
			set => _textColor = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "AskRows", GroupName = "Colors", Order = 220)]
		public System.Windows.Media.Color AskRows
		{
			get => _askColor.Convert();
			set => _askColor = value.Convert();
		}
		
		#endregion

		#region ctor

		public CumulativeDom()
			: base(true)
		{
			DrawAbovePrice = true;
			DenyToChangePanel = true;

			DataSeries[0].IsHidden = true;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);

			UseAutoSize = true;
			ProportionVolume = 100;
			Width = 100;
			RightToLeft = true;

			BidRows = Colors.Green;
			TextColor = Colors.White;
			AskRows = Colors.Red;

		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());

				lock (_locker)
				{
					_mDepth = MarketDepthInfo
						.GetMarketDepthSnapshot()
						.ToList();

					if (!_mDepth.Any())
						return;

					_minAsk = _mDepth
						.Where(x => x.Direction == TradeDirection.Buy)
						.OrderBy(x => x.Price)
						.First()
						.Price;

					_maxBid = _mDepth
						.Where(x => x.Direction == TradeDirection.Sell)
						.OrderByDescending(x => x.Price)
						.First()
						.Price;
					
					_minPrice = _mDepth.Min(x => x.Price);
					_maxPrice = _mDepth.Max(x => x.Price);

					_renderDepth.Clear();
					var lastVolume = 0m;
					for (var i = _minAsk; i <= _maxPrice; i += InstrumentInfo.TickSize)
					{
						var volume = _mDepth.FirstOrDefault(x => x.Price == i)?.Volume ?? 0;

						var curVolume = volume + lastVolume;
						_renderDepth.TryAdd(i, new DepthLevel() { Type = TradeDirection.Buy, Volume = curVolume });

						lastVolume = curVolume;
					}

					lastVolume = 0m;

					for (var i = _maxBid; i >= _minPrice; i -= InstrumentInfo.TickSize)
					{
						var volume = _mDepth.FirstOrDefault(x => x.Price == i)?.Volume ?? 0;

						var curVolume = volume + lastVolume;
						_renderDepth.TryAdd(i, new DepthLevel() { Type = TradeDirection.Sell, Volume = curVolume });

						lastVolume = curVolume;
					}

					_maxVolume = Math.Max(_renderDepth[_maxPrice].Volume, _renderDepth[_minPrice].Volume);
				}
			}
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if (ChartInfo.PriceChartContainer.TotalBars == -1)
				return;

			var depth = _renderDepth.MemberwiseClone();

			if (!depth.Any())
				return;

			var height = (int)Math.Floor(ChartInfo.PriceChartContainer.PriceRowHeight) - 1;

			height = height < 1 ? 1 : height;
			
			var textAutoSize = GetTextSize(context, height);

			lock (_locker)
			{
				if (depth.Any(x => x.Value.Type is TradeDirection.Buy))
				{
					var polygon = new List<Point>();

					var maxPrice = depth.Max(x => x.Key);

					var yTop = ChartInfo.GetYByPrice(maxPrice);
					var yBot = ChartInfo.GetYByPrice(_minAsk - InstrumentInfo.TickSize);

					polygon.Add(new Point(Container.Region.Width, yTop));
					polygon.Add(new Point(Container.Region.Width, yBot));

					var minPrice = depth.Where(x => x.Value.Type is TradeDirection.Buy).Min(x => x.Key);
					for (var i = minPrice; i <= maxPrice; i += InstrumentInfo.TickSize)
					{
						var volume = depth[i].Volume;
						var levelWidth = _maxVolume == 0 ? 0 : (int)(Width * volume / _maxVolume);

						var point = new Point(Container.Region.Width - levelWidth, ChartInfo.GetYByPrice(i));

						if (polygon.Count > 2 && point.X == polygon[polygon.Count - 1].X)
						{
							polygon[polygon.Count - 1] = point;
						}
						else
						{
							polygon.Add(new Point(point.X, polygon[polygon.Count - 1].Y));
							polygon.Add(point);
						}
					}

					context.FillPolygon(_askColor, polygon.ToArray());

					for (var i = minPrice; i <= maxPrice; i += InstrumentInfo.TickSize)
					{
						var renderText = depth[i].Volume.ToString(CultureInfo.InvariantCulture);
						_font = new RenderFont("Arial", textAutoSize);

						var y = ChartInfo.GetYByPrice(i);

						var textWidth = context.MeasureString(renderText, _font).Width;

						var textRect = new Rectangle(new Point(Container.Region.Width - textWidth, y),
							new Size(textWidth, height));


						context.DrawString(renderText,
							_font,
							_textColor,
							textRect,
							_stringRightFormat);
					}
				}
			}

			lock (_locker)
			{
				if (depth.Any(x => x.Value.Type is TradeDirection.Sell))
				{
					var polygon = new List<Point>();

					var minPrice = depth.Min(x => x.Key);

					var yBot = ChartInfo.GetYByPrice(minPrice - InstrumentInfo.TickSize);
					var yTop = ChartInfo.GetYByPrice(_maxBid);

					polygon.Add(new Point(Container.Region.Width, yBot));
					polygon.Add(new Point(Container.Region.Width, yTop));

					var maxPrice = depth.Where(x => x.Value.Type is TradeDirection.Sell).Max(x => x.Key);
					
					for (var i = maxPrice; i >= minPrice; i -= InstrumentInfo.TickSize)
					{
						var volume = depth[i].Volume;
						var levelWidth = _maxVolume == 0 ? 0 : (int)(Width * volume / _maxVolume);

						var point = new Point(Container.Region.Width - levelWidth, ChartInfo.GetYByPrice(i - InstrumentInfo.TickSize));

						if (polygon.Count > 2 && point.X == polygon[polygon.Count - 1].X)
						{
							polygon[polygon.Count - 1] = point;
						}
						else
						{
							polygon.Add(new Point(point.X, polygon[polygon.Count - 1].Y));
							polygon.Add(point);
						}
					}

					context.FillPolygon(_bidColor, polygon.ToArray());

					for (var i = maxPrice; i >= minPrice; i -= InstrumentInfo.TickSize)
					{
						var renderText = depth[i].Volume.ToString(CultureInfo.InvariantCulture);
						_font = new RenderFont("Arial", textAutoSize);

						var y = ChartInfo.GetYByPrice(i);

						var textWidth = context.MeasureString(renderText, _font).Width;

						var textRect = new Rectangle(new Point(Container.Region.Width - textWidth, y),
							new Size(textWidth, height));

						context.DrawString(renderText,
							_font,
							_textColor,
							textRect,
							_stringRightFormat);
					}
				}
			}
		}

		protected override void OnBestBidAskChanged(MarketDataArg depth)
		{
			if (depth.DataType is MarketDataType.Ask)
			{
				_minAsk = depth.Price;
			}
			else
			{
				_maxBid = depth.Price;
			}

			RedrawChart(_emptyRedrawArg);
		}

		protected override void MarketDepthChanged(MarketDataArg depth)
		{
			lock (_locker)
			{
				_mDepth.RemoveAll(x => x.Price == depth.Price);
				_renderDepth.TryRemove(depth.Price, out _);

				if (depth.Volume != 0)
				{
					_mDepth.Add(depth);
				}

				InsertLevel(depth);

				if (!_mDepth.Any())
					return;

				if (depth.Volume == 0)
				{
					if (depth.Price == _maxPrice)
						_maxPrice = _mDepth
							.OrderByDescending(x => x.Price)
							.First().Price;

					if (depth.Price == _minPrice)
						_minPrice = _mDepth
							.OrderBy(x => x.Price)
							.First().Price;
				}

				_maxVolume = _renderDepth.Max(x => x.Value.Volume);
			}

			RedrawChart(_emptyRedrawArg);
		}

		private void InsertLevel(MarketDataArg depth)
		{
			if (depth.Direction is TradeDirection.Buy)
			{
				_renderDepth.RemoveWhere(x => x.Value.Type is TradeDirection.Buy);
				if (!_mDepth.Any(x => x.DataType is MarketDataType.Ask))
					return;

				var prevVolume = 0m;
				for (var i = _minAsk; i <= _mDepth.Max(x => x.Price); i += InstrumentInfo.TickSize)
				{
					var curVolume = prevVolume + (_mDepth.FirstOrDefault(x => x.Price == i)?.Volume ?? 0);

					var newLevel = new DepthLevel() { Type = TradeDirection.Buy, Volume = curVolume };

					if (_renderDepth.ContainsKey(i))
					{
						_renderDepth[i] = newLevel;
					}
					else
					{
						_renderDepth.TryAdd(i, newLevel);
					}

					prevVolume = curVolume;
				}
			}
			else
			{
				_renderDepth.RemoveWhere(x => x.Value.Type is TradeDirection.Sell);
				if (!_mDepth.Any(x => x.DataType is MarketDataType.Bid))
					return;

				var prevVolume = 0m;
				for (var i = _maxBid; i >= _mDepth.Min(x => x.Price); i -= InstrumentInfo.TickSize)
				{
					var curVolume = prevVolume + (_mDepth.FirstOrDefault(x => x.Price == i)?.Volume ?? 0);

					var newLevel = new DepthLevel() { Type = TradeDirection.Sell, Volume = curVolume };

					if (_renderDepth.ContainsKey(i))
					{
						_renderDepth[i] = newLevel;
					}
					else
					{
						_renderDepth.TryAdd(i, newLevel);
					}

					prevVolume = curVolume;
				}
			}

			_renderDepth.RemoveWhere(x => x.Value.Volume == 0);
		}

		#endregion

		#region Private methods

		private int GetTextSize(RenderContext context, int height)
		{
			for (var i = _fontSize; i > 0; i--)
			{
				if (context.MeasureString("12", new RenderFont("Arial", i)).Height < height + 5)
					return i;
			}

			return 0;
		}

		#endregion
	}
}