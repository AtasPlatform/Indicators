namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("OBV")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/16992-obv")]
	public class OBV : Indicator
	{

		private readonly ValueDataSeries _volSignedSeries = new("Signed");

        #region ctor

        public OBV():base(true)
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

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "ShortValues", Order = 100)]
        [Range(1, 10000)]
        public Filter<int> MinimizedMode { get; set; } = new(true) { Value = 10, Enabled = false };
		
        #region Protected methods

        protected override void OnRecalculate()
        {
	        Clear();
        }
		
        protected override void OnCalculate(int bar, decimal value)
		{
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