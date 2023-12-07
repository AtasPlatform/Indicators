namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("McClellan Summation Index")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.MSIDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602427")]
	public class MSI : Indicator
	{
		#region Static and constants

		private const int _shortPeriod = 19;
		private const int _longPeriod = 39;

		#endregion

		#region Fields

		private readonly ValueDataSeries _longEma = new("EmaLong");
		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization) { UseMinimizedModeIfEnabled = true };
		private readonly ValueDataSeries _shortEma = new("EmaShort");

		#endregion

		#region ctor

		public MSI()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_renderSeries.Clear();
				_shortEma[bar] = value;
				_longEma[bar] = value;
				return;
			}

			_shortEma[bar] = 0;

			if (value != 0)
				_shortEma[bar] = 2 * value / (_shortPeriod + 1) + (1 - 2m / (_shortPeriod + 1)) * _shortEma[bar - 1];

			_longEma[bar] = 0;

			if (value != 0)
				_longEma[bar] = 2 * value / (_longPeriod + 1) + (1 - 2m / (_longPeriod + 1)) * _longEma[bar - 1];

			if (value != 0)
				_renderSeries[bar] = _renderSeries[bar - 1] + (_shortEma[bar] - _longEma[bar]);
			else
				_renderSeries[bar] = _renderSeries[bar - 1];
		}

		#endregion
	}
}