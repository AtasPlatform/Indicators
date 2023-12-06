namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("OBV")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.OBVDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602436")]
	public class OBV : Indicator
	{
		private readonly ValueDataSeries _volSignedSeries = new("Signed");

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.ShortValues), Description = nameof(Strings.UsePeriodDescription), Order = 100)]
        [Range(1, 10000)]
        public FilterInt MinimizedMode { get; set; } = new(true) { Value = 10, Enabled = false };

        #region ctor

        public OBV() : base(true)
        {
            Panel = IndicatorDataProvider.NewPanel;

            DataSeries[0].UseMinimizedModeIfEnabled = true;

            MinimizedMode.PropertyChanged += FilterChanged;
        }

        private void FilterChanged(object sender, PropertyChangedEventArgs e)
        {
            RecalculateValues();
        }

        #endregion

        #region Protected methods

        protected override void OnInitialize()
        {
			MinimizedMode.PropertyChanged += (_, _) =>
			{
				RecalculateValues();
				RedrawChart();
			};
        }

        protected override void OnRecalculate()
        {
	        Clear();
        }
		
        protected override void OnCalculate(int bar, decimal value)
        {
	        if (bar is 0)
		        return;

			var currentClose = GetCandle(bar).Close;
			var previousClose = GetCandle(bar - 1).Close;
			var currentVolume = GetCandle(bar).Volume;

            if (MinimizedMode.Enabled)
			{
				_volSignedSeries[bar] = currentClose > previousClose
					? currentVolume
					: currentClose < previousClose
						? -currentVolume
						: 0;
			}

			if (bar == 0)
			{
				this[bar] = 0;
				return;
			}

			if (MinimizedMode.Enabled)
			{
				this[bar] = bar < MinimizedMode.Value
					? this[bar - 1] + _volSignedSeries[bar]
					: this[bar - 1] + _volSignedSeries[bar] - _volSignedSeries[bar - MinimizedMode.Value];
			}
			else
			{
				if (currentClose > previousClose) // UP
					this[bar] = this[bar - 1] + currentVolume;
				else if (currentClose < previousClose) // DOWN
					this[bar] = this[bar - 1] - currentVolume;
				else
					this[bar] = this[bar - 1];
			}
		}

        #endregion
	}
}