namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Localization;

	[DisplayName("Hull Moving Average")]
	public class HMA : Indicator
	{
		#region Fields

		private readonly WMA _wmaHull = new();
		private readonly WMA _wmaPrice = new();

		private readonly WMA _wmaPriceHalf = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = "Period")]
		public int Period
		{
			get => _wmaPrice.Period;
			set
			{
				if (value <= 0)
					return;

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