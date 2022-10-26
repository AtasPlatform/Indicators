namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Herrick Payoff Index")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45245-herrick-payoff-index")]
	public class HerrickPayoff : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _hpiSec = new("HpiSecondary");
		private readonly ValueDataSeries _negSeries = new("Negative")
		{
			VisualType = VisualMode.Histogram,
			IsHidden = true,
			UseMinimizedModeIfEnabled = true
		};

        private readonly ValueDataSeries _posSeries = new("Positive")
		{
			Color = Colors.Blue,
			VisualType = VisualMode.Histogram,
			IsHidden = true,
			UseMinimizedModeIfEnabled = true
		};

		private decimal _divisor = 1;
        private int _smooth = 10;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Divisor", GroupName = "Settings", Order = 110)]
		[Range(0.00000001, 100000000)]
		public decimal Divisor
		{
			get => _divisor;
			set
			{
				_divisor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Smooth", GroupName = "Settings", Order = 120)]
		[Range(1, 10000)]
		public int Smooth
		{
			get => _smooth;
			set
			{
				_smooth = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "BuyColor", GroupName = "Colors", Order = 200)]
		public Color PosColor
		{
			get => _posSeries.Color;
			set => _posSeries.Color = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "SellColor", GroupName = "Colors", Order = 200)]
		public Color NegColor
		{
			get => _negSeries.Color;
			set => _negSeries.Color = value;
		}

		#endregion

		#region ctor

		public HerrickPayoff()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _posSeries;
			DataSeries.Add(_negSeries);
		}

		#endregion

		#region Protected methods
		
		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				return;
			}

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

			var lastValue = _posSeries[bar - 1] == 0 ? _negSeries[bar - 1] : _posSeries[bar - 1];
            
			var renderValue = maxOi > 0
	            ? lastValue + _smooth * (_hpiSec[bar] - _hpiSec[bar - 1])
	            : lastValue;
			
			if (renderValue > 0)
				_posSeries[bar] = renderValue;
			else
				_negSeries[bar] = renderValue;
		}

		#endregion
	}
}