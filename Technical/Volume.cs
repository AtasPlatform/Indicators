namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Windows.Media;

using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

using Color = System.Drawing.Color;

[Category("Bid x Ask,Delta,Volume")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/2471-volume")]
public class Volume : Indicator
{
	#region Nested types

	public enum InputType
	{
		[Display(ResourceType = typeof(Resources), Name = "Volume")]
		Volume,

		[Display(ResourceType = typeof(Resources), Name = "Ticks")]
		Ticks,

		[Display(ResourceType = typeof(Resources), Name = "Ask")]
		Asks,

		[Display(ResourceType = typeof(Resources), Name = "Bid")]
		Bids
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

	private bool _deltaColored;
	private decimal _filter;
	private Color _filterColor = Color.LightBlue;
	private InputType _input = InputType.Volume;
	private int _lastReverseAlert;
	private int _lastVolumeAlert;
	private Color _negColor = Color.Red;
	private Color _neutralColor = Color.Gray;
	private Color _posColor = Color.Green;

    #region Legacy Series

	//For old templates
	private readonly ValueDataSeries _negative = new("Negative")
    {
	    VisualType = VisualMode.Hide,
		IsHidden = true
    };

    private readonly ValueDataSeries _neutral = new("Neutral")
    {
	    VisualType = VisualMode.Hide,
		Color = Colors.Gray,
	    IsHidden = true
    };

    private readonly ValueDataSeries _positive = new("Positive")
    {
	    VisualType = VisualMode.Hide,
	    Color = Colors.Green,
        IsHidden = true
    };

	#endregion

    private ValueDataSeries _renderSeries = new(Resources.Visualization)
    {
	    VisualType = VisualMode.Histogram,
	    ShowZeroValue = false,
	    UseMinimizedModeIfEnabled = true,
	    ResetAlertsOnNewBar = true
    };

    private bool _useFilter;

	protected RenderStringFormat Format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

	protected Highest HighestVol = new();
	protected ValueDataSeries MaxVolSeries;
	protected Color TextColor = Color.Blue;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "Type", GroupName = "Calculation", Order = 10)]
	public InputType Input
	{
		get => _input;
		set
		{
			_input = value;
			RaisePropertyChanged("Type");
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "DeltaColored", GroupName = "Drawing", Order = 600)]
	public bool DeltaColored
	{
		get => _deltaColored;
		set
		{
			_deltaColored = value;
			RaisePropertyChanged("DeltaColored");
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Positive", GroupName = "Drawing", Order = 610)]
	public System.Windows.Media.Color PosColor
	{
		get => _posColor.Convert();
		set
		{
			_positive.Color = value;
			RaisePropertyChanged("Positive");
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Negative", GroupName = "Drawing", Order = 620)]
	public System.Windows.Media.Color NegColor
	{
		get => _negColor.Convert();
		set
		{
            _negative.Color = value;
			RaisePropertyChanged("Negative");
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Neutral", GroupName = "Drawing", Order = 630)]
	public System.Windows.Media.Color NeutralColor
	{
		get => _neutralColor.Convert();
        set
		{
            _neutral.Color = value;
			RaisePropertyChanged("Neutral");
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public Volume()
		: base(true)
	{
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);

		Panel = IndicatorDataProvider.NewPanel;

		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

		MaxVolSeries = (ValueDataSeries)HighestVol.DataSeries[0];
		MaxVolSeries.IsHidden = true;
		MaxVolSeries.VisualType = VisualMode.Hide;
		MaxVolSeries.UseMinimizedModeIfEnabled = true;
		MaxVolSeries.IgnoredByAlerts = true;
		DataSeries[0] = _renderSeries;
		DataSeries.Add(MaxVolSeries);
		DataSeries[1].IgnoredByAlerts = true;

		//Legacy templates
		DataSeries.Add(_positive);
		DataSeries.Add(_negative);
		DataSeries.Add(_neutral);
		_positive.PropertyChanged += PositiveChanged;
		_negative.PropertyChanged += NegativeChanged;
		_neutral.PropertyChanged += NeutralChanged;
    }

    #endregion

    protected override void OnApplyDefaultColors()
    {
	    if (ChartInfo != null)
	    {
		    PosColor = ChartInfo.ColorsStore.UpCandleColor.Convert();
		    NegColor = ChartInfo.ColorsStore.DownCandleColor.Convert();
		    NeutralColor = ChartInfo.ColorsStore.UpCandleColor.Convert();
	    }
    }

    #region Public methods

    public override string ToString()
	{
		return "Volume";
	}

	#endregion

	#region Protected methods

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
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
			var value = _renderSeries[i];
			var renderText = ChartInfo.TryGetMinimizedVolumeString(value);

			var strRect = new Rectangle(ChartInfo.GetXByBar(i),
				y,
				barWidth,
				strHeight);
			context.DrawString(renderText, Font.RenderObject, TextColor, strRect, Format);
		}
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		var candle = GetCandle(bar);

		var val = Input switch
		{
			InputType.Ticks => candle.Ticks,
			InputType.Asks => candle.Ask,
			InputType.Bids => candle.Bid,
			_ => candle.Volume
		};
		_renderSeries[bar] = val;

		if (bar == CurrentBar - 1)
		{
			if (UseVolumeAlerts && _lastVolumeAlert != bar && val >= _filter && _filter != 0)
			{
				AddAlert(AlertVolumeFile, $"Candle volume: {val}");
				_lastVolumeAlert = bar;
			}

			if (UseReverseAlerts && _lastReverseAlert != bar)
			{
				if ((candle.Delta < 0 && candle.Close > candle.Open) || (candle.Delta > 0 && candle.Close < candle.Open))
				{
					AddAlert(AlertReverseFile, $"Candle volume: {val} (Reverse alert)");
					_lastReverseAlert = bar;
				}
			}
		}

		HighestVol.Calculate(bar, candle.Volume);

		if (_useFilter && val > _filter)
		{
			_renderSeries.Colors[bar] = _filterColor;
			return;
		}

		if (_deltaColored)
		{
			if (candle.Delta > 0)
				_renderSeries.Colors[bar] = _posColor;
			else if (candle.Delta < 0)
				_renderSeries.Colors[bar] = _negColor;
			else
				_renderSeries.Colors[bar] = _neutralColor;
		}
		else
		{
			if (candle.Close > candle.Open)
				_renderSeries.Colors[bar] = _posColor;
			else if (candle.Close < candle.Open)
				_renderSeries.Colors[bar] = _negColor;
			else
				_renderSeries.Colors[bar] = _neutralColor;
		}
	}

	#endregion

	#region Private methods

	private int GetMinWidth(RenderContext context, int startBar, int endBar)
	{
		var maxLength = 0;

		for (var i = startBar; i <= endBar; i++)
		{
			var value = _renderSeries[i];
			var length = $"{value:0.#####}".Length;

			if (length > maxLength)
				maxLength = length;
		}

		var sampleStr = "";

		for (var i = 0; i < maxLength; i++)
			sampleStr += '0';

		return context.MeasureString(sampleStr, Font.RenderObject).Width;
	}

	private void NeutralChanged(object sender, PropertyChangedEventArgs e)
	{
		_neutralColor = _neutral.Color.Convert();
	}

	private void NegativeChanged(object sender, PropertyChangedEventArgs e)
	{
		_negColor = _negative.Color.Convert();
	}

	private void PositiveChanged(object sender, PropertyChangedEventArgs e)
	{
		_posColor = _positive.Color.Convert();
	}

    #endregion

    #region Volume label

    [Display(ResourceType = typeof(Resources), Name = "Show", GroupName = "VolumeLabel", Order = 100, Description = "VolumeLabelDescription")]
	public bool ShowVolume { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "VolumeLabel", Order = 110)]
	public System.Windows.Media.Color FontColor
	{
		get => TextColor.Convert();
		set => TextColor = value.Convert();
	}

	[Display(ResourceType = typeof(Resources), Name = "Location", GroupName = "VolumeLabel", Order = 120)]
	public Location VolLocation { get; set; } = Location.Middle;

	[Display(ResourceType = typeof(Resources), Name = "Font", GroupName = "VolumeLabel", Order = 130)]
	public FontSetting Font { get; set; } = new("Arial", 10);

	#endregion

	#region Filter

	[Display(ResourceType = typeof(Resources), Name = "UseFilter", GroupName = "Filter", Order = 210)]
	public bool UseFilter
	{
		get => _useFilter;
		set
		{
			_useFilter = value;
			RaisePropertyChanged("UseFilter");
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Filter", Order = 220)]
	public decimal FilterValue
	{
		get => _filter;
		set
		{
			_filter = value;
			RaisePropertyChanged("Filter");
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Filter", Order = 230)]
	public System.Windows.Media.Color FilterColor
	{
		get => _filterColor.Convert();
		set
		{
			_filterColor = value.Convert();

			RaisePropertyChanged("Color");
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "Filter", Order = 310)]
	public bool UseVolumeAlerts { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Filter", Order = 320)]
	public string AlertVolumeFile { get; set; } = "alert1";

	#endregion

	#region Divergence alert

	[Display(ResourceType = typeof(Resources), Name = "Enabled", GroupName = "ReverseAlert", Order = 410, Description = "ReverseAlertDescription")]
	public bool UseReverseAlerts { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "ReverseAlert", Order = 420)]
	public string AlertReverseFile { get; set; } = "alert1";

	#endregion

	#region MaximumVolume

	[Display(ResourceType = typeof(Resources), Name = "Show", GroupName = "MaximumVolume", Order = 510, Description = "MaximumVolumeDescription")]
	public bool ShowMaxVolume
	{
		get => MaxVolSeries.VisualType is not VisualMode.Hide;
		set => MaxVolSeries.VisualType = value ? VisualMode.Line : VisualMode.Hide;
	}

	[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "MaximumVolume", Order = 520)]
	[Range(1, 100000)]
	public int HiVolPeriod
	{
		get => HighestVol.Period;
		set => HighestVol.Period = value;
	}

	[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "MaximumVolume", Order = 530)]
	public System.Windows.Media.Color LineColor
	{
		get => MaxVolSeries.Color;
		set => MaxVolSeries.Color = value;
	}

	#endregion
}