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

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private bool _autoPrice;
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
		public decimal StartPrice
		{
			get => _customPrice;
			set
			{
				if (value <= 0)
					return;

				_customPrice = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public VolumeIndex()
		{
			_autoPrice = true;
			DataSeries[0] = _renderSeries;
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

				_renderSeries[bar] = _startPrice;
				return;
			}

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			if (candle.Volume < prevCandle.Volume && _calcMode == Mode.Negative || candle.Volume > prevCandle.Volume && _calcMode == Mode.Positive)
			{
				var prevValue = (decimal)SourceDataSeries[bar - 1];
				_renderSeries[bar] = _renderSeries[bar - 1] + (value - prevValue) * _renderSeries[bar - 1] / prevValue;
				return;
			}

			if (candle.Volume >= prevCandle.Volume && _calcMode == Mode.Negative || candle.Volume <= prevCandle.Volume && _calcMode == Mode.Positive)
				_renderSeries[bar] = _renderSeries[bar - 1];
		}

		#endregion
	}
}