namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using Utils.Common.Attributes;
	using Utils.Common.Localization;

	[DisplayName("EMA")]
	[LocalizedDescription(typeof(Resources), "EMA")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/384-ema")]
	public class EMA : Indicator
	{
		#region Fields

		private int _period;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		public int Period
		{
			get => _period;
			set
			{
				if (_period == value)
					return;

				if (value <= 0)
					return;

				_period = value;

				RaisePropertyChanged("Period");
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public EMA()
		{
			Period = 10;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				this[bar] = value;
			else
				this[bar] = value * (2.0m / (1 + Period)) + (1 - 2.0m / (1 + Period)) * this[bar - 1];
		}

		#endregion
	}
}