﻿namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

[DisplayName("Spread Volumes Indicator")]
[Category("Order Flow")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.SpreadVolumeDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602630")]
public class SpreadVolume : Indicator
{
	#region Nested types

	private class SpreadIndicatorItem
	{
		#region Properties

		public decimal BidPrice { get; }

		public decimal AskPrice { get; }

		public decimal BidVol { get; set; }

		public decimal AskVol { get; set; }

		#endregion

		#region ctor

		public SpreadIndicatorItem(decimal bidPrice, decimal askPrice)
		{
			BidPrice = bidPrice;
			AskPrice = askPrice;
		}

		#endregion
	}

	#endregion

	#region Fields

	private readonly RenderFont _font = new("Arial", 10);
	private readonly List<SpreadIndicatorItem> _prints = new();
	private readonly object _syncRoot = new();

	private readonly RenderStringFormat _textFormat = new()
	{
		Alignment = StringAlignment.Center,
		LineAlignment = StringAlignment.Center
	};

	private decimal _askPrice;
	private decimal _bidPrice;

	private Color _buyColor = Color.Green;
	private SpreadIndicatorItem _currentTrade;
	private CumulativeTrade _lastTrade;

	private int _offset = 1;
	private Color _sellColor = Color.Red;
	private int _spacing;
	private Color _textColor = Color.Black;
	private int _width = 20;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BuyColor), GroupName = nameof(Strings.Colors), Description = nameof(Strings.BuySignalColorDescription), Order = 1)]
	public CrossColor BuyColor
	{
		get => _buyColor.Convert();
		set => _buyColor = value.Convert();
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SellColor), GroupName = nameof(Strings.Colors), Description = nameof(Strings.SellSignalColorDescription), Order = 3)]
	public CrossColor SellColor
	{
		get => _sellColor.Convert();
		set => _sellColor = value.Convert();
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextColor), GroupName = nameof(Strings.Colors), Description = nameof(Strings.LabelTextColorDescription), Order = 4)]
	public CrossColor TextColor
	{
		get => _textColor.Convert();
		set => _textColor = value.Convert();
	}

	[Range(0, int.MaxValue)]
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Spacing), GroupName = nameof(Strings.Common), Description = nameof(Strings.SpaceBetweenLabelsDescription))]
	public int Spacing
	{
		get => _spacing;
		set
		{
			_spacing = value;
			RedrawChart();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Offset), GroupName = nameof(Strings.Common), Description = nameof(Strings.LabelOffsetXDescription))]
	public int Offset
	{
		get => _offset;
		set
		{
			_offset = value;
			RedrawChart();
		}
	}

    [Range(10, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.Common), Description = nameof(Strings.LabelWidthDescription))]
	public int Width
	{
		get => _width;
		set
		{
			_width = value;
			RedrawChart();
		}
	}

	#endregion

	#region ctor

	public SpreadVolume()
		: base(true)
	{
		DenyToChangePanel = true;
		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

		EnableCustomDrawing = true;
		DrawAbovePrice = true;
		SubscribeToDrawingEvents(DrawingLayouts.LatestBar);
	}

    #endregion

    #region Protected methods

    protected override void OnApplyDefaultColors()
    {
	    if (ChartInfo is null)
		    return;

	    BuyColor = ChartInfo.ColorsStore.FootprintAskColor.Convert();
	    SellColor = ChartInfo.ColorsStore.FootprintBidColor.Convert();
		TextColor = ChartInfo.ColorsStore.FootprintMaximumVolumeTextColor.Convert();
    }

    protected override void OnCumulativeTrade(CumulativeTrade trade)
	{
		if (trade.Direction == TradeDirection.Between)
			return;

		_lastTrade = trade;

		if (trade.PreviousAsk.Price != _askPrice || trade.PreviousBid.Price != _bidPrice || _currentTrade == null)
		{
			_askPrice = trade.PreviousAsk.Price;
			_bidPrice = trade.PreviousBid.Price;
			_currentTrade = new SpreadIndicatorItem(_bidPrice, _askPrice);

			lock (_syncRoot)
			{
				_prints.Add(_currentTrade);

				if (_prints.Count > 200)
					_prints.RemoveRange(0, 100);
			}
		}

		if (trade.Direction == TradeDirection.Buy)
			_currentTrade.AskVol += trade.Volume;
		else if (trade.Direction == TradeDirection.Sell)
			_currentTrade.BidVol += trade.Volume;
	}

	protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
	{
		if (_currentTrade == null || _lastTrade == null)
			return;

		var diff = trade.Volume - _lastTrade.Volume;

		if (diff <= 0)
			return;

		if (trade.Direction == TradeDirection.Buy)
			_currentTrade.AskVol += diff;
		else if (trade.Direction == TradeDirection.Sell)
			_currentTrade.BidVol += diff;

		_lastTrade = trade;
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		var j = -1;
		var firstBarX = ChartInfo.PriceChartContainer.GetXByBar(CurrentBar - 1);

		lock (_syncRoot)
		{
			for (var i = _prints.Count - 1; i >= 0; i--)
			{
				j++;
				var trade = _prints[i];

				var x = firstBarX - j * (Spacing + Width) - Offset;

				if (x < 0)
					return;

				var y1 = ChartInfo.PriceChartContainer.GetYByPrice(trade.AskPrice, true);
				var h = y1 - ChartInfo.PriceChartContainer.GetYByPrice(trade.AskPrice + InstrumentInfo.TickSize, true);

				if (h == 0)
					continue;

				var y2 = ChartInfo.PriceChartContainer.GetYByPrice(trade.BidPrice, true);

				var rect1 = new Rectangle(x, y1, Width, h);
				var rect2 = new Rectangle(x, y2, Width, h);

				if (trade.AskVol != 0)
				{
					context.FillRectangle(_buyColor, rect1);
					context.DrawString(trade.AskVol.ToString(), _font, _textColor, rect1, _textFormat);
				}

				if (trade.BidVol != 0)
				{
					context.FillRectangle(_sellColor, rect2);
					context.DrawString(trade.BidVol.ToString(), _font, _textColor, rect2, _textFormat);
				}
			}
		}
	}

	protected override void OnCalculate(int bar, decimal value)
	{
	}

	#endregion
}