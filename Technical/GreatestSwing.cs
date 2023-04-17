namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Greatest Swing Value")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45198-greatest-swing-value")]
	public class GreatestSwing : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _buy = new("BuySwing");
		private readonly ValueDataSeries _sell = new("SellSwing");

        private readonly ValueDataSeries _buySeries = new(Resources.Buys) { Color = DefaultColors.Green.Convert() };
		private readonly ValueDataSeries _sellSeries = new(Resources.Sells);
		private decimal _multiplier = 5;
        private int _period = 10;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 110)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Multiplier", GroupName = "Settings", Order = 110)]
		[Range(0.0000001, 10000000)]
        public decimal Multiplier
		{
			get => _multiplier;
			set
			{
				_multiplier = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public GreatestSwing()
			: base(true)
		{
			DenyToChangePanel = true;
			
			DataSeries[0] = _buySeries;
			DataSeries.Add(_sellSeries);
		}

		#endregion

		#region Protected methods
		
		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_buy.Clear();
				_sell.Clear();
				return;
			}

			var candle = GetCandle(bar);

			if (candle.Close < candle.Open)
				_buy[bar] = candle.High - candle.Open;

			if (candle.Close > candle.Open)
				_sell[bar] = candle.Open - candle.Low;

			var buyMa = SkipZeroMa(bar - 1, _buy);
			var sellMa = SkipZeroMa(bar - 1, _sell);

			_buySeries[bar] = candle.Open + _multiplier * buyMa;
			_sellSeries[bar] = candle.Open - _multiplier * sellMa;
		}

		#endregion

		#region Private methods

		private decimal SkipZeroMa(int bar, ValueDataSeries series)
		{
			var nonZeroValues = 0;
			var sum = 0m;

			for (var i = Math.Max(0, bar - _period); i <= bar; i++)
			{
				if (series[i] == 0)
					continue;

				nonZeroValues++;
				sum += series[i];
			}

			if (nonZeroValues == 0)
				return 0;

			return sum / nonZeroValues;
		}

		#endregion
	}
}