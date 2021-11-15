namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Detrended Oscillator - DiNapoli")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45487-detrended-oscillator-dinapoli")]
	public class DeTrendedDi : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private readonly SMA _sma = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DeTrendedDi()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_sma.Period = 10;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = value - _sma.Calculate(bar, value);
		}

		#endregion
	}
}