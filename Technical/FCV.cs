namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Full Contract Value")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45324-full-contract-value")]
	public class FCV : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private bool _customScale;
		private decimal _multiplier;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Enabled", GroupName = "Multiplier", Order = 100)]
		public bool CustomScale
		{
			get => _customScale;
			set
			{
				_customScale = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Value", GroupName = "Multiplier", Order = 110)]
		[Range(0.000000001, 1000000000)]
		public decimal Multiplier
		{
			get => _multiplier;
			set
			{
				_multiplier = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public FCV()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0 && !_customScale)
				_multiplier = InstrumentInfo.TickSize;

			_renderSeries[bar] = value * Math.Max(InstrumentInfo.TickSize, _multiplier) / InstrumentInfo.TickSize;
		}

		#endregion
	}
}