namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("SMA")]
	[LocalizedDescription(typeof(Resources), "SMA")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/9197-sma")]
	public class SMA : Indicator
	{
		#region Fields

		private int _lastBar = -1;
		private int _period;
		private decimal _sum;

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
				if (value <= 0)
					return;

				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public SMA()
		{
			Period = 10;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_sum = 0;
				this[bar] = value;
				return;
			}

			if (bar != _lastBar)
			{
				_lastBar = bar;
				_sum += (decimal)SourceDataSeries[bar - 1];
				if (bar >= Period)
					_sum -= (decimal)SourceDataSeries[bar - Period];
			}

			var sum = _sum + value;
			this[bar] = sum / Math.Min(Period, bar + 1);
		}

		#endregion
	}
}