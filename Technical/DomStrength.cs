﻿namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

[DisplayName("Dom Strength")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.DomStrengthDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602375")]
public class DomStrength : Indicator
{
	#region Fields

	private readonly ValueDataSeries _buySeries = new("BuyValues");
    private readonly ValueDataSeries _sellSeries = new("SellValues");

    private decimal _buyVolume;
	private CumulativeDelta _cDelta = new();
	private decimal _cumAsks;
	private decimal _cumBids;
	private bool _initialized;
	private object _locker = new();
	private SortedList<decimal, decimal> _mDepthAsk = new();
	private SortedList<decimal, decimal> _mDepthBid = new();
	private decimal _percent = 50;
	private int _period = 5;
	private RenderPen _rectPen = new(Color.Black);
	private decimal _sellVolume;
	private List<MarketDataArg> _trades = new();

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.DepthMarketFilter), GroupName = nameof(Strings.Settings), Description = nameof(Strings.DOMMaxFilterDescription), Order = 90)]
	[Range(1, 1000)]
	public FilterInt LevelDepth { get; } = new() { Value = 10 };

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
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
				OnCalculate(CurrentBar - 1, 0);
		}
	}

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Percent), GroupName = nameof(Strings.Settings), Description = nameof(Strings.DOMPercentDescription), Order = 110)]
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
				OnCalculate(CurrentBar - 1, 0);
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color80), GroupName = nameof(Strings.Color), Description = nameof(Strings.PercentColorDescription), Order = 200)]
	public Color Color80 { get; set; } = Color.DarkGreen;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color50), GroupName = nameof(Strings.Color), Description = nameof(Strings.PercentColorDescription), Order = 210)]
	public Color Color50 { get; set; } = Color.LimeGreen;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color20), GroupName = nameof(Strings.Color), Description = nameof(Strings.PercentColorDescription), Order = 220)]
	public Color Color20 { get; set; } = Color.YellowGreen;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ColorMinus20), GroupName = nameof(Strings.Color), Description = nameof(Strings.PercentColorDescription), Order = 230)]
	public Color ColorMinus20 { get; set; } = Color.Orange;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ColorMinus50), GroupName = nameof(Strings.Color), Description = nameof(Strings.PercentColorDescription), Order = 240)]
	public Color ColorMinus50 { get; set; } = Color.PaleVioletRed;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ColorMinus80), GroupName = nameof(Strings.Color), Description = nameof(Strings.PercentColorDescription), Order = 250)]
	public Color ColorMinus80 { get; set; } = Color.Red;

	#endregion

	#region ctor

	public DomStrength()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.LatestBar | DrawingLayouts.Historical);
		DataSeries[0] = _cDelta.DataSeries[1];
		DataSeries[0].Name = "Delta";

		Add(_cDelta);
		LevelDepth.PropertyChanged += FilterDepthChanged;
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
		if (bar == 0)
		{
			lock (_locker)
			{
				_mDepthAsk.Clear();
				_mDepthBid.Clear();
				var depths = MarketDepthInfo.GetMarketDepthSnapshot();

				foreach (var depth in depths)
				{
					if (depth.DataType is MarketDataType.Ask)
						_mDepthAsk[depth.Price] = depth.Volume;
					else
						_mDepthBid[depth.Price] = depth.Volume;
				}
			}
		}

		if (bar != CurrentBar - 1 || !_initialized)
			return;

		CalcCumulativeDepth();

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

		if (LevelDepth.Enabled)
		{
			lock (_locker)
			{
				if (depth.Volume is 0)
				{
					if (depth.DataType is MarketDataType.Ask)
						_mDepthAsk.Remove(depth.Price);
					else
						_mDepthBid.Remove(depth.Price);
				}
				else
				{
					if (depth.DataType is MarketDataType.Ask)
						_mDepthAsk[depth.Price] = depth.Volume;
					else
						_mDepthBid[depth.Price] = depth.Volume;
				}
			}
		}

		CalcCumulativeDepth();

		if (depth.DataType is MarketDataType.Ask)
		{
			var buyRatio = (_cumAsks == 0
				? 0
				: _buyVolume / _cumAsks) * 100;

			_buySeries[CurrentBar - 1] = buyRatio - Percent;
		}

		if (depth.DataType is MarketDataType.Bid)
		{
			var sellRatio = (_cumBids == 0
				? 0
				: _sellVolume / _cumBids) * 100;

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

		var buyRatio = (_cumAsks == 0
			? 0
			: _buyVolume / _cumAsks) * 100;

		var sellRatio = (_cumBids == 0
			? 0
			: _sellVolume / _cumBids) * 100;

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

	private void FilterDepthChanged(object sender, PropertyChangedEventArgs e)
	{
		if (sender is not FilterInt)
			return;

		_buySeries.Clear();
		_sellSeries.Clear();
		RecalculateValues();

		if(Container is not null)
			RedrawChart(new RedrawArg(Container.Region));
	}

	private void CalcCumulativeDepth()
	{
		_cumAsks = MarketDepthInfo.CumulativeDomAsks;
		_cumBids = MarketDepthInfo.CumulativeDomBids;

		if (LevelDepth.Enabled)
		{
			lock (_locker)
			{
				if (_mDepthAsk.Count <= LevelDepth.Value)
				{
					_cumAsks = _mDepthAsk.Values
						.DefaultIfEmpty(0)
						.Sum();
				}
				else
				{
					_cumAsks = 0;

					for (var i = 0; i <= LevelDepth.Value; i++)
						_cumAsks += _mDepthAsk.Values[i];
				}

				if (_mDepthBid.Count <= LevelDepth.Value)
				{
					_cumBids = _mDepthAsk.Values
						.DefaultIfEmpty(0)
						.Sum();
				}
				else
				{
					_cumBids = 0;
					var lastIdx = _mDepthBid.Values.Count - 1;

					for (var i = 0; i <= LevelDepth.Value; i++)
						_cumBids += _mDepthBid.Values[lastIdx - i];
				}
			}
		}
	}

	#endregion
}