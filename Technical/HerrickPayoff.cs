namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Herrick Payoff Index")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45245-herrick-payoff-index")]
	public class HerrickPayoff : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _hpiSec = new("HpiSecondary");
		private readonly ValueDataSeries _negSeries = new("Negative");

		private readonly ValueDataSeries _posSeries = new("Positive");
		private decimal _divisor;
		private int _smooth;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Divisor", GroupName = "Settings", Order = 110)]
		public decimal Divisor
		{
			get => _divisor;
			set
			{
				if (value <= 0m)
					return;

				_divisor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Smooth", GroupName = "Settings", Order = 120)]
		public int Smooth
		{
			get => _smooth;
			set
			{
				if (value <= 0)
					return;

				_smooth = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "BuyColor", GroupName = "Colors", Order = 200)]
		public Color PosColor
		{
			get => _posSeries.Color;
			set
			{
				_posSeries.Color = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SellColor", GroupName = "Colors", Order = 200)]
		public Color NegColor
		{
			get => _negSeries.Color;
			set
			{
				_negSeries.Color = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public HerrickPayoff()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_divisor = 1;
			_smooth = 10;

			_posSeries.Color = Colors.Blue;
			_negSeries.Color = Colors.Red;
			_posSeries.IsHidden = _negSeries.IsHidden = true;
			_posSeries.VisualType = _negSeries.VisualType = VisualMode.Histogram;

			DataSeries[0] = _posSeries;
			DataSeries.Add(_negSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var highLow = (candle.High + candle.Low) / 2m;
			var prevHighLow = (prevCandle.High + prevCandle.Low) / 2m;
			var oi = candle.OI;

			var prevOi = prevCandle.OI;
			var calcOI = oi > 0 ? oi : prevOi;

			var maxOi = Math.Max(calcOI, prevOi);

			if (maxOi == 0)
				return;

			_hpiSec[bar] = InstrumentInfo.TickSize * candle.Volume * (highLow - prevHighLow) / _divisor *
				((1 + 2 * Math.Abs(calcOI - prevOi)) / maxOi);

			var renderValue = 0m;
			var lastValue = _posSeries[bar - 1] == 0 ? _negSeries[bar - 1] : _posSeries[bar - 1];

			if (maxOi > 0)
				renderValue = lastValue + _smooth * (_hpiSec[bar] - _hpiSec[bar - 1]);
			else
				renderValue = lastValue;

			if (renderValue > 0)
				_posSeries[bar] = renderValue;
			else
				_negSeries[bar] = renderValue;
		}

		#endregion
	}
}