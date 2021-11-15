namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Aroon Oscillator")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43407-aroon-oscillator")]
	public class AroonOscillator : Indicator
	{
		#region Fields

		private readonly AroonIndicator _ai = new();

		private readonly ValueDataSeries _renderSeries = new("Aroon");
		private int _period;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 110)]
		public int Period
		{
			get => _ai.Period;
			set
			{
				_ai.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public AroonOscillator()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_period = 10;
			_ai.Period = _period;
			Add(_ai);
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = ((ValueDataSeries)_ai.DataSeries[0])[bar] - ((ValueDataSeries)_ai.DataSeries[1])[bar];
		}

		#endregion
	}
}