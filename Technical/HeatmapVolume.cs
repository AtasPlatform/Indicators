namespace ATAS.Indicators.Technical
{
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	public class HeatmapVolume : Indicator
	{
		#region Nested types

		public enum ColorMode
		{
			[Display(ResourceType = typeof(Resources), Name = "Heatmap")]
			Heatmap,

			[Display(ResourceType = typeof(Resources), Name = "UpDown")]
			UpDown
		}

		public enum ZoneMode
		{
			[Display(ResourceType = typeof(Resources), Name = "None")]
			None,

			[Display(ResourceType = typeof(Resources), Name = "Line")]
			Line,

			[Display(ResourceType = typeof(Resources), Name = "BackGround")]
			Background,

			[Display(ResourceType = typeof(Resources), Name = "All")]
			All
		}

		#endregion

		#region Fields

		private bool _coloredBars = true;

		private Color _downExtraHigh = Colors.Red;
		private Color _downHigh = Color.FromRgb(255, 50, 50);
		private Color _downLow = Color.FromRgb(255, 200, 200);
		private Color _downMedium = Color.FromRgb(255, 100, 100);
		private Color _downNormal = Color.FromRgb(255, 150, 150);

		private ValueDataSeries _extraHighDnSeries = new("extraHighDnSeries")
		{
			VisualType = Indicators.VisualMode.Histogram,
			IsHidden = true
		};

		private ValueDataSeries _extraHighLine = new("extraHighLine")
		{
			IsHidden = true
		};

		private RangeDataSeries _extraHighRange = new("extraHighRange")
		{
			ScaleIt = false,
			IsHidden = true
		};

		private ValueDataSeries _extraHighUpSeries = new("extraHighUpSeries")
		{
			VisualType = Indicators.VisualMode.Histogram,
			IsHidden = true
		};

		private Color _heatmapExtraHigh = Colors.Red;
		private Color _heatmapHigh = Colors.Orange;
		private Color _heatmapLow = Colors.DodgerBlue;
		private Color _heatmapMedium = Colors.Yellow;
		private Color _heatmapNormal = Colors.LightSkyBlue;
		private int _heatmapTransparency = 85;

		private ValueDataSeries _highDnSeries = new("highDnSeries")
		{
			VisualType = Indicators.VisualMode.Histogram,
			IsHidden = true
		};

		private Highest _highestV = new() { Period = 300 };

		private ValueDataSeries _highLine = new("highLine")
		{
			IsHidden = true
		};

		private RangeDataSeries _highRange = new("highRange")
		{
			ScaleIt = false,
			IsHidden = true
		};

		private ValueDataSeries _highUpSeries = new("highUpSeries")
		{
			VisualType = Indicators.VisualMode.Histogram,
			IsHidden = true
		};

		private ValueDataSeries _lowDnSeries = new("lowDnSeries")
		{
			VisualType = Indicators.VisualMode.Histogram,
			IsHidden = true
		};

		private Lowest _lowestV = new() { Period = 300 };

		private RangeDataSeries _lowRange = new("lowRange")
		{
			ScaleIt = false,
			IsHidden = true
		};

		private ValueDataSeries _lowUpSeries = new("lowUpSeries")
		{
			VisualType = Indicators.VisualMode.Histogram,
			IsHidden = true
		};

		private ValueDataSeries _mediumDnSeries = new("mediumDnSeries")
		{
			VisualType = Indicators.VisualMode.Histogram,
			IsHidden = true
		};

		private ValueDataSeries _mediumUpSeries = new("mediumUpSeries")
		{
			VisualType = Indicators.VisualMode.Histogram,
			IsHidden = true
		};

		private ValueDataSeries _middleLine = new("middleLine")
		{
			IsHidden = true
		};

		private RangeDataSeries _middleRange = new("middleRange")
		{
			ScaleIt = false,
			IsHidden = true
		};

		private ValueDataSeries _normalDnSeries = new("normalDnSeries")
		{
			VisualType = Indicators.VisualMode.Histogram,
			IsHidden = true
		};

		private ValueDataSeries _normalLine = new("normalLine")
		{
			IsHidden = true
		};

		private RangeDataSeries _normalRange = new("normalRange")
		{
			ScaleIt = false,
			IsHidden = true
		};

		private ValueDataSeries _normalUpSeries = new("normalUpSeries")
		{
			VisualType = Indicators.VisualMode.Histogram,
			IsHidden = true
		};

		private PaintbarsDataSeries _paintBars = new("paint");
		private bool _showAsOscillator;

		private SMA _sma = new();
		private StdDev _stdDev = new();
		private decimal _thresholdExtraHigh = 4m;
		private decimal _thresholdHigh = 2.5m;
		private decimal _thresholdMedium = 1m;
		private decimal _thresholdNormal = -0.5m;
		private Color _upExtraHigh = Colors.LawnGreen;
		private Color _upHigh = Colors.LimeGreen;
		private Color _upLow = Colors.LightGreen;
		private Color _upMedium = Colors.Green;
		private Color _upNormal = Colors.SeaGreen;

		private ValueDataSeries _valueSeries = new("Values")
		{
			IsHidden = true
		};

		private ColorMode _visualMode = ColorMode.Heatmap;
		private ZoneMode _zonesMode = ZoneMode.Background;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "SMA", Order = 100)]
		public int SmaPeriod
		{
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "StdDev", Order = 110)]
		public int StdPeriod
		{
			get => _stdDev.Period;
			set
			{
				_stdDev.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ExtraHighVolumeThreshold", GroupName = "Settings", Order = 200)]
		public decimal ThresholdExtraHigh
		{
			get => _thresholdExtraHigh;
			set
			{
				_thresholdExtraHigh = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "HighVolumeThreshold", GroupName = "Settings", Order = 210)]
		public decimal ThresholdHigh
		{
			get => _thresholdHigh;
			set
			{
				_thresholdHigh = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MediumVolumeThreshold", GroupName = "Settings", Order = 220)]
		public decimal ThresholdMedium
		{
			get => _thresholdMedium;
			set
			{
				_thresholdMedium = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "NormalVolumeThreshold", GroupName = "Settings", Order = 230)]
		public decimal ThresholdNormal
		{
			get => _thresholdNormal;
			set
			{
				_thresholdNormal = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShowAsOscillator", GroupName = "Settings", Order = 240)]
		public bool ShowAsOscillator
		{
			get => _showAsOscillator;
			set
			{
				_showAsOscillator = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "PaintBars", GroupName = "Visualization", Order = 300)]
		public bool ColoredBars
		{
			get => _coloredBars;
			set
			{
				_coloredBars = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "VisualMode", GroupName = "Visualization", Order = 302)]
		public ColorMode VisualMode
		{
			get => _visualMode;
			set
			{
				_visualMode = value;

				_extraHighUpSeries.Color = value is ColorMode.Heatmap ? _heatmapExtraHigh : _upExtraHigh;
				_highUpSeries.Color = value is ColorMode.Heatmap ? _heatmapHigh : _upHigh;
				_mediumUpSeries.Color = value is ColorMode.Heatmap ? _heatmapMedium : _upMedium;
				_normalUpSeries.Color = value is ColorMode.Heatmap ? _heatmapNormal : _upNormal;
				_lowUpSeries.Color = value is ColorMode.Heatmap ? _heatmapLow : _upLow;

				_extraHighDnSeries.Color = value is ColorMode.Heatmap ? _heatmapExtraHigh : _downExtraHigh;
				_highDnSeries.Color = value is ColorMode.Heatmap ? _heatmapHigh : _downHigh;
				_mediumDnSeries.Color = value is ColorMode.Heatmap ? _heatmapMedium : _downMedium;
				_normalDnSeries.Color = value is ColorMode.Heatmap ? _heatmapNormal : _downNormal;
				_lowDnSeries.Color = value is ColorMode.Heatmap ? _heatmapLow : _downLow;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ZoneMode", GroupName = "Visualization", Order = 304)]
		public ZoneMode ZonesMode
		{
			get => _zonesMode;
			set
			{
				_zonesMode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Transparency", GroupName = "Heatmap", Order = 308)]
		[Range(0, 100)]
		public int HeatmapTransparency
		{
			get => _heatmapTransparency;
			set
			{
				_heatmapTransparency = value;

				_extraHighRange.RangeColor = Color.FromArgb(
					(byte)(255 * (100 - value) / 100),
					_extraHighRange.RangeColor.R,
					_extraHighRange.RangeColor.G,
					_extraHighRange.RangeColor.B);

				_highRange.RangeColor = Color.FromArgb(
					(byte)(255 * (100 - value) / 100),
					_highRange.RangeColor.R,
					_highRange.RangeColor.G,
					_highRange.RangeColor.B);

				_middleRange.RangeColor = Color.FromArgb(
					(byte)(255 * (100 - value) / 100),
					_middleRange.RangeColor.R,
					_middleRange.RangeColor.G,
					_middleRange.RangeColor.B);

				_normalRange.RangeColor = Color.FromArgb(
					(byte)(255 * (100 - value) / 100),
					_normalRange.RangeColor.R,
					_normalRange.RangeColor.G,
					_normalRange.RangeColor.B);

				_lowRange.RangeColor = Color.FromArgb(
					(byte)(255 * (100 - value) / 100),
					_lowRange.RangeColor.R,
					_lowRange.RangeColor.G,
					_lowRange.RangeColor.B);
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ExtraHigh", GroupName = "Heatmap", Order = 310)]
		public Color HeatmapExtraHigh
		{
			get => _heatmapExtraHigh;
			set
			{
				_heatmapExtraHigh = value;

				_extraHighLine.Color = value;

				_extraHighRange.RangeColor = Color.FromArgb(
					(byte)(255 * (100 - HeatmapTransparency) / 100),
					value.R, value.G, value.B);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "High", GroupName = "Heatmap", Order = 320)]
		public Color HeatmapHigh
		{
			get => _heatmapHigh;
			set
			{
				_heatmapHigh = value;

				_highLine.Color = value;

				_highRange.RangeColor = Color.FromArgb(
					(byte)(255 * (100 - HeatmapTransparency) / 100),
					value.R, value.G, value.B);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Medium", GroupName = "Heatmap", Order = 330)]
		public Color HeatmapMedium
		{
			get => _heatmapMedium;
			set
			{
				_heatmapMedium = value;

				_middleLine.Color = value;

				_middleRange.RangeColor = Color.FromArgb(
					(byte)(255 * (100 - HeatmapTransparency) / 100),
					value.R, value.G, value.B);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Normal", GroupName = "Heatmap", Order = 340)]
		public Color HeatmapNormal
		{
			get => _heatmapNormal;
			set
			{
				_heatmapNormal = value;

				_normalLine.Color = value;

				_normalRange.RangeColor = Color.FromArgb(
					(byte)(255 * (100 - HeatmapTransparency) / 100),
					value.R, value.G, value.B);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Low", GroupName = "Heatmap", Order = 350)]
		public Color HeatmapLow
		{
			get => _heatmapLow;
			set
			{
				_heatmapLow = value;

				_lowRange.RangeColor = Color.FromArgb(
					(byte)(255 * (100 - HeatmapTransparency) / 100),
					value.R, value.G, value.B);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ExtraHigh", GroupName = "UpColor", Order = 400)]
		public Color UpExtraHigh
		{
			get => _upExtraHigh;
			set
			{
				_upExtraHigh = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "High", GroupName = "UpColor", Order = 410)]
		public Color UpHigh
		{
			get => _upHigh;
			set
			{
				_upHigh = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Medium", GroupName = "UpColor", Order = 420)]
		public Color UpMedium
		{
			get => _upMedium;
			set
			{
				_upMedium = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Normal", GroupName = "UpColor", Order = 430)]
		public Color UpNormal
		{
			get => _upNormal;
			set
			{
				_upNormal = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Low", GroupName = "UpColor", Order = 440)]
		public Color UpLow
		{
			get => _upLow;
			set
			{
				_upLow = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ExtraHigh", GroupName = "DownColor", Order = 500)]
		public Color DownExtraHigh
		{
			get => _downExtraHigh;
			set
			{
				_downExtraHigh = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "High", GroupName = "DownColor", Order = 510)]
		public Color DownHigh
		{
			get => _downHigh;
			set
			{
				_downHigh = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Medium", GroupName = "DownColor", Order = 520)]
		public Color DownMedium
		{
			get => _downMedium;
			set
			{
				_downMedium = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Normal", GroupName = "DownColor", Order = 530)]
		public Color DownNormal
		{
			get => _downNormal;
			set
			{
				_downNormal = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Low", GroupName = "DownColor", Order = 540)]
		public Color DownLow
		{
			get => _downLow;
			set
			{
				_downLow = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public HeatmapVolume()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DenyToChangePanel = true;

			_highestV.Period = _lowestV.Period = 300;

			StdPeriod = 610;
			SmaPeriod = 610;
			VisualMode = ColorMode.Heatmap;

			HeatmapTransparency = 85;
			HeatmapExtraHigh = Colors.Red;
			HeatmapHigh = Colors.Orange;
			HeatmapLow = Colors.DodgerBlue;
			HeatmapMedium = Colors.Yellow;
			HeatmapNormal = Colors.LightSkyBlue;

			DataSeries[0] = _extraHighRange;
			DataSeries.Add(_highRange);
			DataSeries.Add(_middleRange);
			DataSeries.Add(_normalRange);
			DataSeries.Add(_lowRange);
			DataSeries.Add(_paintBars);

			DataSeries.Add(_extraHighDnSeries);
			DataSeries.Add(_extraHighUpSeries);
			DataSeries.Add(_highDnSeries);
			DataSeries.Add(_highUpSeries);
			DataSeries.Add(_mediumDnSeries);
			DataSeries.Add(_mediumUpSeries);
			DataSeries.Add(_normalDnSeries);
			DataSeries.Add(_normalUpSeries);
			DataSeries.Add(_lowDnSeries);
			DataSeries.Add(_lowUpSeries);

			DataSeries.Add(_extraHighLine);
			DataSeries.Add(_highLine);
			DataSeries.Add(_middleLine);
			DataSeries.Add(_normalLine);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				DataSeries.ForEach(x => x.Clear());

			var candle = GetCandle(bar);
			var mean = _sma.Calculate(bar, candle.Volume);
			var std = _stdDev.Calculate(bar, candle.Volume);

			if (std == 0)
				return;

			var stdBar = (candle.Volume - mean) / std;

			var dir = candle.Close > candle.Open;

			var v = ShowAsOscillator ? candle.Volume - mean : candle.Volume;
			_valueSeries[bar] = v;

			var mosc = ShowAsOscillator ? 0 : mean;

			var tst = _highestV.Calculate(bar, v) * 9999;
			var ts0 = 0;
			var ts1 = std * ThresholdExtraHigh + mosc;
			var ts2 = std * ThresholdHigh + mosc;
			var ts3 = std * ThresholdMedium + mosc;
			var ts4 = std * ThresholdNormal + mosc;

			CalcHeatmap(bar, stdBar, dir, v);

			if (ZonesMode is ZoneMode.All or ZoneMode.Background)
			{
				_extraHighRange[bar] = new RangeValue
				{
					Upper = tst,
					Lower = ts1
				};

				_highRange[bar] = new RangeValue
				{
					Upper = ts1,
					Lower = ts2
				};

				_middleRange[bar] = new RangeValue
				{
					Upper = ts2,
					Lower = ts3
				};

				_normalRange[bar] = new RangeValue
				{
					Upper = ts3,
					Lower = ts4
				};

				_lowRange[bar] = new RangeValue
				{
					Upper = ts4,
					Lower = ts0
				};
			}

			if (ZonesMode is ZoneMode.All or ZoneMode.Line)
			{
				_extraHighLine[bar] = ts1;
				_highLine[bar] = ts2;
				_middleLine[bar] = ts3;
				_normalLine[bar] = ts4;
			}
		}

		#endregion

		#region Private methods

		private void CalcHeatmap(int bar, decimal stdBar, bool dir, decimal value)
		{
			ClearHistogram(bar);

			if (stdBar > ThresholdExtraHigh)
			{
				if (dir)
					_extraHighUpSeries[bar] = value;
				else
					_extraHighDnSeries[bar] = value;
			}
			else
			{
				if (stdBar > ThresholdHigh)
				{
					if (dir)
						_highUpSeries[bar] = value;
					else
						_highDnSeries[bar] = value;
				}
				else
				{
					if (stdBar > ThresholdMedium)
					{
						if (dir)
							_mediumUpSeries[bar] = value;
						else
							_mediumDnSeries[bar] = value;
					}
					else
					{
						if (stdBar > ThresholdNormal)
						{
							if (dir)
								_normalUpSeries[bar] = value;
							else
								_normalDnSeries[bar] = value;
						}
						else
						{
							if (dir)
								_lowUpSeries[bar] = value;
							else
								_lowDnSeries[bar] = value;
						}
					}
				}
			}

			if (ColoredBars)
				_paintBars[bar] = GetValueColor(bar);
		}

		private Color? GetValueColor(int bar)
		{
			if (_extraHighUpSeries[bar] != 0)
				return _extraHighUpSeries.Color;

			if (_extraHighDnSeries[bar] != 0)
				return _extraHighDnSeries.Color;

			if (_highUpSeries[bar] != 0)
				return _highUpSeries.Color;

			if (_highDnSeries[bar] != 0)
				return _highDnSeries.Color;

			if (_mediumUpSeries[bar] != 0)
				return _mediumUpSeries.Color;

			if (_mediumDnSeries[bar] != 0)
				return _mediumDnSeries.Color;

			if (_normalUpSeries[bar] != 0)
				return _normalUpSeries.Color;

			if (_normalDnSeries[bar] != 0)
				return _normalDnSeries.Color;

			if (_lowUpSeries[bar] != 0)
				return _lowUpSeries.Color;

			if (_lowDnSeries[bar] != 0)
				return _lowDnSeries.Color;

			return null;
		}

		private void ClearHistogram(int bar)
		{
			_extraHighUpSeries[bar] = 0;
			_extraHighDnSeries[bar] = 0;
			_highUpSeries[bar] = 0;
			_highDnSeries[bar] = 0;
			_mediumUpSeries[bar] = 0;
			_mediumDnSeries[bar] = 0;
			_normalUpSeries[bar] = 0;
			_normalDnSeries[bar] = 0;
			_lowUpSeries[bar] = 0;
			_lowDnSeries[bar] = 0;
		}

		#endregion
	}
}