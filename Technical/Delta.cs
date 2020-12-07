namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[Category("Bid x Ask,Delta,Volume")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/3996-delta")]
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

		private readonly CandleDataSeries _candles = new CandleDataSeries("Delta candles") { DownCandleColor = Colors.Red, UpCandleColor = Colors.Green};

		private readonly ValueDataSeries _diapasonhigh = new ValueDataSeries("Delta range high")
			{ Color = Color.FromArgb(128, 128, 128, 128), ShowZeroValue = false, ShowCurrentValue = false };

		private readonly ValueDataSeries _diapasonlow = new ValueDataSeries("Delta range low")
			{ Color = Color.FromArgb(128, 128, 128, 128), ShowZeroValue = false, ShowCurrentValue = false };

		private readonly ValueDataSeries _negativeDelta = new ValueDataSeries("Negative delta")
			{ Color = Colors.Red, VisualType = VisualMode.Histogram, ShowZeroValue = false, ShowCurrentValue = false };

		private readonly ValueDataSeries _positiveDelta;
		private readonly CustomValueDataSeries _values = new CustomValueDataSeries("AxisValues") { IsHidden = true };
		private BarDirection _barDirection;
		private DeltaType _deltaType;
		private decimal _filter;
		private int _lastBarAlert;
		private bool _minimizedMode;
		private DeltaVisualMode _mode = DeltaVisualMode.Candles;
		private decimal _prevDeltaValue;

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
		public decimal AlertFilter { get; set; }

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

		#endregion

		#region ctor

		public Delta()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_positiveDelta = (ValueDataSeries)DataSeries[0]; //2
			_positiveDelta.Name = "Positive delta";
			_positiveDelta.Color = Colors.Green;
			_positiveDelta.VisualType = VisualMode.Histogram;
			_positiveDelta.ShowZeroValue = false;
			_positiveDelta.ShowCurrentValue = false;
			DataSeries.Add(_negativeDelta); //3
			DataSeries.Insert(0, _diapasonhigh); //0
			DataSeries.Insert(1, _diapasonlow); //1
			DataSeries.Add(_candles); //4
			DataSeries.Add(_values);
			Mode = Mode;
		}

		#endregion

		#region Protected methods

		#region Overrides of Indicator

		protected override void OnCalculate(int bar, decimal value)
		{
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

				_values[bar] = new CustomValue
				{
					Price = currentCandle.Close == 0 ? currentCandle.Open : currentCandle.Close,
					StringValue = deltavalue.ToString("G"),
					ValueColor = deltavalue > 0 ? _candles.UpCandleColor : _candles.DownCandleColor
				};
			}
			else
			{
				_diapasonlow[bar] = minDelta;
				_diapasonhigh[bar] = maxDelta;

				_candles[bar].Open = 0;
				_candles[bar].Close = deltavalue;
				_candles[bar].High = maxDelta;
				_candles[bar].Low = minDelta;

				_values[bar] = new CustomValue
				{
					Price = _candles[bar].Close,
					StringValue = deltavalue.ToString("G"),
					ValueColor = deltavalue > 0 ? _candles.UpCandleColor : _candles.DownCandleColor
				};
			}

			if (UseAlerts && CurrentBar - 1 == bar && _lastBarAlert != bar)
			{
				if (AlertFilter > 0)
				{
					if (deltavalue > AlertFilter && _prevDeltaValue < AlertFilter)
					{
						_lastBarAlert = bar;
						AddAlert(AlertFile, InstrumentInfo.Instrument, $"Delta reached {AlertFilter} filter", AlertBGColor, AlertForeColor);
					}
				}

				if (AlertFilter < 0)
				{
					if (deltavalue < AlertFilter && _prevDeltaValue > AlertFilter)
					{
						_lastBarAlert = bar;
						AddAlert(AlertFile, InstrumentInfo.Instrument, $"Delta reached {AlertFilter} filter", AlertBGColor, AlertForeColor);
					}
				}
			}

			_prevDeltaValue = deltavalue;
		}

		#endregion

		#endregion
	}
}