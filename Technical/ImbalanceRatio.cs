namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;

using ATAS.DataFeedsCore;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Utils.Common.Collections;

[DisplayName("Imbalance Ratio")]
[Category(IndicatorCategories.VolumeOrderFlow)]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ImbalanceRatioIndDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000602404")]
public class ImbalanceRatio : Indicator
{
	#region Fields

	private readonly CrossColor _transparent = Color.Transparent.Convert();

	private Color _buyColor = Color.Blue;
	private RenderFont _font = new("Arial", 9);
	private RenderStringFormat _format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
	private bool _ignoreZeroValues;
	private int _imbalanceRatio = 4;
	private int _minimumDifference;
	private PriceSelectionDataSeries _renderSeries = new("RenderSeries", Strings.ImbalanceRange) { IsHidden = true };
	private Color _sellColor = Color.Red;
	private Color _textColor = Color.White;
	private int _transparency = 50;
	private int _volumeFilter;

    #endregion

    #region Properties

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ImbalanceRatio), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MinRatioValueDescription), Order = 100)]
	[Range(1, 10000)]
	public int Ratio
	{
		get => _imbalanceRatio;
		set
		{
			_imbalanceRatio = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VolumeFilter), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MinVolumeFilterDescription), Order = 110)]
	[Range(0, 1000000000)]
	public int VolumeFilter
	{
		get => _volumeFilter;
		set
		{
			_volumeFilter = value;
			RecalculateValues();
		}
	}

	[Parameter]
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ImbalanceDifference), GroupName = nameof(Strings.Settings), Order = 120)]
	[Range(0, 1000000000)]
	public int MinimumDifference
	{
		get => _minimumDifference;
		set
		{
			_minimumDifference = value;
			RecalculateValues();
		}
	}

	[Parameter]
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IgnoreZeroValues), GroupName = nameof(Strings.Settings), Description = nameof(Strings.IgnoreZeroValuesDescription), Order = 130)]
	public bool IgnoreZeroValues
	{
		get => _ignoreZeroValues;
		set
		{
			_ignoreZeroValues = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BuyColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BuySignalColorDescription), Order = 200)]
	public CrossColor BuyColor
	{
		get => _buyColor.Convert();
		set
		{
			_buyColor = value.Convert();

			for (var i = 0; i < _renderSeries.Count; i++)
			{
				_renderSeries[i].ForEach(x =>
				{
					if ((OrderDirections)x.Context == OrderDirections.Buy)
					{
						x.PriceSelectionColor =
                            CrossColor.FromArgb((byte)Math.Floor(255 * _transparency / 100m), value.R, value.G, value.B);
					}
				});
			}
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SellColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.SellSignalColorDescription), Order = 210)]
	public CrossColor SellColor
	{
		get => _sellColor.Convert();
		set
		{
			_sellColor = value.Convert();

			for (var i = 0; i < _renderSeries.Count; i++)
			{
				_renderSeries[i].ForEach(x =>
				{
					if ((OrderDirections)x.Context == OrderDirections.Sell)
					{
						x.PriceSelectionColor =
							CrossColor.FromArgb((byte)Math.Floor(255 * _transparency / 100m), value.R, value.G, value.B);
					}
				});
			}
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.LabelTextColorDescription), Order = 220)]
	public CrossColor TextColor
	{
		get => _textColor.Convert();
		set => _textColor = value.Convert();
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ClusterSelectionTransparency), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.PriceSelectionTransparencyDescription), Order = 230)]
	[Range(0, 100)]
	public int Transparency
	{
		get => _transparency;
		set
		{
			_transparency = value;

			for (var i = 0; i < _renderSeries.Count; i++)
			{
				_renderSeries[i].ForEach(x =>
					x.PriceSelectionColor = CrossColor.FromArgb((byte)Math.Floor(255 * value / 100m), x.PriceSelectionColor.R,
						x.PriceSelectionColor.G, x.PriceSelectionColor.B));
			}
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowTopBlock), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.ShowTopElementsDescription), Order = 240)]
	public bool ShowTop { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowBotBlock), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.ShowBottomElementsDescription), Order = 250)]
	public bool ShowBot { get; set; } = true;

	#endregion

	#region ctor

	public ImbalanceRatio()
		: base(true)
	{
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);

		DataSeries[0] = _renderSeries;
	}

    #endregion

    #region Protected methods

    protected override void OnApplyDefaultColors()
    {
	    if (ChartInfo is null)
		    return;

	    _buyColor = ChartInfo.ColorsStore.FootprintAskColor;
	    _sellColor = ChartInfo.ColorsStore.FootprintBidColor;
	    _textColor = ChartInfo.ColorsStore.FootprintMaximumVolumeTextColor;
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		var barWidth = ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0);
		var priceHeight = ChartInfo.GetYByPrice(0) - ChartInfo.GetYByPrice(InstrumentInfo.TickSize);

		for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
		{
			var candle = GetCandle(i);
			var buyRows = _renderSeries[i].Count(x => (OrderDirections)x.Context == OrderDirections.Buy);
			var sellRows = _renderSeries[i].Count(x => (OrderDirections)x.Context == OrderDirections.Sell);

			var y = ChartInfo.GetYByPrice(
				candle.Delta >= 0
					? candle.Low - 2 * InstrumentInfo.TickSize
					: candle.High + 2 * InstrumentInfo.TickSize);

			if ((candle.Delta >= 0 && !ShowBot) || (candle.Delta < 0 && !ShowTop))
				continue;

			var rect = new Rectangle(ChartInfo.GetXByBar(i), y, barWidth, priceHeight);
			context.FillRectangle(candle.Delta >= 0 ? _buyColor : _sellColor, rect);

			var renderText = $"{buyRows}x{sellRows}";
			context.DrawString(renderText, _font, _textColor, rect, _format);
		}
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		var candle = GetCandle(bar);
		_renderSeries[bar].Clear();

		// Diagonal comparison matching the footprint Bid/Ask imbalance logic:
		// ask at the upper level vs bid one tick below. A missing level counts as zero,
		// a zero denominator counts as an infinite imbalance unless IgnoreZeroValues is set.
		for (var price = candle.High; price > candle.Low; price -= InstrumentInfo.TickSize)
		{
			var upperInfo = candle.GetPriceVolumeInfo(price);
			var lowerInfo = candle.GetPriceVolumeInfo(price - InstrumentInfo.TickSize);

			var ask = upperInfo?.Ask ?? 0;
			var bid = lowerInfo?.Bid ?? 0;

			if (Math.Abs(ask - bid) <= _minimumDifference)
				continue;

			if (_ignoreZeroValues && (ask == 0 || bid == 0))
				continue;

			if (ask >= _volumeFilter && (bid == 0 || ask / bid > _imbalanceRatio))
				AddImbalance(bar, price, OrderDirections.Buy);

			if (bid >= _volumeFilter && (ask == 0 || bid / ask > _imbalanceRatio))
				AddImbalance(bar, price - InstrumentInfo.TickSize, OrderDirections.Sell);
		}
	}

	#endregion

	#region Private methods

	private void AddImbalance(int bar, decimal price, OrderDirections direction)
	{
		var color = direction == OrderDirections.Buy ? BuyColor : SellColor;

		_renderSeries[bar].Add(new PriceSelectionValue(price)
		{
			Context = direction,
			ObjectColor = _transparent,
			PriceSelectionColor = CrossColor.FromArgb((byte)Math.Floor(255 * _transparency / 100m), color.R, color.G, color.B),
			VisualObject = ObjectType.OnlyCluster
		});
	}

	#endregion
}