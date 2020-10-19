namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using Utils.Common.Localization;

	[DisplayName("Relative Volume")]
	[LocalizedDescription(typeof(Resources), "RelativeVolume")]
	public class RelativeVolume : Indicator
	{
		#region Nested types

		private class AvgBar
		{
			#region Fields

			private readonly int _lookBack;

			private readonly Queue<decimal> _volume = new Queue<decimal>();

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

		private readonly ValueDataSeries _averagePoints;
		private readonly Dictionary<TimeSpan, AvgBar> _avgVolumes = new Dictionary<TimeSpan, AvgBar>();
		private readonly ValueDataSeries _negative;
		private readonly ValueDataSeries _neutral;
		private readonly ValueDataSeries _positive;
		private bool _deltaColored;
		private bool _isSupportedTimeFrame;
		private int _lastBar;
		private int _lookBack;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources), Name = "DeltaColored", GroupName = "Colors")]
		public bool DeltaColored
		{
			get => _deltaColored;
			set
			{
				_deltaColored = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources), GroupName = "Common", Name = "LookBack", Order = 10)]
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

		#endregion

		#region ctor

		public RelativeVolume()
			: base(true)
		{
			_deltaColored = false;
			LookBack = 20;
			Panel = IndicatorDataProvider.NewPanel;

			_positive = new ValueDataSeries("Positive")
			{
				VisualType = VisualMode.Histogram,
				Color = Colors.Green,
				ShowZeroValue = false
			};

			_negative = new ValueDataSeries("Negative")
			{
				VisualType = VisualMode.Histogram,
				Color = Colors.Red,
				ShowZeroValue = false
			};

			_neutral = new ValueDataSeries("Neutral")
			{
				VisualType = VisualMode.Histogram,
				Color = Colors.Gray,
				ShowZeroValue = false
			};

			_averagePoints = new ValueDataSeries("AveragePoints")
			{
				VisualType = VisualMode.Dots,
				Color = Colors.Blue,
				Width = 2,
				ShowZeroValue = false
			};

			DataSeries[0] = _positive;
			DataSeries.Add(_negative);
			DataSeries.Add(_neutral);
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

			var currentCandle = GetCandle(bar);

			if (_isSupportedTimeFrame && bar > _lastBar)
			{
				_lastBar = bar;

				var time = currentCandle.Time.TimeOfDay;

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

			ColoringBar(currentCandle, bar);
		}

		#endregion

		#region Private methods

		private void ColoringBar(IndicatorCandle candle, int bar)
		{
			if (_deltaColored)
			{
				if (candle.Delta > 0)
				{
					_positive[bar] = candle.Volume;
					_negative[bar] = _neutral[bar] = 0;
				}
				else if (candle.Delta < 0)
				{
					_negative[bar] = candle.Volume;
					_positive[bar] = _neutral[bar] = 0;
				}
				else
				{
					_neutral[bar] = candle.Volume;
					_positive[bar] = _negative[bar] = 0;
				}
			}
			else
			{
				if (candle.Open < candle.Close)
				{
					_positive[bar] = candle.Volume;
					_negative[bar] = _neutral[bar] = 0;
				}
				else if (candle.Open > candle.Close)
				{
					_negative[bar] = candle.Volume;
					_positive[bar] = _neutral[bar] = 0;
				}
				else
				{
					_neutral[bar] = candle.Volume;
					_positive[bar] = _negative[bar] = 0;
				}
			}
		}

		#endregion
	}
}