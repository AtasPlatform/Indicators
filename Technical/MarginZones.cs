namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Windows.Media;

using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Brushes = System.Drawing.Brushes;
using Color = System.Drawing.Color;

[DisplayName("Margin zones")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/20340-margin-zones")]
public class MarginZones : Indicator
{
	#region Nested types

	public enum ZoneDirection
	{
		[Display(ResourceType = typeof(Resources),
			Name = "Up")]
		Up = 0,

		[Display(ResourceType = typeof(Resources),
			Name = "Down")]
		Down = 1
	}

	#endregion

	#region Fields

	private readonly ValueDataSeries _100Line = new("100% line")
		{ Color = DefaultColors.Maroon.Convert(), Width = 2, ScaleIt = false, VisualType = VisualMode.OnlyValueOnAxis, IsHidden = true };

	private readonly DrawingRectangle _100Rectangle = new(0, 0, 0, 0, Pens.Gray, Brushes.Gray);

	private readonly ValueDataSeries _150Line = new("150% line")
		{ Color = Colors.SkyBlue, Width = 1, ScaleIt = false, VisualType = VisualMode.Hide, IsHidden = true };

	private readonly DrawingRectangle _150Rectangle = new(0, 0, 0, 0, Pens.Gray, Brushes.Gray);

	private readonly ValueDataSeries _200Line = new("200% line")
		{ Color = Colors.CadetBlue, Width = 1, ScaleIt = false, VisualType = VisualMode.Hide, IsHidden = true };

	private readonly DrawingRectangle _200Rectangle = new(0, 0, 0, 0, Pens.Gray, Brushes.Gray);

	private readonly ValueDataSeries _25Line = new("25% line")
		{ Color = Colors.LightSkyBlue, Width = 1, ScaleIt = false, VisualType = VisualMode.OnlyValueOnAxis, IsHidden = true };

	private readonly DrawingRectangle _25Rectangle = new(0, 0, 0, 0, Pens.Gray, Brushes.Gray);

	private readonly ValueDataSeries _50Line = new("50% line")
		{ Color = Colors.SkyBlue, Width = 1, ScaleIt = false, VisualType = VisualMode.OnlyValueOnAxis, IsHidden = true };

	private readonly DrawingRectangle _50Rectangle = new(0, 0, 0, 0, Pens.Gray, Brushes.Gray);

	private readonly ValueDataSeries _75Line = new("75% line")
		{ Color = Colors.LightSkyBlue, Width = 1, ScaleIt = false, VisualType = VisualMode.Hide, IsHidden = true };

	private readonly DrawingRectangle _75Rectangle = new(0, 0, 0, 0, Pens.Gray, Brushes.Gray);

	private readonly ValueDataSeries _baseLineLabel = new("Base line")
		{ Color = Colors.Gray, Width = 2, ScaleIt = false, VisualType = VisualMode.OnlyValueOnAxis, IsHidden = true };

	private readonly List<int> _newDays = new();
	private bool _autoPrice = true;

	private TrendLine _baseLine = new(0, 0, 0, 0, Pens.Gray);
	private RenderPen _baseLineRenderPen = new(Color.Gray);
	private bool _calculated;
	private decimal _customPrice;
	private ZoneDirection _direction;
	private int _lastCalculated;
	private int _margin = 3200;
	private decimal _secondPrice;
	private decimal _tickCost = 6.25m;
	private decimal _zonePrice;
	private decimal _zoneWidth;

	private int _zoneWidthDays = 3;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources),
		Name = "Color",
		GroupName = "Zone200",
		Order = 20)]
	public Color Zone200LineColor
	{
		get => _200Line.Color.Convert();
		set => _200Line.Color = value.Convert();
	}

	[Display(ResourceType = typeof(Resources),
		Name = "Show",
		GroupName = "Zone200",
		Order = 21)]
	public bool ShowZone200
	{
		get => _200Line.VisualType != VisualMode.Hide;
		set => _200Line.VisualType = value ? VisualMode.OnlyValueOnAxis : VisualMode.Hide;
	}

	[Display(ResourceType = typeof(Resources),
		Name = "Color",
		GroupName = "Zone150",
		Order = 30)]
	public Color Zone150LineColor
	{
		get => _150Line.Color.Convert();
		set => _150Line.Color = value.Convert();
	}

	[Display(ResourceType = typeof(Resources),
		Name = "Show",
		GroupName = "Zone150",
		Order = 31)]
	public bool ShowZone150
	{
		get => _150Line.VisualType != VisualMode.Hide;
		set => _150Line.VisualType = value ? VisualMode.OnlyValueOnAxis : VisualMode.Hide;
	}

	[Display(ResourceType = typeof(Resources),
		Name = "Color",
		GroupName = "Zone75",
		Order = 40)]
	public Color Zone75LineColor
	{
		get => _75Line.Color.Convert();
		set => _75Line.Color = value.Convert();
	}

	[Display(ResourceType = typeof(Resources),
		Name = "Show",
		GroupName = "Zone75",
		Order = 41)]
	public bool ShowZone75
	{
		get => _75Line.VisualType != VisualMode.Hide;
		set => _75Line.VisualType = value ? VisualMode.OnlyValueOnAxis : VisualMode.Hide;
	}

	[Display(ResourceType = typeof(Resources),
		Name = "Color",
		GroupName = "Zone50",
		Order = 50)]
	public Color Zone50LineColor
	{
		get => _50Line.Color.Convert();
		set => _50Line.Color = value.Convert();
	}

	[Display(ResourceType = typeof(Resources),
		Name = "Show",
		GroupName = "Zone50",
		Order = 51)]
	public bool ShowZon50
	{
		get => _50Line.VisualType != VisualMode.Hide;
		set => _50Line.VisualType = value ? VisualMode.OnlyValueOnAxis : VisualMode.Hide;
	}

	[Display(ResourceType = typeof(Resources),
		Name = "Color",
		GroupName = "Zone25",
		Order = 60)]
	public Color Zone25LineColor
	{
		get => _25Line.Color.Convert();
		set => _25Line.Color = value.Convert();
	}

	[Display(ResourceType = typeof(Resources),
		Name = "Show",
		GroupName = "Zone25",
		Order = 61)]
	public bool ShowZone25
	{
		get => _25Line.VisualType != VisualMode.Hide;
		set => _25Line.VisualType = value ? VisualMode.OnlyValueOnAxis : VisualMode.Hide;
	}

	[Display(ResourceType = typeof(Resources),
		Name = "Color",
		GroupName = "Zone100",
		Order = 70)]
	public Color Zone100LineColor
	{
		get => _100Line.Color.Convert();
		set => _100Line.Color = value.Convert();
	}

	[Display(ResourceType = typeof(Resources),
		Name = "Show",
		GroupName = "Zone100",
		Order = 71)]
	public bool ShowZone100
	{
		get => _100Line.VisualType != VisualMode.Hide;
		set => _100Line.VisualType = value ? VisualMode.OnlyValueOnAxis : VisualMode.Hide;
	}

	[Display(ResourceType = typeof(Resources),
		Name = "Color",
		GroupName = "BaseLine",
		Order = 80)]
	public Color BaseLineColor
	{
		get => _baseLineRenderPen.Color;
		set => _baseLineRenderPen = new RenderPen(value);
	}

	[Display(ResourceType = typeof(Resources),
		Name = "Show",
		GroupName = "BaseLine",
		Order = 81)]
	public bool ShowBaseLine
	{
		get => _baseLineLabel.VisualType != VisualMode.Hide;
		set => _baseLineLabel.VisualType = value
			? VisualMode.OnlyValueOnAxis
			: VisualMode.Hide;
	}

	[Display(ResourceType = typeof(Resources),
		Name = "Margin",
		GroupName = "InstrumentParameters",
		Order = 90)]

	public int Margin
	{
		get => _margin;
		set
		{
			_margin = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources),
		Name = "TickCost",
		GroupName = "InstrumentParameters",
		Order = 91)]
	public decimal TickCost
	{
		get => _tickCost;
		set
		{
			_tickCost = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources),
		Name = "DirectionOfZone",
		GroupName = "Other",
		Order = 100)]
	public ZoneDirection Direction
	{
		get => _direction;
		set
		{
			_direction = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources),
		Name = "ZoneWidth",
		GroupName = "Other",
		Order = 101)]
	public int ZoneWidth
	{
		get => _zoneWidthDays;
		set
		{
			_zoneWidthDays = Math.Max(1, value);
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources),
		Name = "AutoCalculation",
		GroupName = "StartPrice",
		Order = 110)]
	public bool AutoPrice
	{
		get => _autoPrice;
		set
		{
			_autoPrice = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources),
		Name = "CustomPrice",
		GroupName = "StartPrice",
		Order = 111)]
	public decimal CustomPrice
	{
		get => _customPrice;
		set
		{
			_customPrice = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public MarginZones()
		: base(true)
	{
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		DrawAbovePrice = false;
		SubscribeToDrawingEvents(DrawingLayouts.Historical);

		DataSeries[0].IsHidden = true;
		DataSeries.Add(_baseLineLabel);
		DataSeries.Add(_100Line);
		DataSeries.Add(_25Line);
		DataSeries.Add(_50Line);
		DataSeries.Add(_75Line);
		DataSeries.Add(_150Line);
		DataSeries.Add(_200Line);
	}

	#endregion

	#region Protected methods

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo is null)
			return;

		if (ShowBaseLine)
		{
			var x1 = ChartInfo.GetXByBar(_baseLine.FirstBar);
			var x2 = ChartInfo.GetXByBar(LastVisibleBarNumber);
			var y = ChartInfo.GetYByPrice(_baseLine.FirstPrice);

			if (x2 >= 0 && x1 <= Container.Region.Width && y >= 0 && y <= Container.Region.Height)
				context.DrawLine(_baseLineRenderPen, x1, y, x2, y);
		}

		if (ShowZone25)
			DrawZone(context, _25Rectangle, Zone25LineColor);

		if (ShowZon50)
			DrawZone(context, _50Rectangle, Zone50LineColor);

		if (ShowZone75)
			DrawZone(context, _75Rectangle, Zone75LineColor);

		if (ShowZone100)
			DrawZone(context, _100Rectangle, Zone100LineColor);

		if (ShowZone150)
			DrawZone(context, _150Rectangle, Zone150LineColor);

		if (ShowZone200)
			DrawZone(context, _200Rectangle, Zone200LineColor);
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			_calculated = false;
			_newDays.Clear();
			_newDays.Add(0);
			_lastCalculated = 0;
			return;
		}

		if (IsNewSession(bar) && !_newDays.Contains(bar))
			_newDays.Add(bar);

		if (bar != CurrentBar - 1)
			return;

		if (_calculated)
		{
			if (IsNewWeek(bar))
				_calculated = false;
			var candle = GetCandle(bar);

			if (_direction == ZoneDirection.Up)
			{
				//ищем low
				if (candle.Low < _zonePrice)
					_calculated = false;
			}
			else if (_direction == ZoneDirection.Down)
			{
				//ищем high
				if (candle.High > _zonePrice)
					_calculated = false;
			}
		}

		if (!_calculated)
		{
			_calculated = true;
			_baseLine.FirstBar = 0;
			_baseLine.SecondBar = 0;
			_baseLine.FirstPrice = 0;
			_baseLine.SecondPrice = 0;

			if (_autoPrice)
			{
				var currentWeek = true;

				for (var i = bar; i >= _lastCalculated; i--)
				{
					if (IsNewWeek(i))
					{
						if (!currentWeek)
							break;

						currentWeek = false;
					}

					if (currentWeek)
						continue;
					
					_zonePrice = 0;

					var candle = GetCandle(i);

					if (_direction == ZoneDirection.Up)
					{
						//ищем low
						if (_zonePrice == 0 || candle.Low < _zonePrice)
							_zonePrice = candle.Low;
					}
					else if (_direction == ZoneDirection.Down)
					{
						//ищем high
						if (_zonePrice == 0 || candle.High > _zonePrice)
							_zonePrice = candle.High;
					}
				}
			}
			else
				_zonePrice = _customPrice;

			var firstBarNumber = Math.Max(_newDays.Count - _zoneWidthDays, 0);
			var firstBar = _newDays.Any() ? _newDays[firstBarNumber] : 0;
			var zoneSize = Margin / _tickCost * (_direction == ZoneDirection.Up ? 1 : -1);
			_zoneWidth = zoneSize * 0.1m * InstrumentInfo.TickSize;
			_secondPrice = _zonePrice + zoneSize * InstrumentInfo.TickSize;
			
			_baseLine.FirstBar = firstBar;
			_baseLine.SecondBar = bar;
			_baseLine.FirstPrice = _zonePrice;
			_baseLine.SecondPrice = _zonePrice;

			//Last value is hidden with single value in series
			_baseLineLabel[bar] = _baseLineLabel[bar - 1] = _zonePrice;
			_100Line[bar] = _100Line[bar - 1] = _secondPrice;
			_25Line[bar] = _25Line[bar - 1] = _zonePrice + zoneSize * 0.25m * InstrumentInfo.TickSize;
			_50Line[bar] = _50Line[bar - 1] = _zonePrice + zoneSize * 0.5m * InstrumentInfo.TickSize;
			_75Line[bar] = _75Line[bar - 1] = _zonePrice + zoneSize * 0.75m * InstrumentInfo.TickSize;
			_150Line[bar] = _150Line[bar - 1] = _zonePrice + zoneSize * 1.5m * InstrumentInfo.TickSize;
			_200Line[bar] = _200Line[bar - 1] = _zonePrice + zoneSize * 2m * InstrumentInfo.TickSize;

			if (_100Line.VisualType != VisualMode.Hide)
			{
				_100Rectangle.FirstBar = firstBar;
				_100Rectangle.SecondBar = bar;
				_100Rectangle.FirstPrice = _secondPrice;
				_100Rectangle.SecondPrice = _secondPrice + _zoneWidth;
				_100Rectangle.Brush = new SolidBrush(ConvertColor(_100Line.Color));
				_100Rectangle.Pen = Pens.Transparent;
			}

			if (_25Line.VisualType != VisualMode.Hide)
			{
				_25Rectangle.FirstBar = firstBar;
				_25Rectangle.SecondBar = bar;
				_25Rectangle.FirstPrice = _25Line[bar];
				_25Rectangle.SecondPrice = _25Line[bar] + _zoneWidth / 4;
				_25Rectangle.Brush = new SolidBrush(ConvertColor(_25Line.Color));
				_25Rectangle.Pen = Pens.Transparent;
			}

			if (_50Line.VisualType != VisualMode.Hide)
			{
				_50Rectangle.FirstBar = firstBar;
				_50Rectangle.SecondBar = bar;
				_50Rectangle.FirstPrice = _50Line[bar];
				_50Rectangle.SecondPrice = _50Line[bar] + _zoneWidth / 2;
				_50Rectangle.Brush = new SolidBrush(ConvertColor(_50Line.Color));
				_50Rectangle.Pen = Pens.Transparent;
			}

			if (_75Line.VisualType != VisualMode.Hide)
			{
				_75Rectangle.FirstBar = firstBar;
				_75Rectangle.SecondBar = bar;
				_75Rectangle.FirstPrice = _75Line[bar];
				_75Rectangle.SecondPrice = _75Line[bar] + _zoneWidth / 4;
				_75Rectangle.Brush = new SolidBrush(ConvertColor(_75Line.Color));
				_75Rectangle.Pen = Pens.Transparent;
			}

			if (_150Line.VisualType != VisualMode.Hide)
			{
				_150Rectangle.FirstBar = firstBar;
				_150Rectangle.SecondBar = bar;
				_150Rectangle.FirstPrice = _150Line[bar];
				_150Rectangle.SecondPrice = _150Line[bar] + _zoneWidth;
				_150Rectangle.Brush = new SolidBrush(ConvertColor(_150Line.Color));
				_150Rectangle.Pen = Pens.Transparent;
			}

			if (_200Line.VisualType != VisualMode.Hide)
			{
				_200Rectangle.FirstBar = firstBar;
				_200Rectangle.SecondBar = bar;
				_200Rectangle.FirstPrice = _200Line[bar];
				_200Rectangle.SecondPrice = _200Line[bar] + _zoneWidth;
				_200Rectangle.Brush = new SolidBrush(ConvertColor(_200Line.Color));
				_200Rectangle.Pen = Pens.Transparent;
			}

			_lastCalculated = bar;
		}

		foreach (var dataSeries in DataSeries)
		{
			var series = (ValueDataSeries)dataSeries;
			series[bar] = series[bar - 1];
		}
	}

	#endregion

	#region Private methods

	private void DrawZone(RenderContext context, DrawingRectangle drawRect, Color color)
	{
		var x1 = ChartInfo.GetXByBar(drawRect.FirstBar);
		var x2 = ChartInfo.GetXByBar(LastVisibleBarNumber);
		var y1 = ChartInfo.GetYByPrice(drawRect.SecondPrice);
		var y2 = ChartInfo.GetYByPrice(drawRect.FirstPrice);

		if (x2 < 0 || x1 > Container.Region.Width || y2 < 0 || y2 > Container.Region.Height)
			return;

		var rect = new Rectangle(x1, y1, x2 - x1, y2 - y1);
		context.FillRectangle(color, rect);
	}

	private Color ConvertColor(System.Windows.Media.Color color)
	{
		return Color.FromArgb(color.A, color.R, color.G, color.B);
	}

	#endregion
}