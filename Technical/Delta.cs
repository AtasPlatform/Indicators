namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Windows.Media;

using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

using Color = System.Windows.Media.Color;

[Category("Bid x Ask,Delta,Volume")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/3996-delta")]
public class Delta : Indicator
{
	#region Nested types

	[Serializable]
	public enum BarDirection
	{
		[Display(ResourceType = typeof(Resources), Name = "Any")]
		Any = 0,

		[Display(ResourceType = typeof(Resources), Name = "Bullish")]
		Bullish = 1,

		[Display(ResourceType = typeof(Resources), Name = "Bearlish")]
		Bearlish = 2
	}

	[Serializable]
	public enum DeltaType
	{
		[Display(ResourceType = typeof(Resources), Name = "Any")]
		Any = 0,

		[Display(ResourceType = typeof(Resources), Name = "Positive")]
		Positive = 1,

		[Display(ResourceType = typeof(Resources), Name = "Negative")]
		Negative = 2
	}

	[Serializable]
	public enum DeltaVisualMode
	{
		[Display(ResourceType = typeof(Resources), Name = "Candles")]
		Candles = 0,

		[Display(ResourceType = typeof(Resources), Name = "HighLow")]
		HighLow = 1,

		[Display(ResourceType = typeof(Resources), Name = "Histogram")]
		Histogram = 2,

		[Display(ResourceType = typeof(Resources), Name = "Bars")]
		Bars = 3
	}

	public enum Location
	{
		[Display(ResourceType = typeof(Resources), Name = "Up")]
		Up,

		[Display(ResourceType = typeof(Resources), Name = "Middle")]
		Middle,

		[Display(ResourceType = typeof(Resources), Name = "Down")]
		Down
	}

	#endregion

	#region Fields

	private readonly CandleDataSeries _candles = new("Delta candles")
	{
		DownCandleColor = Colors.Red,
		UpCandleColor = Colors.Green,
		IsHidden = true,
		ShowCurrentValue = false,
		UseMinimizedModeIfEnabled = true,
		ResetAlertsOnNewBar = true
    };

	private readonly ValueDataSeries _currentValues = new("Current Values")
	{
		IsHidden = true,
		VisualType = VisualMode.OnlyValueOnAxis,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true,
		IgnoredByAlerts = true
    };

	private readonly ValueDataSeries _diapasonHigh = new("Delta range high")
	{
		Color = Color.FromArgb(128, 128, 128, 128),
		ShowZeroValue = false,
		ShowCurrentValue = false,
		VisualType = VisualMode.Hide,
		IsHidden = true,
		UseMinimizedModeIfEnabled = true,
		IgnoredByAlerts = true
    };

	private readonly ValueDataSeries _diapasonLow = new("Delta range low")
	{
		Color = Color.FromArgb(128, 128, 128, 128),
		ShowZeroValue = false,
		ShowCurrentValue = false,
		VisualType = VisualMode.Hide,
		IsHidden = true,
		UseMinimizedModeIfEnabled = true,
		IgnoredByAlerts = true
    };

	private readonly ValueDataSeries _delta = new("Delta")
	{
		Color = Colors.Red, 
		VisualType = VisualMode.Hide,
		ShowZeroValue = false,
		ShowCurrentValue = false,
		IsHidden = true,
		UseMinimizedModeIfEnabled = true,
		ResetAlertsOnNewBar = true
	};
	
	private decimal _alertFilter;
	private BarDirection _barDirection;
	private DeltaType _deltaType;
	private System.Drawing.Color _downColor = System.Drawing.Color.Red;

	private ValueDataSeries _downSeries = new(Resources.Down)
	{
		VisualType = VisualMode.Hide,
		IsHidden = true,
		UseMinimizedModeIfEnabled = true,
		IgnoredByAlerts = true
    };

	private decimal _filter;

	private System.Drawing.Color _fontColor;

	private RenderStringFormat _format = new()
	{
		Alignment = StringAlignment.Center,
		LineAlignment = StringAlignment.Center
	};

	private int _lastBar;
	private int _lastBarAlert;
	private bool _minimizedMode;
	private DeltaVisualMode _mode = DeltaVisualMode.Candles;
	private Color _neutralColor = Colors.Gray;
	private decimal _prevDeltaValue;
	private bool _showCurrentValues = true;

	private System.Drawing.Color _upColor = System.Drawing.Color.Green;

	private ValueDataSeries _upSeries = new(Resources.Up)
	{
		Color = Colors.Green,
		VisualType = VisualMode.Hide,
		IsHidden = true,
		UseMinimizedModeIfEnabled = true,
		IgnoredByAlerts = true
	};

    #endregion

    #region Properties

    #region Visualization

    [Display(ResourceType = typeof(Resources), Name = "VisualMode", GroupName = "Visualization", Order = 10)]
    public DeltaVisualMode Mode
    {
	    get => _mode;
	    set
	    {
		    _mode = value;

		    if (_mode == DeltaVisualMode.Histogram)
		    {
			    _delta.VisualType = VisualMode.Histogram;
			    _diapasonHigh.VisualType = VisualMode.Hide;
			    _diapasonLow.VisualType = VisualMode.Hide;
			    _candles.Visible = false;
		    }
		    else if (_mode == DeltaVisualMode.HighLow)
		    {
			    _delta.VisualType = VisualMode.Histogram;
                _diapasonHigh.VisualType = VisualMode.Histogram;
			    _diapasonLow.VisualType = VisualMode.Histogram;
			    _candles.Visible = false;
		    }
		    else if (_mode == DeltaVisualMode.Candles)
		    {
			    _delta.VisualType = VisualMode.Hide;
			    _diapasonHigh.VisualType = VisualMode.Hide;
			    _diapasonLow.VisualType = VisualMode.Hide;
			    _candles.Visible = true;
			    _candles.Mode = CandleVisualMode.Candles;
		    }
		    else
		    {
			    _delta.VisualType = VisualMode.Hide;
			    _diapasonHigh.VisualType = VisualMode.Hide;
			    _diapasonLow.VisualType = VisualMode.Hide;
			    _candles.Visible = true;
			    _candles.Mode = CandleVisualMode.Bars;
		    }

		    RaisePropertyChanged("Mode");
		    RecalculateValues();
	    }
    }

    [Display(ResourceType = typeof(Resources), Name = "Minimizedmode", GroupName = "Visualization", Order = 20)]

    public bool MinimizedMode
    {
	    get => _minimizedMode;
	    set
	    {
		    _minimizedMode = value;
		    RaisePropertyChanged("MinimizedMode");
		    RecalculateValues();
	    }
    }

    [Display(ResourceType = typeof(Resources), Name = "ShowCurrentValue", GroupName = "Visualization", Order = 30)]
    public bool ShowCurrentValues
    {
	    get => _showCurrentValues;
	    set
	    {
		    _showCurrentValues = value;
		    _currentValues.ShowCurrentValue = value;
	    }
    }

    [Display(ResourceType = typeof(Resources), Name = "BullishColor", GroupName = "Drawing", Order = 40)]
    public Color UpColor
    {
	    get => _upColor.Convert();
	    set
	    {
		    _upColor = value.Convert();
		    _candles.UpCandleColor = value;
		    _upSeries.Color = value;
	    }
    }

    [Display(ResourceType = typeof(Resources), Name = "BearlishColor", GroupName = "Drawing", Order = 50)]
    public Color DownColor
    {
	    get => _downColor.Convert();
	    set
	    {
		    _downColor = value.Convert();
		    _candles.DownCandleColor = value;
		    _downSeries.Color = value;
	    }
    }

    [Display(ResourceType = typeof(Resources), Name = "NeutralBorderColor", GroupName = "Drawing", Order = 60)]
    public Color NeutralColor
    {
	    get => _neutralColor;
	    set
	    {
		    _neutralColor = value;
		    _candles.BorderColor = value;
		    _diapasonHigh.Color = _diapasonLow.Color = value;
	    }
    }

    #endregion

    #region Filters

    [Display(ResourceType = typeof(Resources), Name = "BarsDirection", GroupName = "Filters", Order = 100)]
    public BarDirection BarsDirection
    {
	    get => _barDirection;
	    set
	    {
		    _barDirection = value;
		    RecalculateValues();
	    }
    }

    [Display(ResourceType = typeof(Resources), Name = "DeltaType", GroupName = "Filters", Order = 110)]
    public DeltaType DeltaTypes
    {
	    get => _deltaType;
	    set
	    {
		    _deltaType = value;
		    RecalculateValues();
	    }
    }

    [Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Filters", Order = 120)]
    public decimal Filter
    {
	    get => _filter;
	    set
	    {
		    _filter = value;
		    RecalculateValues();
	    }
    }

    [Display(ResourceType = typeof(Resources), Name = "ShowDivergence", GroupName = "Filters", Order = 130)]
    public bool ShowDivergence { get; set; }

    #endregion

    #region Volume

    [Display(ResourceType = typeof(Resources), Name = "Show", GroupName = "VolumeLabel", Order = 200, Description = "VolumeLabelDescription")]
    public bool ShowVolume { get; set; }

    [Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "VolumeLabel", Order = 210)]
    public Color FontColor
    {
	    get => _fontColor.Convert();
	    set => _fontColor = value.Convert();
    }

    [Display(ResourceType = typeof(Resources), Name = "Location", GroupName = "VolumeLabel", Order = 220)]
    public Location VolLocation { get; set; } = Location.Middle;

    [Display(ResourceType = typeof(Resources), Name = "Font", GroupName = "VolumeLabel", Order = 230)]
    public FontSetting Font { get; set; } = new("Arial", 10);

    #endregion

    #region Alerts

    [Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "Alerts", Order = 300)]
    public bool UseAlerts { get; set; }

    [Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Alerts", Order = 310)]
    public decimal AlertFilter
    {
	    get => _alertFilter;
	    set
	    {
		    _lastBarAlert = 0;
		    _alertFilter = value;
	    }
    }

    [Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts", Order = 320)]
    public string AlertFile { get; set; } = "alert1";

    [Display(ResourceType = typeof(Resources), Name = "FontColor", GroupName = "Alerts", Order = 330)]
    public Color AlertForeColor { get; set; } = Color.FromArgb(255, 247, 249, 249);

    [Display(ResourceType = typeof(Resources), Name = "BackGround", GroupName = "Alerts", Order = 340)]
    public Color AlertBGColor { get; set; } = Color.FromArgb(255, 75, 72, 72);

    #endregion

	#endregion

	#region ctor

	public Delta()
		: base(true)
	{
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
		FontColor = Colors.Blue;

		Panel = IndicatorDataProvider.NewPanel;
		DataSeries[0] = _delta; //2
		
		DataSeries.Insert(0, _diapasonHigh); //0
		DataSeries.Insert(1, _diapasonLow); //1
		DataSeries.Add(_candles); //4

		DataSeries.Add(_upSeries);
		DataSeries.Add(_downSeries);
		DataSeries.Add(_currentValues);
	}

    #endregion

    #region Protected methods

    protected override void OnApplyDefaultColors()
    {
	    if (ChartInfo is null)
		    return;

	    UpColor = ChartInfo.ColorsStore.UpCandleColor.Convert();
	    DownColor = ChartInfo.ColorsStore.DownCandleColor.Convert();
	    NeutralColor = ChartInfo.ColorsStore.BarBorderPen.Color.Convert();
	    FontColor = ChartInfo.ColorsStore.FootprintMaximumVolumeTextColor.Convert();
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo is null || InstrumentInfo is null)
			return;

		if (ShowDivergence)
		{
			for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
			{
				try
				{
					if (_upSeries[i] == 0 && _downSeries[i] == 0)
						continue;

					var candle = GetCandle(i);
					var x = ChartInfo.PriceChartContainer.GetXByBar(i, false);

					if (_upSeries[i] != 0)
					{
						var yPrice = ChartInfo.PriceChartContainer.GetYByPrice(candle.Low, false) + 10;

						if (yPrice <= ChartInfo.PriceChartContainer.Region.Bottom)
						{
							var rect = new Rectangle(x - 5, yPrice - 4, 8, 8);
							context.FillEllipse(_upColor, rect);
						}
					}

					if (_downSeries[i] != 0)
					{
						var yPrice = ChartInfo.PriceChartContainer.GetYByPrice(candle.High, false) - 10;

						if (yPrice <= ChartInfo.PriceChartContainer.Region.Bottom)
						{
							var rect = new Rectangle(x - 5, yPrice - 4, 8, 8);
							context.FillEllipse(_downColor, rect);
						}
					}
				}
				catch (OverflowException)
				{
					//Old instrument coordinates exception
					return;
				}
			}
		}

		if (!ShowVolume || ChartInfo.ChartVisualMode != ChartVisualModes.Clusters || Panel == IndicatorDataProvider.CandlesPanel)
			return;

		var minWidth = GetMinWidth(context, FirstVisibleBarNumber, LastVisibleBarNumber);
		var barWidth = ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0);

		if (minWidth > barWidth)
			return;

		var strHeight = context.MeasureString("0", Font.RenderObject).Height;

		var y = VolLocation switch
		{
			Location.Up => Container.Region.Y,
			Location.Down => Container.Region.Bottom - strHeight,
			_ => Container.Region.Y + (Container.Region.Bottom - Container.Region.Y) / 2
		};

		for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
		{
			decimal value;

			if (MinimizedMode)
			{
				value = _candles[i].Close > _candles[i].Open
					? _candles[i].Close
					: -_candles[i].Open;
			}
			else
				value = _candles[i].Close;

			var renderText = $"{value:0.#####}";

			var strRect = new Rectangle(ChartInfo.GetXByBar(i),
				y,
				barWidth,
				strHeight);

			context.DrawString(renderText, Font.RenderObject, _fontColor, strRect, _format);
		}
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			DataSeries.ForEach(x => x.Clear());
			_upSeries.Clear();
			_downSeries.Clear();
		}

		var candle = GetCandle(bar);
		var deltaValue = candle.Delta;
		var absDelta = Math.Abs(deltaValue);
		var maxDelta = candle.MaxDelta;
		var minDelta = candle.MinDelta;

		var isUnderFilter = absDelta < _filter;

		if (_barDirection == BarDirection.Bullish)
		{
			if (candle.Close < candle.Open)
				isUnderFilter = true;
		}
		else if (_barDirection == BarDirection.Bearlish)
		{
			if (candle.Close > candle.Open)
				isUnderFilter = true;
		}

		if (_deltaType == DeltaType.Negative && deltaValue > 0)
			isUnderFilter = true;

		if (_deltaType == DeltaType.Positive && deltaValue < 0)
			isUnderFilter = true;

		if (isUnderFilter)
		{
			deltaValue = 0;
			absDelta = 0;
			minDelta = maxDelta = 0;
		}

		_delta[bar] = MinimizedMode ? absDelta : deltaValue;

		_delta.Colors[bar] = deltaValue > 0 ? _upColor : _downColor;

		if (MinimizedMode)
		{
			var high = Math.Abs(maxDelta);
			var low = Math.Abs(minDelta);
			_diapasonLow[bar] = Math.Min(Math.Min(high, low), absDelta);
			_diapasonHigh[bar] = Math.Max(high, low);

			var currentCandle = _candles[bar];
			currentCandle.Open = deltaValue > 0 ? 0 : absDelta;
			currentCandle.Close = deltaValue > 0 ? absDelta : 0;
			currentCandle.High = _diapasonHigh[bar];
			currentCandle.Low = _diapasonLow[bar];
		}
		else
		{
			_diapasonLow[bar] = minDelta;
			_diapasonHigh[bar] = maxDelta;

			_candles[bar].Open = 0;
			_candles[bar].Close = deltaValue;
			_candles[bar].High = maxDelta;
			_candles[bar].Low = minDelta;
		}

		if (candle.Close > candle.Open && _candles[bar].Close < _candles[bar].Open)
			_downSeries[bar] = _candles[bar].High;
		else
			_downSeries[bar] = 0;

		if (candle.Close < candle.Open && _candles[bar].Close > _candles[bar].Open)
			_upSeries[bar] = MinimizedMode ? _candles[bar].High : _candles[bar].Low;
		else
			_upSeries[bar] = 0;

		if (_lastBar != bar)
		{
			_prevDeltaValue = deltaValue;
			_lastBar = bar;
		}

		if (UseAlerts && CurrentBar - 1 == bar && _lastBarAlert != bar)
		{
			if ((deltaValue >= AlertFilter && _prevDeltaValue < AlertFilter) || (deltaValue <= AlertFilter && _prevDeltaValue > AlertFilter))
			{
				_lastBarAlert = bar;
				AddAlert(AlertFile, InstrumentInfo.Instrument, $"Delta reached {AlertFilter} filter", AlertBGColor, AlertForeColor);
			}
		}

		_prevDeltaValue = deltaValue;

		if (!ShowCurrentValues)
			return;

		_currentValues[bar] = MinimizedMode ? absDelta : deltaValue;
        _currentValues.Colors[bar] = deltaValue > 0 ? _upColor : _downColor;
	}

	#endregion

	#region Private methods
	

	private int GetMinWidth(RenderContext context, int startBar, int endBar)
	{
		var maxLength = 0;

		for (var i = startBar; i <= endBar; i++)
		{
			decimal value;

			if (MinimizedMode)
			{
				value = _candles[i].Close > _candles[i].Open
					? _candles[i].Close
					: -_candles[i].Open;
			}
			else
				value = _candles[i].Close;

			var length = $"{value:0.#####}".Length;

			if (length > maxLength)
				maxLength = length;
		}

		var sampleStr = "";

		for (var i = 0; i < maxLength; i++)
			sampleStr += '0';

		return context.MeasureString(sampleStr, Font.RenderObject).Width;
	}

	#endregion
}