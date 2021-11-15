namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("ADXR")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43355-adxr")]
	public class ADXR : Indicator
	{
		#region Fields

		private readonly ADX _adx = new();

		private readonly ValueDataSeries _renderSeries = new("ADXR");
		private int _period;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
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

		[Display(ResourceType = typeof(Resources), Name = "AdxPeriod", GroupName = "Settings", Order = 110)]
		public int AdxPeriod
		{
			get => _adx.Period;
			set
			{
				if (value <= 0)
					return;

				_adx.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ADXR()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			_adx.Period = 14;
			_period = 2;
			_renderSeries.ShowZeroValue = true;
			Add(_adx);
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar < _period)
				return;

			_renderSeries[bar] = Math.Max(0.01m, (_adx[bar] + _adx[bar - _period]) / 2m);
			RaiseBarValueChanged(bar);
		}

		#endregion
	}
}