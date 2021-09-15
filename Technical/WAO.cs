namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
	using OFT.Localization;

	[DisplayName("Weighted Average Oscillator")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45330-weighted-average-oscillator")]
	public class WAO : Indicator
	{
		#region Fields

		private readonly WMA _longWma = new();

		private readonly ValueDataSeries _renderSeries = new(Strings.Visualization);

		private readonly WMA _shortWma = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = "ShortPeriod", GroupName = "Settings", Order = 100)]
		public int ShortPeriod
		{
			get => _shortWma.Period;
			set
			{
				if (value <= 0)
					return;

				_shortWma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = "LongPeriod", GroupName = "Settings", Order = 100)]
		public int LongPeriod
		{
			get => _longWma.Period;
			set
			{
				if (value <= 0)
					return;

				_longWma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public WAO()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_shortWma.Period = 10;
			_longWma.Period = 30;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = _shortWma.Calculate(bar, value) - _longWma.Calculate(bar, value);
		}

		#endregion
	}
}