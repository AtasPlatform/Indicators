namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

    using OFT.Attributes;
    using OFT.Rendering.Settings;

	[DisplayName("ZigZag pro")]
	[Description("ZigZag pro")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/20324-zigzag-pro")]
	public class Zigzag : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Resources), Name = "RelativeInPerent")]
			Relative = 0,

			[Display(ResourceType = typeof(Resources), Name = "AbsolutePrice")]
			Absolute = 1,

			[Display(ResourceType = typeof(Resources), Name = "Ticks")]
			Ticks = 2
		}

		public enum TimeFormat
		{
			None,
			Days,
			Exact
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _data = new ValueDataSeries("Data")
		{
			Color = Colors.Red,
			LineDashStyle = LineDashStyle.Dot,
			VisualType = VisualMode.Line,
			Width = 2
		};

		private Mode _calcMode = Mode.Ticks;
		private int _cumulativeBars;
		private decimal _cumulativeDelta;
		private decimal _cumulativeTicks;
		private decimal _cumulativeVolume;

		private int _direction;
		private bool _ignoreWicks = true;
		private int _lastHighBar;
		private int _lastLowBar;

		private decimal _percentage = 30.0m;
		private bool _showBars = true;
		private bool _showDelta = true;
		private bool _showTicks = true;
		private TimeFormat _showTime = TimeFormat.Exact;
		private bool _showVolume = true;
		private Color _textColor = Colors.Red;
		private float _textSize = 15.0f;
		private TimeSpan _trendDuration;

		#endregion

		#region Properties

		[Category("Calculation Settings")]
		[DisplayName("Calculation Mode")]
		public Mode CalcMode
		{
			get => _calcMode;
			set
			{
				_calcMode = value;
				RecalculateValues();
			}
		}

		[Category("Calculation Settings")]
		[DisplayName("Ignore wicks")]
		public bool IgnoreWicks
		{
			get => _ignoreWicks;
			set
			{
				_ignoreWicks = value;
				RecalculateValues();
			}
		}

		[Category("Text Settings")]
		[DisplayName("Text Size")]
		public float TextSize
		{
			get => _textSize;
			set
			{
				_textSize = value;
				RecalculateValues();
			}
		}

		[Category("Text Settings")]
		[DisplayName("Text Color")]
		public Color TextColor
		{
			get => _textColor;
			set
			{
				_textColor = value;
				RecalculateValues();
			}
		}

		[Category("Text Settings")]
		[DisplayName("Show Delta")]
		public bool ShowDelta
		{
			get => _showDelta;
			set
			{
				_showDelta = value;
				RecalculateValues();
			}
		}

		[Category("Text Settings")]
		[DisplayName("Show Volume")]
		public bool ShowVolume
		{
			get => _showVolume;
			set
			{
				_showVolume = value;
				RecalculateValues();
			}
		}

		[Category("Text Settings")]
		[DisplayName("Show Ticks")]
		public bool ShowTicks
		{
			get => _showTicks;
			set
			{
				_showTicks = value;
				RecalculateValues();
			}
		}

		[Category("Text Settings")]
		[DisplayName("Show Bars")]
		public bool ShowBars
		{
			get => _showBars;
			set
			{
				_showBars = value;
				RecalculateValues();
			}
		}

		[Category("Text Settings")]
		[DisplayName("Show Time")]
		public TimeFormat ShowTime
		{
			get => _showTime;
			set
			{
				_showTime = value;
				RecalculateValues();
			}
		}

		[Category("Calculation Settings")]
		[DisplayName("Required Change")]
		public decimal Percentage
		{
			get => _percentage;
			set
			{
				_percentage = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Zigzag()
			: base(true)
		{
			DataSeries[0].IsHidden = true;
			DenyToChangePanel = true;

			DataSeries.Add(_data);
		}

		#endregion

		#region Protected methods

		protected override void OnRecalculate()
		{
			_direction = 0;
			_lastHighBar = 0;
			_lastLowBar = 0;
			_cumulativeVolume = 0;
			_cumulativeDelta = 0;
			_cumulativeTicks = 0;
			_cumulativeBars = 0;
			base.OnRecalculate();
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0 || bar < Math.Min(_lastHighBar, _lastLowBar))
				return;

			var requiredChange = 0.0m;

			var candleHigh = GetCandle(bar).High;
			var candleLow = GetCandle(bar).Low;

			var lastHighBarMax = GetCandle(_lastHighBar).High;
			var lastLowBarMin = GetCandle(_lastLowBar).Low;

			if (IgnoreWicks)
			{
				candleHigh = Math.Max(GetCandle(bar).Open, GetCandle(bar).Close);
				candleLow = Math.Min(GetCandle(bar).Open, GetCandle(bar).Close);
				lastHighBarMax = Math.Max(GetCandle(_lastHighBar).Open, GetCandle(_lastHighBar).Close);
				lastLowBarMin = Math.Min(GetCandle(_lastLowBar).Open, GetCandle(_lastLowBar).Close);
			}

			if (_direction == 0)
			{
				var candleZeroHigh = GetCandle(0).High;
				var candleZeroLow = GetCandle(0).Low;

				if (IgnoreWicks)
				{
					candleZeroHigh = Math.Max(GetCandle(0).Open, GetCandle(0).Close);
					candleZeroLow = Math.Min(GetCandle(0).Open, GetCandle(0).Close);
				}

				if (candleHigh > candleZeroHigh && candleLow > candleZeroLow) //currently in an uptrend
				{
					_direction = 1;
					_lastHighBar = bar;
				}
				else if (candleLow < candleZeroLow && candleHigh < candleZeroLow) //currently in a downtrend
				{
					_direction = -1;
					_lastLowBar = bar;
				}
			}
			else if (_direction == 1)
			{
				if (_calcMode == Mode.Relative)
					requiredChange = lastHighBarMax * _percentage / 100;
				else if (_calcMode == Mode.Absolute)
					requiredChange = _percentage;
				else if (_calcMode == Mode.Ticks)
					requiredChange = _percentage * InstrumentInfo.TickSize;

				if (candleHigh > lastHighBarMax) //continue uptrend
					_lastHighBar = bar;

				else if (candleHigh < lastHighBarMax && lastHighBarMax - requiredChange >= candleLow
				) //uptrend ended
				{
					_direction = -1;
					_cumulativeVolume = 0;
					_cumulativeDelta = 0;

					for (var i = _lastLowBar; i <= _lastHighBar; i++)
					{
						var candle = GetCandle(i);
						_cumulativeVolume += candle.Volume;
						_cumulativeDelta += candle.Delta;
						_cumulativeTicks += candle.Ticks;
						_data[i] = Linear(lastLowBarMin, lastHighBarMax, _lastHighBar - _lastLowBar + 1, i - _lastLowBar);
					}

					_trendDuration = GetCandle(_lastHighBar).Time - GetCandle(_lastLowBar).Time;
					_cumulativeTicks = Math.Abs((lastHighBarMax - lastLowBarMin) / InstrumentInfo.TickSize);
					_cumulativeBars = Math.Abs(_lastHighBar - _lastLowBar) + 1;
					var label = "";

					if (_showDelta)
						label += DecimalToShortString(_cumulativeDelta) + "Δ" + Environment.NewLine;

					if (_showVolume)
						label += DecimalToShortString(_cumulativeVolume) + Environment.NewLine;

					if (_showTicks)
						label += DecimalToShortString(_cumulativeTicks) + " Ticks" + Environment.NewLine;

					if (_showBars)
						label += DecimalToShortString(_cumulativeBars) + "Bars" + Environment.NewLine;

					if (_showTime == TimeFormat.Days)
						label += _trendDuration.ToString(@"d\d\a\y\s");

					if (_showTime == TimeFormat.Exact)
					{
						if (_trendDuration.Days > 0)
							label += _trendDuration.ToString(@"d\d\a\y\s\ hh\:mm\:ss");
						else
							label += _trendDuration.ToString(@"hh\:mm\:ss");
					}

					AddText(_lastHighBar + value.ToString(), label, true, _lastHighBar, lastHighBarMax + TickSize, 0, 0,
						ConvertColor(_textColor), System.Drawing.Color.Transparent, System.Drawing.Color.Transparent, _textSize,
						DrawingText.TextAlign.Center);
					_lastLowBar = bar;
				}
			}
			else if (_direction == -1)
			{
				if (_calcMode == Mode.Relative)
					requiredChange = lastLowBarMin * _percentage / 100;
				else if (_calcMode == Mode.Absolute)
					requiredChange = _percentage;
				else if (_calcMode == Mode.Ticks)
					requiredChange = _percentage * InstrumentInfo.TickSize;

				if (candleLow < lastLowBarMin) //continue downtrend
					_lastLowBar = bar;
				else if (candleLow > lastLowBarMin && lastLowBarMin + requiredChange <= candleHigh
				) //downtrend ended
				{
					_direction = 1;
					_cumulativeVolume = 0;
					_cumulativeDelta = 0;
					var spacing = 0;

					for (var i = _lastHighBar; i <= _lastLowBar; i++)
					{
						_cumulativeVolume += GetCandle(i).Volume;
						_cumulativeDelta += GetCandle(i).Delta;
						_data[i] = Linear(lastHighBarMax, lastLowBarMin, _lastLowBar - _lastHighBar + 1, i - _lastHighBar);
					}

					_trendDuration = GetCandle(_lastLowBar).Time - GetCandle(_lastHighBar).Time;
					_cumulativeTicks = Math.Abs((lastLowBarMin - lastHighBarMax) / InstrumentInfo.TickSize);
					_cumulativeBars = Math.Abs(_lastHighBar - _lastLowBar) + 1;
					var label = "";

					if (_showDelta)
					{
						label += DecimalToShortString(_cumulativeDelta) + "Δ" + Environment.NewLine;
						spacing += 20;
					}

					if (_showVolume)
					{
						label += DecimalToShortString(_cumulativeVolume) + Environment.NewLine;
						spacing += 20;
					}

					if (_showTicks)
					{
						label += DecimalToShortString(_cumulativeTicks) + " Ticks" + Environment.NewLine;
						spacing += 20;
					}

					if (_showBars)
					{
						label += DecimalToShortString(_cumulativeBars) + "Bars" + Environment.NewLine;
						spacing += 20;
					}

					if (_showTime == TimeFormat.Days)
					{
						label += _trendDuration.ToString(@"d\d\a\y\s");
						spacing += 20;
					}

					if (_showTime == TimeFormat.Exact)
					{
						if (_trendDuration.Days > 0)
							label += _trendDuration.ToString(@"d\d\a\y\s\ hh\:mm\:ss");
						else
							label += _trendDuration.ToString(@"hh\:mm\:ss");

						spacing += 20;
					}

					AddText(_lastLowBar + value.ToString(), label, true, _lastLowBar, lastLowBarMin - TickSize, spacing, 0,
						ConvertColor(_textColor), System.Drawing.Color.Transparent, System.Drawing.Color.Transparent, _textSize,
						DrawingText.TextAlign.Center);
					_lastHighBar = bar;
				}
			}

			if (bar == SourceDataSeries.Count - 1)
			{
				_cumulativeVolume = 0;

				if (_direction == 1)
				{
					for (var i = _lastLowBar; i <= bar; i++)
					{
						_cumulativeVolume += GetCandle(i).Volume;
						_data[i] = Linear(lastLowBarMin, lastHighBarMax, bar - _lastLowBar + 1, i - _lastLowBar);
					}
				}
				else if (_direction == -1)
				{
					for (var i = _lastHighBar; i <= bar; i++)
					{
						_cumulativeVolume += GetCandle(i).Volume;
						_data[i] = Linear(lastHighBarMax, candleLow, bar - _lastHighBar + 1, i - _lastHighBar);
					}
				}
			}
		}

		#endregion

		#region Private methods

		private decimal Linear(decimal start, decimal stop, int steps, int position)
		{
			if (steps > 1)
				return start + (stop - start) * position / (steps - 1);

			return start + (stop - start) * position / steps;
		}

		private string DecimalToShortString(decimal input)
		{
			if (Math.Abs(input) > 1000000)
			{
				input /= 1000000;
				return input.ToString("0.####") + "m";
			}

			if (Math.Abs(input) > 1000)
			{
				input /= 1000;
				return input.ToString("0.####") + "k";
			}

			return input.ToString();
		}

		private System.Drawing.Color ConvertColor(Color input)
		{
			return System.Drawing.Color.FromArgb(input.A, input.R, input.G, input.B);
		}

		#endregion
	}
}