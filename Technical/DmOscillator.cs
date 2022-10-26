namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Directional Movement Oscillator")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45189-directional-movement-oscillator")]
	public class DmOscillator : Indicator
	{
		#region Fields

		private readonly DmIndex _dm = new() { Period = 14 };

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 110)]
		[Range(1, 10000)]
		public int Period
		{
			get => _dm.Period;
			set
			{
				_dm.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DmOscillator()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			Add(_dm);
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = ((ValueDataSeries)_dm.DataSeries[0])[bar] - ((ValueDataSeries)_dm.DataSeries[1])[bar];
		}

		#endregion
	}
}