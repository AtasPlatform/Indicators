namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Full Contract Value")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.FCVDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602389")]
	public class FCV : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private bool _customScale;
		private decimal _multiplier;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Multiplier), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MultiplierDescription), Order = 100)]
		[Range(0.000000001, 1000000000)]
		public Filter CustomTickFilter { get; set; } = new Filter() { Value = 1 };

        [Browsable(false)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled), GroupName = nameof(Strings.Multiplier), Order = 100)]
		public bool CustomScale
		{
			get => _customScale;
			set => _customScale = value;
        }

        [Browsable(false)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Value), GroupName = nameof(Strings.Multiplier), Order = 110)]
		[Range(0.000000001, 1000000000)]
		public decimal Multiplier
		{
			get => _multiplier;
			set => _multiplier = value;
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

        protected override void OnInitialize()
        {
			CustomTickFilter.PropertyChanged += (o, _) =>
			{
                _customScale = ((Filter)o).Enabled;
                _multiplier = ((Filter)o).Value;

                RecalculateValues();
				RedrawChart();
            };
        }

        protected override void OnCalculate(int bar, decimal value)
		{
            if (bar == 0 && !_customScale)
                _multiplier = InstrumentInfo.TickSize;

			_renderSeries[bar] = value * Math.Max(InstrumentInfo.TickSize, _multiplier) / InstrumentInfo.TickSize;
        }

        #endregion
    }
}