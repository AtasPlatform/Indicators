namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("DX")]
	[LocalizedDescription(typeof(Resources), "DX")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/8526-adx-di-di-")]
	public class DX : Indicator
	{
		#region Fields

		private readonly DINeg _diNeg = new DINeg();
		private readonly DIPos _diPos = new DIPos();

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		public int Period
		{
			get => _diPos.Period;
			set
			{
				if (value <= 0)
					return;

				_diPos.Period = _diNeg.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DX()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			DataSeries.Add(_diPos.DataSeries[0]);
			DataSeries.Add(_diNeg.DataSeries[0]);

			Period = 10;

			Add(_diNeg);

			Add(_diPos);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var pos = _diPos[bar];
			var neg = _diNeg[bar];

			var sum = pos + neg;
			var diff = Math.Abs(pos - neg);

			this[bar] = sum != 0m ? 100 * diff / sum : 0m;
		}

		#endregion
	}
}