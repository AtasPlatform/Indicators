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

	private readonly ValueDataSeries _filterSeries = new("Filter")
	{
		Color = Colors.LightBlue,
		VisualType = VisualMode.Histogram,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};
    private readonly ValueDataSeries _negative = new("Negative")
	{
		Color = Colors.Red,
		VisualType = VisualMode.Histogram,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

    private readonly ValueDataSeries _neutral = new("Neutral")
    {
	    Color = Colors.Gray,
	    VisualType = VisualMode.Histogram,
	    ShowZeroValue = false,
	    UseMinimizedModeIfEnabled = true
    };

    private readonly ValueDataSeries _positive = new("Positive")
	{
		Color = Colors.Green,
		VisualType = VisualMode.Histogram,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

	protected Highest HighestVol = new();
	private ValueDataSeries _maxVolSeries;

	private bool _deltaColored;
	private bool _useFilter;
    private decimal _filter;
    private InputType _input = InputType.Volume;
	private int _lastReverseAlert;
	private int _lastVolumeAlert;
	
	protected RenderStringFormat Format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
	protected Color TextColor = Color.Blue;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "DeltaColored", GroupName = "Colors")]
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

	[Display(ResourceType = typeof(Resources), Name = "UseFilter", GroupName = "Filter")]
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

	[Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Filter")]
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

	[Display(ResourceType = typeof(Resources), Name = "Type", GroupName = "Calculation")]
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

	[Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "VolumeAlert")]
	public bool UseVolumeAlerts { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "VolumeAlert")]
	public string AlertVolumeFile { get; set; } = "alert1";

	[Display(ResourceType = typeof(Resources), Name = "ReverseAlert", GroupName = "ReverseAlert")]
	public bool UseReverseAlerts { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "ReverseAlert")]
	public string AlertReverseFile { get; set; } = "alert1";

	[Display(ResourceType = typeof(Resources), Name = "ShowVolume", GroupName = "Volume", Order = 200)]
	public bool ShowVolume { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "Location", GroupName = "Volume", Order = 210)]
	public Location VolLocation { get; set; } = Location.Middle;

	[Display(ResourceType = typeof(Resources), Name = "Font", GroupName = "Volume", Order = 220)]
	public FontSetting Font { get; set; } = new("Arial", 10);

	[Display(ResourceType = typeof(Resources), Name = "FontColor", GroupName = "Volume", Order = 230)]
	public System.Windows.Media.Color FontColor
	{
		get => TextColor.Convert();
		set => TextColor = value.Convert();
	}

	[Display(ResourceType = typeof(Resources), Name = "Show", GroupName = "MaximumVolume", Order = 300)]
	public bool ShowMaxVolume
	{
		get => _maxVolSeries.VisualType is not VisualMode.Hide;
		set => _maxVolSeries.VisualType = value ? VisualMode.Line : VisualMode.Hide;
	}

	[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "MaximumVolume", Order = 310)]
	[Range(1, 100000)]
	public int HiVolPeriod
	{
		get => HighestVol.Period;
		set => HighestVol.Period = value;
	}

	[Display(ResourceType = typeof(Resources), Name = "HighLineColor", GroupName = "MaximumVolume", Order = 320)]
	public System.Windows.Media.Color LineColor
	{
		get => _maxVolSeries.Color;
		set => _maxVolSeries.Color = value;
	}

	#endregion

	#region ctor

	public Volume()
		: base(true)
	{
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);

		Panel = IndicatorDataProvider.NewPanel;
		DataSeries[0] = _positive;

		_maxVolSeries = (ValueDataSeries)HighestVol.DataSeries[0];
		_maxVolSeries.IsHidden = true;
		_maxVolSeries.UseMinimizedModeIfEnabled = true;

		DataSeries.Add(_negative);
		DataSeries.Add(_neutral);
		DataSeries.Add(_filterSeries);
		DataSeries.Add(_maxVolSeries);
	}

	#endregion

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
			var value = GetBarValue(i);
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
			_filterSeries[bar] = val;
			_positive[bar] = _negative[bar] = _neutral[bar] = 0;
			return;
		}

		_filterSeries[bar] = 0;

		if (_deltaColored)
		{
			if (candle.Delta > 0)
			{
				_positive[bar] = val;
				_negative[bar] = _neutral[bar] = 0;
			}
			else if (candle.Delta < 0)
			{
				_negative[bar] = val;
				_positive[bar] = _neutral[bar] = 0;
			}
			else
			{
				_neutral[bar] = val;
				_positive[bar] = _negative[bar] = 0;
			}
		}
		else
		{
			if (candle.Close > candle.Open)
			{
				_positive[bar] = val;
				_negative[bar] = _neutral[bar] = 0;
			}
			else if (candle.Close < candle.Open)
			{
				_negative[bar] = val;
				_positive[bar] = _neutral[bar] = 0;
			}
			else
			{
				_neutral[bar] = val;
				_positive[bar] = _negative[bar] = 0;
			}
		}
	}

	#endregion

	#region Private methods

	private int GetMinWidth(RenderContext context, int startBar, int endBar)
	{
		var maxLength = 0;

		for (var i = startBar; i <= endBar; i++)
		{
			var value = GetBarValue(i);
			var length = $"{value:0.#####}".Length;

			if (length > maxLength)
				maxLength = length;
		}

		var sampleStr = "";

		for (var i = 0; i < maxLength; i++)
			sampleStr += '0';

		return context.MeasureString(sampleStr, Font.RenderObject).Width;
	}

	private decimal GetBarValue(int bar)
	{
		if (_positive[bar] != 0)
			return _positive[bar];

		if (_negative[bar] != 0)
			return _negative[bar];

		return _neutral[bar] != 0
			? _neutral[bar]
			: _filterSeries[bar];
	}

	#endregion
}