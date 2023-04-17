namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

[Category("Bid x Ask,Delta,Volume")]
[DisplayName("Bar's volume filter")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/27599-bars-volume-filter")]
public class BarVolumeFilter : Indicator
{
	#region Nested types

	public enum VolumeType
	{
		[Display(ResourceType = typeof(Resources), Name = "Volume")]
		Volume,

		[Display(ResourceType = typeof(Resources), Name = "Ticks")]
		Ticks,

		[Display(ResourceType = typeof(Resources), Name = "Delta")]
		Delta,

		[Display(ResourceType = typeof(Resources), Name = "Bid")]
		Bid,

		[Display(ResourceType = typeof(Resources), Name = "Ask")]
		Ask
	}

	#endregion

	#region Fields
	
	private readonly PaintbarsDataSeries _paintBars = new("Paint bars");
	private Color _color = DefaultColors.Orange.Convert();
	private TimeSpan _endTime;
	private TimeSpan _startTime;
	private int _targetBar;
	private bool _timeFilterEnabled;
	private VolumeType _volumeType;

    #endregion

    #region Properties

	[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "Type", Order = 5)]
	public VolumeType Type
	{
		get => _volumeType;
		set
		{
			_volumeType = value;
			RecalculateValues();
		}
	}

	[Browsable(false)]
	[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "Minimum", Order = 10)]
	public decimal MinFilter
	{
		get => MinimumFilter.Value;
		set
		{
			MinimumFilter.Value = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "Minimum", Order = 10)]
	public Filter MinimumFilter { get; set; } = new()
		{ Value = 0, Enabled = false };

	[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "Maximum", Order = 20)]
	public Filter MaximumFilter { get; set; } = new()
		{ Value = 100 };

	[Browsable(false)]
	[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "Maximum", Order = 30)]
	public decimal MaxFilter
	{
		get => MaximumFilter.Value;
		set
		{
			MaximumFilter.Value = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "Color", Order = 40)]
	public Color FilterColor
	{
		get => _color;
		set
		{
			_color = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "TimeFilter", Name = "Enabled", Order = 100)]
	public bool TimeFilterEnabled
	{
		get => _timeFilterEnabled;
		set
		{
			_timeFilterEnabled = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "TimeFilter", Name = "StartTime", Order = 110)]
	public TimeSpan StartTime
	{
		get => _startTime;
		set
		{
			_startTime = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "TimeFilter", Name = "EndTime", Order = 120)]
	public TimeSpan EndTime
	{
		get => _endTime;
		set
		{
			_endTime = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public BarVolumeFilter()
		: base(true)
	{
		DataSeries[0] = _paintBars;
		_paintBars.IsHidden = true;
		DenyToChangePanel = true;
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			_paintBars.Clear();
			_targetBar = 0;
		}

		if (bar < _targetBar)
			return;

		var candle = GetCandle(bar);
		decimal volume;

		switch (Type)
		{
			case VolumeType.Volume:
			{
				volume = candle.Volume;
				break;
			}
			case VolumeType.Ticks:
			{
				volume = candle.Ticks;
				break;
			}
			case VolumeType.Delta:
			{
				volume = candle.Delta;
				break;
			}
			case VolumeType.Bid:
			{
				volume = candle.Bid;
				break;
			}
			case VolumeType.Ask:
			{
				volume = candle.Ask;
				break;
			}
			default:
				throw new ArgumentOutOfRangeException();
		}

		var filtered = (!MinimumFilter.Enabled || volume >= MinimumFilter.Value) && (!MaximumFilter.Enabled || volume <= MaximumFilter.Value);

		if (TimeFilterEnabled && filtered)
		{
			var time = candle.Time.AddHours(InstrumentInfo.TimeZone).TimeOfDay;
			var lastTime = candle.LastTime.AddHours(InstrumentInfo.TimeZone).TimeOfDay;

			if (StartTime <= EndTime)
				filtered = (StartTime <= time || StartTime <= lastTime) && time < EndTime;
			else
			{
				filtered = (StartTime <= lastTime && time >= StartTime && time > EndTime)
					||
					((EndTime >= time || EndTime >= lastTime) && time < EndTime);
			}
		}

		_paintBars[bar] = filtered ? _color : null;
	}

	protected override void OnInitialize()
	{
		MaximumFilter.PropertyChanged += (a, b) =>
		{
			RecalculateValues();
			RedrawChart();
		};

		MinimumFilter.PropertyChanged += (a, b) =>
		{
			RecalculateValues();
			RedrawChart();
		};
	}

	#endregion
}