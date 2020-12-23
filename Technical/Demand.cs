namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Demand Index")]
	public class Demand : Indicator
	{
		#region Fields

		private readonly EMA _emaBp = new EMA();
		private readonly EMA _emaRange = new EMA();
		private readonly EMA _emaSp = new EMA();
		private readonly EMA _emaVolume = new EMA();
		private readonly Highest _maxHigh = new Highest();
		private readonly Lowest _minHigh = new Lowest();
		private readonly ValueDataSeries _priceSumSeries = new ValueDataSeries("PriceSum");

		private readonly ValueDataSeries _renderSeries = new ValueDataSeries(Resources.Visualization);
		private readonly SMA _sma = new SMA();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "BuySellPower", GroupName = "Period", Order = 100)]
		public int BuySellPower
		{
			get => _emaRange.Period;
			set
			{
				if (value <= 0)
					return;

				_emaRange.Period = _emaVolume.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "BuySellPower", GroupName = "Smooth", Order = 200)]
		public int BuySellSmooth
		{
			get => _emaBp.Period;
			set
			{
				if (value <= 0)
					return;

				_emaBp.Period = _emaSp.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Indicator", GroupName = "Smooth", Order = 210)]
		public int IndicatorSmooth
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Demand()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_emaRange.Period = _emaVolume.Period = 10;
			_emaBp.Period = _emaSp.Period = 10;
			_sma.Period = 10;
			_maxHigh.Period = _minHigh.Period = 2;
			LineSeries.Add(new LineSeries(Resources.ZeroValue) { Color = Colors.Gray, Value = 0 });

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_priceSumSeries[bar] = candle.High + candle.Low + 2 * candle.Close;
			_emaVolume.Calculate(bar, candle.Volume);

			if (bar == 0)
			{
				_sma.Calculate(bar, 0);
				return;
			}

			var firstCandle = GetCandle(0);

			var bp = 0m;

			if (_emaVolume[bar] != 0 && firstCandle.High != firstCandle.Low && _priceSumSeries[bar] != 0)
			{
				if (_priceSumSeries[bar] < _priceSumSeries[bar - 1])
				{
					bp = candle.Volume / _emaVolume[bar] /
						(decimal)Math.Exp(0.375 * (double)(
							(_priceSumSeries[bar] + _priceSumSeries[bar - 1]) / (firstCandle.High - firstCandle.Low) *
							(_priceSumSeries[bar - 1] - _priceSumSeries[bar]) / _priceSumSeries[bar]
						));
				}
				else
					bp = candle.Volume / _emaVolume[bar];
			}
			else
				bp = candle.Volume / _emaVolume[bar - 1];

			var sp = 0m;

			if (_emaVolume[bar] != 0 && firstCandle.High != firstCandle.Low && _priceSumSeries[bar - 1] != 0)
			{
				if (_priceSumSeries[bar] <= _priceSumSeries[bar - 1])
					sp = candle.Volume / _emaVolume[bar];
				else
				{
					sp = candle.Volume / _emaVolume[bar] /
						(decimal)Math.Exp(0.375 * (double)(
							(_priceSumSeries[bar] + _priceSumSeries[bar - 1]) / (firstCandle.High - firstCandle.Low) *
							(_priceSumSeries[bar] - _priceSumSeries[bar - 1]) / _priceSumSeries[bar - 1]
						));
				}
			}
			else
				sp = candle.Volume / _emaVolume[bar - 1];

			_emaBp.Calculate(bar, bp);
			_emaSp.Calculate(bar, sp);

			var q = 0m;

			if (_emaBp[bar] > _emaSp[bar])
				q = _emaBp[bar] == 0 ? 0 : _emaSp[bar] / _emaBp[bar];
			else if (_emaBp[bar] < _emaSp[bar])
				q = _emaSp[bar] == 0 ? 0 : _emaBp[bar] / _emaSp[bar];
			else
				q = 1;

			var di = 0m;

			if (_emaSp[bar] <= _emaBp[bar])
				di = 100 * (1 - q);
			else
				di = 100 * (q - 1);

			_renderSeries[bar] = _sma.Calculate(bar, di);
		}

		#endregion
	}
}