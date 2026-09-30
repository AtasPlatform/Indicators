namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;

using ATAS.DataFeedsCore;

using Newtonsoft.Json;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

using Utils.Common.Collections;

[DisplayName("Imbalance Ratio")]
[Category(IndicatorCategories.VolumeOrderFlow)]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ImbalanceRatioIndDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000602404")]
public class ImbalanceRatio : Indicator
{
	#region Fields

	private readonly Dictionary<int, RenderFont> _fontCache = new();
	private readonly CrossColor _transparent = Color.Transparent.Convert();

	private Color _buyColor = Color.Blue;
	private string _cachedFontFamily;
	private int _cachedFontSize;
	private FontStyle _cachedFontStyle;
	private RenderStringFormat _format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
	private bool _ignoreZeroValues;
	private int _imbalanceRatio = 4;
	// PLAT-5080: the volume thresholds may be set in money; persisted as the old numbers plus the exact values and the money scalars
	private ATAS.Indicators.VolumeFilter _minimumDifference;
	private PriceSelectionDataSeries _renderSeries = new("RenderSeries", Strings.ImbalanceRange) { IsHidden = true };
	private Color _sellColor = Color.Red;
	private Color _textColor = Color.White;
	private int _transparency = 50;
	private ATAS.Indicators.VolumeFilter _volumeFilter;

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

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VolumeFilter), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MinVolumeFilterDescription), Order = 110)]
	[JsonIgnore]
	public ATAS.Indicators.VolumeFilter MinVolumeFilter
	{
		get => _volumeFilter;
		set => SetTrackedProperty(ref _volumeFilter, value, OnFilterChanged);
	}

    [Parameter]
    [Browsable(false)]
	[Range(0, 1000000000)]
	public int VolumeFilter
	{
		get => (int)Math.Round(_volumeFilter.Value);
		set => _volumeFilter.Value = value;
	}

	// declared after VolumeFilter: loaded last, it restores a fractional threshold; older versions ignore it
	[Browsable(false)]
	public decimal VolumeFilterExact
	{
		get => _volumeFilter.Value;
		set => _volumeFilter.Value = value;
	}

	[Browsable(false)]
	public string? VolumeFilterMoney
	{
		get => _volumeFilter.MoneyScalar;
		set => _volumeFilter.MoneyScalar = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ImbalanceDifference), GroupName = nameof(Strings.Settings), Order = 120)]
	[JsonIgnore]
	public ATAS.Indicators.VolumeFilter MinimumDifferenceFilter
	{
		get => _minimumDifference;
		set => SetTrackedProperty(ref _minimumDifference, value, OnFilterChanged);
	}

	[Parameter]
	[Browsable(false)]
	[Range(0, 1000000000)]
	public int MinimumDifference
	{
		get => (int)Math.Round(_minimumDifference.Value);
		set => _minimumDifference.Value = value;
	}

	// declared after MinimumDifference: loaded last, it restores a fractional threshold; older versions ignore it
	[Browsable(false)]
	public decimal MinimumDifferenceExact
	{
		get => _minimumDifference.Value;
		set => _minimumDifference.Value = value;
	}

	[Browsable(false)]
	public string? MinimumDifferenceMoney
	{
		get => _minimumDifference.MoneyScalar;
		set => _minimumDifference.MoneyScalar = value;
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Font), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.FontSettingDescription), Order = 225)]
	public FontSetting Font { get; set; } = new("Arial", 9);

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
		MinVolumeFilter = new ATAS.Indicators.VolumeFilter(false);
		MinimumDifferenceFilter = new ATAS.Indicators.VolumeFilter(false);

		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);

		DataSeries[0] = _renderSeries;
	}

    #endregion

    #region Protected methods

	// money filters select other levels when the rates or the display currency change
	protected override void OnValuationChanged()
	{
		if (_volumeFilter.IsMoney || _minimumDifference.IsMoney)
			DoActionInGuiThread(RecalculateValues);
	}

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
		var barWidth = Math.Max(1, ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0));
		var priceHeight = Math.Max(1, ChartInfo.GetYByPrice(0) - ChartInfo.GetYByPrice(InstrumentInfo.TickSize));

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
			var font = GetFittingFont(context, renderText, rect.Size);

			if (font is not null)
				context.DrawString(renderText, font, _textColor, rect, _format);
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

			// a money threshold is compared at the price of the level the volume traded at
			if (_minimumDifference.Compare(Math.Abs(ask - bid), price) <= 0)
				continue;

			if (_ignoreZeroValues && (ask == 0 || bid == 0))
				continue;

			if (_volumeFilter.Compare(ask, price) >= 0 && (bid == 0 || ask / bid > _imbalanceRatio))
				AddImbalance(bar, price, OrderDirections.Buy);

			if (_volumeFilter.Compare(bid, price - InstrumentInfo.TickSize) >= 0 && (ask == 0 || bid / ask > _imbalanceRatio))
				AddImbalance(bar, price - InstrumentInfo.TickSize, OrderDirections.Sell);
		}
	}

	#endregion

	#region Private methods

	private void OnFilterChanged(string property)
	{
		// binding the instrument valuation is not an edit
		if (property != nameof(ATAS.Indicators.VolumeFilter.Valuation))
			RecalculateValues();
	}

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

	private RenderFont GetFittingFont(RenderContext context, string text, Size availableSize)
	{
		var configuredFont = Font.RenderObject;

		if (_cachedFontFamily != configuredFont.FontFamily
			|| _cachedFontSize != configuredFont.Size
			|| _cachedFontStyle != configuredFont.Style)
		{
			_fontCache.Clear();
			_cachedFontFamily = configuredFont.FontFamily;
			_cachedFontSize = (int)configuredFont.Size;
			_cachedFontStyle = configuredFont.Style;

			for (var size = _cachedFontSize; size > 0; size--)
				_fontCache[size] = new RenderFont(_cachedFontFamily, size, _cachedFontStyle);
		}

		for (var size = _cachedFontSize; size > 0; size--)
		{
			var font = _fontCache[size];
			var textSize = context.MeasureString(text, font);

			if (textSize.Width <= availableSize.Width && textSize.Height <= availableSize.Height)
				return font;
		}

		return null;
	}

	#endregion
}
