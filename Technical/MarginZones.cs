namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Brushes = System.Drawing.Brushes;
using Color = System.Drawing.Color;
using FilterColor2 = Indicators.FilterColor;

[DisplayName("Margin zones")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.MarginZonesDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602421")]
public class MarginZones : Indicator
{
	#region Nested types

	public enum ZoneDirection
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Up))]
		Up = 0,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Down))]
		Down = 1
	}

	#endregion

	#region Fields

	private readonly ValueDataSeries _100Line = new("100Line", "100% line")
		{ Color = DefaultColors.Maroon.Convert(), Width = 2, ScaleIt = false, VisualType = VisualMode.OnlyValueOnAxis, IsHidden = true };

	private readonly DrawingRectangle _100Rectangle = new(0, 0, 0, 0, Pens.Transparent, Brushes.Gray);

	private readonly ValueDataSeries _150Line = new("150Line", "150% line")
		{ Color = Color.SkyBlue.Convert(), Width = 1, ScaleIt = false, VisualType = VisualMode.Hide, IsHidden = true };

	private readonly DrawingRectangle _150Rectangle = new(0, 0, 0, 0, Pens.Transparent, Brushes.Gray);

	private readonly ValueDataSeries _200Line = new("200Line", "200% line")
		{ Color = Color.CadetBlue.Convert(), Width = 1, ScaleIt = false, VisualType = VisualMode.Hide, IsHidden = true };

	private readonly DrawingRectangle _200Rectangle = new(0, 0, 0, 0, Pens.Transparent, Brushes.Gray);

	private readonly ValueDataSeries _25Line = new("25Line", "25% line")
		{ Color = Color.LightSkyBlue.Convert(), Width = 1, ScaleIt = false, VisualType = VisualMode.OnlyValueOnAxis, IsHidden = true };

	private readonly DrawingRectangle _25Rectangle = new(0, 0, 0, 0, Pens.Transparent, Brushes.Gray);

	private readonly ValueDataSeries _50Line = new("50Line", "50% line")
		{ Color = Color.SkyBlue.Convert(), Width = 1, ScaleIt = false, VisualType = VisualMode.OnlyValueOnAxis, IsHidden = true };

	private readonly DrawingRectangle _50Rectangle = new(0, 0, 0, 0, Pens.Transparent, Brushes.Gray);

	private readonly ValueDataSeries _75Line = new("75Line", "75% line")
		{ Color = Color.LightSkyBlue.Convert(), Width = 1, ScaleIt = false, VisualType = VisualMode.Hide, IsHidden = true };

	private readonly DrawingRectangle _75Rectangle = new(0, 0, 0, 0, Pens.Transparent, Brushes.Gray);

	private readonly ValueDataSeries _baseLineLabel = new("BaseLineLabel", "Base line")
		{ Color = Color.Gray.Convert(), Width = 2, ScaleIt = false, VisualType = VisualMode.OnlyValueOnAxis, IsHidden = true };

	private readonly List<int> _newDays = new();

    private TrendLine _baseLine = new(0, 0, 0, 0, Pens.Gray);
	private RenderPen _baseLineRenderPen = new(Color.Gray);
	private bool _calculated;
	private ZoneDirection _direction;
	private int _lastCalculated;
	private int _margin = 3200;
	private decimal _secondPrice;
	private decimal _tickCost = 6.25m;
	private decimal _zonePrice;
	private decimal _zoneWidth;

	private int _zoneWidthDays = 3;

    private FilterColor2 _zone200Filter;
    private FilterColor2 _zone150Filter;
    private FilterColor2 _zone100Filter;
    private FilterColor2 _zone75Filter;
    private FilterColor2 _zone50Filter;
    private FilterColor2 _zone25Filter;
    private FilterColor2 _baseLineFilter;
    private Filter _customPriceFilter;

    #endregion

    #region Properties

    #region Settings

    [Parameter]
    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Margin),
       GroupName = nameof(Strings.Settings), Description = nameof(Strings.MarginValueDescription), Order = 10)]

    public int Margin
    {
        get => _margin;
        set
        {
            _margin = value;
            RecalculateValues();
        }
    }

    [Parameter]
    [Range(0.000001, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TickCost),
        GroupName = nameof(Strings.Settings), Description = nameof(Strings.TickCostValueDescription), Order = 20)]
    public decimal TickCost
    {
        get => _tickCost;
        set
        {
            _tickCost = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ZoneWidth),
        GroupName = nameof(Strings.Settings), Description = nameof(Strings.DaysLookBackDescription), Order = 30)]
    public int ZoneWidth
    {
        get => _zoneWidthDays;
        set
        {
            _zoneWidthDays = Math.Max(1, value);
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomPrice),
      GroupName = nameof(Strings.Settings), Description = nameof(Strings.CustomPriceFilterDescription), Order = 40)]
    public Filter CustomPriceFilter 
    { 
        get => _customPriceFilter;
        set => SetTrackedProperty(ref _customPriceFilter, value, _ =>
        {
            RecalculateValues();
            RedrawChart();
        });
    }

    #endregion

    #region Visualization

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.DirectionOfZone),
        GroupName = nameof(Strings.Visualization), Description = nameof(Strings.ZoneDirectionDescription), Order = 10)]
    public ZoneDirection Direction
    {
        get => _direction;
        set
        {
            _direction = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Zone200),
       GroupName = nameof(Strings.Visualization), Description = nameof(Strings.EnabledColorElementDescription), Order = 15)]
    public FilterColor2 Zone200Filter 
	{ 
		get => _zone200Filter;
		set => SetTrackedProperty(ref _zone200Filter, value, propName =>
		{
            switch (propName)
            {
                case nameof(value.Value):
                    _200Line.Color = value.Value;
                    _200Rectangle.Brush = new SolidBrush(value.Value.Convert());
                    break;
                case nameof(value.Enabled):
                    _200Line.VisualType = value.Enabled ? VisualMode.OnlyValueOnAxis : VisualMode.Hide;
                    break;
            }

			RedrawChart();
        });
	}

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Zone150),
      GroupName = nameof(Strings.Visualization), Description = nameof(Strings.EnabledColorElementDescription), Order = 20)]
    public FilterColor2 Zone150Filter
    {
        get => _zone150Filter;
        set => SetTrackedProperty(ref _zone150Filter, value, propName =>
        {
            switch (propName)
            {
                case nameof(value.Value):
                    _150Line.Color = value.Value;
                    _150Rectangle.Brush = new SolidBrush(value.Value.Convert());
                    break;
                case nameof(value.Enabled):
                    _150Line.VisualType = value.Enabled ? VisualMode.OnlyValueOnAxis : VisualMode.Hide;
                    break;
            }

            RedrawChart();
        });
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Zone100),
    GroupName = nameof(Strings.Visualization), Description = nameof(Strings.EnabledColorElementDescription), Order = 30)]
    public FilterColor2 Zone100Filter
    {
        get => _zone100Filter; 
        set => SetTrackedProperty(ref _zone100Filter, value, propName =>
        {
            switch (propName)
            {
                case nameof(value.Value):
                    _100Line.Color = value.Value;
                    _100Rectangle.Brush = new SolidBrush(value.Value.Convert());
                    break;
                case nameof(value.Enabled):
                    _100Line.VisualType = value.Enabled ? VisualMode.OnlyValueOnAxis : VisualMode.Hide;
                    break;
            }

            RedrawChart();
        });
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Zone75),
    GroupName = nameof(Strings.Visualization), Description = nameof(Strings.EnabledColorElementDescription), Order = 40)]
    public FilterColor2 Zone75Filter
    {
        get => _zone75Filter;
        set => SetTrackedProperty(ref _zone75Filter, value, propName =>
        {
            switch (propName)
            {
                case nameof(value.Value):
                    _75Line.Color = value.Value;
                    _75Rectangle.Brush = new SolidBrush(value.Value.Convert());
                    break;
                case nameof(value.Enabled):
                    _75Line.VisualType = value.Enabled ? VisualMode.OnlyValueOnAxis : VisualMode.Hide;
                    break;
            }

            RedrawChart();
        });
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Zone50),
    GroupName = nameof(Strings.Visualization), Description = nameof(Strings.EnabledColorElementDescription), Order = 50)]
    public FilterColor2 Zone50Filter
    {
        get => _zone50Filter;
        set => SetTrackedProperty(ref _zone50Filter, value, propName =>
        {
            switch (propName)
            {
                case nameof(value.Value):
                    _50Line.Color = value.Value;
                    _50Rectangle.Brush = new SolidBrush(value.Value.Convert());
                    break;
                case nameof(value.Enabled):
                    _50Line.VisualType = value.Enabled ? VisualMode.OnlyValueOnAxis : VisualMode.Hide;
                    break;
            }

            RedrawChart();
        });
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Zone25),
    GroupName = nameof(Strings.Visualization), Description = nameof(Strings.EnabledColorElementDescription), Order = 60)]
    public FilterColor2 Zone25Filter
    {
        get => _zone25Filter;
        set => SetTrackedProperty(ref _zone25Filter, value, propName =>
        {
            switch (propName)
            {
                case nameof(value.Value):
                    _25Line.Color = value.Value;
                    _25Rectangle.Brush = new SolidBrush(value.Value.Convert());
                    break;
                case nameof(value.Enabled):
                    _25Line.VisualType = value.Enabled ? VisualMode.OnlyValueOnAxis : VisualMode.Hide;
                    break;
            }

            RedrawChart();
        });
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BaseLine),
    GroupName = nameof(Strings.Visualization), Description = nameof(Strings.EnabledColorElementDescription), Order = 60)]
    public FilterColor2 BaseLineFilter
    {
        get => _baseLineFilter;
        set => SetTrackedProperty(ref _baseLineFilter, value, propName =>
        {
			switch (propName)
			{
				case "Value":
                    _baseLineRenderPen = new(value.Value.Convert());
                    break;
                case "Enabled":
                    _baseLineLabel.VisualType = value.Enabled ? VisualMode.OnlyValueOnAxis : VisualMode.Hide;
                    break;
            }

            RedrawChart();
        });
    }

    #endregion

    #region Hidden 

    [Browsable(false)]
    [Obsolete]
    public Color Zone200LineColor
	{
		get => Zone200Filter.Value.Convert();
		set => Zone200Filter.Value = value.Convert();
	}

    [Browsable(false)]
	[Obsolete]
	public bool ShowZone200
	{
		get => Zone200Filter.Enabled;
		set => Zone200Filter.Enabled = value;
    }

    [Browsable(false)]
    [Obsolete]
    public Color Zone150LineColor
	{
        get => Zone150Filter.Value.Convert();
        set => Zone150Filter.Value = value.Convert();
    }

    [Browsable(false)]
    [Obsolete]
    public bool ShowZone150
	{
        get => Zone150Filter.Enabled;
        set => Zone150Filter.Enabled = value;
    }

    [Browsable(false)]
    [Obsolete]
    public Color Zone75LineColor
	{
        get => Zone75Filter.Value.Convert();
        set => Zone75Filter.Value = value.Convert();
    }

    [Browsable(false)]
    [Obsolete]
    public bool ShowZone75
	{
        get => Zone75Filter.Enabled;
        set => Zone75Filter.Enabled = value;
    }

    [Browsable(false)]
    [Obsolete]
    public Color Zone50LineColor
	{
        get => Zone50Filter.Value.Convert();
        set => Zone50Filter.Value = value.Convert();
    }

    [Browsable(false)]
    [Obsolete]
    public bool ShowZon50
	{
        get => Zone50Filter.Enabled;
        set => Zone50Filter.Enabled = value;
    }

    [Browsable(false)]
    [Obsolete]
    public Color Zone25LineColor
	{
        get => Zone25Filter.Value.Convert();
        set => Zone25Filter.Value = value.Convert();
    }

    [Browsable(false)]
    [Obsolete]
    public bool ShowZone25
	{
        get => Zone25Filter.Enabled;
        set => Zone25Filter.Enabled = value;
    }

    [Browsable(false)]
    [Obsolete]
    public Color Zone100LineColor
	{
        get => Zone100Filter.Value.Convert();
        set => Zone100Filter.Value = value.Convert();
    }

    [Browsable(false)]
    [Obsolete]
    public bool ShowZone100
	{
        get => Zone100Filter.Enabled;
        set => Zone100Filter.Enabled = value;
    }

    [Browsable(false)]
    [Obsolete]
    public Color BaseLineColor
	{
        get => BaseLineFilter.Value.Convert();
        set => BaseLineFilter.Value = value.Convert();
    }

    [Browsable(false)]
    [Obsolete]
    public bool ShowBaseLine
	{
        get => BaseLineFilter.Enabled;
        set => BaseLineFilter.Enabled = value;
    }

    [Browsable(false)]
    [Obsolete]
    public bool AutoPrice
	{
		get => !CustomPriceFilter.Enabled;
        set => CustomPriceFilter.Enabled = !value;

    }

    [Browsable(false)]
    [Obsolete]
    public decimal CustomPrice
	{
        get => CustomPriceFilter.Value;
        set => CustomPriceFilter.Value = value;
    }

    #endregion

    #endregion

    #region ctor

    public MarginZones()
		: base(true)
	{
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		DrawAbovePrice = false;
		SubscribeToDrawingEvents(DrawingLayouts.Historical);

        DataSeries[0] = _baseLineLabel;
        DataSeries.Add(_100Line);
		DataSeries.Add(_25Line);
		DataSeries.Add(_50Line);
		DataSeries.Add(_75Line);
		DataSeries.Add(_150Line);
		DataSeries.Add(_200Line);

        Zone200Filter = new(true) { Value = _200Line.Color, Enabled = _200Line.VisualType != VisualMode.Hide };
        Zone150Filter = new(true) { Value = _150Line.Color, Enabled = _150Line.VisualType != VisualMode.Hide };
        Zone100Filter = new(true) { Value = _100Line.Color, Enabled = _100Line.VisualType != VisualMode.Hide };
        Zone75Filter = new(true) { Value = _75Line.Color, Enabled = _75Line.VisualType != VisualMode.Hide };
        Zone50Filter = new(true) { Value = _50Line.Color, Enabled = _50Line.VisualType != VisualMode.Hide };
        Zone25Filter = new(true) { Value = _25Line.Color, Enabled = _25Line.VisualType != VisualMode.Hide };
        BaseLineFilter = new(true) { Value = _baseLineLabel.Color, Enabled = true };
        CustomPriceFilter = new(true);
    }

	#endregion

	#region Protected methods

	protected override void OnInitialize()
	{
		_baseLineLabel.ShowZeroValue = false;
		_100Line.ShowZeroValue = false;
		_25Line.ShowZeroValue = false;
		_50Line.ShowZeroValue = false;
		_75Line.ShowZeroValue = false;
		_150Line.ShowZeroValue = false;
		_200Line.ShowZeroValue = false;
    }

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo is null)
			return;

		if (BaseLineFilter.Enabled)
		{
			var x1 = ChartInfo.GetXByBar(_baseLine.FirstBar);
			var x2 = ChartInfo.GetXByBar(LastVisibleBarNumber);
			var y = ChartInfo.GetYByPrice(_baseLine.FirstPrice);

			if (x2 >= 0 && x1 <= Container.Region.Width && y >= 0 && y <= Container.Region.Height)
				context.DrawLine(_baseLineRenderPen, x1, y, x2, y);
		}

        if (Zone25Filter.Enabled)
            DrawZone(context, _25Rectangle, Zone25Filter.Value.Convert());

        if (Zone50Filter.Enabled)
            DrawZone(context, _50Rectangle, Zone50Filter.Value.Convert());

        if (Zone75Filter.Enabled)
            DrawZone(context, _75Rectangle, Zone75Filter.Value.Convert());

        if (Zone100Filter.Enabled)
            DrawZone(context, _100Rectangle, Zone100Filter.Value.Convert());

        if (Zone150Filter.Enabled)
            DrawZone(context, _150Rectangle, Zone150Filter.Value.Convert());

		if (Zone200Filter.Enabled)
			DrawZone(context, _200Rectangle, Zone200Filter.Value.Convert());
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
		else
		{
			_calculated = true;
			_baseLine.FirstBar = 0;
			_baseLine.SecondBar = 0;
			_baseLine.FirstPrice = 0;
			_baseLine.SecondPrice = 0;

            if (CustomPriceFilter.Enabled)
                _zonePrice = CustomPriceFilter.Value;
            else
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

            SetRectanglesValues(_100Rectangle,firstBar, bar, _secondPrice, _zoneWidth);
            SetRectanglesValues(_25Rectangle, firstBar, bar, _25Line[bar], _zoneWidth / 4);
            SetRectanglesValues(_50Rectangle, firstBar, bar, _50Line[bar], _zoneWidth / 2);
            SetRectanglesValues(_75Rectangle, firstBar, bar, _75Line[bar], _zoneWidth / 4);
            SetRectanglesValues(_150Rectangle, firstBar, bar, _150Line[bar], _zoneWidth);
            SetRectanglesValues(_200Rectangle, firstBar, bar, _200Line[bar], _zoneWidth);

            _lastCalculated = bar;
		}

		foreach (var dataSeries in DataSeries)
		{
			var series = (ValueDataSeries)dataSeries;
			series[bar] = series[bar - 1];
        }
	}

    private void SetRectanglesValues(DrawingRectangle rectangle, int firstBar, int secondBar, decimal firstPrice, decimal width)
    {
        rectangle.FirstBar = firstBar;
        rectangle.SecondBar = secondBar;
        rectangle.FirstPrice = firstPrice;
        rectangle.SecondPrice = firstPrice + width;
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

	#endregion
}