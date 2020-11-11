namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("ADX")]
	[LocalizedDescription(typeof(Resources), "ADX")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/8526-adx-di-di-")]
	public class ADX : Indicator
	{
		#region Fields

		private readonly DX _dx = new DX();
		private readonly WMA _sma = new WMA();

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
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

			((ValueDataSeries)DataSeries[0]).Color = Colors.Green;

			DataSeries.Add(_dx.DataSeries[1]);
			DataSeries.Add(_dx.DataSeries[2]);

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