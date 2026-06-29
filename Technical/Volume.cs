namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Attributes.Editors;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

using Color = System.Drawing.Color;

[Category(IndicatorCategories.VolumeOrderFlow)]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.VolumeIndDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000602498")]
public class Volume : Indicator
{
	#region Nested types

	public enum InputType
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Volume))]
		Volume,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ticks))]
		Ticks,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ask))]
		Asks,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bid))]
		Bids
	}

	public enum Location
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Up))]
		Up,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Middle))]
		Middle,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Down))]
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
	private readonly ValueDataSeries _negative = new("NegativeId", "Negative")
    {
	    VisualType = VisualMode.Hide,
		IsHidden = true
    };

    private readonly ValueDataSeries _neutral = new("NeutralId", "Neutral")
    {
	    VisualType = VisualMode.Hide,
		Color = Color.Gray.Convert(),
	    IsHidden = true
    };

    private readonly ValueDataSeries _positive = new("PositiveId", "Positive")
    {
	    VisualType = VisualMode.Hide,
	    Color = Color.Green.Convert(),
        IsHidden = true
    };

	#endregion

    private ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization)
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
	protected Color TextColor = DefaultColors.Blue;

    #endregion

    #region Properties

    #region Calculation

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Type), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.SourceTypeDescription))]
    [Tab(TabName = nameof(Strings.Data), TabOrder = 0, ResourceType = typeof(Strings))]
	public InputType Input
	{
		get => _input;
		set
		{
			_input = value;
			RaisePropertyChanged(nameof(Input));
			RecalculateValues();
		}
	}

    #endregion

    #region Filter

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseFilter), GroupName = nameof(Strings.Filter), Description = nameof(Strings.UseFilterDescription))]
    [Tab(TabName = nameof(Strings.Data), TabOrder = 0, ResourceType = typeof(Strings))]
    public bool UseFilter
    {
        get => _useFilter;
        set
        {
            _useFilter = value;
            RaisePropertyChanged(nameof(UseFilter));
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.Filter), Description = nameof(Strings.MinVolumeFilterCommonDescription))]
    [Tab(TabName = nameof(Strings.Data), TabOrder = 0, ResourceType = typeof(Strings))]
    [Range(0, double.MaxValue)]
    public decimal FilterValue
    {
        get => _filter;
        set
        {
            _filter = value;
            RaisePropertyChanged(nameof(FilterValue));
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Filter), Description = nameof(Strings.FilterColorDescription))]
    [Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
    public CrossColor FilterColor
    {
        get => _filterColor.Convert();
        set
        {
            _filterColor = value.Convert();

            RaisePropertyChanged(nameof(FilterColor));
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlerts), GroupName = nameof(Strings.Filter), Description = nameof(Strings.UseAlertsDescription))]
    [Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
    public bool UseVolumeAlerts { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Filter), Description = nameof(Strings.AlertFileDescription))]
    [Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
    [SoundComboBoxEditor]
    public string AlertVolumeFile { get; set; } = "alert1";

    #endregion

    #region MaximumVolume

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show), GroupName = nameof(Strings.MaximumVolume), Description = nameof(Strings.MaximumVolumeDescription))]
    [Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
    public bool ShowMaxVolume
    {
        get => MaxVolSeries.VisualType is not VisualMode.Hide;
        set => MaxVolSeries.VisualType = value ? VisualMode.Line : VisualMode.Hide;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.MaximumVolume), Description = nameof(Strings.MaximumVolumePeriodDescription))]
    [Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
    [Range(1, 100000)]
    public int HiVolPeriod
    {
        get => HighestVol.Period;
        set => HighestVol.Period = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.MaximumVolume), Description = nameof(Strings.ColorDescription))]
    [Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
    public CrossColor LineColor
    {
        get => MaxVolSeries.Color;
        set => MaxVolSeries.Color = value;
    }

    #endregion

    #region Volume label

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show), GroupName = nameof(Strings.VolumeLabel), Description = nameof(Strings.VolumeLabelDescription))]
    [Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
    public bool ShowVolume { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.VolumeLabel), Description = nameof(Strings.LabelTextColorDescription))]
    [Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
    public CrossColor FontColor
    {
        get => TextColor.Convert();
        set => TextColor = value.Convert();
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Location), GroupName = nameof(Strings.VolumeLabel), Description = nameof(Strings.LabelLocationDescription))]
    [Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
    public Location VolLocation { get; set; } = Location.Middle;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Font), GroupName = nameof(Strings.VolumeLabel), Description = nameof(Strings.FontSettingDescription))]
    [Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
    public FontSetting Font { get; set; } = new("Arial", 10);

    #endregion

    #region Divergence alert

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.ReverseAlert), Description = nameof(Strings.ReverseAlertDescription))]
    [Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
    public bool UseReverseAlerts { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.ReverseAlert), Description = nameof(Strings.AlertFileDescription))]
    [Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
    [SoundComboBoxEditor]
    public string AlertReverseFile { get; set; } = "alert1";

    #endregion

    #region Drawing

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.DeltaColored), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.DeltaColoredDescription))]
    [Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
	public bool DeltaColored
	{
		get => _deltaColored;
		set
		{
			_deltaColored = value;
			RaisePropertyChanged(nameof(DeltaColored));
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Positive), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.PositiveValueColorDescription))]
	[Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
	public CrossColor PosColor
	{
		get => _posColor.Convert();
		set
		{
            _posColor = value.Convert();
            _positive.Color = value;
			RaisePropertyChanged(nameof(PosColor));
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Negative), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.NegativeValueColorDescription))]
	[Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
	public CrossColor NegColor
	{
		get => _negColor.Convert();
		set
		{
            _negColor = value.Convert();
            _negative.Color = value;
			RaisePropertyChanged(nameof(NegColor));
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Neutral), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.NeutralColorDescription))]
	[Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
	public CrossColor NeutralColor
	{
		get => _neutralColor.Convert();
        set
		{
            _neutralColor = value.Convert();
            _neutral.Color = value;
			RaisePropertyChanged(nameof(NeutralColor));
			RecalculateValues();
		}
	}

    #endregion

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
		    NeutralColor = ChartInfo.ColorsStore.DojiBarPen.Color.Convert();
	    }
    }

    #region Public methods

    public override string ToString()
	{
		return "Volume";
	}

    #endregion

    #region Protected methods

    protected override void OnInitialize()
    {
		_positive.VisualType = VisualMode.Hide;
		_negative.VisualType = VisualMode.Hide;
		_neutral.VisualType = VisualMode.Hide;
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
        if (ChartInfo == null) 
			return;

        if (!ShowVolume || ChartInfo.ChartVisualMode != ChartVisualModes.Clusters || Panel == IndicatorDataProvider.CandlesPanel)
			return;

        var minWidth = GetMinWidth(context, FirstVisibleBarNumber, LastVisibleBarNumber);
		var barWidth = ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0);

		if (minWidth > barWidth)
			return;

        context.SetClip(Container.Region);

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

        context.ResetClip();
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
				AddAlert(AlertVolumeFile, $"Candle {GetInputLabel()}: {val}");
				_lastVolumeAlert = bar;
			}

			if (UseReverseAlerts && _lastReverseAlert != bar)
			{
				if ((candle.Delta < 0 && candle.Close > candle.Open) || (candle.Delta > 0 && candle.Close < candle.Open))
				{
					AddAlert(AlertReverseFile, $"Candle {GetInputLabel()}: {val} (Reverse alert)");
					_lastReverseAlert = bar;
				}
			}
		}

		HighestVol.Calculate(bar, val);

		if (_useFilter && val >= _filter)
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

    protected override void OnDispose()
    {
        _positive.PropertyChanged -= PositiveChanged;
        _negative.PropertyChanged -= NegativeChanged;
        _neutral.PropertyChanged -= NeutralChanged;
    }

    #endregion

    #region Private methods

    private int GetMinWidth(RenderContext context, int startBar, int endBar)
	{
		var maxLength = 0;

		for (var i = startBar; i <= endBar; i++)
		{
			var value = _renderSeries[i];
            var renderText = ChartInfo.TryGetMinimizedVolumeString(value);
            var length = renderText.Length;

            if (length > maxLength)
				maxLength = length;
		}

        var sampleStr = new string('0', maxLength);

        return context.MeasureString(sampleStr, Font.RenderObject).Width;
	}

	private void NeutralChanged(object sender, PropertyChangedEventArgs e)
	{
        if (e.PropertyName == nameof(ValueDataSeries.Color))
            _neutralColor = _neutral.Color.Convert();
	}

	private void NegativeChanged(object sender, PropertyChangedEventArgs e)
	{
        if (e.PropertyName == nameof(ValueDataSeries.Color))
            _negColor = _negative.Color.Convert();
	}

	private void PositiveChanged(object sender, PropertyChangedEventArgs e)
	{
        if (e.PropertyName == nameof(ValueDataSeries.Color))
            _posColor = _positive.Color.Convert();
	}

    private string GetInputLabel()
    {
        return Input switch
        {
            InputType.Ticks => nameof(Strings.Ticks),
            InputType.Asks => nameof(Strings.Asks),
            InputType.Bids => nameof(Strings.Bids),
            _ => nameof(Strings.Volume)
        };
    }

    #endregion
}
