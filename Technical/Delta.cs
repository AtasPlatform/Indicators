namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Globalization;
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

		#endregion

		#region Fields

		private readonly CandleDataSeries _candles = new("Delta candles") { DownCandleColor = Colors.Red, UpCandleColor = Colors.Green };

		private readonly ValueDataSeries _diapasonhigh = new("Delta range high")
			{ Color = Color.FromArgb(128, 128, 128, 128), ShowZeroValue = false, ShowCurrentValue = false };

		private readonly ValueDataSeries _diapasonlow = new("Delta range low")
			{ Color = Color.FromArgb(128, 128, 128, 128), ShowZeroValue = false, ShowCurrentValue = false };

		private readonly ValueDataSeries _negativeDelta = new("Negative delta")
			{ Color = Colors.Red, VisualType = VisualMode.Histogram, ShowZeroValue = false };

		private readonly ValueDataSeries _positiveDelta;
		private decimal _alertFilter;
		private BarDirection _barDirection;
		private DeltaType _deltaType;
		private ValueDataSeries _downSeries = new(Resources.Down);
		private decimal _filter;
		private System.Drawing.Color _fontColor;

		private RenderStringFormat _format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
		private int _lastBar;
		private int _lastBarAlert;
		private bool _minimizedMode;
		private DeltaVisualMode _mode = DeltaVisualMode.Candles;
		private decimal _prevDeltaValue;
		private bool _showDivergence;

		private ValueDataSeries _upSeries = new(Resources.Up);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "Alerts")]
		public bool UseAlerts { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts")]
		public string AlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Resources), Name = "FontColor", GroupName = "Alerts")]
		public Color AlertForeColor { get; set; } = Color.FromArgb(255, 247, 249, 249);

		[Display(ResourceType = typeof(Resources), Name = "BackGround", GroupName = "Alerts")]
		public Color AlertBGColor { get; set; } = Color.FromArgb(255, 75, 72, 72);

		[Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Alerts")]
		public decimal AlertFilter
		{
			get => _alertFilter;
			set
			{
				_lastBarAlert = 0;
				_alertFilter = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "VisualMode")]
		public DeltaVisualMode Mode
		{
			get => _mode;
			set
			{
				_mode = value;

				if (_mode == DeltaVisualMode.Histogram)
				{
					_positiveDelta.VisualType = _negativeDelta.VisualType = VisualMode.Histogram;
					_diapasonhigh.VisualType = VisualMode.Hide;
					_diapasonlow.VisualType = VisualMode.Hide;
					_candles.Visible = false;
				}
				else if (_mode == DeltaVisualMode.HighLow)
				{
					_positiveDelta.VisualType = _negativeDelta.VisualType = VisualMode.Histogram;
					_diapasonhigh.VisualType = VisualMode.Histogram;
					_diapasonlow.VisualType = VisualMode.Histogram;
					_candles.Visible = false;
				}
				else if (_mode == DeltaVisualMode.Candles)
				{
					_positiveDelta.VisualType = _negativeDelta.VisualType = VisualMode.Hide;
					_diapasonhigh.VisualType = VisualMode.Hide;
					_diapasonlow.VisualType = VisualMode.Hide;
					_candles.Visible = true;
					_candles.Mode = CandleVisualMode.Candles;
				}
				else
				{
					_positiveDelta.VisualType = _negativeDelta.VisualType = VisualMode.Hide;
					_diapasonhigh.VisualType = VisualMode.Hide;
					_diapasonlow.VisualType = VisualMode.Hide;
					_candles.Visible = true;
					_candles.Mode = CandleVisualMode.Bars;
				}

				RaisePropertyChanged("Mode");
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Minimizedmode")]
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

		[Display(ResourceType = typeof(Resources), Name = "BarsDirection", GroupName = "Filters")]
		public BarDirection BarsDirection
		{
			get => _barDirection;
			set
			{
				_barDirection = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "DeltaType", GroupName = "Filters")]
		public DeltaType DeltaTypes
		{
			get => _deltaType;
			set
			{
				_deltaType = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Filters")]
		public decimal Filter
		{
			get => _filter;
			set
			{
				_filter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShowDivergence", GroupName = "Filters")]
		public bool ShowDivergence
		{
			get => _showDivergence;
			set
			{
				_showDivergence = value;

				if (value)
				{
					_upSeries.VisualType = VisualMode.UpArrow;
					_downSeries.VisualType = VisualMode.DownArrow;
				}
				else
					_upSeries.VisualType = _downSeries.VisualType = VisualMode.Hide;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShowVolume", GroupName = "Visualization", Order = 200)]
		public bool ShowVolume { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Font", GroupName = "Visualization", Order = 210)]
		public FontSetting Font { get; set; } = new("Arial", 10);

		[Display(ResourceType = typeof(Resources), Name = "FontColor", GroupName = "Visualization", Order = 220)]
		public Color FontColor
		{
			get => _fontColor.Convert();
			set => _fontColor = value.Convert();
		}

		#endregion

		#region ctor

		public Delta()
			: base(true)
		{
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);
			FontColor = Colors.Blue;

			Panel = IndicatorDataProvider.NewPanel;
			_positiveDelta = (ValueDataSeries)DataSeries[0]; //2
			_positiveDelta.Name = "Positive delta";
			_positiveDelta.Color = Colors.Green;
			_positiveDelta.VisualType = VisualMode.Histogram;
			_positiveDelta.ShowCurrentValue = true;
			_negativeDelta.ShowCurrentValue = true;

			_upSeries.VisualType = _downSeries.VisualType = VisualMode.Hide;
			_upSeries.ShowCurrentValue = _downSeries.ShowCurrentValue = false;
			_upSeries.ShowZeroValue = _downSeries.ShowZeroValue = false;
			_upSeries.Color = Colors.Green;
			_downSeries.Color = Colors.Red;

			DataSeries.Add(_negativeDelta); //3
			DataSeries.Insert(0, _diapasonhigh); //0
			DataSeries.Insert(1, _diapasonlow); //1
			DataSeries.Add(_candles); //4

			DataSeries.Add(_upSeries);
			DataSeries.Add(_downSeries);
			Mode = Mode;
		}

		#endregion

		#region Protected methods

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if (!ShowVolume || ChartInfo.ChartVisualMode != ChartVisualModes.Clusters || Panel == IndicatorDataProvider.CandlesPanel)
				return;

			var barWidth = ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0);
			var y = Container.Region.Y + (Container.Region.Bottom - Container.Region.Y) / 2;

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

				var renderText = value.ToString(CultureInfo.InvariantCulture);

				var strRect = new Rectangle(ChartInfo.GetXByBar(i),
					y,
					barWidth,
					context.MeasureString(renderText, Font.RenderObject).Height);
				context.DrawString(renderText, Font.RenderObject, _fontColor, strRect, _format);
			}
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				DataSeries.ForEach(x => x.Clear());

			var candle = GetCandle(bar);
			var deltavalue = candle.Delta;
			var absdelta = Math.Abs(deltavalue);
			var maxDelta = candle.MaxDelta;
			var minDelta = candle.MinDelta;

			var isUnderFilter = absdelta < _filter;

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

			if (_deltaType == DeltaType.Negative && deltavalue > 0)
				isUnderFilter = true;

			if (_deltaType == DeltaType.Positive && deltavalue < 0)
				isUnderFilter = true;

			if (isUnderFilter)
			{
				deltavalue = 0;
				absdelta = 0;
				minDelta = maxDelta = 0;
			}

			if (deltavalue > 0)
			{
				_positiveDelta[bar] = deltavalue;
				_negativeDelta[bar] = 0;
			}
			else
			{
				_positiveDelta[bar] = 0;
				_negativeDelta[bar] = MinimizedMode ? absdelta : deltavalue;
			}

			if (MinimizedMode)
			{
				var high = Math.Abs(maxDelta);
				var low = Math.Abs(minDelta);
				_diapasonlow[bar] = Math.Min(Math.Min(high, low), absdelta);
				_diapasonhigh[bar] = Math.Max(high, low);

				var currentCandle = _candles[bar];
				currentCandle.Open = deltavalue > 0 ? 0 : absdelta;
				currentCandle.Close = deltavalue > 0 ? absdelta : 0;
				currentCandle.High = _diapasonhigh[bar];
				currentCandle.Low = _diapasonlow[bar];
			}
			else
			{
				_diapasonlow[bar] = minDelta;
				_diapasonhigh[bar] = maxDelta;

				_candles[bar].Open = 0;
				_candles[bar].Close = deltavalue;
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
				_prevDeltaValue = deltavalue;
				_lastBar = bar;
			}

			if (UseAlerts && CurrentBar - 1 == bar && _lastBarAlert != bar)
			{
				if (deltavalue >= AlertFilter && _prevDeltaValue < AlertFilter || deltavalue <= AlertFilter && _prevDeltaValue > AlertFilter)
				{
					_lastBarAlert = bar;
					AddAlert(AlertFile, InstrumentInfo.Instrument, $"Delta reached {AlertFilter} filter", AlertBGColor, AlertForeColor);
				}
			}

			_prevDeltaValue = deltavalue;
		}

		#endregion
	}
}