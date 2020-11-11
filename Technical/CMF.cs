namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Chaikin Money Flow")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/21701-chaikin-money-flow")]
	public class CMF : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _adSeries = new ValueDataSeries("ADSeries");
		private readonly ValueDataSeries _cmf = new ValueDataSeries("CMFline");
		private readonly RangeDataSeries _cmfHigh = new RangeDataSeries("CMFHigh");
		private readonly RangeDataSeries _cmfLow = new RangeDataSeries("CMFLow");
		private decimal _ad;
		private decimal _dailyHigh;
		private decimal _dailyLow;
		private DateTime _lastSessionTime;

		private int _period = 21;

		#endregion

		#region Properties
		[Display(ResourceType = typeof(Resources), Name = "Period")]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor
		
		public CMF()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0].IsHidden = true;
			_cmfHigh.RangeColor = Colors.Green;
			_cmfLow.RangeColor = Colors.Red;
			_cmf.Color = Colors.Gray;
			DataSeries.Add(_cmfHigh);
			DataSeries.Add(_cmfLow);
			DataSeries.Add(_cmf);
		}

		#endregion

		#region Protected methods

		#region Overrides of Indicator

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_dailyHigh = _dailyLow = 0;
				return;
			}

			var candle = GetCandle(bar);
			if (IsNewSession(bar))
			{
				if (_lastSessionTime != candle.Time)
				{
					_lastSessionTime = candle.Time;
					_dailyHigh = _dailyLow = 0;
				}
			}

			if (candle.High > _dailyHigh || _dailyHigh == 0)
				_dailyHigh = candle.High;
			if (candle.Low < _dailyLow || _dailyLow == 0)
				_dailyLow = candle.Low;

			if (_dailyHigh != _dailyLow)
				_ad = (candle.Close - _dailyLow - (_dailyHigh - candle.Close)) / (_dailyHigh - _dailyLow) * candle.Volume;
			else
				_ad = _adSeries[bar - 1];

			_adSeries[bar] = _ad;

			if (bar > _period)
			{
				decimal adSum = 0;
				decimal volumeSum = 0;
				for (var i = 0; i <= _period; i++)
				{
					adSum += _adSeries[bar - i];
					volumeSum += GetCandle(bar - i).Volume;
				}

				var result = adSum / volumeSum;
				_cmf[bar] = result;
				if (result >= 0)
				{
					_cmfHigh[bar].Upper = result;
					_cmfHigh[bar].Lower = 0;
				}
				else
				{
					_cmfLow[bar].Upper = 0;
					_cmfLow[bar].Lower = result;
				}
			}
		}

		#endregion

		#endregion
	}
}