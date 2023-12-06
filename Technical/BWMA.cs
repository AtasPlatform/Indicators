namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;

    using ATAS.Indicators.Drawing;

    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Bill Williams Moving Average")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.BWMADescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602334")]
	public class BWMA : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private int _period = 10;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BWMA()
		{
			_renderSeries.Color = DefaultColors.Blue.Convert();
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_renderSeries.Clear();
				_renderSeries[bar] = value;
				return;
			}

			_renderSeries[bar] = (1m - 1m / _period) * _renderSeries[bar - 1] + value / _period;
		}

		#endregion
	}
}