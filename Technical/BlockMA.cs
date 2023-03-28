namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;
    using ATAS.Indicators.Drawing;
    using ATAS.Indicators.Technical.Properties;
    using OFT.Attributes;

	[DisplayName("Block Moving Average")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45435-moving-average-block")]
	public class BlockMA : Indicator
	{
		#region Fields

		private readonly ATR _atr = new();

		private readonly ValueDataSeries _bot1 = new("bot1");
		private readonly ValueDataSeries _bot2 = new("bot2");
		private readonly ValueDataSeries _mid1 = new(Resources.FirstLine);
		private readonly ValueDataSeries _mid2 = new(Resources.SecondLine);

		private readonly ValueDataSeries _top1 = new("top1");
		private readonly ValueDataSeries _top2 = new("top2");
		private decimal _multiplier1;
		private decimal _multiplier2;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ATR", GroupName = "Settings", Order = 100)]
		[Range(1,1000000)]
		public int Period
		{
			get => _atr.Period;
			set
			{
				_atr.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Multiplier1", GroupName = "Settings", Order = 110)]
		[Range(0.0001, 100000)]
		public decimal Multiplier1
		{
			get => _multiplier1;
			set
			{
				_multiplier1 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Multiplier2", GroupName = "Settings", Order = 120)]
		[Range(0.0001, 100000)]
		public decimal Multiplier2
		{
			get => _multiplier2;
			set
			{
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

			_mid1.Color = DefaultColors.Red.Convert();
			_mid2.Color = DefaultColors.Green.Convert();

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