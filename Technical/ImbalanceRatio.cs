namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Windows.Media;

using ATAS.DataFeedsCore;

using MoreLinq;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Color = System.Drawing.Color;

[DisplayName("Imbalance Ratio")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ImbalanceRatioIndDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602404")]
public class ImbalanceRatio : Indicator
{
	#region Fields

	private Color _buyColor = Color.Blue;
	private RenderFont _font = new("Arial", 9);
	private RenderStringFormat _format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
	private int _imbalanceRatio = 4;
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BuyColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BuySignalColorDescription), Order = 200)]
	public System.Windows.Media.Color BuyColor
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
							System.Windows.Media.Color.FromArgb((byte)Math.Floor(255 * _transparency / 100m), value.R, value.G, value.B);
					}
				});
			}
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SellColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.SellSignalColorDescription), Order = 210)]
	public System.Windows.Media.Color SellColor
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
							System.Windows.Media.Color.FromArgb((byte)Math.Floor(255 * _transparency / 100m), value.R, value.G, value.B);
					}
				});
			}
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.LabelTextColorDescription), Order = 220)]
	public System.Windows.Media.Color TextColor
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
					x.PriceSelectionColor = System.Windows.Media.Color.FromArgb((byte)Math.Floor(255 * value / 100m), x.PriceSelectionColor.R,
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

		for (var i = candle.High; i > candle.Low; i -= InstrumentInfo.TickSize)
		{
			var upperInfo = candle.GetPriceVolumeInfo(i);
			var lowerInfo = candle.GetPriceVolumeInfo(i - InstrumentInfo.TickSize);

			if (lowerInfo == default || upperInfo == default)
				continue;

			if (lowerInfo.Volume + upperInfo.Volume < _volumeFilter || lowerInfo.Bid == 0)
				continue;

			if (upperInfo.Ask / lowerInfo.Bid < _imbalanceRatio)
				continue;

			_renderSeries[bar].Add(new PriceSelectionValue(i)
			{
				Context = OrderDirections.Buy,
				ObjectColor = Colors.Transparent,
				PriceSelectionColor = System.Windows.Media.Color.FromArgb((byte)Math.Floor(255 * _transparency / 100m), BuyColor.R, BuyColor.G, BuyColor.B),
				VisualObject = ObjectType.OnlyCluster
			});
		}

		for (var i = candle.Low; i < candle.High; i += InstrumentInfo.TickSize)
		{
			var lowerInfo = candle.GetPriceVolumeInfo(i);
			var upperInfo = candle.GetPriceVolumeInfo(i + InstrumentInfo.TickSize);

			if (lowerInfo == default || upperInfo == default)
				continue;

			if (lowerInfo.Volume + upperInfo.Volume < _volumeFilter || upperInfo.Ask == 0)
				continue;

			if (lowerInfo.Bid / upperInfo.Ask < _imbalanceRatio)
				continue;

			_renderSeries[bar].Add(new PriceSelectionValue(i)
			{
				Context = OrderDirections.Sell,
				ObjectColor = Colors.Transparent,
				PriceSelectionColor =
					System.Windows.Media.Color.FromArgb((byte)Math.Floor(255 * _transparency / 100m), SellColor.R, SellColor.G, SellColor.B),
				VisualObject = ObjectType.OnlyCluster
			});
		}
	}

	#endregion
}