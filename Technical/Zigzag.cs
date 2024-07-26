﻿namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Settings;
	
    [DisplayName("ZigZag pro")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ZigzagIndDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602632")]
	public class Zigzag : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.RelativeInPercent))]
			Relative = 0,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AbsolutePrice))]
			Absolute = 1,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ticks))]
			Ticks = 2
		}

		public enum TimeFormat
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.None))]
			None,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Days))]
			Days,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Exact))]
			Exact
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _data = new("Data", Strings.Data)
		{
			Color = DefaultColors.Red.Convert(),
			LineDashStyle = LineDashStyle.Dot,
			VisualType = VisualMode.Line,
			Width = 2,
			DescriptionKey = nameof(Strings.BaseLineSettingsDescription)
		};

		private Mode _calcMode = Mode.Ticks;
		private int _cumulativeBars;
		private decimal _cumulativeDelta;
		private decimal _cumulativeTicks;
		private decimal _cumulativeVolume;
		private int _days = 20;

        private int _direction;
		private bool _ignoreWicks = true;
		private int _lastBar = -1;
		private int _lastHighBar;
		private int _lastLowBar;

		private decimal _percentage = 30.0m;
		private bool _showBars = true;
		private bool _showDelta = true;
		private bool _showTicks = true;
		private TimeFormat _showTime = TimeFormat.Exact;
		private bool _showVolume = true;
		private int _targetBar;
		private CrossColor _textColor = DefaultColors.Red.Convert();
		private float _textSize = 15.0f;
		private TimeSpan _trendDuration;
		private int _verticalOffset = 1;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue, Description = nameof(Strings.DaysLookBackDescription))]
        [Range(0, 1000)]
		public int Days
		{
			get => _days;
			set
			{
				_days = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CalculationMode), GroupName = nameof(Strings.CalculationSettings), Description = nameof(Strings.CalculationModeDescription), Order = 100)]
		public Mode CalcMode
		{
			get => _calcMode;
			set
			{
				_calcMode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IgnoreWicks), GroupName = nameof(Strings.CalculationSettings), Description = nameof(Strings.IgnoreWicksDescription), Order = 110)]
		public bool IgnoreWicks
		{
			get => _ignoreWicks;
			set
			{
				_ignoreWicks = value;
				RecalculateValues();
			}
		}

		[Range(0, int.MaxValue)]
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.RequiredChange), GroupName = nameof(Strings.CalculationSettings), Description = nameof(Strings.RequiredChangeValueDescription), Order = 120)]
		public decimal Percentage
		{
			get => _percentage;
			set
			{
				_percentage = value;
				RecalculateValues();
			}
		}

		[Range(1, 100)]
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextSize), GroupName = nameof(Strings.TextSettings), Description = nameof(Strings.FontSizeDescription), Order = 200)]
		public float TextSize
		{
			get => _textSize;
			set
			{
				_textSize = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextColor), GroupName = nameof(Strings.TextSettings), Description = nameof(Strings.LabelTextColorDescription), Order = 210)]
		public CrossColor TextColor
		{
			get => _textColor;
			set
			{
				_textColor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowDelta), GroupName = nameof(Strings.TextSettings), Description = nameof(Strings.ShowCurrentParameterInfoDescription), Order = 220)]
		public bool ShowDelta
		{
			get => _showDelta;
			set
			{
				_showDelta = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowVolume), GroupName = nameof(Strings.TextSettings), Description = nameof(Strings.ShowCurrentParameterInfoDescription), Order = 230)]
		public bool ShowVolume
		{
			get => _showVolume;
			set
			{
				_showVolume = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowTicks), GroupName = nameof(Strings.TextSettings), Description = nameof(Strings.ShowCurrentParameterInfoDescription), Order = 240)]
		public bool ShowTicks
		{
			get => _showTicks;
			set
			{
				_showTicks = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowBars), GroupName = nameof(Strings.TextSettings), Description = nameof(Strings.ShowCurrentParameterInfoDescription), Order = 250)]
		public bool ShowBars
		{
			get => _showBars;
			set
			{
				_showBars = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowTime), GroupName = nameof(Strings.TextSettings), Description = nameof(Strings.ShowCurrentParameterInfoDescription), Order = 260)]
		public TimeFormat ShowTime
		{
			get => _showTime;
			set
			{
				_showTime = value;
				RecalculateValues();
			}
		}
		
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.VerticalOffset), GroupName = nameof(Strings.TextSettings), Description = nameof(Strings.LabelOffsetYDescription), Order = 270)]
		[Range(0, 1000)]
		public int VerticalOffset
		{
			get => _verticalOffset;
			set
			{
				_verticalOffset = value;
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
			_cumulativeVolume = 0;
			_cumulativeDelta = 0;
			_cumulativeTicks = 0;
			_cumulativeBars = 0;
			base.OnRecalculate();
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				AddText("LastText", "", true, CurrentBar - 1, 0, TextColor.Convert(),
					System.Drawing.Color.Transparent, System.Drawing.Color.Transparent, _textSize,
					DrawingText.TextAlign.Center);

				_targetBar = 0;

				if (_days <= 0)
					return;

				var days = 0;

				for (var i = CurrentBar - 1; i >= 0; i--)
				{
					_targetBar = i;

					if (!IsNewSession(i))
						continue;

					days++;

					if (days == _days)
						break;
				}

				_lastHighBar = _targetBar;
				_lastLowBar = _targetBar;

				if (_targetBar > 0)
					_data.SetPointOfEndLine(_targetBar - 1);
				return;
			}

			if (bar < _targetBar || bar < Math.Min(_lastHighBar, _lastLowBar) || bar == 0)
				return;

			var calcBar = bar - 1;

			var requiredChange = 0.0m;

			var candleHigh = GetCandle(calcBar).High;
			var candleLow = GetCandle(calcBar).Low;

			var lastHighBarMax = GetCandle(_lastHighBar).High;
			var lastLowBarMin = GetCandle(_lastLowBar).Low;

			if (IgnoreWicks)
			{
				candleHigh = Math.Max(GetCandle(calcBar).Open, GetCandle(calcBar).Close);
				candleLow = Math.Min(GetCandle(calcBar).Open, GetCandle(calcBar).Close);
				lastHighBarMax = Math.Max(GetCandle(_lastHighBar).Open, GetCandle(_lastHighBar).Close);
				lastLowBarMin = Math.Min(GetCandle(_lastLowBar).Open, GetCandle(_lastLowBar).Close);
			}

			if (bar != _lastBar)
			{
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
						_lastHighBar = calcBar;
					}
					else if (candleLow < candleZeroLow && candleHigh < candleZeroLow) //currently in a downtrend
					{
						_direction = -1;
						_lastLowBar = calcBar;
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
						_lastHighBar = calcBar;

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

                        AddText(_lastHighBar + value.ToString(), label.TrimEnd(), true, _lastHighBar, lastHighBarMax + InstrumentInfo.TickSize * VerticalOffset, 0, 0,
							ConvertColor(_textColor), System.Drawing.Color.Transparent, System.Drawing.Color.Transparent, _textSize,
							DrawingText.TextAlign.Center);
						_lastLowBar = calcBar;
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
						_lastLowBar = calcBar;
					else if (candleLow > lastLowBarMin && lastLowBarMin + requiredChange <= candleHigh
					        ) //downtrend ended
					{
						_direction = 1;
						_cumulativeVolume = 0;
						_cumulativeDelta = 0;

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
						}

						if (_showVolume)
						{
							label += DecimalToShortString(_cumulativeVolume) + Environment.NewLine;
						}

						if (_showTicks)
						{
							label += DecimalToShortString(_cumulativeTicks) + " Ticks" + Environment.NewLine;
						}

						if (_showBars)
						{
							label += DecimalToShortString(_cumulativeBars) + "Bars" + Environment.NewLine;
						}

						if (_showTime == TimeFormat.Days)
						{
							label += _trendDuration.ToString(@"d\d\a\y\s");
						}

						if (_showTime == TimeFormat.Exact)
						{
							if (_trendDuration.Days > 0)
								label += _trendDuration.ToString(@"d\d\a\y\s\ hh\:mm\:ss");
							else
								label += _trendDuration.ToString(@"hh\:mm\:ss");
						}

						AddText(_lastLowBar + value.ToString(), label.TrimEnd(), false, _lastLowBar, lastLowBarMin - InstrumentInfo.TickSize * VerticalOffset, 0, 0,
							ConvertColor(_textColor), System.Drawing.Color.Transparent, System.Drawing.Color.Transparent, _textSize,
							DrawingText.TextAlign.Center);
						_lastHighBar = calcBar;
					}
				}
			}

			if (bar == SourceDataSeries.Count - 1)
			{
				var candle = GetCandle(bar);
				_cumulativeVolume = 0;
				_cumulativeDelta = 0;

				if (_direction == 1)
				{
					for (var i = _lastLowBar; i <= bar; i++)
					{
						_cumulativeVolume += GetCandle(i).Volume;
						_cumulativeDelta += GetCandle(i).Delta;
						_data[i] = Linear(lastLowBarMin, candle.Close, bar - _lastLowBar + 1, i - _lastLowBar);
					}
				}
				else if (_direction == -1)
				{
					for (var i = _lastHighBar; i <= bar; i++)
					{
						_cumulativeVolume += GetCandle(i).Volume;
						_cumulativeDelta += GetCandle(i).Delta;
						_data[i] = Linear(lastHighBarMax, candle.Close, bar - _lastHighBar + 1, i - _lastHighBar);
					}
				}

				DrawLastText();
			}

			_lastBar = bar;
		}

		#endregion

		#region Private methods

		private void DrawLastText()
		{
			var renderText = "";
			var lastWave = _direction == 1 ? _lastLowBar : _lastHighBar;

			if (_showDelta)
			{
				renderText += DecimalToShortString(_cumulativeDelta) + "Δ" + Environment.NewLine;
			}

			if (_showVolume)
			{
				renderText += DecimalToShortString(_cumulativeVolume) + Environment.NewLine;
			}

			if (_showTicks)
			{
				var ticks = Math.Abs(_data[CurrentBar - 1] - _data[lastWave]) / InstrumentInfo.TickSize;
				renderText += DecimalToShortString(ticks) + " Ticks" + Environment.NewLine;
			}

			if (_showBars)
			{
				renderText += DecimalToShortString(CurrentBar - lastWave) + "Bars" + Environment.NewLine;
			}

			var duration = GetCandle(CurrentBar - 1).Time - GetCandle(lastWave).Time;

			if (_showTime == TimeFormat.Days)
			{
				renderText += duration.ToString(@"d\d\a\y\s");
			}

			if (_showTime == TimeFormat.Exact)
			{
				if (duration.Days > 0)
					renderText += duration.ToString(@"d\d\a\y\s\ hh\:mm\:ss");
				else
					renderText += duration.ToString(@"hh\:mm\:ss");
			}

			Labels["LastText"].IsAbovePrice = _direction > 0;
			Labels["LastText"].Text = renderText.TrimEnd();
			Labels["LastText"].TextPrice = _data[CurrentBar - 1] + InstrumentInfo.TickSize * VerticalOffset * _direction;
			Labels["LastText"].Bar = CurrentBar - 1;
		}

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

		private System.Drawing.Color ConvertColor(CrossColor input)
		{
			return System.Drawing.Color.FromArgb(input.A, input.R, input.G, input.B);
		}

		#endregion
	}
}