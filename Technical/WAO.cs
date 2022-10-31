namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Weighted Average Oscillator")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45330-weighted-average-oscillator")]
	public class WAO : Indicator
	{
		#region Fields

		private readonly WMA _longWma = new() { Period = 30 };
		private readonly WMA _shortWma = new() { Period = 10 };

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int ShortPeriod
		{
			get => _shortWma.Period;
			set
			{
				_shortWma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Settings", Order = 110)]
		[Range(1, 10000)]
        public int LongPeriod
		{
			get => _longWma.Period;
			set
			{
				_longWma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public WAO()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0].UseMinimizedModeIfEnabled = true;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			this[bar] = _shortWma.Calculate(bar, value) - _longWma.Calculate(bar, value);
		}

		#endregion
	}
}