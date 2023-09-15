namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Synthetic VIX")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45338-synthetic-vix")]
	public class SyntheticVix : Indicator
	{
		#region Fields

		private readonly Highest _highest = new() { Period = 10 };

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _highest.Period;
			set
			{
				_highest.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public SyntheticVix()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_highest.Calculate(bar, candle.Close);
			var maxClose = _highest.DataSeries[0].MAX(Period, bar);
			_renderSeries[bar] = 100 * (maxClose - candle.Low) / maxClose;
		}

		#endregion
	}
}