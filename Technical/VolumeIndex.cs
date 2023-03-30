namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Positive/Negative Volume Index")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45503-positivenegative-volume-index")]
	public class VolumeIndex : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Resources), Name = "Positive")]
			Positive,

			[Display(ResourceType = typeof(Resources), Name = "Negative")]
			Negative
		}

		#endregion

		#region Fields
		
		private bool _autoPrice = true;
        private Mode _calcMode;
		private decimal _customPrice;

		private decimal _startPrice;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "CalculationMode", GroupName = "Settings", Order = 100)]
		public Mode CalcMode
		{
			get => _calcMode;
			set
			{
				_calcMode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Auto", GroupName = "StartPrice", Order = 200)]
		public bool PriceMod
		{
			get => _autoPrice;
			set
			{
				_autoPrice = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "StartPrice", GroupName = "StartPrice", Order = 210)]
		[Range(0, 100000000)]
		public decimal StartPrice
		{
			get => _customPrice;
			set
			{
				_customPrice = value;
				RecalculateValues();
			}
		}

		#endregion
		
		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				if (_autoPrice)
					_startPrice = ((decimal)SourceDataSeries[0] + (decimal)SourceDataSeries[CurrentBar - 1]) / 2;
				else
					_startPrice = _customPrice;

				this[bar] = _startPrice;
				return;
			}

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			if (candle.Volume < prevCandle.Volume && _calcMode == Mode.Negative || candle.Volume > prevCandle.Volume && _calcMode == Mode.Positive)
			{
				var prevValue = (decimal)SourceDataSeries[bar - 1];
				this[bar] = this[bar - 1] + (value - prevValue) * this[bar - 1] / prevValue;
				return;
			}

			if (candle.Volume >= prevCandle.Volume && _calcMode == Mode.Negative || candle.Volume <= prevCandle.Volume && _calcMode == Mode.Positive)
				this[bar] = this[bar - 1];
		}

		#endregion
	}
}