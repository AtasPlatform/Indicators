namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;

[Category("Bid x Ask,Delta,Volume")]
[DisplayName("Bar's Volume Filter")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.BarVolumeFilterDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602326")]
public class BarVolumeFilter : Indicator
{
	#region Nested types

	public enum VolumeType
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Volume))]
		Volume,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ticks))]
		Ticks,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Delta))]
		Delta,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bid))]
		Bid,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ask))]
		Ask
	}

	#endregion

	#region Fields
	
	private readonly PaintbarsDataSeries _paintBars = new("PaintBars", "Paint bars");
	private CrossColor _color = DefaultColors.Orange.Convert();
	private TimeSpan _endTime;
	private TimeSpan _startTime;
	private int _targetBar;
	private bool _timeFilterEnabled;
	private VolumeType _volumeType;

    #endregion

    #region Properties

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.Type), Description = nameof(Strings.VolumeTypeDescription), Order = 5)]
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
	public decimal MinFilter
	{
		get => MinimumFilter.Value;
		set
		{
			MinimumFilter.Value = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.Minimum), Description = nameof(Strings.MinimumFilterDescription), Order = 10)]
	public Filter MinimumFilter { get; set; } = new()
		{ Value = 0, Enabled = false };

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.Maximum), Description = nameof(Strings.MaximumFilterDescription), Order = 20)]
	public Filter MaximumFilter { get; set; } = new()
		{ Value = 100 };

	[Browsable(false)]
	public decimal MaxFilter
	{
		get => MaximumFilter.Value;
		set
		{
			MaximumFilter.Value = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.Color), Description = nameof(Strings.FilterCandleColorDescription), Order = 40)]
	public CrossColor FilterColor
	{
		get => _color;
		set
		{
			_color = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.TimeFilter), Name = nameof(Strings.Enabled), Description = nameof(Strings.UseTimeFilterDescription), Order = 100)]
	public bool TimeFilterEnabled
	{
		get => _timeFilterEnabled;
		set
		{
			_timeFilterEnabled = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.TimeFilter), Name = nameof(Strings.StartTime), Description = nameof(Strings.StartTimeFilterDescription), Order = 110)]
	public TimeSpan StartTime
	{
		get => _startTime;
		set
		{
			_startTime = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.TimeFilter), Name = nameof(Strings.EndTime), Description = nameof(Strings.EndTimeFilterDescription), Order = 120)]
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