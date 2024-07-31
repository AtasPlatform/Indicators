namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;
    using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Settings;

	[DisplayName("Chaikin Oscillator")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ChaikinOscillatorDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602273")]
	public class ChaikinOscillator : Indicator
	{
		#region Fields

		private readonly EMA _emaLong = new();
		private readonly EMA _emaShort = new();

		private LineSeries _overbought;
		private LineSeries _oversold;
		private int _divisor;
		private decimal _exAd;
		private decimal _lastAd;
		private int _lastBar;
		private bool _drawLines = true;

        #endregion

        #region Properties

        [Parameter]
		[Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.LongPeriod), Description = nameof(Strings.LongPeriodDescription), Order = 1)]
		public int LongAvg
		{
			get => _emaLong.Period;
			set
			{
				_emaLong.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.ShortPeriod), Description = nameof(Strings.ShortPeriodDescription), Order = 2)]
		public int ShortAvg
		{
			get => _emaShort.Period;
			set
			{
				_emaShort.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.Divisor), Description = nameof(Strings.DivisorDescription), Order = 3)]
		public int Divisor
		{
			get => _divisor;
			set
			{
				_divisor = value;
				RecalculateValues();
			}
		}

        [Parameter]
		[Browsable(false)]
		public decimal Overbought
		{
			get => _overbought.Value;
			set => _overbought.Value = value;
        }

        [Parameter]
        [Browsable(false)]
		public decimal Oversold
		{
			get => _oversold.Value;
			set => _oversold.Value = value;
        }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show), GroupName = nameof(Strings.Line), Description = nameof(Strings.DrawLinesDescription), Order = 300)]
		public bool DrawLines
		{
			get => _drawLines;
			set
			{
				_drawLines = value;

				if (value)
				{
					if (LineSeries.Contains(_overbought))
						return;

					LineSeries.Add(_overbought);
					LineSeries.Add(_oversold);
				}
				else
				{
					LineSeries.Clear();
				}

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Overbought), GroupName = nameof(Strings.Line), Description = nameof(Strings.OverboughtLimitDescription), Order = 310)]
		public LineSeries OverboughtLine
		{
			get => _overbought;
			set => _overbought = value;
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Oversold), GroupName = nameof(Strings.Line), Description = nameof(Strings.OversoldLimitDescription), Order = 320)]
		public LineSeries OversoldLine
		{
			get => _oversold;
			set => _oversold = value;
		}

        #endregion

        #region ctor

        public ChaikinOscillator()
			: base(true)
		{
			_emaLong.Period = 10;
			_emaShort.Period = 3;
			_divisor = 3;
			_lastBar = -1;

			Panel = IndicatorDataProvider.NewPanel;

			DataSeries[0] = new ValueDataSeries("RenderSeries", "ChaikinOscillator")
			{
				Color = DefaultColors.Blue.Convert(),
				LineDashStyle = LineDashStyle.Solid,
				VisualType = VisualMode.Line,
				Width = 2
			};

			_overbought = new LineSeries("Overbought", Strings.Overbought)
			{
				Color = DefaultColors.Red.Convert(),
				Width = 1,
				IsHidden = true
			};

            _oversold = new LineSeries("Oversold", Strings.Oversold)
			{
				Color = DefaultColors.Red.Convert(),
				Width = 1,
				IsHidden = true
			};

			LineSeries.Add(_overbought);
			LineSeries.Add(_oversold);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var currentCandle = GetCandle(bar);

			var ad = AccumulationDistributionBase(currentCandle);

			if (bar == 0)
				_exAd = ad;
			else
			{
				if (bar != _lastBar)
					_exAd = _lastAd;
				else
					_lastAd = ad;

				ad += _exAd;
			}

			var emaShort = _emaShort.Calculate(bar, ad);
			var emaLong = _emaLong.Calculate(bar, ad);

			var oscValue = (emaShort - emaLong) / Divisor;

			DataSeries[0][bar] = oscValue;

			_lastBar = bar;
		}

		#endregion

		#region Private methods

		private decimal AccumulationDistributionBase(IndicatorCandle candle)
		{
			var high = Convert.ToDouble(candle.High);
			var low = Convert.ToDouble(candle.Low);
			var close = Convert.ToDouble(candle.Close);
			var volume = Convert.ToDouble(candle.Volume);

			var ad = (close - low - (high - close)) / (high - low + Math.Pow(10, -9)) * volume;
			return Convert.ToDecimal(ad);
		}

		#endregion
	}
}