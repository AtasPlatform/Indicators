namespace BionicCandle;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Windows.Media;

using ATAS.Indicators;
using ATAS.Indicators.Technical.Properties;

using OFT.Rendering.Context;

using Color = System.Windows.Media.Color;

[DisplayName("Bionic Candle")]
public class BionicCandle : Indicator
{
	#region Nested types

	public enum RenderLoc
	{
		Chart,
		Panel
	}

	#endregion

	#region Fields

	private readonly ValueDataSeries _color1Series = new("color1") { ShowTooltip = false, ShowCurrentValue = false, IsHidden = true, Color = Colors.Red };
	private readonly ValueDataSeries _color2Series = new("color2") { ShowTooltip = false, ShowCurrentValue = false, IsHidden = true, Color = Colors.LimeGreen };

	private readonly ValueDataSeries _color3Series = new("color3")
		{ ShowTooltip = false, ShowCurrentValue = false, IsHidden = true, Color = Color.FromRgb(51, 51, 51) };

	private readonly ValueDataSeries _color4Series = new("color4")
		{ ShowTooltip = false, ShowCurrentValue = false, IsHidden = true, Color = Color.FromRgb(51, 51, 51) };

	private readonly ValueDataSeries _color5Series = new("color5") { ShowTooltip = false, ShowCurrentValue = false, IsHidden = true, Color = Colors.Yellow };
	private readonly ValueDataSeries _color6Series = new("color6") { ShowTooltip = false, ShowCurrentValue = false, IsHidden = true, Color = Colors.Blue };
	private readonly ValueDataSeries _color7Series = new("color7") { ShowTooltip = false, ShowCurrentValue = false, IsHidden = true, Color = Colors.DarkGreen };
	private readonly ValueDataSeries _color8Series = new("color8") { ShowTooltip = false, ShowCurrentValue = false, IsHidden = true, Color = Colors.Maroon };
	private readonly ValueDataSeries _color9Series = new("color9") { ShowTooltip = false, ShowCurrentValue = false, IsHidden = true, Color = Colors.White };
	private readonly ValueDataSeries _color10Series = new("color10") { ShowTooltip = false, ShowCurrentValue = false, IsHidden = true, Color = Colors.White };
	private readonly ValueDataSeries _color11Series = new("color11") { ShowTooltip = false, ShowCurrentValue = false, IsHidden = true, Color = Colors.White };


    public PaintbarsDataSeries _paintBars = new("paintBars") { IsHidden = true };
	private RenderLoc _visMode = RenderLoc.Chart;

	#endregion

	#region Properties

	[Display(Name = "Width", GroupName = "Histogram", Order = 50)]
	[Range(1, 100)]
	public int HistogramWidth
	{
		get => _color1Series.Width;
		set
		{
			_color1Series.Width = value;
			_color2Series.Width = value;
			_color3Series.Width = value;
			_color4Series.Width = value;
			_color5Series.Width = value;
			_color6Series.Width = value;
			_color7Series.Width = value;
			_color8Series.Width = value;
			_color9Series.Width = value;
			_color10Series.Width = value;
			_color11Series.Width = value;
		}
	}

	[Display(Name = "Visual mode", GroupName = "Visualization", Order = 100)]
	public RenderLoc VisMode
	{
		get => _visMode;
		set
		{
			_visMode = value;

			if (value is RenderLoc.Chart)
			{
				Panel = IndicatorDataProvider.CandlesPanel;
				_color1Series.VisualType = VisualMode.Hide;
				_color2Series.VisualType = VisualMode.Hide;
				_color3Series.VisualType = VisualMode.Hide;
				_color4Series.VisualType = VisualMode.Hide;
				_color5Series.VisualType = VisualMode.Hide;
				_color6Series.VisualType = VisualMode.Hide;
				_color7Series.VisualType = VisualMode.Hide;
				_color8Series.VisualType = VisualMode.Hide;
				_color9Series.VisualType = VisualMode.Hide;
				_color10Series.VisualType = VisualMode.Hide;
				_color11Series.VisualType = VisualMode.Hide;
			}
			else
			{
				Panel = IndicatorDataProvider.NewPanel;
				_color1Series.VisualType = VisualMode.Histogram;
				_color2Series.VisualType = VisualMode.Histogram;
				_color3Series.VisualType = VisualMode.Histogram;
				_color4Series.VisualType = VisualMode.Histogram;
				_color5Series.VisualType = VisualMode.Histogram;
				_color6Series.VisualType = VisualMode.Histogram;
				_color7Series.VisualType = VisualMode.Histogram;
				_color8Series.VisualType = VisualMode.Histogram;
				_color9Series.VisualType = VisualMode.Histogram;
				_color10Series.VisualType = VisualMode.Histogram;
				_color11Series.VisualType = VisualMode.Histogram;
			}

			RecalculateValues();
		}
	}

    [Display(ResourceType = typeof(Resources), Name = "ShowAboveChart", GroupName = "Visualization", Order = 1000)]
	public bool AbovePrice
	{
		get => DrawAbovePrice;
		set => DrawAbovePrice = value;
	}

	[Display(ResourceType = typeof(Resources), Name = "PullbackCandleBull", GroupName = "Visualization", Order = 1010)]
	public System.Drawing.Color Color1
	{
		get => _color1Series.Color.Convert();
		set => _color1Series.Color = value.Convert();
	} //Up candle wick 

	[Display(ResourceType = typeof(Resources), Name = "PullbackCandleBear", GroupName = "Visualization", Order = 1020)]
	public System.Drawing.Color Color2
	{
		get => _color2Series.Color.Convert();
		set => _color2Series.Color = value.Convert();
	} //Down candle wick 

	[Display(ResourceType = typeof(Resources), Name = "MoveCandleBull", GroupName = "Visualization", Order = 1030)]
	public System.Drawing.Color Color3
	{
		get => _color3Series.Color.Convert();
		set => _color3Series.Color = value.Convert();
	} //Up candle body

	[Display(ResourceType = typeof(Resources), Name = "MoveCandleBear", GroupName = "Visualization", Order = 1040)]
	public System.Drawing.Color Color4
	{
		get => _color4Series.Color.Convert();
		set => _color4Series.Color = value.Convert();
	} //Down candle body

	[Display(ResourceType = typeof(Resources), Name = "StrongCandleBull", GroupName = "Visualization", Order = 1050)]
	public System.Drawing.Color Color5
	{
		get => _color5Series.Color.Convert();
		set => _color5Series.Color = value.Convert();
	} //High == Close

	[Display(ResourceType = typeof(Resources), Name = "StrongCandleBear", GroupName = "Visualization", Order = 1060)]
	public System.Drawing.Color Color6
	{
		get => _color6Series.Color.Convert();
		set => _color6Series.Color = value.Convert();
	} //Low == Close

	[Display(ResourceType = typeof(Resources), Name = "DojiCandleBull", GroupName = "Visualization", Order = 1070)]
	public System.Drawing.Color Color7
	{
		get => _color7Series.Color.Convert();
		set => _color7Series.Color = value.Convert();
	} //High - Close > Close - Low

	[Display(ResourceType = typeof(Resources), Name = "DojiCandleBear", GroupName = "Visualization", Order = 1080)]
	public System.Drawing.Color Color8
	{
		get => _color8Series.Color.Convert();
		set => _color8Series.Color = value.Convert();
	} //High

	[Display(ResourceType = typeof(Resources), Name = "EquilibriumTopCandle", GroupName = "Visualization", Order = 1100)]
	public System.Drawing.Color Color9
	{
		get => _color9Series.Color.Convert();
		set => _color9Series.Color = value.Convert();
	} //High - Close == Close - Low

	[Display(ResourceType = typeof(Resources), Name = "EquilibriumBottomCandle", GroupName = "Visualization", Order = 1200)]
	public System.Drawing.Color Color10
	{
		get => _color10Series.Color.Convert();
		set => _color10Series.Color = value.Convert();
	} //High - Close == Close - Low

	[Display(ResourceType = typeof(Resources), Name = "DojiEmptyColor", GroupName = "Visualization", Order = 1300)]
	public System.Drawing.Color Color11
	{
		get => _color11Series.Color.Convert();
		set => _color11Series.Color = value.Convert();
	} //High == Close == Low == Open

	#endregion

	#region ctor

	public BionicCandle()
		: base(true)
	{
		DrawAbovePrice = true;
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Historical | DrawingLayouts.LatestBar);
		DenyToChangePanel = true;

		DataSeries[0] = _paintBars;
		DataSeries.Add(_color1Series);
		DataSeries.Add(_color2Series);
		DataSeries.Add(_color3Series);
		DataSeries.Add(_color4Series);
		DataSeries.Add(_color5Series);
		DataSeries.Add(_color6Series);
		DataSeries.Add(_color7Series);
		DataSeries.Add(_color8Series);
		DataSeries.Add(_color9Series);
		DataSeries.Add(_color10Series);
		DataSeries.Add(_color11Series);
	}

	#endregion

	#region Protected methods

	#region Overrides of ExtendedIndicator

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo is null)
			return;

		if (VisMode is RenderLoc.Panel)
			return;

		var width = (int)ChartInfo.PriceChartContainer.BarsWidth;

		for (var bar = FirstVisibleBarNumber; bar <= LastVisibleBarNumber; bar++)
		{
			var candle = GetCandle(bar);

			var x = ChartInfo.GetXByBar(bar);

			if (candle.High == candle.Low && candle.Open == candle.Close)
			{
				var y = VisMode is RenderLoc.Chart
					? ChartInfo.GetYByPrice(candle.Close, false)
					: Container.Region.Y + Container.Region.Height / 2;

				var dodgeRect = new Rectangle(x, y - 1, width, 2);
				context.FillRectangle(Color11, dodgeRect);

				continue;
			}

			if (VisMode is RenderLoc.Panel)
				continue;

			var high = ChartInfo.GetYByPrice(candle.High, false);
			var low = ChartInfo.GetYByPrice(candle.Low, false);
			var close = ChartInfo.GetYByPrice(candle.Close, false);

			if (candle.Close > candle.Open && candle.High == candle.Close)
			{
				var rect = new Rectangle(x, high, width, low - high);

				context.FillRectangle(Color5, rect);
				continue;
			}

			if (candle.Close < candle.Open && candle.Low == candle.Close)
			{
				var rect = new Rectangle(x, high, width, low - high);
				context.FillRectangle(Color6, rect);
				continue;
			}

			if (candle.Close > candle.Open)
			{
				var bodyRect = new Rectangle(x, close, width, low - close);
				context.FillRectangle(Color3, bodyRect);

				var wickRect = new Rectangle(x, high, width, close - high);
				context.FillRectangle(Color1, wickRect);

				continue;
			}

			if (candle.Close < candle.Open)
			{
				var bodyRect = new Rectangle(x, high, width, close - high);
				context.FillRectangle(Color4, bodyRect);

				var wickRect = new Rectangle(x, close, width, low - close);
				context.FillRectangle(Color2, wickRect);
				continue;
			}

			if (candle.Close == candle.Open)
			{
				if (candle.High - candle.Close == candle.Close - candle.Low)
				{
					var upRect = new Rectangle(x, high, width, close - high);
					var downRect = new Rectangle(x, close - 1, width, low - close);

					context.FillRectangle(Color9, upRect);
					context.FillRectangle(Color10, downRect);
					continue;
				}

				var rect = new Rectangle(x, high, width, low - high);

				var color = candle.High - candle.Close > candle.Close - candle.Low ? Color7 : Color8;

				context.FillRectangle(color, rect);
			}
		}
	}

	#endregion

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
			DataSeries.ForEach(x => x.Clear());

		if (VisMode is RenderLoc.Chart)
		{
			_paintBars[bar] = Colors.Transparent;
			return;
		}

		RefreshValues(bar);

		var candle = GetCandle(bar);

		if (candle.Close > candle.Open && candle.High == candle.Close)
		{
			_color5Series[bar] = candle.High - candle.Low;
			return;
		}

		if (candle.Close < candle.Open && candle.Low == candle.Close)
		{
			_color6Series[bar] = candle.Low - candle.High;
			return;
		}

		if (candle.Close > candle.Open)
		{
			_color3Series[bar] = candle.Close - candle.Low;
			_color1Series[bar] = candle.High - candle.Low;
			return;
		}

		if (candle.Close < candle.Open)
		{
			_color4Series[bar] = candle.Close - candle.High;
			_color2Series[bar] = candle.Low - candle.High;
			return;
		}

		if (candle.Close == candle.Open)
		{
			if (candle.High - candle.Close > candle.Close - candle.Low)
				_color7Series[bar] = candle.High - candle.Low;
			else if (candle.High - candle.Close < candle.Close - candle.Low)
				_color8Series[bar] = candle.Low - candle.High;
			else
			{
				if (candle.High == candle.Low)
					_color11Series[bar] = InstrumentInfo.TickSize / 10;
				else
				{
					_color9Series[bar] = candle.High - candle.Close;
					_color10Series[bar] = candle.Low - candle.Close;
				}
			}
		}
	}

	#endregion

	#region Private methods

	private void RefreshValues(int bar)
	{
		_color1Series[bar] = 0;
		_color2Series[bar] = 0;
		_color3Series[bar] = 0;
		_color4Series[bar] = 0;
		_color5Series[bar] = 0;
		_color6Series[bar] = 0;
		_color7Series[bar] = 0;
		_color8Series[bar] = 0;
		_color9Series[bar] = 0;
		_color10Series[bar] = 0;
		_color11Series[bar] = 0;
	}

	#endregion
}