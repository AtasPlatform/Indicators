namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Aroon Oscillator")]
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
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = value;
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