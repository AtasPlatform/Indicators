namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Positive/Negative Volume Index")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.VolumeIndexDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602304")]
	public class VolumeIndex : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Positive))]
			Positive,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Negative))]
			Negative
		}

		#endregion

		#region Fields
		
        private Mode _calcMode;
		private decimal _startPrice;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CalculationMode), GroupName = nameof(Strings.Settings), Description = nameof(Strings.CalculationModeDescription), Order = 100)]
		public Mode CalcMode
		{
			get => _calcMode;
			set
			{
				_calcMode = value;
				RecalculateValues();
			}
		}

        [Range(0, 100000000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomStartPrice), GroupName = nameof(Strings.Settings), Description = nameof(Strings.CustomStartPriceFilterDescription), Order = 210)]
        public Filter StartPriceFilter { get; set; }

		[Browsable(false)]
		[Obsolete]
		public bool PriceMod
		{
			get => !StartPriceFilter.Enabled;
			set => StartPriceFilter.Enabled = !value;
        }

        [Browsable(false)]
        [Obsolete]
        public decimal StartPrice
		{
            get => StartPriceFilter.Value;
            set => StartPriceFilter.Value = value;
        }

        #endregion

        #region ctor

        public VolumeIndex()
        {
			StartPriceFilter = new(true);
        }

        #endregion

        #region Protected methods

        protected override void OnInitialize()
        {
			StartPriceFilter.PropertyChanged += (_, _) =>
			{
				RecalculateValues();
				RedrawChart();
			};
        }

        protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				if (!StartPriceFilter.Enabled)
					_startPrice = ((decimal)SourceDataSeries[0] + (decimal)SourceDataSeries[CurrentBar - 1]) / 2;
				else
					_startPrice = StartPriceFilter.Value;

				this[bar] = _startPrice;
				return;
			}

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			if (candle.Volume < prevCandle.Volume && _calcMode == Mode.Negative || candle.Volume > prevCandle.Volume && _calcMode == Mode.Positive)
			{
				var prevValue = (decimal)SourceDataSeries[bar - 1];
				this[bar] = this[bar - 1] + (value - prevValue) * this[bar - 1] / prevValue;
				return;
			}

			if (candle.Volume >= prevCandle.Volume && _calcMode == Mode.Negative || candle.Volume <= prevCandle.Volume && _calcMode == Mode.Positive)
				this[bar] = this[bar - 1];
		}

		#endregion
	}
}