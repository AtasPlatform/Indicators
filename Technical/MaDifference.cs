namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Moving Average Difference")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45297-moving-average-difference")]
	public class MaDifference : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _downSeries = new(Resources.Down)
		{
			Color = Colors.Red,
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true
		};
		private readonly ValueDataSeries _upSeries = new(Resources.Up)
		{
			Color = Colors.Green,
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
            UseMinimizedModeIfEnabled = true
		};
        
		private readonly SMA _sma1 = new() { Period = 10 };
		private readonly SMA _sma2 = new() { Period = 20 };

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "SMA1", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
        public int Period1
		{
			get => _sma1.Period;
			set
			{
				_sma1.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMA2", GroupName = "Settings", Order = 110)]
		[Range(1, 10000)]
        public int Period2
		{
			get => _sma2.Period;
			set
			{
				_sma2.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MaDifference()
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _upSeries;
			DataSeries.Add(_downSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_sma1.Calculate(bar, value);
			_sma2.Calculate(bar, value);

			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				return;
			}

			var diff = _sma1[bar] - _sma2[bar];
			var lastValue = _upSeries[bar - 1] == 0 ? _downSeries[bar - 1] : _upSeries[bar - 1];

			if (diff > lastValue)
				_upSeries[bar] = diff;
			else
				_downSeries[bar] = diff;
		}

		#endregion
	}
}