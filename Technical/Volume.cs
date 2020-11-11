namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[Category("Bid x Ask,Delta,Volume")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/2471-volume")]
	public class Volume : Indicator
	{
		#region Nested types

		public enum InputType
		{
			Volume = 0,
			Ticks = 1
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _filterseries;
		private readonly ValueDataSeries _negative;
		private readonly ValueDataSeries _neutral;
		private readonly ValueDataSeries _positive;

		private bool _deltaColored;

		private decimal _filter;

		private InputType _input = InputType.Volume;

		private bool _useFilter;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "DeltaColored", GroupName = "Colors")]
		public bool DeltaColored
		{
			get => _deltaColored;
			set
			{
				_deltaColored = value;
				RaisePropertyChanged("DeltaColored");
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "UseFilter", GroupName = "Filter")]
		public bool UseFilter
		{
			get => _useFilter;
			set
			{
				_useFilter = value;
				RaisePropertyChanged("UseFilter");
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Filter")]
		public decimal FilterValue
		{
			get => _filter;
			set
			{
				_filter = value;
				RaisePropertyChanged("Filter");
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Type", GroupName = "Calculation")]
		public InputType Input
		{
			get => _input;
			set
			{
				_input = value;
				RaisePropertyChanged("Type");
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Volume()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_positive = (ValueDataSeries)DataSeries[0];
			_positive.Color = Colors.Green;
			_positive.VisualType = VisualMode.Histogram;
			_positive.ShowZeroValue = false;
			_positive.Name = "Positive";
			_negative = new ValueDataSeries("Negative")
			{
				Color = Colors.Red,
				VisualType = VisualMode.Histogram,
				ShowZeroValue = false
			};
			DataSeries.Add(_negative);
			_neutral = new ValueDataSeries("Neutral")
			{
				Color = Colors.Gray,
				VisualType = VisualMode.Histogram,
				ShowZeroValue = false
			};
			DataSeries.Add(_neutral);
			_filterseries = new ValueDataSeries("Filter")
			{
				Color = Colors.LightBlue,
				VisualType = VisualMode.Histogram,
				ShowZeroValue = false
			};
			DataSeries.Add(_filterseries);
		}

		#endregion

		#region Public methods

		public override string ToString()
		{
			return "Volume";
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			var val = candle.Volume;
			if (Input == InputType.Ticks)
				val = candle.Ticks;

			if (_useFilter && val > _filter)
			{
				_filterseries[bar] = val;
				_positive[bar] = _negative[bar] = _neutral[bar] = 0;
				return;
			}

			_filterseries[bar] = 0;
			if (_deltaColored)
			{
				if (candle.Delta > 0)
				{
					_positive[bar] = val;
					_negative[bar] = _neutral[bar] = 0;
				}
				else if (candle.Delta < 0)
				{
					_negative[bar] = val;
					_positive[bar] = _neutral[bar] = 0;
				}
				else
				{
					_neutral[bar] = val;
					_positive[bar] = _negative[bar] = 0;
				}
			}
			else
			{
				if (candle.Close > candle.Open)
				{
					_positive[bar] = val;
					_negative[bar] = _neutral[bar] = 0;
				}
				else if (candle.Close < candle.Open)
				{
					_negative[bar] = val;
					_positive[bar] = _neutral[bar] = 0;
				}
				else
				{
					_neutral[bar] = val;
					_positive[bar] = _negative[bar] = 0;
				}
			}
		}

		#endregion
	}
}