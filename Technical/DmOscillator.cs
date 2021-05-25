namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Directional Movement Oscillator")]
	[FeatureId("NotReady")]
	public class DmOscillator : Indicator
	{
		#region Fields

		private readonly DmIndex _dm = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 110)]
		public int Period
		{
			get => _dm.Period;
			set
			{
				if (value <= 0)
					return;

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
			_dm.Period = 14;
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