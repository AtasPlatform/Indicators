namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Linq;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/53408-dom-strength")]
	[DisplayName("Dom Strength")]
	public class DomStrength : Indicator
	{
		#region Fields

		private ValueDataSeries _buySeries = new("BuyValues");
		private decimal _buyVolume;
		private CumulativeDelta _cDelta = new();
		private bool _initialized;
		private decimal _percent = 50;
		private int _period = 5;
		private RenderPen _rectPen = new(Color.Black);
		private ValueDataSeries _sellSeries = new("SellValues");
		private decimal _sellVolume;
		private List<MarketDataArg> _trades = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 1000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				_buySeries.Clear();
				_sellSeries.Clear();

				if (_buySeries.Count > 0)
					Calculate(CurrentBar - 1, 0);
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Percent", GroupName = "Settings", Order = 110)]
		[Range(0, 100)]
		public decimal Percent
		{
			get => _percent;
			set
			{
				_percent = value;
				_buySeries.Clear();
				_sellSeries.Clear();

				if (_buySeries.Count > 0)
					Calculate(CurrentBar - 1, 0);
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Color80", GroupName = "Color", Order = 200)]
		public Color Color80 { get; set; } = Color.DarkGreen;

		[Display(ResourceType = typeof(Resources), Name = "Color50", GroupName = "Color", Order = 210)]
		public Color Color50 { get; set; } = Color.LimeGreen;

		[Display(ResourceType = typeof(Resources), Name = "Color20", GroupName = "Color", Order = 220)]
		public Color Color20 { get; set; } = Color.YellowGreen;

		[Display(ResourceType = typeof(Resources), Name = "ColorMinus20", GroupName = "Color", Order = 230)]
		public Color ColorMinus20 { get; set; } = Color.Orange;

		[Display(ResourceType = typeof(Resources), Name = "ColorMinus50", GroupName = "Color", Order = 240)]
		public Color ColorMinus50 { get; set; } = Color.PaleVioletRed;

		[Display(ResourceType = typeof(Resources), Name = "ColorMinus80", GroupName = "Color", Order = 250)]
		public Color ColorMinus80 { get; set; } = Color.Red;

		#endregion

		#region ctor

		public DomStrength()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);
			DataSeries[0] = _cDelta.DataSeries[1];
		}

        #endregion

        #region Protected methods
		
        protected override void OnApplyDefaultColors()
        {
	        if (ChartInfo is null)
		        return;

	        var candles = (CandleDataSeries)DataSeries[0];

		    candles.UpCandleColor = ChartInfo.ColorsStore.UpCandleColor.Convert();
		    candles.DownCandleColor = ChartInfo.ColorsStore.DownCandleColor.Convert();
		    candles.BorderColor = ChartInfo.ColorsStore.BarBorderPen.Color.Convert();
        }

        protected override void OnInitialize()
		{
			_trades.Clear();

			RequestForCumulativeTrades(
				new CumulativeTradesRequest(GetCandle(CurrentBar - 1).Time.Date)
			);
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar != CurrentBar - 1 && !_initialized)
				return;

			CalcRatio(bar);
		}

		protected override void OnNewTrade(MarketDataArg trade)
		{
			if (_initialized)
				_trades.Add(trade);

			if (_trades.Count > 100000)
			{
				_trades = _trades
					.Skip(10000)
					.ToList();
			}
		}

		protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
		{
			_trades.AddRange(
				cumulativeTrades.SelectMany(x => x.Ticks)
			);

			_initialized = true;
		}

		protected override void MarketDepthChanged(MarketDataArg depth)
		{
			if (!_initialized)
				return;

			if (depth.DataType is MarketDataType.Ask)
			{
				var buyRatio = (MarketDepthInfo.CumulativeDomAsks == 0
					? 0
					: _buyVolume / MarketDepthInfo.CumulativeDomAsks) * 100;

				_buySeries[CurrentBar - 1] = buyRatio - Percent;
			}

			if (depth.DataType is MarketDataType.Bid)
			{
				var sellRatio = (MarketDepthInfo.CumulativeDomBids == 0
					? 0
					: _sellVolume / MarketDepthInfo.CumulativeDomBids) * 100;

				_sellSeries[CurrentBar - 1] = sellRatio - Percent;
			}
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if (!_initialized)
				return;

			var buyY = Container.Region.Top + 2;
			var sellY = Container.Region.Bottom - 12;

			for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
			{
				if (_buySeries[i] == 0 && _sellSeries[i] == 0)
					continue;

				var x = ChartInfo.PriceChartContainer.GetXByBar(i);

				if (_buySeries[i] != 0)
				{
					var color = GetColor(_buySeries[i]);

					var rect = new Rectangle(x, buyY, (int)ChartInfo.PriceChartContainer.BarsWidth, 10);
					context.FillRectangle(color, rect);
					context.DrawRectangle(_rectPen, rect);
				}

				if (_sellSeries[i] != 0)
				{
					var color = GetColor(_sellSeries[i]);

					var rect = new Rectangle(x, sellY, (int)ChartInfo.PriceChartContainer.BarsWidth, 10);
					context.FillRectangle(color, rect);
					context.DrawRectangle(_rectPen, rect);
				}
			}
		}

		#endregion

		#region Private methods

		private void CalcRatio(int bar)
		{
			var startBar = Math.Max(0, bar - Period);
			var startTime = GetCandle(startBar).Time;

			_buyVolume = _trades
				.Where(x => x.Time >= startTime && x.Direction is TradeDirection.Buy)
				.Sum(x => x.Volume);

			_sellVolume = _trades
				.Where(x => x.Time >= startTime && x.Direction is TradeDirection.Sell)
				.Sum(x => x.Volume);

			var buyRatio = (MarketDepthInfo.CumulativeDomAsks == 0
				? 0
				: _buyVolume / MarketDepthInfo.CumulativeDomAsks) * 100;

			var sellRatio = (MarketDepthInfo.CumulativeDomBids == 0
				? 0
				: _sellVolume / MarketDepthInfo.CumulativeDomBids) * 100;

			_buySeries[bar] = buyRatio - Percent;
			_sellSeries[bar] = sellRatio - Percent;
		}

		private Color GetColor(decimal percent)
		{
			return percent switch
			{
				>= 80 => Color80,
				< 80 and >= 50 => Color50,
				< 50 and >= 20 => Color20,
				< 20 and >= -20 => ColorMinus20,
				< -20 and >= -50 => ColorMinus50,
				_ => ColorMinus80
			};
		}

		#endregion
	}
}