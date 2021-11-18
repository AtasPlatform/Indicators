namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Chande Forecast Oscillator")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43356-chande-forecast-oscillator")]
	public class CFO : Indicator
	{
		#region Fields

		private readonly LinearReg _linReg = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _linReg.Period;
			set
			{
				if (value <= 0)
					return;

				_linReg.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public CFO()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_linReg.Period = 10;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var cfoValue = 0m;

			if (value != 0)
				cfoValue = 100m * (value - _linReg.Calculate(bar, value)) / value;

			_renderSeries[bar] = cfoValue;
		}

		#endregion
	}
}