namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Block Moving Average")]
	public class BlockMA : Indicator
	{
		#region Fields

		private readonly ATR _atr = new ATR();

		private readonly ValueDataSeries _bot1 = new ValueDataSeries("bot1");
		private readonly ValueDataSeries _bot2 = new ValueDataSeries("bot2");
		private readonly ValueDataSeries _mid1 = new ValueDataSeries(Resources.FirstLine);
		private readonly ValueDataSeries _mid2 = new ValueDataSeries(Resources.SecondLine);

		private readonly ValueDataSeries _top1 = new ValueDataSeries("top1");
		private readonly ValueDataSeries _top2 = new ValueDataSeries("top2");
		private decimal _multiplier1;
		private decimal _multiplier2;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ATR", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _atr.Period;
			set
			{
				if (value <= 0)
					return;

				_atr.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Multiplier1", GroupName = "Settings", Order = 110)]
		public decimal Multiplier1
		{
			get => _multiplier1;
			set
			{
				if (value <= 0)
					return;

				_multiplier1 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Multiplier2", GroupName = "Settings", Order = 120)]
		public decimal Multiplier2
		{
			get => _multiplier2;
			set
			{
				if (value <= 0)
					return;

				_multiplier2 = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BlockMA()
			: base(true)
		{
			_atr.Period = 10;
			_multiplier1 = 1;
			_multiplier2 = 2;

			_mid1.Color = Colors.Red;
			_mid2.Color = Colors.Green;

			_mid1.ShowZeroValue = _mid2.ShowZeroValue = false;

			Add(_atr);
			DataSeries[0] = _mid1;
			DataSeries.Add(_mid2);
		}

		#endregion

		#region Protected methods

		protected override void OnRecalculate()
		{
			_top1.Clear();
			_top2.Clear();
			_mid1.Clear();
			_mid2.Clear();
			_bot1.Clear();
			_bot2.Clear();
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar < _atr.Period)
				return;

			var box1 = _multiplier1 * _atr[bar] / 2;
			var box2 = _multiplier2 * _atr[bar] / 2;

			var candle = GetCandle(bar);

			if (candle.High > _top1[bar - 1])
				_top1[bar] = candle.High;
			else if (candle.Low < _bot1[bar - 1] && candle.High <= _top1[bar - 1])
				_top1[bar] = _bot1[bar] + 2 * box1;
			else
				_top1[bar] = _top1[bar - 1];

			if (candle.High > _top2[bar - 1])
				_top2[bar] = candle.High;
			else if (candle.Low < _bot2[bar - 1] && candle.High <= _top2[bar - 1])
				_top2[bar] = _bot2[bar] + 2 * box2;
			else
				_top2[bar] = _top2[bar - 1];

			if (candle.High > _top1[bar - 1])
				_bot1[bar] = _top1[bar] - 2 * box1;
			else if (candle.Low < _bot1[bar - 1] && candle.High <= _top1[bar - 1])
				_bot1[bar] = candle.Low;
			else
				_bot1[bar] = _bot1[bar - 1];

			if (candle.High > _top2[bar - 1])
				_bot2[bar] = _top2[bar] - 2 * box2;
			else if (candle.Low < _bot2[bar - 1] && candle.High <= _top2[bar - 1])
				_bot2[bar] = candle.Low;
			else
				_bot2[bar] = _bot2[bar - 1];

			if (candle.High > _top1[bar - 1])
				_mid1[bar] = _top1[bar] - box1;
			else if (candle.Low < _bot1[bar - 1] && candle.High <= _top1[bar - 1])
				_mid1[bar] = _bot1[bar] + box1;
			else
				_mid1[bar] = _mid1[bar - 1];

			if (candle.High > _top2[bar - 1])
				_mid2[bar] = _top2[bar] - box2;
			else if (candle.Low < _bot2[bar - 1] && candle.High <= _top2[bar - 1])
				_mid2[bar] = _bot2[bar] + box2;
			else
				_mid2[bar] = _mid2[bar - 1];
		}

		#endregion
	}
}