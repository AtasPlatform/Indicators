namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("ATR Normalized")]
	public class ATRN : Indicator
	{
		#region Fields

		private readonly ATR _atr = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _atr.Period;
			set
			{
				if (value <= 0)
					return;

				_atr.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ATRN()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			_atr.Period = 10;
			Add(_atr);
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = 100m * ((ValueDataSeries)_atr.DataSeries[0])[bar] / GetCandle(bar).Close;
		}

		#endregion
	}
}