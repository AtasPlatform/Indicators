namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Hull Moving Average")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/38046-hull-moving-average-hma")]
	public class HMA : Indicator
	{
		#region Fields

		private readonly WMA _wmaHull = new();
		private readonly WMA _wmaPrice = new();

		private readonly WMA _wmaPriceHalf = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period")]
		[Range(1, 10000)]
		public int Period
		{
			get => _wmaPrice.Period;
			set
			{
				_wmaPrice.Period = value;
				_wmaHull.Period = Convert.ToInt32(Math.Sqrt(value));
				_wmaPriceHalf.Period = value / 2;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public HMA()
			: base(true)
		{
			DenyToChangePanel = true;
			Period = 16;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var wmaPriceHalf = _wmaPriceHalf.Calculate(bar, candle.Close);
			var wmaPrice = _wmaPrice.Calculate(bar, candle.Close);

			var wmaHull = _wmaHull.Calculate(bar, 2.0m * wmaPriceHalf - wmaPrice);
			this[bar] = wmaHull;
		}

		#endregion
	}
}