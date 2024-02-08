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
        private decimal _multiplier;

		#endregion

		#region Properties

		[ReadOnly(true)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TickSize), GroupName = nameof(Strings.Settings), Description = nameof(Strings.TickSizeDescription), Order = 100)]
        public decimal CurrentTickSize { get; set; }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomTickSize), GroupName = nameof(Strings.Settings), Description = nameof(Strings.CustomTickSizeDescription), Order = 110)]
		[Range(0.000000001, 1000000000)]
		public Filter CustomTickFilter { get; set; } = new Filter() { Value = 1 };

        [Browsable(false)]
		public bool CustomScale
		{
			get => CustomTickFilter.Enabled;
			set => CustomTickFilter.Enabled = value;
        }

        [Browsable(false)]
		[Range(0.000000001, 1000000000)]
		public decimal Multiplier
		{
			get => CustomTickFilter.Value;
			set => CustomTickFilter.Value = value;
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
				_multiplier = ((Filter)o).Value;

                RecalculateValues();
				RedrawChart();
            };

			CurrentTickSize = InstrumentInfo.TickSize;
			_multiplier = CustomTickFilter.Value;
        }

        protected override void OnCalculate(int bar, decimal value)
		{
            if (bar == 0 && !CustomTickFilter.Enabled)
                _multiplier = InstrumentInfo.TickSize;

			_renderSeries[bar] = value * Math.Max(InstrumentInfo.TickSize, _multiplier) / InstrumentInfo.TickSize;
        }

        #endregion
    }
}