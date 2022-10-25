namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("DeTrended Oscillator")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43368-detrended-oscillator")]
	public class DeTrended : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private readonly SMA _sma = new();
		private int _lookBack;
		private int _period = 10;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DeTrended()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_renderSeries.Clear();
				_sma.Period = _period / 2;
				_lookBack = _sma.Period / 2 + 1;
			}

			_sma.Calculate(bar, value);

			if (bar < _lookBack)
				return;

			_renderSeries[bar] = value - _sma[bar - _lookBack];
		}

		#endregion
	}
}