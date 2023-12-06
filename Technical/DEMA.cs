namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Double Exponential Moving Average")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.DEMADescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602543")]
	public class DEMA : Indicator
	{
		#region Fields

		private readonly EMA _emaFirst = new()
		{
			Period = 10
		};
		private readonly EMA _emaSecond = new()
		{
			Period = 10
		};

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization) { UseMinimizedModeIfEnabled = true };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _emaFirst.Period;
			set
			{
				_emaFirst.Period = _emaSecond.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DEMA()
		{
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_emaFirst.Calculate(bar, value);
			_emaSecond.Calculate(bar, _emaFirst[bar]);
			_renderSeries[bar] = 2 * _emaFirst[bar] - _emaSecond[bar];
		}

		#endregion
	}
}