namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("KeltnerChannel")]
	[Description(
		"The Keltner Channel is a similar indicator to Bollinger Bands. Here the midline is a standard moving average with the upper and lower bands offset by the SMA of the difference between the high and low of the previous bars. The offset multiplier as well as the SMA period is configurable.")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/6712-keltner-channel")]
	public class KeltnerChannel : Indicator
	{
		#region Fields

		private readonly ATR _atr = new ATR();
		private readonly RangeDataSeries _keltner = new RangeDataSeries("BackGround");
		private readonly SMA _sma = new SMA();

		private decimal _koef;

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

				_sma.Period = _atr.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "OffsetMultiplier",
			GroupName = "Common",
			Order = 20)]
		[Parameter]
		public decimal Koef
		{
			get => _koef;
			set
			{
				if (value <= 0)
					return;

				_koef = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public KeltnerChannel()
			: base(true)
		{
			DataSeries.Add(new ValueDataSeries("Upper")
			{
				VisualType = VisualMode.Line
			});
			DataSeries.Add(new ValueDataSeries("Lower")
			{
				VisualType = VisualMode.Line
			});
			DataSeries.Add(_keltner);
			Period = 34;
			Koef = 4;

			Add(_atr);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var currentCandle = GetCandle(bar);
			var ema = _sma.Calculate(bar, currentCandle.Close);
			var atr = _atr[bar];
			this[bar] = ema;
			DataSeries[1][bar] = ema + atr * Koef;
			DataSeries[2][bar] = ema - atr * Koef;
			_keltner[bar].Upper = ema + atr * Koef;
			_keltner[bar].Lower = ema - atr * Koef;
		}

		#endregion
	}
}