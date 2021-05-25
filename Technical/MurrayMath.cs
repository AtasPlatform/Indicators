namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	[DisplayName("Murray Math")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/38049-murray-math")]
	public class MurrayMath : Indicator
	{
		#region Nested types

		public enum FrameMultiplierEnum
		{
			[Display(Name = "1.0")]
			Min = 10,

			[Display(Name = "1.5")]
			Mid = 15,

			[Display(Name = "2.0")]
			Max = 20
		}

		public enum FrameSizeEnum
		{
			[Display(Name = "4")]
			Pow2 = 4,

			[Display(Name = "8")]
			Pow3 = 8,

			[Display(Name = "16")]
			Pow4 = 16,

			[Display(Name = "32")]
			Pow5 = 32,

			[Display(Name = "64")]
			Pow6 = 64,

			[Display(Name = "128")]
			Pow7 = 128,

			[Display(Name = "256")]
			Pow8 = 256,

			[Display(Name = "512")]
			Pow9 = 512
		}

		#endregion

		#region Fields

		private readonly Highest _high = new();

		private readonly double _log10 = Math.Log(10);
		private readonly double _log2 = Math.Log(2);
		private readonly double _log8 = Math.Log(8);
		private readonly Lowest _low = new();
		private int _days;

		private decimal _frameMultiplier;
		private int _frameSize;
		private bool _ignoreWicks;
		private int _lookback;
		private int _targetBar;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), GroupName = "Common", Name = "Days", Order = 90)]
		public int Days
		{
			get => _days;
			set
			{
				if (value < 0)
					return;

				_days = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Common", Name = "IgnoreWicks", Order = 100)]
		public bool IgnoreWicks
		{
			get => _ignoreWicks;
			set
			{
				_ignoreWicks = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Common", Name = "FrameSize", Order = 110)]
		public FrameSizeEnum FrameSize
		{
			get => (FrameSizeEnum)_frameSize;
			set
			{
				_frameSize = (int)value;
				_lookback = (int)(_frameSize * _frameMultiplier);
				_high.Period = _lookback;
				_low.Period = _lookback;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Common", Name = "FrameMultiplier", Order = 200)]
		public FrameMultiplierEnum FrameMultiplier
		{
			get => (FrameMultiplierEnum)(int)(_frameMultiplier * 10);
			set
			{
				_frameMultiplier = Convert.ToDecimal((int)value / 10.0);
				_lookback = (int)(_frameSize * _frameMultiplier);
				_high.Period = _lookback;
				_low.Period = _lookback;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MurrayMath()
			: base(true)
		{
			DenyToChangePanel = true;
			_days = 20;
			_frameSize = 64;
			_frameMultiplier = 1.5m;
			_ignoreWicks = true;

			DataSeries.Clear();

			for (var i = -3; i <= 8 + 3; i++)
			{
				var name = i <= 8
					? $"Level {i}/8"
					: $"Level +{i % 4}/8";

				DataSeries.Add(new ValueDataSeries(name)
				{
					ShowZeroValue = false,
					LineDashStyle = LineDashStyle.Solid,
					VisualType = VisualMode.Line,
					Width = i % 4 == 0 ? 2 : 1,

					Color = i % 4 == 0 ? Colors.Blue :
						i % 4 == 1 || i % 4 == 3 ? Colors.Green :
						i % 4 == 2 ? Colors.Red :
						Colors.Gray
				});
			}
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				_targetBar = 0;

				if (_days > 0)
				{
					var days = 0;

					for (var i = CurrentBar - 1; i >= 0; i--)
					{
						_targetBar = i;

						if (!IsNewSession(i))
							continue;

						days++;

						if (days == _days)
							break;
					}

					if (_targetBar > 0)
						DataSeries.ForEach(x => ((ValueDataSeries)x).SetPointOfEndLine(_targetBar - 1));
				}
			}

			if (bar < _targetBar)
				return;

			var currentCandle = GetCandle(bar);

			var hValue = IgnoreWicks
				? Math.Max(currentCandle.Open, currentCandle.Close)
				: currentCandle.High;

			var lValue = IgnoreWicks
				? Math.Min(currentCandle.Open, currentCandle.Close)
				: currentCandle.Low;

			var lowPeriod = _low.Calculate(bar, lValue);
			var highPeriod = _high.Calculate(bar, hValue);

			var difference = highPeriod - lowPeriod;

			var tmpHigh = Convert.ToDouble(lowPeriod < 0 ? 0 - lowPeriod : highPeriod);
			var tmpLow = Convert.ToDouble(lowPeriod < 0 ? 0 - lowPeriod - difference : lowPeriod);

			var shift = lowPeriod < 0;

			var sfVar = Math.Log(0.4 * tmpHigh) / _log10
				- Math.Floor(Math.Log(0.4 * tmpHigh) / _log10);

			double SR;

			if (tmpHigh > 25)
			{
				SR = sfVar > 0
					? Math.Exp(_log10 * Math.Floor(Math.Log(0.4 * tmpHigh) / _log10) + 1.0)
					: Math.Exp(_log10 * Math.Floor(Math.Log(0.4 * tmpHigh) / _log10));
			}
			else
				SR = 100.0 * Math.Exp(_log8 * Math.Floor(Math.Log(0.005 * tmpHigh) / _log8));

			var highDiff = tmpHigh - tmpLow;
			var nVar1 = Math.Log(SR / (highDiff == 0 ? 1 : highDiff)) / _log8;
			var nVar2 = nVar1 - Math.Floor(nVar1);

			var N = nVar1 <= 0 ? 0 :
				nVar2 == 0 ? Math.Floor(nVar1) : Math.Floor(nVar1) + 1;

			var SI = SR * Math.Exp(-N * _log8);
			var M = Math.Floor(1.0 / _log2 * Math.Log((tmpHigh - tmpLow) / SI + 0.0000001));

			var I = Math.Round((tmpHigh + tmpLow) * 0.5 / (SI * Math.Exp((M - 1.0) * _log2)));
			var bot = (I - 1.0) * SI * Math.Exp((M - 1.0) * _log2);
			var top = (I + 1.0) * SI * Math.Exp((M - 1.0) * _log2);

			var doShift =
				tmpHigh - top > 0.25 * (top - bot)
				||
				bot - tmpLow > 0.25 * (top - bot);

			var ER = doShift ? 1 : 0;

			var MM = ER == 0 ? M : ER == 1 && M < 2 ? M + 1.0 : 0;
			var NN = ER == 0 ? N : ER == 1 && M < 2 ? N : N - 1.0;

			var finalSI = ER == 1 ? SR * Math.Exp(-NN * _log8) : SI;
			var finalI = ER == 1 ? Math.Round((tmpHigh + tmpLow) * 0.5 / (finalSI * Math.Exp((MM - 1.0) * _log2))) : I;
			var finalBot = ER == 1 ? (finalI - 1.0) * finalSI * Math.Exp((MM - 1.0) * _log2) : bot;
			var finalTop = ER == 1 ? (finalI + 1.0) * finalSI * Math.Exp((MM - 1.0) * _log2) : top;

			var increment = (finalTop - finalBot) / 8.0;

			var absTop = shift
				? -(finalBot - 3.0 * increment)
				: finalTop + 3.0 * increment;

			var lineCount = 0;

			for (var i = DataSeries.Count - 1; i >= 0; i--)
			{
				DataSeries[i][bar] = Convert.ToDecimal(absTop - lineCount * increment);
				lineCount++;
			}
		}

		#endregion
	}
}