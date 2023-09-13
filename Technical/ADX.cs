namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;
    using Utils.Common.Localization;

	[DisplayName("ADX")]
	[LocalizedDescription(typeof(Strings), "ADX")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/8526-adx-di-di-")]
	public class ADX : Indicator
	{
		#region Fields

		private readonly DX _dx = new();
		private readonly WMA _sma = new();

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

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