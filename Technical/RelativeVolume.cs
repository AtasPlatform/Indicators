﻿namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;

using Color = System.Drawing.Color;

[DisplayName("Relative Volume")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.RelativeVolumeDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602457")]
public class RelativeVolume : Indicator
{
	#region Nested types

	private class AvgBar
	{
		#region Fields

		private readonly int _lookBack;

		private readonly Queue<decimal> _volume = new();

		#endregion

		#region Properties

		public decimal AvgValue { get; private set; }

		#endregion

		#region ctor

		public AvgBar(int lookBack)
		{
			_lookBack = lookBack;
		}

		#endregion

		#region Public methods

		public void Add(decimal volume)
		{
			_volume.Enqueue(volume);

			if (_volume.Count > _lookBack)
				_volume.Dequeue();

			AvgValue = Avg();
		}

		#endregion

		#region Private methods

		private decimal Avg()
		{
			if (_volume.Count == 0)
				return 0;

			decimal sum = 0;

			foreach (var vol in _volume)
				sum += vol;

			return sum / _volume.Count;
		}

		#endregion
	}

	#endregion

	#region Fields

	private readonly ValueDataSeries _averagePoints = new("AveragePointsId", "AveragePoints")
	{
		VisualType = VisualMode.Dots,
		Color = DefaultColors.Blue.Convert(),
		Width = 2,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true,
		IgnoredByAlerts = true,
		DescriptionKey = nameof(Strings.AverageVolumeSettingsDescription)
	};

	private readonly Dictionary<TimeSpan, AvgBar> _avgVolumes = new();

	private readonly ValueDataSeries _volumeSeries = new("VolumeSeries", Strings.Volume)
	{
		VisualType = VisualMode.Histogram,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true,
		ResetAlertsOnNewBar = true,
        DescriptionKey = nameof(Strings.VolumeSettingsDescription)
    };

	private bool _deltaColored;
	private bool _isSupportedTimeFrame;
	private int _lastBar = -1;
	private int _lookBack = 20;

	private Color _negColor = DefaultColors.Red;
	private Color _neutralColor = DefaultColors.Gray;
	private Color _posColor = DefaultColors.Green;

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.AnalysisPeriod), Description = nameof(Strings.PeriodDescription))]
    public int LookBack
    {
        get => _lookBack;
        set
        {
            if (value <= 0)
                return;

            _lookBack = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Positive), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.PositiveValueColorDescription), Order = 610)]
	public CrossColor PosColor
	{
		get => _posColor.Convert();
		set
		{
			_posColor = value.Convert();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Negative), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.NegativeValueColorDescription), Order = 620)]
	public CrossColor NegColor
	{
		get => _negColor.Convert();
		set
		{
			_negColor = value.Convert();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Neutral), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.NeutralValueDescription), Order = 630)]
	public CrossColor NeutralColor
	{
		get => _neutralColor.Convert();
		set
		{
			_neutralColor = value.Convert();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.DeltaColored), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.DeltaColoredDescription), Order = 640)]
	public bool DeltaColored
	{
		get => _deltaColored;
		set
		{
			_deltaColored = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public RelativeVolume()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;

		DataSeries[0] = _volumeSeries;
		DataSeries.Add(_averagePoints);
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			_isSupportedTimeFrame = ChartInfo.ChartType == "TimeFrame" || ChartInfo.ChartType == "Seconds";
			_avgVolumes.Clear();
			_lastBar = 0;
		}

		var candle = GetCandle(bar);

		if (_isSupportedTimeFrame && bar > _lastBar)
		{
			_lastBar = bar;

			var time = candle.Time.TimeOfDay;

			if (!_avgVolumes.TryGetValue(time, out var avgVolumes))
			{
				avgVolumes = new AvgBar(LookBack);
				_avgVolumes.Add(time, avgVolumes);
			}

			_averagePoints[bar] = avgVolumes.AvgValue;
			var previousCandle = GetCandle(bar - 1);

			if (_avgVolumes.TryGetValue(previousCandle.Time.TimeOfDay, out var prevavgVolumes))
				prevavgVolumes.Add(previousCandle.Volume);
		}

		_volumeSeries[bar] = candle.Volume;

		if (DeltaColored)
		{
			_volumeSeries.Colors[bar] = candle.Delta switch
			{
				> 0 => _posColor,
				< 0 => _negColor,
				_ => _neutralColor
			};
		}
		else
		{
			if (candle.Open < candle.Close)
				_volumeSeries.Colors[bar] = _posColor;
			else if (candle.Open > candle.Close)
				_volumeSeries.Colors[bar] = _negColor;
			else
				_volumeSeries.Colors[bar] = _neutralColor;
		}
	}

	#endregion
}