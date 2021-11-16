namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;

	using OFT.Attributes;

	[DisplayName("OBV")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/16992-obv")]
	public class OBV : Indicator
	{
		#region ctor

		public OBV()
		{
			Panel = IndicatorDataProvider.NewPanel;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				this[bar] = 0;
			else
			{
				var currentClose = GetCandle(bar).Close;
				var previousClose = GetCandle(bar - 1).Close;
				var currentVolume = GetCandle(bar).Volume;

				if (currentClose > previousClose) // UP
					this[bar] = this[bar - 1] + currentVolume;
				else if (currentClose < previousClose) // DOWN
					this[bar] = this[bar - 1] - currentVolume;
				else
					this[bar] = this[bar - 1];
			}
		}

		#endregion
	}
}