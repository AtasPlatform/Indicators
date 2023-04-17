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
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/8526-adx-di-di-")]
	public class DX : Indicator
	{
		#region Fields

		private readonly DINeg _diNeg = new() { Period = 10 };
		private readonly DIPos _diPos = new() { Period = 10 };

        #endregion

        #region Properties

        [Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		[Range(1, 10000)]
		public int Period
		{
			get => _diPos.Period;
			set
			{
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
			DataSeries[0].IgnoredByAlerts = DataSeries[1].IgnoredByAlerts = true;

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