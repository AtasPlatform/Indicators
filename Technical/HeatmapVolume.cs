namespace ATAS.Indicators.Technical
{
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	public class HeatmapVolume : Indicator
	{
		#region Nested types

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

		public enum ColorMode
		{
			[Display(ResourceType = typeof(Resources), Name = "Heatmap")]
			Heatmap,

			[Display(ResourceType = typeof(Resources), Name = "UpDown")]
			UpDown
		}

		#endregion

		#region Fields

		private bool _coloredBars = true;
		private Color _cthresholdExtraHighDn;
		private Color _cthresholdExtraHighUp;
		private Color _cthresholdHighDn;
		private Color _cthresholdHighUp;
		private Color _cthresholdLowDn;
		private Color _cthresholdLowUp;
		private Color _cthresholdMediumDn;
		private Color _cthresholdMediumUp;
		private Color _cthresholdNormalDn;
		private Color _cthresholdNormalUp;
		private Color _downExtraHigh = Colors.Red;
		private Color _downHigh = Color.FromRgb(255, 50, 50);
		private Color _downLow = Color.FromRgb(255, 200, 200);
		private Color _downMedium = Color.FromRgb(255, 100, 100);
		private Color _downNormal = Color.FromRgb(255, 150, 150);

		private RangeDataSeries _extraHighRange = new("extraHighRange")
		{
			ScaleIt = false,
			IsHidden = true
		};

		private Color _heatmapExtraHigh = Colors.Red;
		private Color _heatmapHigh = Colors.Orange;
		private Color _heatmapLow = Colors.DodgerBlue;
		private Color _heatmapMedium = Colors.Yellow;
		private Color _heatmapNormal = Colors.LightSkyBlue;
		private int _heatmapTransparency = 85;

		private Highest _highestV = new();

		private RangeDataSeries _highRange = new("highRange")
		{
			ScaleIt = false,
			IsHidden = true
		};

		private Lowest _lowestV = new();

		private RangeDataSeries _lowRange = new("lowRange")
		{
			ScaleIt = false,
			IsHidden = true
		};

		private RangeDataSeries _middleRange = new("middleRange")
		{
			ScaleIt = false,
			IsHidden = true
		};

		private RangeDataSeries _normalRange = new("normalRange")
		{
			ScaleIt = false,
			IsHidden = true
		};

		private PaintbarsDataSeries _paintBars = new("paint");
		private bool _showAsOscillator;
		private bool _showBackground;
		private bool _showLines;

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

				_cthresholdExtraHighUp = value is ColorMode.Heatmap ? HeatmapExtraHigh : UpExtraHigh;
				_cthresholdHighUp = value is ColorMode.Heatmap ? HeatmapHigh : UpHigh;
				_cthresholdMediumUp = value is ColorMode.Heatmap ? HeatmapMedium : UpMedium;
				_cthresholdNormalUp = value is ColorMode.Heatmap ? HeatmapNormal : UpNormal;
				_cthresholdLowUp = value is ColorMode.Heatmap ? HeatmapLow : UpLow;

				_cthresholdExtraHighDn = value is ColorMode.Heatmap ? HeatmapExtraHigh : DownExtraHigh;
				_cthresholdHighDn = value is ColorMode.Heatmap ? HeatmapHigh : DownHigh;
				_cthresholdMediumDn = value is ColorMode.Heatmap ? HeatmapMedium : DownMedium;
				_cthresholdNormalDn = value is ColorMode.Heatmap ? HeatmapNormal : DownNormal;
				_cthresholdLowDn = value is ColorMode.Heatmap ? HeatmapLow : DownLow;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "VisualMode", GroupName = "Visualization", Order = 304)]
		public ZoneMode ZonesMode
		{
			get => _zonesMode;
			set
			{
				_zonesMode = value;
				_showLines = value is ZoneMode.All or ZoneMode.Line;
				_showBackground = value is ZoneMode.All or ZoneMode.Background;
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
					(byte)(255 * value / 100),
					_extraHighRange.RangeColor.R,
					_extraHighRange.RangeColor.G,
					_extraHighRange.RangeColor.B);

				_highRange.RangeColor = Color.FromArgb(
					(byte)(255 * value / 100),
					_highRange.RangeColor.R,
					_highRange.RangeColor.G,
					_highRange.RangeColor.B);

				_middleRange.RangeColor = Color.FromArgb(
					(byte)(255 * value / 100),
					_middleRange.RangeColor.R,
					_middleRange.RangeColor.G,
					_middleRange.RangeColor.B);

				_normalRange.RangeColor = Color.FromArgb(
					(byte)(255 * value / 100),
					_normalRange.RangeColor.R,
					_normalRange.RangeColor.G,
					_normalRange.RangeColor.B);

				_lowRange.RangeColor = Color.FromArgb(
					(byte)(255 * value / 100),
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

				_extraHighRange.RangeColor = Color.FromArgb(
					(byte)(255 * HeatmapTransparency / 100),
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

				_highRange.RangeColor = Color.FromArgb(
					(byte)(255 * HeatmapTransparency / 100),
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

				_middleRange.RangeColor = Color.FromArgb(
					(byte)(255 * HeatmapTransparency / 100),
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

				_normalRange.RangeColor = Color.FromArgb(
					(byte)(255 * HeatmapTransparency / 100),
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
					(byte)(255 * HeatmapTransparency / 100),
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
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);

			_highestV.Period = _lowestV.Period = 300;

			DataSeries[0] = _extraHighRange;
			DataSeries.Add(_highRange);
			DataSeries.Add(_middleRange);
			DataSeries.Add(_normalRange);
			DataSeries.Add(_lowRange);
			DataSeries.Add(_paintBars);
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
			var stdBar = (candle.Volume - mean) / std;

			var dir = candle.Close > candle.Open;
			var v = ShowAsOscillator ? candle.Volume - mean : candle.Volume;
			var mosc = ShowAsOscillator ? 0 : mean;

			var tst = _highestV.Calculate(bar, v) * 9999;
			var ts0 = _lowestV.Calculate(bar, v) * 9999;
			var ts1 = std * ThresholdExtraHigh + mosc;
			var ts2 = std * ThresholdHigh + mosc;
			var ts3 = std * ThresholdMedium + mosc;
			var ts4 = std * ThresholdNormal + mosc;

			if (ColoredBars)
			{
				var barColor = stdBar > ThresholdExtraHigh
					? dir
						? _cthresholdExtraHighUp
						: _cthresholdExtraHighDn
					: stdBar > ThresholdHigh
						? dir
							? _cthresholdHighUp
							: _cthresholdHighDn
						: stdBar > ThresholdMedium
							? dir
								? _cthresholdMediumUp
								: _cthresholdMediumDn
							: stdBar > ThresholdNormal
								? dir
									? _cthresholdNormalUp
									: _cthresholdNormalDn
								: dir
									? _cthresholdLowUp
									: _cthresholdLowDn;

				_paintBars[bar] = barColor;
			}

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
		}

		#endregion
	}

}