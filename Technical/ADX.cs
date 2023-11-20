namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;

    using ATAS.Indicators.Drawing;

    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("ADX")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ADXDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602313")]
	public class ADX : Indicator
	{
		#region Fields

		private readonly DX _dx = new();
		private readonly WMA _sma = new();

		#endregion

		#region Properties

		[Parameter]
		[Range(1, 10000)]
		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Period),
			GroupName = nameof(Strings.Common),
            Description = nameof(Strings.PeriodDescription),
            Order = 20)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				_sma.Period = _dx.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ADX()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			((ValueDataSeries)DataSeries[0]).Color = DefaultColors.Green.Convert();

			var posDataSeries = (ValueDataSeries)_dx.DataSeries[1];
			posDataSeries.IgnoredByAlerts = true;

			var negDataSeries = (ValueDataSeries)_dx.DataSeries[2];
            negDataSeries.IgnoredByAlerts = true;

            DataSeries.Add(posDataSeries);
			DataSeries.Add(negDataSeries);

			Period = 10;

			Add(_dx);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			this[bar] = _sma.Calculate(bar, _dx[bar]);
		}

		#endregion
	}
}